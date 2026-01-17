using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.AdditionalsRequests;

public abstract class AdditionalRequestBase()
{
    public abstract void ProcessRequest(
        Dictionary<Key, EntityMatchesBundle> searchResult,
        byte entityType,
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct);
}
