using AntIndex.Models;
using AntIndex.Models.Index;

namespace AntIndex.Services.Search.Requests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов в выдачу
/// </summary>
/// <param name="targetType">Целевой тип</param>
/// <param name="parentType">Тип родителя</param>
/// <param name="appendFilter">Фильтр дочерних сущностей КАЖДОГО родителя</param>
/// <param name="parentByTop">Топ родителей по prescore для добавления дочерних</param>
public class AppendChilds(
    byte targetType,
    byte parentType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    int parentByTop = 0) : AntRequestBase(targetType)
{
    public override void ProcessRequest(
        AntHill index,
        AntSearcherBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        if (searchContext.GetResultsByType(parentType) is { } from)
        {
            IEnumerable<Key> GetKeys()
            {
                if (parentByTop < 1)
                    return from.Keys;
                else
                    return from
                        .OrderByDescending(i => i.Value.Prescore)
                        .Take(parentByTop)
                        .Select(i => i.Key);
            }

            foreach (Key i in GetKeys())
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!(index.Entities.TryGetValue(i, out var byParent)))
                    continue;

                var parentEntityChilds = byParent.Childs;

                foreach (Key child in appendFilter(parentEntityChilds.Where(i => i.Type == TargetType)))
                {
                    var entityMeta = index.Entities[child];

                    searchContext.AddResult(child, entityMeta);
                }
            }
        }
    }
}
