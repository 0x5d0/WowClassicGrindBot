using Core;

using System;

namespace CoreTests;

internal static class QuestDialogProtocolTests
{
    public static void Run()
    {
        TestHeader();
        TestRecords();
        TestInvalidRecords();
        TestEnd();

        Console.WriteLine("Quest dialog protocol tests passed.");
    }

    private static void TestHeader()
    {
        int value = QuestDialogWireFormat.HeaderBase +
            QuestDialogWireFormat.MaxGeneration *
            QuestDialogWireFormat.HeaderGenerationStride +
            QuestDialogWireFormat.MaxSnapshotCount;

        Assert(
            QuestDialogWireFormat.TryDecodeHeader(
                value,
                out int generation,
                out int count),
            "The highest valid dialog header should decode.");

        Assert(generation == QuestDialogWireFormat.MaxGeneration,
            "The dialog header generation should decode.");

        Assert(count == QuestDialogWireFormat.MaxSnapshotCount,
            "The dialog header count should decode.");
    }

    private static void TestRecords()
    {
        int offeredValue = 6062 *
            QuestDialogWireFormat.RecordStride +
            (int)QuestDialogState.Offered;

        Assert(
            QuestDialogWireFormat.TryDecodeRecord(
                offeredValue,
                out int offeredGeneration,
                out QuestDialogRecord offered),
            "An offered quest record should decode.");

        Assert(offeredGeneration == 0,
            "The offered record generation should decode.");

        Assert(offered.QuestId == 6062,
            "The offered record quest ID should decode.");

        Assert(offered.State == QuestDialogState.Offered,
            "The offered record state should decode.");

        int rewardValue = 6081 *
            QuestDialogWireFormat.RecordStride +
            QuestDialogWireFormat.MaxGeneration *
            QuestDialogWireFormat.StateStride +
            (int)QuestDialogState.Reward;

        Assert(
            QuestDialogWireFormat.TryDecodeRecord(
                rewardValue,
                out int rewardGeneration,
                out QuestDialogRecord reward),
            "A reward quest record should decode.");

        Assert(rewardGeneration == QuestDialogWireFormat.MaxGeneration,
            "The reward record generation should decode.");

        Assert(reward.QuestId == 6081,
            "The reward record quest ID should decode.");

        Assert(reward.State == QuestDialogState.Reward,
            "The reward record state should decode.");
    }

    private static void TestInvalidRecords()
    {
        Assert(
            !QuestDialogWireFormat.TryDecodeRecord(
                0,
                out _,
                out _),
            "Zero is idle data, not a dialog record.");

        int invalidState = 6062 *
            QuestDialogWireFormat.RecordStride +
            7;

        Assert(
            !QuestDialogWireFormat.TryDecodeRecord(
                invalidState,
                out _,
                out _),
            "States above six must be rejected.");

        int invalidGeneration = 6062 *
            QuestDialogWireFormat.RecordStride +
            3 * QuestDialogWireFormat.StateStride +
            (int)QuestDialogState.Offered;

        Assert(
            !QuestDialogWireFormat.TryDecodeRecord(
                invalidGeneration,
                out _,
                out _),
            "Generations above two must be rejected.");

        int aboveMaximumQuestId =
            (QuestDialogWireFormat.MaxQuestId + 1) *
            QuestDialogWireFormat.RecordStride;

        Assert(
            !QuestDialogWireFormat.TryDecodeRecord(
                aboveMaximumQuestId,
                out _,
                out _),
            "Quest IDs outside the RGB-safe range must be rejected.");
    }

    private static void TestEnd()
    {
        int value = QuestDialogWireFormat.EndBase +
            QuestDialogWireFormat.MaxGeneration;

        Assert(
            QuestDialogWireFormat.TryDecodeEnd(
                value,
                out int generation),
            "The highest valid dialog end marker should decode.");

        Assert(generation == QuestDialogWireFormat.MaxGeneration,
            "The dialog end generation should decode.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
