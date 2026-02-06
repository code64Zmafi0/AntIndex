using System.Runtime.InteropServices;
using AntIndex.Models;
using AntIndex.Models.Index;
using AntIndex.Services.Search.Requests;

namespace AntIndex.Services.Search;

public class SearchContext(AntHill ant, string query, string[] splittedTextQuery,  AntRequestBase[] request, QueryWordContainer[] splittedQuery)
{
    public AntHill AntHill { get; } = ant;

    public string Query { get; } = query;

    public string[] SplittedTextQuery { get; } = splittedTextQuery;

    public AntRequestBase[] Request { get; } = request;

    public QueryWordContainer[] SplittedQuery { get; } = splittedQuery;

    public Dictionary<byte, Dictionary<Key, EntityMatchesBundle>> SearchResult { get; set; } = [];

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
}
