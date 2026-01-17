using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.AdditionalsRequests;

public class ForceSelect(IEnumerable<int> ids) : AdditionalRequestBase
{
    public override void ProcessRequest(
        Dictionary<Key, EntityMatchesBundle> searchResult,
        byte entityType,
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        if (!index.Entities.TryGetValue(entityType, out var entities))
            return;

        foreach (int id in ids)
        {
            if (entities.TryGetValue(id, out EntityMeta? meta))
                searchResult.TryAdd(meta.Key, new(meta));
        }
    }
}
