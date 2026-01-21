using AntIndex.Models.Abstract;

namespace AntIndex.Models.Runtime.Requests;

public abstract class AntRequestBase(byte targetType)
{
    public byte TargetType { get; } = targetType;

    public abstract void ProcessRequest(
        AntHill index,
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        CancellationToken ct);
}
