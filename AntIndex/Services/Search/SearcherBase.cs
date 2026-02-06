using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AntIndex.Models;
using AntIndex.Models.Index;
using AntIndex.Services.Extensions;
using AntIndex.Services.Normalizing;
using AntIndex.Services.Search.Requests;
using AntIndex.Services.Splitting;

namespace AntIndex.Services.Search;

public class SearcherBase(IPhraseSplitter splitter, INormalizer normalizer)
{
    #region Search

    public EntityMatchesBundle[] Search(AntHill ant,
    string query,
    AntRequestBase[] request,
    int take,
    CancellationToken? cancellationToken = null)
    {
        var ct = cancellationToken ?? new CancellationTokenSource(TimeoutMs).Token;

        SearchContext context = CreateContext(ant, query, request, cancellationToken);

        PerfomanceSettings perfomance = GetPerfomance(context);

        List<KeyValuePair<int, byte>>[] wordsBundle = SearchSimlarIndexWordsByQuery(context, perfomance);

        foreach (var i in request) i.ProcessRequest(context, wordsBundle, perfomance, ct);

        return PostProcessing(GetAllResults()
            .OrderByDescending(i =>
            {
                i.Score = CalculateScore(context, i);
                return i.Score;
            }))
            .Take(take)
            .ToArray();

        IEnumerable<EntityMatchesBundle> GetAllResults()
        {
            foreach (var typeResults in context.SearchResult)
            {
                foreach (var item in ResultVisionFilter(typeResults.Key, typeResults.Value.Values))
                    yield return item;
            }
        }
    }

    public TypeSearchResult[] SearchTypes(
        AntHill ant,
        string query,
        AntRequestBase[] request,
        (byte Type, int Take)[] selectTypes,
        CancellationToken? cancellationToken = null)
    {
        var ct = cancellationToken ?? new CancellationTokenSource(TimeoutMs).Token;

        SearchContext context = CreateContext(ant, query, request, cancellationToken);

        PerfomanceSettings perfomance = GetPerfomance(context);

        List<KeyValuePair<int, byte>>[] wordsBundle = SearchSimlarIndexWordsByQuery(context, perfomance);

        foreach (var i in request) i.ProcessRequest(context, wordsBundle, perfomance, ct);

        var result = new TypeSearchResult[selectTypes.Length];

        for (int i = 0; i < selectTypes.Length; i++)
        {
            (byte Type, int Take) = selectTypes[i];
            Dictionary<Key, EntityMatchesBundle>? typeSearchResult = context.GetResultsByType(Type);

            if (typeSearchResult is null)
            {
                result[i] = new(Type, []);
                continue;
            }

            var typeResult =
                PostProcessing(ResultVisionFilter(Type, typeSearchResult.Values)
                    .OrderByDescending(matchBundle =>
                    {
                        matchBundle.Score = CalculateScore(context, matchBundle);
                        return matchBundle.Score;
                    })
                )
                .Take(Take)
                .ToArray();

            result[i] = new(Type, typeResult);
        }

        return result;
    }

    private SearchContext CreateContext(AntHill ant, string query, AntRequestBase[] request, CancellationToken? cancellationToken)
    {
        string normalizedQuery = normalizer.Normalize(query);
        string[] splittedQuery = splitter.Tokenize(normalizedQuery);

        QueryWordContainer[] ngrammedWords = Array.ConvertAll(splittedQuery, i =>
        {
            bool notRealivated = NotRealivatedWords.Contains(i);

            Word[] alterantivesMetas = [];

            if (AlternativeWords.TryGetValue(i, out var alternatives))
                alterantivesMetas = Array.ConvertAll(alternatives, alt => new Word(alt));

            return new QueryWordContainer(
                new Word(i),
                alterantivesMetas,
                notRealivated);
        });

        SearchContext context = new(
            ant,
            normalizedQuery,
            splittedQuery,
            request,
            ngrammedWords);

        return context;
    }    

