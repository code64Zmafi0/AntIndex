using AntIndex.Models.Abstract;
using AntIndex.Models.Index;
using AntIndex.Models.Runtime.AdditionalsRequests;

namespace AntIndex.Models.Runtime.Requests;

public abstract class AntRequestBase(
    byte entityType,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? resultVisionFilter,
    Func<Key, bool>? filter,
    AdditionalRequestBase[]? additionals)
{
    public byte EntityType { get; } = entityType;

    public Dictionary<Key, EntityMatchesBundle> SearchResult { get; } = [];

    public AdditionalRequestBase[]? Additionals { get; } = additionals;

    public Func<Key, bool>? Filter { get; } = filter;

    public void Process(
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        ProcessRequest(index, searchContext, wordsBundle, ct);
        ProcessAdditionals(index, searchContext, wordsBundle, ct);
    }

    protected abstract void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct);

    protected void ProcessAdditionals(
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        foreach (AdditionalRequestBase additional in Additionals ?? [])
        {
            additional.ProcessRequest(SearchResult, EntityType, index, searchContext, wordsBundle, ct);
        }
    }

    public IEnumerable<EntityMatchesBundle> GetVisibleResults()
        => resultVisionFilter?.Invoke(SearchResult.Values) ?? SearchResult.Values;
}
