using MessagePack;

namespace AntIndex.Models.Index;

[MessagePackObject]
public class EntityMeta
{
    public EntityMeta() { }

    public EntityMeta(Key[] links)
    {
        Links = links;
    }

    [Key(1)]
    public Key[] Links { get; set; } = Array.Empty<Key>();

    [Key(2)]
    public Key[] Childs { get; set; } = Array.Empty<Key>();
}
