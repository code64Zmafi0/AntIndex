using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.AdditionalsRequests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов в выдачу
/// </summary>
/// <param name="parentType">Тип родителя</param>
/// <param name="appendFilter">Фильтр дочерних сущностей КАЖДОГО родителя</param>
/// <param name="parentsTop">Топ родителей по prescore для добавления дочерних</param>
public class AppendByParent(
    byte parentType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    int parentsTop = 0) : AdditionalRequestBase
{
    public override void ProcessRequest(
        Dictionary<Key, EntityMatchesBundle> searchResult,
        byte entityType,
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        if (searchContext.GetRequestByType(parentType) is { } from
            && index.Entities.TryGetValue(entityType, out var entities))
        {
            IEnumerable<Key> GetKeys()
            {
                if (parentsTop < 1)
                    return from.SearchResult.Keys;
                else
                    return from.SearchResult
                        .OrderByDescending(i => i.Value.Prescore)
                        .Take(parentsTop)
                        .Select(i => i.Key);
            }

            foreach (Key i in GetKeys())
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!(index.Entities.TryGetValue(i.Type, out var byEntities) && byEntities.TryGetValue(i.Id, out var byParent)))
                    continue;

                Key[] parentEntityChilds = byParent.Childs;

                foreach (var child in appendFilter(parentEntityChilds.Where(i => i.Type == entityType)))
                {
                    var entityMeta = entities[child.Id];

                    searchResult.TryAdd(child, new(entityMeta));
                }
            }
        }
    }
}
