using System.Runtime;
using AntIndex.Models.Index;
using MessagePack;

namespace AntIndex.Models;

[MessagePackObject]
public class AntHill
{
    public AntHill() { }

    [Key(1)]
    public Dictionary<byte /*TypeId*/, Dictionary<int /*EntityId*/, EntityMeta>> Entities { get; set; } = [];

    [Key(2)]
    public Dictionary<int /*NGrammHash*/, int[] /*WordsIds*/> WordsIdsByNgramms { get; set; } = [];

    [Key(3)]
    public EntitiesByWordsIndex EntitiesByWordsIndex { get; set; } = new();

    [IgnoreMember]
    public int EntitesCount => Entities.Sum(i => i.Value.Count);

    public void Trim()
    {
        foreach (var collection in Entities.Values)
        {
            foreach (var meta in collection.Values)
            {
                if (meta.Links.Length == 0)
                    meta.Links = Array.Empty<Key>();

                if (meta.Childs.Length == 0)
                    meta.Childs = Array.Empty<Key>();
            }

            collection.TrimExcess();
        }

        Entities.TrimExcess();
        WordsIdsByNgramms.TrimExcess();
        EntitiesByWordsIndex.Trim();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
    }
}