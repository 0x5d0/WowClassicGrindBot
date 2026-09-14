using Microsoft.Extensions.Logging;

using System.Collections.Generic;

namespace Core;

public sealed class QuestReader : IReader
{
    private readonly ILogger<QuestReader> logger;

    private readonly Dictionary<int, QuestStatus> activeQuests = [];
    private readonly Dictionary<int, QuestStatus> pendingQuests = [];

    private int pendingGeneration = -1;
    private int expectedCount = -1;

    private int previousValue;
    private bool hasPreviousValue;

    public IReadOnlyDictionary<int, QuestStatus> ActiveQuests => activeQuests;

    public bool IsInitialized { get; private set; }

    public QuestReader(ILogger<QuestReader> logger)
    {
        this.logger = logger;
    }

    public void Update(IAddonDataProvider reader)
    {
        int value = reader.GetInt(QuestWireFormat.Cell);

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

        if (QuestWireFormat.TryDecodeHeader(
            value,
            out int headerGeneration,
            out int count))
        {
            StartSnapshot(headerGeneration, count);
            return;
        }

        if (QuestWireFormat.TryDecodeRecord(
            value,
            out int recordGeneration,
            out QuestRecord record))
        {
            AddRecord(recordGeneration, record);
            return;
        }

        if (QuestWireFormat.TryDecodeEnd(
            value,
            out int endGeneration))
        {
            EndSnapshot(endGeneration);
            return;
        }

        DiscardPendingSnapshot();
    }

    public bool IsActive(int questId)
    {
        return IsInitialized && activeQuests.ContainsKey(questId);
    }

    public bool IsReadyForTurnIn(int questId)
    {
        return IsInitialized &&
            activeQuests.TryGetValue(questId, out QuestStatus status) &&
            status == QuestStatus.ReadyForTurnIn;
    }

    public bool IsFailed(int questId)
    {
        return IsInitialized &&
            activeQuests.TryGetValue(questId, out QuestStatus status) &&
            status == QuestStatus.Failed;
    }

    public bool TryGetStatus(int questId, out QuestStatus status)
    {
        if (IsInitialized &&
            activeQuests.TryGetValue(questId, out status))
        {
            return true;
        }

        status = default;
        return false;
    }

    public void Reset()
    {
        activeQuests.Clear();
        IsInitialized = false;

        previousValue = 0;
        hasPreviousValue = false;

        DiscardPendingSnapshot();
    }

    private void StartSnapshot(int generation, int count)
    {
        pendingQuests.Clear();

        pendingGeneration = generation;
        expectedCount = count;
    }

    private void AddRecord(int generation, QuestRecord record)
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

        if (pendingQuests.TryGetValue(record.QuestId, out QuestStatus existing))
        {
            if (existing != record.Status)
            {
                DiscardPendingSnapshot();
            }

            return;
        }

        pendingQuests.Add(record.QuestId, record.Status);

        if (pendingQuests.Count > expectedCount)
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
            pendingQuests.Count != expectedCount)
        {
            DiscardPendingSnapshot();
            return;
        }

        bool changed = !IsInitialized || !MatchesPublishedSnapshot();

        activeQuests.Clear();

        foreach ((int questId, QuestStatus status) in pendingQuests)
        {
            activeQuests.Add(questId, status);
        }

        IsInitialized = true;
        DiscardPendingSnapshot();

        if (changed)
        {
            logger.LogInformation(
                "Quest snapshot committed: {Count} active quest(s)",
                activeQuests.Count);
        }
    }

    private bool MatchesPublishedSnapshot()
    {
        if (activeQuests.Count != pendingQuests.Count)
        {
            return false;
        }

        foreach ((int questId, QuestStatus status) in pendingQuests)
        {
            if (!activeQuests.TryGetValue(questId, out QuestStatus current) ||
                current != status)
            {
                return false;
            }
        }

        return true;
    }

    private void DiscardPendingSnapshot()
    {
        pendingQuests.Clear();

        pendingGeneration = -1;
        expectedCount = -1;
    }
}
