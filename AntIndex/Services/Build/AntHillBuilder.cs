using System.Runtime.InteropServices;
using AntIndex.Interfaces;
using AntIndex.Models;
using AntIndex.Models.Index;
using AntIndex.Services.Extensions;
using AntIndex.Services.Normalizing;
using AntIndex.Services.Splitting;

namespace AntIndex.Services.Build;

public class AntHillBuilder(INormalizer normalizer, IPhraseSplitter phraseSplitter, HierarchySettings? settings = null)
{
    private readonly Dictionary<byte, Dictionary<int, EntityMeta>> Entities = [];
    private readonly Dictionary<Key, HashSet<Key>> Childs = [];
    private readonly EntitiesByWordsBuilder EntitiesByWordsIndex = new();
    private readonly WordsBuildBundle WordsBundle = new();
    private readonly HierarchySettings hierarchySettings = settings ?? HierarchySettings.Default;

    public void AddEntity(in IIndexedEntity indexedEntity)
    {
        Key key = indexedEntity.GetKey();
        Key? containerKey = null;

        if (Entities.TryGetValue(key.Type, out var ids) && ids.ContainsKey(key.Id))
            return;

        var names = indexedEntity.GetNames();

        HashSet<Key> linksKeys = [];

        byte? containerType = hierarchySettings.EntitesContainers.TryGetValue(key.Type, out byte type) ? type : null;
        byte[] parentsTypes = hierarchySettings.EntitiesParents.TryGetValue(key.Type, out byte[]? types) ? types : [];

        foreach (Key link in indexedEntity.GetLinks())
        {
            if (!containerKey.HasValue && link.Type == containerType)
            {
                containerKey = link;
            }

            if (parentsTypes.Contains(link.Type))
            {
                ref var set = ref CollectionsMarshal.GetValueRefOrAddDefault(Childs, link, out var exists);

                if (!exists)
                    set = [];

                set!.Add(key);
            }

            linksKeys.Add(link);
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
                EntitiesByWordsIndex.AddMatch(wordId, key.Type, containerKey, wordMatchMeta);
            }
        }

        ref var byTypeEntiteies = ref CollectionsMarshal.GetValueRefOrAddDefault(Entities, key.Type, out var containsType);

        if (!containsType)
            byTypeEntiteies = [];

        byTypeEntiteies![key.Id] = new(key, [.. linksKeys]);
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
        bool CheckMeta(Key key, out EntityMeta? meta)
        {
            meta = null;

            return Entities.TryGetValue(key.Type, out var entitiesByIds)
                   && entitiesByIds.TryGetValue(key.Id, out meta);
        }

        foreach (var entity in Entities.Values.SelectMany(i => i.Values))
        {
            entity.Childs = [.. entity.Childs.Where(i => CheckMeta(i, out _))];
        }

        foreach (KeyValuePair<Key, HashSet<Key>> item in Childs)
        {
            if (CheckMeta(item.Key, out var meta))
            {
                meta!.Childs = [.. item.Value.Where(i => CheckMeta(i, out _))];
            }
        };

        Dictionary<int, HashSet<int>> wordsIdsByNgramms = [];

        foreach (var item in WordsBundle.GetWordsByIds())
        {
            int[] ngramms = Ant.GetNgrams(item.Key);

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
            EntitiesByWordsIndex = EntitiesByWordsIndex.CreateIndex(),
            WordsIdsByNgramms = wordsIdsByNgramms.ToDictionary(i => i.Key, i => i.Value.ToArray()),
        };
    }
}

public class WordsBuildBundle()
{
    private int CurrentId = 0;

    public readonly Dictionary<string, int> Pairs = [];

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
        foreach (var wordIdPair in Pairs.OrderBy(i => i.Key))
            yield return wordIdPair;
    }
}
