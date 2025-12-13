using System.Diagnostics.CodeAnalysis;
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
public class SearchBy(
    byte entityType,
    byte byType,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? resultVisionFilter = null,
    Func<Key, bool>? filter = null) : AntRequest(entityType, resultVisionFilter, filter)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual IEnumerable<Key> SelectParents(AntRequest parentRequest)
        => parentRequest.SearchResult.Keys;

    public override void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        Dictionary<int, byte>[] wordsBundle,
        CancellationToken ct)
    {
        if (!index.Entities.TryGetValue(EntityType, out var entities)
            || !(searchContext.GetRequestByType(byType) is { } byStrat))
            return;

        for (int queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            var ck = searchContext.Perfomance.GetPerfomancer(wordsBundle[queryWordPosition].Count);

            foreach (var indexWordInfo in wordsBundle[queryWordPosition])
            {
                if (!ck.NeedContinue)
                    break;

                ck.IncrementCheck();

                int wordId = indexWordInfo.Key;

                bool isMatchedWord = false;
                foreach (var wordMatchMeta in index.EntitiesByWordsIndex.GetMatchesByWordAndParents(
                    wordId,
                    EntityType,
                    SelectParents(byStrat)))
                {
                    if (ct.IsCancellationRequested)
                        return;

                    isMatchedWord = true;

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

                if (isMatchedWord)
                    ck.IncrementMatch();
            }
        }
    }
}
