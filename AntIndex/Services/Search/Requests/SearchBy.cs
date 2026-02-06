using System.Runtime.CompilerServices;
using AntIndex.Models;
using AntIndex.Models.Index;

namespace AntIndex.Services.Search.Requests;

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
        SearchContext searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        PerfomanceSettings perfomance,
        CancellationToken ct)
    {
        AntHill index = searchContext.AntHill;

        if (!(searchContext.GetResultsByType(parentType) is { } byStrat))
            return;

        Key[] parents = SelectParents(byStrat);

        for (byte queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentBundle = wordsBundle[queryWordPosition];

            Perfomancer perfomancer = perfomance.GetPerfomancer();

            for (int i = 0; i < currentBundle.Count; i++)
            {
                if (!perfomancer.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentBundle[i];

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

                    Key entityKey = new(TargetType, wordMatchMeta.EntityId);
                    EntityMeta entityMeta = index.Entities[entityKey];

                    if (!((filter?.Invoke(entityKey)) ?? true))
                        continue;

                    searchContext.AddResult(
                        entityKey,
                        entityMeta,
                        wordMatchMeta.NameWordPosition,
                        wordMatchMeta.PhraseType,
                        queryWordPosition,
                        indexWordInfo.Value);
                }

                if (isMatchedWord) perfomancer.IncrementMatch();
            }
        }
    }
}
