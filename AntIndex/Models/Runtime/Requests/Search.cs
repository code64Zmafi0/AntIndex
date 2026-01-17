using System.Runtime.InteropServices;
using AntIndex.Models.Abstract;
using AntIndex.Models.Index;
using AntIndex.Models.Runtime.AdditionalsRequests;

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
    AdditionalRequestBase[]? additionals = null)
    : AntRequestBase(entityType, resultVisionFilter, filter, additionals)
{
    protected override void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        if (!index.Entities.TryGetValue(EntityType, out var entities))
            return;

        for (int queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentBundle = wordsBundle[queryWordPosition];

            var ck = searchContext.Perfomance.GetPerfomancer(currentBundle.Count);

            for (int wbIndex = 0; wbIndex < currentBundle.Count; wbIndex++)
            {
                if (!ck.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentBundle[wbIndex];

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

                    if (!((Filter?.Invoke(entityKey)) ?? true))
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
