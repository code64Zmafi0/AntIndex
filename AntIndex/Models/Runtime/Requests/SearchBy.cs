using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа по найденным родителям (Parent)
/// </summary>
/// <param name="entityType">Целевой тип сущности</param>
/// <param name="byType">Тип сущности родителя (Parent)</param>
/// <param name="resultVisionFilter">Фильтр отображения результатов в итоговом списке поиска</param>
/// <param name="filter">Фильтр добавления в словарь найденных</param>
/// <param name="parentsFilter">Фильтр родителей по которым осущетсвляем поиск</param>
public class SearchBy(
    byte entityType,
    byte byType,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? resultVisionFilter = null,
    Func<Key, bool>? filter = null,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<Key>>? parentsFilter = null) : AntRequest(entityType, resultVisionFilter, filter)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual IEnumerable<Key> SelectParents(AntRequest parentRequest)
        => parentsFilter is null
            ? parentRequest.SearchResult.Keys
            : parentsFilter.Invoke(parentRequest.SearchResult.Values).ToArray();

    public override void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        if (!index.Entities.TryGetValue(EntityType, out var entities)
            || !(searchContext.GetRequestByType(byType) is { } byStrat))
            return;

        IEnumerable<Key> parents = SelectParents(byStrat);

        for (int queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentBundle = wordsBundle[queryWordPosition];

            var ck = searchContext.Perfomance.GetPerfomancer(currentBundle.Count);

            for (int i = 0; i < currentBundle.Count; i++)
            {
                if (!ck.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentBundle[i];

                ck.IncrementCheck();

                int wordId = indexWordInfo.Key;

                bool isMatchedWord = false;
                foreach (var wordMatchMeta in index.EntitiesByWordsIndex.GetMatchesByWordAndParents(
                    wordId,
                    EntityType,
                    parents))
                {
                    if (ct.IsCancellationRequested)
                        return;

                    isMatchedWord = true;

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

                if (isMatchedWord)
                    ck.IncrementMatch();
            }
        }
    }
}
