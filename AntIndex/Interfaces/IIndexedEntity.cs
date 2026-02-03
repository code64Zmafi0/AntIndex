using AntIndex.Models.Index;
using AntIndex.Services.Extensions;

namespace AntIndex.Interfaces;

public interface IIndexedEntity
{
    /// <summary>
    /// Entity key.
    /// </summary>
    Key GetKey();

    /// <summary>
    /// Entity names.
    /// </summary>
    IEnumerable<Phrase> GetNames();

    /// <summary>
    /// Components of entity.
    /// </summary>
    IEnumerable<Key> GetLinks();
}
