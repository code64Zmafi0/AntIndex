using System.Data;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AntIndex.Models.Abstract;
using AntIndex.Models.Index;
using AntIndex.Models.Runtime;
using AntIndex.Models.Runtime.Requests;
using ProtoBuf;

namespace AntIndex.Models;

[ProtoContract]
public class AntHill
{
    public AntHill() { }

    [ProtoMember(1)]
    public Dictionary<byte /*TypeId*/, Dictionary<int /*EntityId*/, EntityMeta>> Entities { get; set; } = [];

    [ProtoMember(2)]
    public Dictionary<int /*NGrammHash*/, int[] /*WordsIds*/> WordsIdsByNgramms { get; set; } = [];

    [ProtoMember(3)]
    public Dictionary<int /*WordId*/, int[] /*NGrammHashes*/> WordsByIds { get; set; } = [];

    [ProtoMember(4)]
    public EntitiesByWordsIndex EntitiesByWordsIndex { get; set; } = new();

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
            AntRequest? request = Array.Find(searchContext.Request, i => i is not AppendChilds && i.EntityType == Type);

            if (request is null)
            {
                result[i] = new(Type, []);
                continue;
            }

            var typeResult = searchContext
                .PostProcessing(request
                    .GetVisibleResults()
                    .OrderByDescending(matchBundle =>
                    {
                        matchBundle.Score = CalculateScore(matchBundle, searchContext);
                        return matchBundle.Score;
                    })
                )
                .Take(Take)
                .ToArray();

            result[i] = new(request.EntityType, typeResult);
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
            for (int i = 0; i < searchContext.Request.Length; i++)
            {
                AntRequest? request = searchContext.Request[i];
                foreach (var item in request.GetVisibleResults())
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
                    searchContext.Perfomance.MaxCheckingCount);
            }
        }

        return result;
    }

    private List<KeyValuePair<int, byte>> SearchSimilarWordByQueryAndAlternatives(
        QueryWordContainer wordContainer,
        double similarityTreshold,
        int maxBundleLength)
    {
        List<KeyValuePair<int, byte>> result = [];

        for (int i = 0; i < wordContainer.Alternatives.Length; i++)
            SearchAlternative(wordContainer.Alternatives[i], (byte)wordContainer.QueryWord.NGrammsHashes.Length);

        SearchSimilars(wordContainer.QueryWord);

        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SearchSimilars(Word queryWord)
        {
            int treshold = queryWord.IsDigit
                ? queryWord.NGrammsHashes.Length - 1
                : (int)(wordContainer.QueryWord.NGrammsHashes.Length * similarityTreshold);

            foreach (KeyValuePair<int, byte> item in GetSimilarWords(queryWord, treshold)
                .Where(i => ValidateSimilarWord(queryWord, i, treshold))
                .OrderByDescending(i => i.Value))
            {
                if (result.Count > maxBundleLength)
                    return;

                result.Add(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SearchAlternative(Word altWord, byte queryWordLength)
        {
            int treshold = altWord.NGrammsHashes.Length;

            if (GetSimilarWords(altWord, treshold).FirstOrDefault(i => ValidateAlternativeWord(i, treshold)) is { } item)
                result.Add(new(item.Key, queryWordLength));
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

        int matchesCounter = 0;
        int previousMatchPosition = 0;
        for (var i = 0; i < indexWordHashes.Length; i++)
        {
            for (var j = previousMatchPosition; j < queryWordHashes.Length; j++)
            {
                if (indexWordHashes[i] == queryWordHashes[j])
                {
                    if (j - previousMatchPosition > MaxDistance)
                        return false;

                    matchesCounter++;
                    previousMatchPosition = i;

                    if (matchesCounter == indexWordMathes.Value)
                        return true;

                    break;
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
    private Dictionary<int, byte> GetSimilarWords(Word queryWord, int treshold)
    {
        var wordLength = queryWord.NGrammsHashes.Length;

        //Считаем количество совпавших ngramm для каждого слова
        Dictionary<int, byte> words = new(400_000);

        for (int queryWordNgrammIndex = 0; queryWordNgrammIndex < wordLength; queryWordNgrammIndex++)
        {
            if (!WordsIdsByNgramms.TryGetValue(queryWord.NGrammsHashes[queryWordNgrammIndex], out var wordsIds))
                continue;

            for (int i = 0; i < wordsIds.Length; i++)
            {
                int wordId = wordsIds[i];

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
        for (int i = 0; i < nodes.Length; i++)
        {
            Key nodeKey = nodes[i];

            if (searchContext.GetRequestByType(nodeKey.Type) is { } req
                && req.SearchResult.TryGetValue(nodeKey, out var chaiedMathes))
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

        for (int i = 0; i < entityMatchesBundle.Rules.Count; i++)
            resultScore += entityMatchesBundle.Rules[i].Score;

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
        WordsByIds.TrimExcess();
        WordsIdsByNgramms.TrimExcess();
        EntitiesByWordsIndex.Trim(GetKey);

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
    }
}