namespace Core;

public readonly record struct QuestCompletionRecord(
    int QuestId,
    bool IsCompleted);
