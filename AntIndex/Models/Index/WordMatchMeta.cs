using MessagePack;

namespace AntIndex.Models.Index;

[MessagePackObject]
public class WordMatchMeta
{
    [Key(1)]
    public int EntityId { get; }

    [Key(2)]
    public byte NameWordPosition { get; }

    [Key(3)]
    public byte PhraseType {  get; }

    public WordMatchMeta()
    {
    }

    public WordMatchMeta(
        int entityId,
        byte nameWordPosition,
        byte phraseType)
    {
        EntityId = entityId;
        NameWordPosition = nameWordPosition;
        PhraseType = phraseType; 
    }
}
