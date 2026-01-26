using MessagePack;

namespace AntIndex.Models.Index;

[MessagePackObject]
public class EntitiesByWordsIndex()
{
    [Key(1)]
    public Dictionary<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, WordMatchMeta[]>>[/*WordId*/] EntitiesByWords { get; set; } = [];

    public WordMatchMeta[]? GetMatchesByWord(int wordId, byte entityType)
    {
        var wordMatches = EntitiesByWords[wordId];

        if (wordMatches.TryGetValue(entityType, out var mathesBundle)
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
        var wordMatches = EntitiesByWords[wordId];

        if (!wordMatches.TryGetValue(entityType, out var mathesBundle))
            yield break;

        foreach (var byKey in parentKeys)
        {
            if (!mathesBundle.TryGetValue(byKey, out var entityMatches))
                continue;

            foreach (WordMatchMeta wordMatchMeta in entityMatches)
                yield return wordMatchMeta;
        }
    }

    public void Trim(Func<Key, Key> GetKey)
    {
        foreach (var collection in EntitiesByWords)
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
    }
}
