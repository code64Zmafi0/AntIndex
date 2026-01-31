using AntIndex.Models;

namespace AntIndex.Services.Search.Requests;

public abstract class AntRequestBase(byte targetType)
{
    public byte TargetType { get; } = targetType;

    public abstract void ProcessRequest(
        AntHill index,
        AntSearcherBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct);
}
