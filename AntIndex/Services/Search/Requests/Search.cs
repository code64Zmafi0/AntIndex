using AntIndex.Models;
using AntIndex.Models.Index;

namespace AntIndex.Services.Search.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа
/// </summary>
/// <param name="entityType">Целевой тип сущности</param>
/// <param name="filter">Фильтр добавления в словарь найденных</param>
public class Search(
    byte entityType,
    Func<Key, bool>? filter = null)
    : AntRequestBase(entityType)
{
    public override void ProcessRequest(
        AntHill index,
        AntSearcherBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct)
    {
        for (byte queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentBundle = wordsBundle[queryWordPosition];

            var ck = searchContext.Perfomance.GetPerfomancer();

            for (int wbIndex = 0; wbIndex < currentBundle.Count; wbIndex++)
            {
                if (!ck.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentBundle[wbIndex];

                int wordId = indexWordInfo.Key;

                WordMatchMeta[]? list = index.EntitiesByWordsIndex.GetMatchesByWord(wordId, TargetType);

                if (list is null)
                    continue;

                ck.IncrementMatch();

                foreach (WordMatchMeta wordMatchMeta in list)
                {
                    if (ct.IsCancellationRequested)
                        return;

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
            }
        }
    }
}
