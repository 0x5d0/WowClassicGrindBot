namespace Core;

public static class QuestDialogWireFormat
{
    public const int Cell = 119;

    public const int HeaderBase = QuestWireFormat.HeaderBase;
    public const int HeaderGenerationStride =
        QuestWireFormat.HeaderGenerationStride;
    public const int MaxSnapshotCount =
        QuestWireFormat.MaxSnapshotCount;
    public const int EndBase = QuestWireFormat.EndBase;
    public const int MaxGeneration = QuestWireFormat.MaxGeneration;

    public const int RecordStride = 32;
    public const int StateStride = 8;
    public const int MaxQuestId = 524_280;

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
        out QuestDialogRecord record)
    {
        generation = 0;
        record = default;

        if (value <= 0 || value >= HeaderBase)
        {
            return false;
        }

        int questId = value / RecordStride;
        int payload = value % RecordStride;

        generation = payload / StateStride;
        int stateValue = payload % StateStride;

        if (questId <= 0 ||
            questId > MaxQuestId ||
            generation > MaxGeneration ||
            stateValue is < 1 or > 6)
        {
            return false;
        }

        record = new QuestDialogRecord(
            questId,
            (QuestDialogState)stateValue);

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
