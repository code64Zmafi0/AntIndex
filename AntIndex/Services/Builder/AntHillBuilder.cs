using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AntIndex.Interfaces;
using AntIndex.Models;
using AntIndex.Models.Index;
using AntIndex.Models.Runtime;
using AntIndex.Services.Extensions;
using AntIndex.Services.Normalizing;
using AntIndex.Services.Splitting;

namespace AntIndex.Services.Builder;

public class AntHillBuilder(INormalizer normalizer, IPhraseSplitter phraseSplitter)
{
    private readonly Dictionary<byte, Dictionary<int, EntityMeta>> Entities = [];
    private readonly Dictionary<Key, HashSet<Key>> Childs = [];
    private readonly EntitiesByWordsIndex EntitiesByWordsIndex = new();
    private readonly WordsBuildBundle WordsBundle = new();

    public void AddEntity(IIndexedEntity indexedEntity)
    {
        Key key = indexedEntity.GetKey();

        if (Entities.TryGetValue(key.Type, out var ids) && ids.ContainsKey(key.Id))
            return;

        var names = indexedEntity.GetNames();
        var byKey = indexedEntity.ByParent();
        HashSet<Key> nodesKeys = [];

        if (byKey is not null)
            nodesKeys.Add(byKey);

        foreach (var node in indexedEntity.Parents())
        {
            nodesKeys.Add(node);

            ref var set = ref CollectionsMarshal.GetValueRefOrAddDefault(Childs, node, out var exists);

            if (!exists)
                set = [];

            set!.Add(key);
        }

        HashSet<int> uniqWords = [];
        (string[] TokenizedPhrase, byte PhraseType)[] namesToBuild = GetNamesToBuild(names, normalizer, phraseSplitter);
        for (int nameIndex = 0; nameIndex < namesToBuild.Length; nameIndex++)
        {
            (string[] phrase, byte phraseType) = namesToBuild[nameIndex];

            for (byte wordNamePosition = 0; wordNamePosition < phrase.Length && wordNamePosition < byte.MaxValue; wordNamePosition++)
            {
                string word = phrase[wordNamePosition];
                var wordId = WordsBundle.GetWordId(word);

                if (!uniqWords.Add(wordId))
                    continue;

                WordMatchMeta wordMatchMeta = new(key.Id, wordNamePosition, phraseType);
                EntitiesByWordsIndex.AddMatch(wordId, key.Type, byKey, wordMatchMeta);
            }
        }

        ref var byTypeEntiteies = ref CollectionsMarshal.GetValueRefOrAddDefault(Entities, key.Type, out var containsType);

        if (!containsType)
            byTypeEntiteies = [];

        byTypeEntiteies![key.Id] = new(key, [.. nodesKeys]);
    }

    private static (string[] TokenizedPhrase, byte PhraseType)[] GetNamesToBuild(
        IEnumerable<Phrase> phrases,
        INormalizer normalizer,
        IPhraseSplitter phraseSplitter)
        => [.. phrases.Select(phrase =>
        {
            string normalizedPhrase = normalizer.Normalize(phrase.Text!);
            string[] tokenizedPhrase = phraseSplitter.Tokenize(normalizedPhrase);
            return (tokenizedPhrase, phrase.PhraseType);
        })];

    public AntHill Build()
    {
        foreach (var item in Childs)
            Entities[item.Key.Type][item.Key.Id].Childs = [.. item.Value];

        Dictionary<int, HashSet<int>> wordsIdsByNgramms = [];
        Dictionary<int, int[]> wordsByIds = [];

        foreach (var item in WordsBundle.GetWordsByIds())
        {
            int[] ngramms = Ant.GetNgrams(item.Key);

            wordsByIds[item.Value] = ngramms;

            for (int i = 0; i < ngramms.Length; i++)
            {
                int ngramm = ngramms[i];
                ref var words = ref CollectionsMarshal.GetValueRefOrAddDefault(wordsIdsByNgramms, ngramm, out var exists);

                if (!exists)
                    words = [];

                words!.Add(item.Value);
            }
        }

        return new AntHill()
        {
            Entities = Entities,
            WordsByIds = wordsByIds,
            EntitiesByWordsIndex = EntitiesByWordsIndex,
            WordsIdsByNgramms = wordsIdsByNgramms.ToDictionary(i => i.Key, i => i.Value.ToArray()),
        };
    }
}

public class WordsBuildBundle()
{
    private int CurrentId = int.MinValue;

    private readonly Dictionary<string, int> Pairs = [];

    public int GetWordId(string word)
    {
        ref var id = ref CollectionsMarshal.GetValueRefOrAddDefault(Pairs, word, out var exists);
        if (exists)
            return id;

        id = CurrentId++;
        return id;
    }

    public IEnumerable<KeyValuePair<string, int>> GetWordsByIds()
    {
        var words = new Dictionary<int, Word>(Pairs.Count);

        foreach (var wordIdPair in Pairs.OrderBy(i => i.Key))
            yield return wordIdPair;
    }
}
