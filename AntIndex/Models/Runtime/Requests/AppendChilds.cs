using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов в выдачу
/// </summary>
/// <param name="parentType">Тип родителя</param>
/// <param name="entityType">Целевой тип</param>
/// <param name="appendFilter">Фильтр дочерних сущностей КАЖДОГО родителя</param>
/// <param name="parentByTop">Топ родителей по prescore для добавления дочерних</param>
public class AppendChilds(
    byte parentType,
    byte entityType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    int parentByTop = 0) : AntRequest(entityType, null, null)
{
    public override void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        if (searchContext.GetRequestByType(parentType) is { } from
            && searchContext.GetRequestByType(EntityType) is { } to
            && index.Entities.TryGetValue(EntityType, out var entities))
        {
            IEnumerable<Key> GetKeys()
            {
                if (parentByTop < 1)
                    return from.SearchResult.Keys;
                else
                    return from.SearchResult
                        .OrderByDescending(i => i.Value.Prescore)
                        .Take(parentByTop)
                        .Select(i => i.Key);
            }

            foreach (Key i in GetKeys())
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!(index.Entities.TryGetValue(i.Type, out var byEntities) && byEntities.TryGetValue(i.Id, out var byParent)))
                    continue;

                var parentEntityChilds = byParent.Childs;

                foreach (var child in appendFilter(parentEntityChilds.Where(i => i.Type == EntityType)))
                {
                    var entityMeta = entities[child.Id];

                    to.SearchResult.TryAdd(child, new(entityMeta));
                }
            }
        }
    }
}
