using System.Data;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AntIndex.Models.Abstract;
using AntIndex.Models.Index;
using AntIndex.Models.Runtime;
using MessagePack;

namespace AntIndex.Models;

[MessagePackObject]
public class AntHill
{
    public AntHill() { }

    [Key(1)]
    public Dictionary<byte /*TypeId*/, Dictionary<int /*EntityId*/, EntityMeta>> Entities { get; set; } = [];

    [Key(2)]
    public Dictionary<int /*NGrammHash*/, int[] /*WordsIds*/> WordsIdsByNgramms { get; set; } = [];

    [Key(3)]
    public int[/*NGrammHashes*/][/*WordId*/] WordsByIds { get; set; } = [];

    [Key(4)]
    public EntitiesByWordsIndex EntitiesByWordsIndex { get; set; } = new();

    [IgnoreMember]
    public int EntitesCount => Entities.Sum(i => i.Value.Count);

    #region Search
    public TypeSearchResult[] SearchTypes<TContext>(TContext searchContext, (byte Type, int Take)[] selectTypes, CancellationToken? cancellationToken = null)
        where TContext : SearchContextBase
    {
        SearchInternal(searchContext, cancellationToken);

        var result = new TypeSearchResult[selectTypes.Length];

        for (int i = 0; i < selectTypes.Length; i++)
        {
            (byte Type, int Take) = selectTypes[i];
            Dictionary<Key, EntityMatchesBundle>? request = searchContext.GetResultsByType(Type);

            if (request is null)
            {
                result[i] = new(Type, []);
                continue;
            }

            var typeResult = searchContext
                .PostProcessing(searchContext.ResultVisionFilter(Type, request.Values)
                    .OrderByDescending(matchBundle =>
                    {
                        matchBundle.Score = CalculateScore(matchBundle, searchContext);
                        return matchBundle.Score;
                    })
                )
                .Take(Take)
                .ToArray();

            result[i] = new(Type, typeResult);
        }

        return result;
    }

    public EntityMatchesBundle[] Search<TContext>(TContext searchContext, int take = 30, CancellationToken? cancellationToken = null)
        where TContext : SearchContextBase
    {
        SearchInternal(searchContext, cancellationToken);

        return searchContext.PostProcessing(GetAllResults()
            .OrderByDescending(i =>
            {
                i.Score = CalculateScore(i, searchContext);
                return i.Score;
            }))
            .Take(take)
            .ToArray();

        IEnumerable<EntityMatchesBundle> GetAllResults()
        {
            foreach (var typeResults in searchContext.SearchResult)
            {
                foreach (var item in searchContext.ResultVisionFilter(typeResults.Key, typeResults.Value.Values))
                    yield return item;
            }
        }
    }

    private void SearchInternal<TContext>(TContext searchContext, CancellationToken? cancellationToken = null)
        where TContext : SearchContextBase
    {
        var ct = cancellationToken ?? new CancellationTokenSource(searchContext.TimeoutMs).Token;

        List<KeyValuePair<int, byte>>[] wordsBundle = SearchSimlarIndexWordsByQuery(searchContext);

        foreach (var i in searchContext.Request)
            i.ProcessRequest(this, searchContext, wordsBundle, ct);
    }

    private List<KeyValuePair<int, byte>>[] SearchSimlarIndexWordsByQuery<TContext>(TContext searchContext)
        where TContext : SearchContextBase
    {
        var result = new List<KeyValuePair<int, byte>>[searchContext.SplittedQuery.Length];

        //Используем один словарь для расчета совпавщих нграмм для каждого слова дабы лишний раз не аллоцировать
        Dictionary<int, byte> wordsSearchProcessDict = new(400_000);

        for (int i = 0; i < result.Length; i++)
        {
            QueryWordContainer currentWord = searchContext.SplittedQuery[i];

            //Проверка на введеное слово ранее, чтоб не повторять вычисления
            for (int j = i - 1; j >= 0; j--)
            {
                if (searchContext.SplittedQuery[j].QueryWord.Equals(currentWord.QueryWord))
                {
                    result[i] = result[j];
                    break;
                }
            }

            if (result[i] is null)
            {
                result[i] = SearchSimilarWordByQueryAndAlternatives(
                    currentWord,
                    searchContext.SimilarityTreshold,
                    searchContext.Perfomance.MaxCheckingCount,
                    wordsSearchProcessDict);
            }
        }

        return result;
    }

