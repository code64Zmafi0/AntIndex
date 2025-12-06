using ProtoBuf;

namespace AntIndex.Models.Index;

[ProtoContract]
public class EntitiesByWordsIndex()
{
    [ProtoMember(1)]
    public Dictionary<int /*WordId*/, Dictionary<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, WordMatchMeta[]>>> EntitiesByWords { get; set; } = [];

    public WordMatchMeta[]? GetMatchesByWord(int wordId, byte entityType)
    {
        if (EntitiesByWords.TryGetValue(wordId, out var wordMatches)
            && wordMatches.TryGetValue(entityType, out var mathesBundle)
            && mathesBundle.TryGetValue(Key.Default, out var matches))
        {
            return matches;
        }

        return null;
    }

    public IEnumerable<WordMatchMeta> GetMatchesByWordAndParents(
        int wordId,
        byte entityType,
        IEnumerable<Key> parentKeys)
    {
        if (!EntitiesByWords.TryGetValue(wordId, out var wordMatches)
            || !wordMatches.TryGetValue(entityType, out var mathesBundle))
            yield break;

        foreach (var byKey in parentKeys)
        {
            if (!mathesBundle.TryGetValue(byKey, out var entityMatches))
                continue;

            for (int i = 0; i < entityMatches.Length; i++)
                yield return entityMatches[i];
        }
    }

    public void Trim(Func<Key, Key> GetKey)
    {
        foreach (var collection in EntitiesByWords.Values)
        {
            foreach (var subCollection in collection)
            {
                Dictionary<Key, WordMatchMeta[]> res = subCollection.Value.Select(i =>
                {
                    var key = i.Key.Equals(Key.Default)
                        ? Key.Default
                        : GetKey(i.Key);

                    return new KeyValuePair<Key, WordMatchMeta[]>(key, i.Value);
                })
                .ToDictionary();

                res.TrimExcess();

                collection[subCollection.Key] = res;
            }

            collection.TrimExcess();
        }

        EntitiesByWords.TrimExcess();
    }
}