    private List<KeyValuePair<int, byte>>[] SearchSimlarIndexWordsByQuery(SearchContext searchContext, PerfomanceSettings perfomance)
    {
        var splittedQuery = searchContext.SplittedQuery;
        var result = new List<KeyValuePair<int, byte>>[splittedQuery.Length];

        //Используем один словарь для расчета совпавщих нграмм для каждого слова дабы лишний раз не аллоцировать
        Dictionary<int, WordCompareFactor> wordsSearchProcessDict = new(400_000);

        for (int i = 0; i < result.Length; i++)
        {
            QueryWordContainer currentWord = splittedQuery[i];

            //Проверка на введеное слово ранее, чтоб не повторять вычисления
            for (int j = i - 1; j >= 0; j--)
            {
                if (splittedQuery[j].QueryWord.Equals(currentWord.QueryWord))
                {
                    result[i] = result[j];
                    break;
                }
            }

            if (result[i] is null)
            {
                result[i] = SearchSimilarWordByQueryAndAlternatives(
                    searchContext.AntHill,
                    currentWord,
                    perfomance,
                    wordsSearchProcessDict);
            }
        }

        return result;
    }

    private List<KeyValuePair<int, byte>> SearchSimilarWordByQueryAndAlternatives(
        AntHill ant,
        QueryWordContainer wordContainer,
        PerfomanceSettings perfomance,
        Dictionary<int, WordCompareFactor> wordsSearchProcessDict)
    {
        List<KeyValuePair<int, byte>> result = [];

        foreach (Word altWord in wordContainer.Alternatives)
            SearchSimilars(altWord, (byte)wordContainer.QueryWord.NGrammsHashes.Length, wordsSearchProcessDict);

        int treshold = wordContainer.QueryWord.IsDigit
            ? wordContainer.QueryWord.NGrammsHashes.Length - Ant.NGRAM_LENGTH + 1
            : (int)(wordContainer.QueryWord.NGrammsHashes.Length * SimilarityTreshold);

        SearchSimilars(wordContainer.QueryWord, treshold, wordsSearchProcessDict);

        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SearchSimilars(Word queryWord, int treshold, Dictionary<int, WordCompareFactor> wordsSearchProcessDict)
        {
            int queryLength = queryWord.NGrammsHashes.Length;

            Dictionary<int, WordCompareFactor> similars = GetSimilarWords(ant, queryWord, treshold, wordsSearchProcessDict);

            foreach (KeyValuePair<int, WordCompareFactor> item in similars
                .Where(i => i.Value.Score >= treshold)
                .OrderByDescending(i => i.Value.Score)
                .Take(perfomance.MaxCheckingWordsCount))
            {
                result.Add(new(item.Key, item.Value.Score));
            }

            wordsSearchProcessDict.Clear();
        }
    }

    /// <summary>
    /// Метод отвечает за поиск похожих слов по 2-gramm
    /// </summary>
    /// <returns>Словарь id слова, количество ngramm</returns>
    private static Dictionary<int, WordCompareFactor> GetSimilarWords(
        AntHill ant,
        Word queryWord,
        int treshold,
        Dictionary<int, WordCompareFactor> wordsSearchProcessDict)
    {
        byte wordLength = (byte)queryWord.NGrammsHashes.Length;

        //Считаем количество совпавших ngramm для каждого слова
        Dictionary<int, WordCompareFactor> words = wordsSearchProcessDict;

        for (byte queryWordNgrammIndex = 0; queryWordNgrammIndex < wordLength; queryWordNgrammIndex++)
        {
            if (!ant.WordsIdsByNgramms.TryGetValue(queryWord.NGrammsHashes[queryWordNgrammIndex], out int[]? wordsIds))
                continue;

            for (int i = 0; i < wordsIds.Length; i++)
            {
                int wordId = wordsIds[i];

                ref var matchInfo = ref CollectionsMarshal.GetValueRefOrNullRef(words, wordId);

                if (!Unsafe.IsNullRef(ref matchInfo))
                {
                    matchInfo = new()
                    {
                        Mathes = (byte)(matchInfo.Mathes + 1),
                        Misses = CalculateMiss(in matchInfo, queryWordNgrammIndex),
                        PreviousMatch = queryWordNgrammIndex,
                    };

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static byte CalculateMiss(in WordCompareFactor compareFactor, int queryWordNgrammIndex)
                    {
                        if (queryWordNgrammIndex == 0) return 0;

                        byte missCount = (byte)(queryWordNgrammIndex - compareFactor.PreviousMatch - 1);

                        return (byte)(compareFactor.Misses + missCount);
                    }
                }
                else if (queryWordNgrammIndex == 0 || (!queryWord.IsDigit && queryWordNgrammIndex <= treshold))
                    words[wordId] = new(1, 0, queryWordNgrammIndex);
            }
        }

        return words;
    }

