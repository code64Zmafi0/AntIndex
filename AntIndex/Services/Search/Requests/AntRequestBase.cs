using AntIndex.Models;

namespace AntIndex.Services.Search.Requests;

public abstract class AntRequestBase(byte targetType)
{
    public byte TargetType { get; } = targetType;

    public abstract void ProcessRequest(
        SearchContext searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        PerfomanceSettings perfomance,
        CancellationToken ct);
}
