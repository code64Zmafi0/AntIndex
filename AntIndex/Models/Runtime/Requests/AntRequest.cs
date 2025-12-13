using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

public abstract class AntRequest(
    byte entityType,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? resultVisionFilter,
    Func<Key, bool>? filter)
{
    public byte EntityType { get; } = entityType;

    public Dictionary<Key, EntityMatchesBundle> SearchResult { get; } = [];

    public Func<Key, bool>? Filter { get; } = filter;

    public abstract void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        Dictionary<int, byte>[] wordsBundle,
        CancellationToken ct);

    public IEnumerable<EntityMatchesBundle> GetVisibleResults()
        => resultVisionFilter?.Invoke(SearchResult.Values) ?? SearchResult.Values;
}
