using System.Runtime.InteropServices;
using AntIndex.Models.Index;

namespace AntIndex.Services.Builder;

public class EntitiesByWordsBuilder()
{
    public Dictionary<int /*WordId*/, Dictionary<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, List<WordMatchMeta>>>> EntitiesByWords { get; } = [];

    public void AddMatch(int wordId, byte entityType, Key? containerKey, WordMatchMeta wordMatch)
    {
        containerKey ??= Key.Default;
        ref var wordMatches = ref CollectionsMarshal.GetValueRefOrAddDefault(EntitiesByWords, wordId, out var exists);

        if (!exists)
            wordMatches = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(wordMatches!, entityType, out exists);

        if (!exists)
            matchesBundle = [];

        ref var matches = ref CollectionsMarshal.GetValueRefOrAddDefault(matchesBundle!, containerKey.Value, out exists);

        if (!exists)
            matches = [];

        matches!.Add(wordMatch);
    }

    public EntitiesByWordsIndex CreateIndex()
    {
        var entitiesByWords = new Dictionary<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, WordMatchMeta[]>>[EntitiesByWords.Count];

        foreach (var wordMatch in EntitiesByWords)
        {
            entitiesByWords[wordMatch.Key] = wordMatch.Value
                .ToDictionary(
                    i => i.Key,
                    i => i.Value
                        .ToDictionary(
                            i => i.Key,
                            i => i.Value.ToArray()));
        }

        return new()
        {
            EntitiesByWords = entitiesByWords
        };
    }
}