    private List<KeyValuePair<int, byte>> SearchSimilarWordByQueryAndAlternatives(
        QueryWordContainer wordContainer,
        double similarityTreshold,
        int maxBundleLength,
        Dictionary<int, byte> wordsSearchProcessDict)
    {
        List<KeyValuePair<int, byte>> result = [];

        foreach (Word altWord in wordContainer.Alternatives)
            SearchAlternative(altWord, (byte)wordContainer.QueryWord.NGrammsHashes.Length, wordsSearchProcessDict);

        SearchSimilars(wordContainer.QueryWord, wordsSearchProcessDict);

        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SearchSimilars(Word queryWord, Dictionary<int, byte> wordsSearchProcessDict)
        {
            int treshold = queryWord.IsDigit
                ? queryWord.NGrammsHashes.Length - 1
                : (int)(queryWord.NGrammsHashes.Length * similarityTreshold);

            Dictionary<int, byte> similars = GetSimilarWords(queryWord, treshold, wordsSearchProcessDict);

            foreach (KeyValuePair<int, byte> item in similars
                .Where(i => ValidateSimilarWord(queryWord, i, treshold))
                .OrderByDescending(i => i.Value))
            {
                if (result.Count >= maxBundleLength)
                    return;

                result.Add(item);
            }

            wordsSearchProcessDict.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SearchAlternative(Word altWord, byte queryWordLength, Dictionary<int, byte> wordsSearchProcessDict)
        {
            int treshold = altWord.NGrammsHashes.Length;

            if (GetSimilarWords(altWord, treshold, wordsSearchProcessDict).FirstOrDefault(i => ValidateAlternativeWord(i, treshold)) is { } item)
                result.Add(new(item.Key, queryWordLength));

            wordsSearchProcessDict.Clear();
        }
    }

    private bool ValidateAlternativeWord(in KeyValuePair<int, byte> indexWordMathes, int treshold)
        => indexWordMathes.Value == treshold && WordsByIds[indexWordMathes.Key].Length == treshold;

    private bool ValidateSimilarWord(Word queryWord, in KeyValuePair<int, byte> indexWordMathes, int treshold)
    {
        if (indexWordMathes.Value < treshold)
            return false;

        const int MaxDistance = 2;

        int[] indexWordHashes = WordsByIds[indexWordMathes.Key];
        int[] queryWordHashes = queryWord.NGrammsHashes;

        int ptr = 0;
        int missed = 0;

        for (int i = 0; i < indexWordHashes.Length; i++)
        {
            if (indexWordHashes[i] == queryWordHashes[ptr])
            {
                ptr++;
                missed = 0;
                if (ptr == queryWordHashes.Length)
                {
                    return true;
                }
            }
            else
            {
                if (ptr == queryWordHashes.Length - 1)
                    return true;

                missed++;
                if (missed > MaxDistance)
                {
                    return false;
                }
                else if (ptr < queryWordHashes.Length - 1)
                {
                    if (queryWordHashes[ptr + 1] == indexWordHashes[i])
                    {
                        i--;
                    }

                    ptr++;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Метод отвечает за поиск похожих слов по 2-gramm
    /// </summary>
    /// <param name="queryWord"></param>
    /// <param name="treshold"></param>
    /// <returns>Словарь id слова, количество ngramm</returns>
    private Dictionary<int, byte> GetSimilarWords(Word queryWord, int treshold, Dictionary<int, byte> wordsSearchProcessDict)
    {
        var wordLength = queryWord.NGrammsHashes.Length;

        //Считаем количество совпавших ngramm для каждого слова
        Dictionary<int, byte> words = wordsSearchProcessDict;

        for (int queryWordNgrammIndex = 0; queryWordNgrammIndex < wordLength; queryWordNgrammIndex++)
        {
            if (!WordsIdsByNgramms.TryGetValue(queryWord.NGrammsHashes[queryWordNgrammIndex], out int[]? wordsIds))
                continue;

            foreach (int wordId in wordsIds)
            {
                ref var matchInfo = ref CollectionsMarshal.GetValueRefOrNullRef(words, wordId);

                if (!Unsafe.IsNullRef(ref matchInfo))
                    matchInfo++;
                else if (queryWordNgrammIndex == 0 || (!queryWord.IsDigit && queryWordNgrammIndex <= treshold))
                    words[wordId] = 1;
            }
        }

        return words;
    }

    private static int CalculateScore(
        EntityMatchesBundle entityMatchesBundle,
        SearchContextBase searchContext)
    {
        Span<int> wordsScores = stackalloc int[searchContext.SplittedQuery.Length];

        //Считаем основные совпадения
        CalculateNodeMatchesScore(in wordsScores, searchContext, entityMatchesBundle.WordsMatches, 1);

        //Считаем совпадения в связанных нодах
        Key[] nodes = entityMatchesBundle.EntityMeta.Links;
        foreach (Key nodeKey in nodes)
        {
            if (searchContext.GetResultsByType(nodeKey.Type) is { } req
                && req.TryGetValue(nodeKey, out var chaiedMathes))
            {
                double nodeMultipler = searchContext.GetLinkedEntityMatchMiltipler(entityMatchesBundle.Key.Type, nodeKey.Type);
                CalculateNodeMatchesScore(in wordsScores, searchContext, chaiedMathes.WordsMatches, nodeMultipler);

                if (searchContext.OnLinkedEntityMatched(entityMatchesBundle.Key, nodeKey) is { } chainedMatchRule)
                    entityMatchesBundle.Rules.Add(chainedMatchRule);
            }
        }

        if (searchContext.OnEntityProcessed(entityMatchesBundle) is { } rule)
            entityMatchesBundle.Rules.Add(rule);

        int resultScore = 0;

        for (int i = 0; i < wordsScores.Length; i++)
            resultScore += wordsScores[i];

        return resultScore;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateNodeMatchesScore(
        in Span<int> wordsScores,
        SearchContextBase searchContext,
        List<WordCompareResult> wordsMatches,
        double nodeMultipler)
    {
        //TODO: тут надо хорошо подумать как дистинктить слова и находить целое слово написанное раздельно
        for (int wordMatchIndex = 0; wordMatchIndex < wordsMatches.Count; wordMatchIndex++)
        {
            WordCompareResult compareResult = wordsMatches[wordMatchIndex];
            WordMatchMeta matchMeta = compareResult.MatchMeta;

            int score = compareResult.MatchLength;

            int queryWordPosition = compareResult.QueryWordPosition;
            double phraseMultipler = searchContext.GetPhraseMultiplerInternal(matchMeta.PhraseType);

            score = (int)(score * phraseMultipler * nodeMultipler);

            if (wordsScores[queryWordPosition] < score)
                wordsScores[queryWordPosition] = score;
        }
    }
    #endregion

    public void Trim()
    {
        Key GetKey(Key key)
            => Entities.TryGetValue(key.Type, out var entities) && entities.TryGetValue(key.Id, out var meta)
            ? meta.Key
            : key;

        foreach (var collection in Entities.Values)
        {
            foreach (var meta in collection.Values)
            {
                if (meta.Links.Length == 0)
                    meta.Links = Array.Empty<Key>();
                else
                {
                    for (int i = 0; i < meta.Links.Length; i++)
                        meta.Links[i] = GetKey(meta.Links[i]);
                }

                if (meta.Childs.Length == 0)
                    meta.Childs = Array.Empty<Key>();
                else
                {
                    for (int i = 0; i < meta.Childs.Length; i++)
                        meta.Childs[i] = GetKey(meta.Childs[i]);
                }
            }

            collection.TrimExcess();
        }

        Entities.TrimExcess();
        WordsIdsByNgramms.TrimExcess();
        EntitiesByWordsIndex.Trim(GetKey);

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
    }
}