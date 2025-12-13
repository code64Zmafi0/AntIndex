using System.Runtime.CompilerServices;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа по заданным родителям (Parent)
/// </summary>
/// <param name="entityType">Целевой тип сущности</param>
/// <param name="parentsKeys">Ключи родителей (Parent)</param>
/// <param name="resultVisionFilter">Фильтр отображения результатов в итоговом списке поиска</param>
/// <param name="filter">Фильтр добавления в словарь найденных</param>
public class SearchByKeys(
    byte entityType,
    Key[] parentsKeys,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? resultVisionFilter = null,
    Func<Key, bool>? filter = null)
    : SearchBy(entityType, parentsKeys[0].Type, resultVisionFilter, filter)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override IEnumerable<Key> SelectParents(AntRequest context)
        => parentsKeys;
}
