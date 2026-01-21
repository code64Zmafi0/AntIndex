using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

public class Select(byte targetType, IEnumerable<int> ids) : AntRequestBase(targetType)
{
    public override void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        if (!index.Entities.TryGetValue(TargetType, out var entities))
            return;

        foreach (int id in ids)
        {
            if (entities.TryGetValue(id, out EntityMeta? meta))
                searchContext.AddResult(meta, null);
        }
    }
}