    private int CalculateScore(SearchContext searchContext, EntityMatchesBundle entityMatchesBundle)
    {
        Span<int> wordsScores = stackalloc int[searchContext.SplittedQuery.Length];

        //Считаем основные совпадения
        CalculateNodeMatchesScore(in wordsScores, entityMatchesBundle.WordsMatches, 1);

        //Считаем совпадения в связанных нодах
        Key[] nodes = entityMatchesBundle.EntityMeta.Links;
        foreach (Key nodeKey in nodes)
        {
            if (searchContext.GetResultsByType(nodeKey.Type) is { } req
                && req.TryGetValue(nodeKey, out var chaiedMathes))
            {
                double nodeMultipler = GetLinkedEntityMatchMiltipler(entityMatchesBundle.Key.Type, nodeKey.Type);
                CalculateNodeMatchesScore(in wordsScores, chaiedMathes.WordsMatches, nodeMultipler);

                if (OnLinkedEntityMatched(entityMatchesBundle.Key, nodeKey) is { } chainedMatchRule)
                    entityMatchesBundle.Rules.Add(chainedMatchRule);
            }
        }

        if (OnEntityProcessed(entityMatchesBundle) is { } rule)
            entityMatchesBundle.Rules.Add(rule);

        int resultScore = 0;

        for (int i = 0; i < wordsScores.Length; i++)
            resultScore += wordsScores[i];

        return resultScore;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateNodeMatchesScore(
        in Span<int> wordsScores,
        List<WordCompareResult> wordsMatches,
        double nodeMultipler)
    {
        //TODO: тут надо хорошо подумать как дистинктить слова и находить целое слово написанное раздельно
        for (int wordMatchIndex = 0; wordMatchIndex < wordsMatches.Count; wordMatchIndex++)
        {
            WordCompareResult compareResult = wordsMatches[wordMatchIndex];

            int score = compareResult.MatchLength;

            int queryWordPosition = compareResult.QueryWordPosition;
            double phraseMultipler = GetPhraseMultiplerInternal(compareResult.PhraseType);

            score = (int)(score * phraseMultipler * nodeMultipler);

            if (wordsScores[queryWordPosition] < score)
                wordsScores[queryWordPosition] = score;
        }
    }

    internal double GetPhraseMultiplerInternal(byte phraseType)
    {
        if (phraseType == 0)
            return 1;

        return GetPhraseTypeMultipler(phraseType);
    }
    #endregion

    public virtual IOrderedEnumerable<EntityMatchesBundle> PostProcessing(IOrderedEnumerable<EntityMatchesBundle> result)
        => result;

    public virtual IEnumerable<EntityMatchesBundle> ResultVisionFilter(byte type, IEnumerable<EntityMatchesBundle> result)
        => result;

    public virtual double GetLinkedEntityMatchMiltipler(byte entityType, byte linkedType)
        => 1;

    public virtual double GetPhraseTypeMultipler(byte phraseType)
        => 1;

    public virtual AdditionalRule? OnLinkedEntityMatched(Key entityKey, Key linkedKey)
        => null;

    public virtual AdditionalRule? OnEntityProcessed(EntityMatchesBundle entityMatchesBundle)
        => null;

    public virtual int TimeoutMs
        => 1500;

    public virtual double SimilarityTreshold
        => 0.5;

    public virtual HashSet<string> NotRealivatedWords { get; } = [];

    public virtual Dictionary<string, string[]> AlternativeWords { get; } = [];

    public virtual PerfomanceSettings GetPerfomance(SearchContext searchContext)
        => searchContext.SplittedQuery.Length > 5
            ? PerfomanceSettings.Fast
            : PerfomanceSettings.Default;
}

public record QueryWordContainer(Word QueryWord, Word[] Alternatives, bool NotRealivated);
