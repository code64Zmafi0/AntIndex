using MessagePack;

namespace AntIndex.Models.Index;

[MessagePackObject]
public class EntityMeta
{
    public EntityMeta() { }

    public EntityMeta(Key key, Key[] nodes)
    {
        Key = key;
        Links = nodes;
    }

    [Key(1)]
    public Key Key { get; } = new(0, 0);

    [Key(2)]
    public Key[] Links { get; set; } = Array.Empty<Key>();

    [Key(3)]
    public Key[] Childs { get; set; } = Array.Empty<Key>();
}
