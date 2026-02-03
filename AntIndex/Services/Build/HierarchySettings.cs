namespace AntIndex.Services.Build;

public class HierarchySettings
{
    public static readonly HierarchySettings Default = new();

    /// <summary>
    /// Parents types for entity. Types for support AppendChilds request. (1 on many)
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><term>Key (byte)</term><description>Child type.</description></item>
    /// <item><term>Value (byte[])</term><description>Parent types.</description></item>
    /// </list>
    /// </remarks>
    public Dictionary<byte, byte[]> EntitiesParents { get; init; } = [];

    /// <summary>
    /// Container types for entity. An entity cannot be found without a found container. (1 on 1)
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><term>Key (byte)</term><description>Entity type.</description></item>
    /// <item><term>Value (byte)</term><description>Container type.</description></item>
    /// </list>
    /// </remarks>
    public Dictionary<byte, byte> EntitesContainers { get; init; } = [];
}
