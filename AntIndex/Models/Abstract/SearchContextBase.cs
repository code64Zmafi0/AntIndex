using System.Runtime.InteropServices;
using AntIndex.Models.Index;
using AntIndex.Models.Runtime;
using AntIndex.Models.Runtime.Requests;
using AntIndex.Services.Normalizing;
using AntIndex.Services.Splitting;

namespace AntIndex.Models.Abstract;

public abstract class SearchContextBase(
    string query,
    INormalizer normalizer,
    IPhraseSplitter splitter)
{
    public string Query { get; } = normalizer.Normalize(query);

    public Dictionary<byte, Dictionary<Key, EntityMatchesBundle>> SearchResult { get; set; } = [];

    public abstract AntRequestBase[] Request { get; }


    private QueryWordContainer[]? _splittedQuery;

    public QueryWordContainer[] SplittedQuery
        => _splittedQuery ??= GetSplittedQuery();

    private QueryWordContainer[] GetSplittedQuery()
        => Array.ConvertAll(splitter.Tokenize(Query),
            word =>
            {
                bool notRealivated = NotRealivatedWords.Contains(word);

                Word[] alterantivesMetas = Array.Empty<Word>();

                if (AlternativeWords.TryGetValue(word, out var alternatives))
                    alterantivesMetas = Array.ConvertAll(alternatives, alt => new Word(alt));

                return new QueryWordContainer(
                    new Word(word),
                    alterantivesMetas,
                    notRealivated);
            });

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

    public virtual bool IsSimpleQuery
        => SplittedQuery.Length < 2;

    public virtual double SimilarityTreshold
        => 0.7;

    public virtual HashSet<string> NotRealivatedWords { get; } = [];

    public virtual Dictionary<string, string[]> AlternativeWords { get; } = [];

    public virtual PerfomanceSettings Perfomance
        => SplittedQuery.Length > 5
            ? PerfomanceSettings.Fast
            : PerfomanceSettings.Default;

    internal double GetPhraseMultiplerInternal(byte phraseType)
    {
        if (phraseType == 0)
            return 1;

        return GetPhraseTypeMultipler(phraseType);
    }

    internal Dictionary<Key, EntityMatchesBundle>? GetResultsByType(byte type)
    {
        if (SearchResult.TryGetValue(type, out var result))
            return result;

        return null;
    }

    internal void AddResult(EntityMeta meta)
    {
        Key key = meta.Key;
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(meta);
    }

    internal void AddResult(EntityMeta entityMeta, in WordCompareResult wordCompareResult)
    {
        Key key = entityMeta.Key;
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(entityMeta);

        matchesBundle!.AddMatch(wordCompareResult);
    }
}

public record QueryWordContainer(Word QueryWord, Word[] Alternatives, bool NotRealivated);
