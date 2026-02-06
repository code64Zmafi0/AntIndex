using AntIndex.Models;
using AntIndex.Models.Index;

namespace AntIndex.Services.Search.Requests;

public class Select(byte targetType, IEnumerable<int> ids) : AntRequestBase(targetType)
{
    public override void ProcessRequest(
        SearchContext searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        PerfomanceSettings perfomanceSettings,
        CancellationToken ct)
    {
        AntHill index = searchContext.AntHill;

        foreach (int id in ids)
        {
            Key key = new(TargetType, id);
            if (index.Entities.TryGetValue(key, out EntityMeta? meta))
                searchContext.AddResult(key, meta);
        }
    }
}
