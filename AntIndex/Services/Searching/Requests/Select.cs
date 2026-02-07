using AntIndex.Models.Index;

namespace AntIndex.Services.Searching.Requests;

/// <summary>
/// Принудительное добавление сущностей целевого типа
/// </summary>
/// <param name="targetType">Целевой тип</param>
/// <param name="ids">Идентификаторы</param>
public class Select(byte targetType, IEnumerable<int> ids) : AntRequestBase(targetType)
{
    public override void ProcessRequest(
        AntSearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        PerfomanceSettings perfomanceSettings,
        CancellationToken ct)
    {
        Dictionary<Key, EntityMeta> entities = searchContext.AntHill.Entities;

        foreach (int id in ids)
        {
            Key key = new(TargetType, id);
            if (entities.TryGetValue(key, out EntityMeta? meta))
                searchContext.AddResult(key, meta);
        }
    }
}
