using System.Runtime.InteropServices;
using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа
/// </summary>
/// <param name="entityType">Целевой тип сущности</param>
/// <param name="resultVisionFilter">Фильтр отображения результатов в итоговом списке поиска</param>
/// <param name="filter">Фильтр добавления в словарь найденных</param>
/// <param name="forceSelect">Принудительное добаваления сущностей целеовго типа по id</param>
public class Search(
    byte entityType,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? resultVisionFilter = null,
    Func<Key, bool>? filter = null,
    IEnumerable<int>? forceSelect = null)
    : AntRequest(entityType, resultVisionFilter, filter)
{
    public override void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        Dictionary<int, byte>[] wordsBundle,
        CancellationToken ct)
    {
        if (!index.Entities.TryGetValue(EntityType, out var entities))
            return;

        foreach (int id in forceSelect ?? [])
        {
            if (entities.TryGetValue(id, out EntityMeta? meta))
                SearchResult.TryAdd(meta.Key, new(meta));
        }

        for (int queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            var ck = searchContext.Perfomance.GetPerfomancer(wordsBundle[queryWordPosition].Count);

            foreach (var indexWordInfo in wordsBundle[queryWordPosition])
            {
                if (!ck.NeedContinue)
                    break;

                ck.IncrementCheck();

                int wordId = indexWordInfo.Key;

                WordMatchMeta[]? list = index.EntitiesByWordsIndex.GetMatchesByWord(wordId, EntityType);

                if (list is null)
                    continue;

                ck.IncrementMatch();

                for (int i = 0; i < list.Length; i++)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    WordMatchMeta? wordMatchMeta = list[i];
                    EntityMeta entityMeta = entities[wordMatchMeta.EntityId];
                    Key entityKey = entityMeta.Key;

                    if (!((Filter?.Invoke(entityKey)) ?? false))
                        continue;

                    ref var entityMatch = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, entityKey, out var exists);

                    if (!exists)
                        entityMatch = new(entityMeta);

                    entityMatch!.AddMatch(
                        new(queryWordPosition,
                            wordMatchMeta,
                            indexWordInfo.Value));
                }
            }
        }
    }
}
