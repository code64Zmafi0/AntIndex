using AntIndex.Models.Index;

namespace AntIndex.Services.Search;

public class TypeSearchResult(byte type, EntityMatchesBundle[] result)
{
    public byte Type { get; } = type;

    public EntityMatchesBundle[] Result { get; } = result;
}

public class EntityMatchesBundle(EntityMeta entityMeta)
{
    public EntityMeta EntityMeta { get; } = entityMeta;

    public List<WordCompareResult> WordsMatches { get; } = new(2);

    public List<AdditionalRule> Rules { get; } = [];

    public Key Key => EntityMeta.Key;

    public int RulesScore => Rules.Sum(i => i.Score);

    public int Prescore;

    public int Score;

    internal void AddMatch(in WordCompareResult wordCompareResult)
    {
        WordsMatches.Add(wordCompareResult);
        Prescore += wordCompareResult.MatchLength;
    }
}

public readonly record struct WordCompareResult(
    WordMatchMeta MatchMeta,
    byte QueryWordPosition,
    byte MatchLength);

public readonly record struct WordCompareFactor(byte Mathes, byte Misses, byte PreviousMatch)
{
    public byte Score => Mathes > Misses ? (byte)(Mathes - Misses) : (byte)0;
}

public record AdditionalRule(string Name, int Score);