namespace Core;

public static class QuestHistoryWireFormat
{
    public const int Cell = 118;

    public const int HeaderBase = QuestWireFormat.HeaderBase;
    public const int HeaderGenerationStride =
        QuestWireFormat.HeaderGenerationStride;
    public const int MaxSnapshotCount =
        QuestWireFormat.MaxSnapshotCount;
    public const int EndBase = QuestWireFormat.EndBase;
    public const int MaxGeneration = QuestWireFormat.MaxGeneration;

    // Record: questId * 8 + generation * 2 + completedBit
    public const int RecordStride = 8;
    public const int CompletionStride = 2;
    public const int MaxQuestId = 2_097_124;

    public static bool TryDecodeHeader(
        int value,
        out int generation,
        out int count)
    {
        return QuestWireFormat.TryDecodeHeader(
            value,
            out generation,
            out count);
    }

    public static bool TryDecodeRecord(
        int value,
        out int generation,
        out QuestCompletionRecord record)
    {
        generation = 0;
        record = default;

        if (value <= 0 || value >= HeaderBase)
        {
            return false;
        }

        int questId = value / RecordStride;
        int payload = value % RecordStride;

        generation = payload / CompletionStride;
        int completedBit = payload % CompletionStride;

        if (questId <= 0 ||
            questId > MaxQuestId ||
            generation > MaxGeneration)
        {
            return false;
        }

        record = new QuestCompletionRecord(
            questId,
            completedBit == 1);

        return true;
    }

    public static bool TryDecodeEnd(
        int value,
        out int generation)
    {
        return QuestWireFormat.TryDecodeEnd(
            value,
            out generation);
    }
}
