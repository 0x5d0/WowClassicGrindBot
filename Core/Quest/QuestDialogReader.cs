using Microsoft.Extensions.Logging;

using System.Collections.Generic;

namespace Core;

public sealed class QuestDialogReader : IReader
{
    private readonly ILogger<QuestDialogReader> logger;

    private readonly Dictionary<int, QuestDialogState> quests = [];
    private readonly Dictionary<int, QuestDialogState> pendingQuests = [];

    private int pendingGeneration = -1;
    private int expectedCount = -1;

    private int previousValue;
    private bool hasPreviousValue;

    public IReadOnlyDictionary<int, QuestDialogState> Quests => quests;

    public bool IsInitialized { get; private set; }

    public QuestDialogReader(ILogger<QuestDialogReader> logger)
    {
        this.logger = logger;
    }

    public void Update(IAddonDataProvider reader)
    {
        int value = reader.GetInt(QuestDialogWireFormat.Cell);

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

        if (QuestDialogWireFormat.TryDecodeHeader(
            value,
            out int generation,
            out int count))
        {
            StartSnapshot(generation, count);
            return;
        }

        if (QuestDialogWireFormat.TryDecodeRecord(
            value,
            out generation,
            out QuestDialogRecord record))
        {
            AddRecord(generation, record);
            return;
        }

        if (QuestDialogWireFormat.TryDecodeEnd(value, out generation))
        {
            EndSnapshot(generation);
            return;
        }

        DiscardPendingSnapshot();
    }

    public bool IsOffered(int questId)
    {
        return TryGetState(questId, out QuestDialogState state) &&
            state == QuestDialogState.Offered;
    }

    public bool IsActive(int questId)
    {
        return TryGetState(questId, out QuestDialogState state) &&
            state is QuestDialogState.Active or
                QuestDialogState.ReadyForTurnIn;
    }

    public bool IsReadyForTurnIn(int questId)
    {
        return TryGetState(questId, out QuestDialogState state) &&
            state == QuestDialogState.ReadyForTurnIn;
    }

    public bool TryGetState(
        int questId,
        out QuestDialogState state)
    {
        if (IsInitialized && quests.TryGetValue(questId, out state))
        {
            return true;
        }

        state = default;
        return false;
    }

    public void Reset()
    {
        quests.Clear();
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

    private void AddRecord(
        int generation,
        QuestDialogRecord record)
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

        if (pendingQuests.TryGetValue(record.QuestId, out var current))
        {
            if (current != record.State)
            {
                DiscardPendingSnapshot();
            }

            return;
        }

        pendingQuests.Add(record.QuestId, record.State);

        if (pendingQuests.Count > expectedCount)
        {
            DiscardPendingSnapshot();
        }
    }

    private void EndSnapshot(int generation)
    {
        if (expectedCount < 0 ||
            generation != pendingGeneration ||
            pendingQuests.Count != expectedCount)
        {
            DiscardPendingSnapshot();
            return;
        }

        bool changed = !IsInitialized || !MatchesSnapshot();

        quests.Clear();

        foreach ((int questId, QuestDialogState state) in pendingQuests)
        {
            quests.Add(questId, state);
        }

        IsInitialized = true;
        DiscardPendingSnapshot();

        if (changed)
        {
            logger.LogInformation(
                "Quest dialog snapshot committed: {Count} quest(s)",
                quests.Count);
        }
    }

    private bool MatchesSnapshot()
    {
        if (quests.Count != pendingQuests.Count)
        {
            return false;
        }

        foreach ((int questId, QuestDialogState state) in pendingQuests)
        {
            if (!quests.TryGetValue(questId, out var current) ||
                current != state)
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
