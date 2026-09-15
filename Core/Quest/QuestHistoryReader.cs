using Microsoft.Extensions.Logging;

using System.Collections.Generic;

namespace Core;

public sealed class QuestHistoryReader : IReader
{
    private readonly ILogger<QuestHistoryReader> logger;

    private readonly Dictionary<int, bool> completions = [];
    private readonly Dictionary<int, bool> pendingCompletions = [];

    private int pendingGeneration = -1;
    private int expectedCount = -1;

    private int previousValue;
    private bool hasPreviousValue;

    public IReadOnlyDictionary<int, bool> Completions => completions;

    public bool IsInitialized { get; private set; }

    public QuestHistoryReader(ILogger<QuestHistoryReader> logger)
    {
        this.logger = logger;
    }

    public void Update(IAddonDataProvider reader)
    {
        int value = reader.GetInt(QuestHistoryWireFormat.Cell);

        if (value == 0)
        {
            hasPreviousValue = false;
            return;
        }

        if (hasPreviousValue && value == previousValue)
        {
            return;
        }

        previousValue = value;
        hasPreviousValue = true;

        if (QuestHistoryWireFormat.TryDecodeHeader(
            value,
            out int headerGeneration,
            out int count))
        {
            StartSnapshot(headerGeneration, count);
            return;
        }

        if (QuestHistoryWireFormat.TryDecodeRecord(
            value,
            out int recordGeneration,
            out QuestCompletionRecord record))
        {
            AddRecord(recordGeneration, record);
            return;
        }

        if (QuestHistoryWireFormat.TryDecodeEnd(
            value,
            out int endGeneration))
        {
            EndSnapshot(endGeneration);
            return;
        }

        DiscardPendingSnapshot();
    }

    public bool TryGetCompletion(int questId, out bool isCompleted)
    {
        if (IsInitialized &&
            completions.TryGetValue(questId, out isCompleted))
        {
            return true;
        }

        isCompleted = false;
        return false;
    }

    public void Reset()
    {
        completions.Clear();
        IsInitialized = false;

        previousValue = 0;
        hasPreviousValue = false;

        DiscardPendingSnapshot();
    }

    private void StartSnapshot(int generation, int count)
    {
        pendingCompletions.Clear();

        pendingGeneration = generation;
        expectedCount = count;
    }

    private void AddRecord(
        int generation,
        QuestCompletionRecord record)
    {
        if (expectedCount < 0)
        {
            return;
        }

        if (generation != pendingGeneration)
        {
            DiscardPendingSnapshot();
            return;
        }

        if (pendingCompletions.TryGetValue(
            record.QuestId,
            out bool existing))
        {
            if (existing != record.IsCompleted)
            {
                DiscardPendingSnapshot();
            }

            return;
        }

        pendingCompletions.Add(
            record.QuestId,
            record.IsCompleted);

        if (pendingCompletions.Count > expectedCount)
        {
            DiscardPendingSnapshot();
        }
    }

    private void EndSnapshot(int generation)
    {
        if (expectedCount < 0)
        {
            return;
        }

        if (generation != pendingGeneration ||
            pendingCompletions.Count != expectedCount)
        {
            DiscardPendingSnapshot();
            return;
        }

        bool changed = !IsInitialized || !MatchesPublishedSnapshot();

        completions.Clear();

        foreach ((int questId, bool isCompleted) in pendingCompletions)
        {
            completions.Add(questId, isCompleted);
        }

        IsInitialized = true;
        DiscardPendingSnapshot();

        if (changed)
        {
            logger.LogInformation(
                "Quest completion snapshot committed: {Count} record(s)",
                completions.Count);
        }
    }

    private bool MatchesPublishedSnapshot()
    {
        if (completions.Count != pendingCompletions.Count)
        {
            return false;
        }

        foreach ((int questId, bool isCompleted) in pendingCompletions)
        {
            if (!completions.TryGetValue(
                questId,
                out bool current) ||
                current != isCompleted)
            {
                return false;
            }
        }

        return true;
    }

    private void DiscardPendingSnapshot()
    {
        pendingCompletions.Clear();

        pendingGeneration = -1;
        expectedCount = -1;
    }
}
