using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

public abstract class AntRequest(byte entityType, Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? filter = null)
{
    public byte EntityType { get; } = entityType;

    public Dictionary<Key, EntityMatchesBundle> SearchResult { get; set; } = [];

    public Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? Filter { get; set; } = filter;

    public abstract void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        Dictionary<int, byte>[] wordsBundle,
        CancellationToken ct);

    public IEnumerable<EntityMatchesBundle> GetFilteredResult()
        => Filter?.Invoke(SearchResult.Values) ?? SearchResult.Values;
}
