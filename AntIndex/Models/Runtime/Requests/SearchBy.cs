using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа по найденным родителям (Parent)
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="parentType">Тип сущности родителя (Parent)</param>
/// <param name="filter">Фильтр добавления в словарь найденных</param>
/// <param name="parentsFilter">Фильтр родителей по которым осущетсвляем поиск</param>
public class SearchBy(
    byte targetType,
    byte parentType,
    Func<Key, bool>? filter = null,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<Key>>? parentsFilter = null) : AntRequestBase(targetType)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual Key[] SelectParents(Dictionary<Key, EntityMatchesBundle> byStrat)
        => (parentsFilter is null
            ? byStrat.Keys
            : parentsFilter.Invoke(byStrat.Values)).ToArray();

    public override void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        if (!index.Entities.TryGetValue(TargetType, out var entities)
            || !(searchContext.GetResultsByType(parentType) is { } byStrat))
            return;

        Key[] parents = SelectParents(byStrat);

        for (byte queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
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
                    TargetType,
                    parents))
                {
                    if (ct.IsCancellationRequested)
                        return;

                    isMatchedWord = true;

                    EntityMeta entityMeta = entities[wordMatchMeta.EntityId];
                    Key entityKey = entityMeta.Key;

                    if (!((filter?.Invoke(entityKey)) ?? true))
                        continue;

                    searchContext.AddResult(entityMeta, new(wordMatchMeta, queryWordPosition, indexWordInfo.Value));
                }

                if (isMatchedWord) ck.IncrementMatch();
            }
        }
    }
}
