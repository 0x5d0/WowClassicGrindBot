namespace Core;

public static class QuestWireFormat
{
    public const int Cell = 117;

    public const int HeaderBase = 16_777_000;
    public const int HeaderGenerationStride = 64;
    public const int MaxSnapshotCount = 63;

    public const int EndBase = 16_777_192;

    public const int RecordStride = 16;
    public const int StatusStride = 4;

    public const int MaxGeneration = 2;
    public const int MaxQuestId = 1_048_561;

    public static bool TryDecodeHeader(
        int value,
        out int generation,
        out int count)
    {
        generation = 0;
        count = 0;

        if (value < HeaderBase || value >= EndBase)
        {
            return false;
        }

        int offset = value - HeaderBase;
        generation = offset / HeaderGenerationStride;
        count = offset % HeaderGenerationStride;

        return generation <= MaxGeneration &&
            count <= MaxSnapshotCount;
    }

    public static bool TryDecodeRecord(
        int value,
        out int generation,
        out QuestRecord record)
    {
        generation = 0;
        record = default;

        if (value <= 0 || value >= HeaderBase)
        {
            return false;
        }

        int questId = value / RecordStride;
        int payload = value % RecordStride;

        generation = payload / StatusStride;
        int statusValue = payload % StatusStride;

        if (questId <= 0 ||
            questId > MaxQuestId ||
            generation > MaxGeneration ||
            statusValue is < 1 or > 3)
        {
            return false;
        }

        record = new QuestRecord(
            questId,
            (QuestStatus)statusValue);

        return true;
    }

    public static bool TryDecodeEnd(
        int value,
        out int generation)
    {
        generation = 0;

        if (value < EndBase ||
            value > EndBase + MaxGeneration)
        {
            return false;
        }

        generation = value - EndBase;
        return true;
    }
}
