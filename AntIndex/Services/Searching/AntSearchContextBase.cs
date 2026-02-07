using System.Runtime.InteropServices;
using AntIndex.Models;
using AntIndex.Models.Index;
using AntIndex.Services.Searching.Requests;

namespace AntIndex.Services.Searching;

public abstract class AntSearchContextBase(AntHill ant, string query)
{
    #region Overrides
    public abstract AntRequestBase[] Request { get; }

    public virtual HashSet<string> NotRealivatedWords { get; } = [];

    public virtual Dictionary<string, string[]> AlternativeWords { get; } = [];
    #endregion

    public AntHill AntHill { get; set; } = ant;

    public string Query { get; set; } = query;

    public string[] SplittedAndNormalizedQuery { get; set; } = [];

    public QueryWordContainer[] NgrammedQuery { get; set; } = [];

    public Dictionary<byte, Dictionary<Key, EntityMatchesBundle>> SearchResult { get; set; } = [];

    #region Search Tools
    public Dictionary<Key, EntityMatchesBundle>? GetResultsByType(byte type)
    {
        if (SearchResult.TryGetValue(type, out var result))
            return result;

        return null;
    }

    public void AddResult(Key key, EntityMeta meta)
    {
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, meta);
    }

    public void AddResult(Key key, EntityMeta entityMeta, byte nameWordPosition, byte phraseType, byte queryWordPosition, byte matchLength)
    {
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, entityMeta);

        matchesBundle!.AddMatch(new(nameWordPosition, phraseType, queryWordPosition, matchLength));
    }
    #endregion
}
