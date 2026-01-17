using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime;

public class TypeSearchResult(byte type, EntityMatchesBundle[] result)
{
    public byte Type { get; } = type;

    public EntityMatchesBundle[] Result { get; } = result;
}

public class EntityMatchesBundle(EntityMeta entityMeta)
{
    public EntityMeta EntityMeta { get; } = entityMeta;

    public List<WordCompareResult> WordsMatches { get; } = new(2);

    public Key Key => EntityMeta.Key;

    public List<AdditionalRule> Rules { get; } = [];

    public int RulesScore => Rules.Sum(i => i.Score);

    public int Prescore;

    public int Score;

    internal void AddMatch(WordCompareResult wordCompareResult)
    {
        WordsMatches.Add(wordCompareResult);
        Prescore += wordCompareResult.MatchLength;
    }
}

public record WordCompareResult(
    int QueryWordPosition,
    WordMatchMeta MatchMeta,
    int MatchLength);

public record AdditionalRule(string Name, int Score);