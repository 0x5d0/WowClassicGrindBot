using Core;

using System;

namespace CoreTests;

internal static class QuestHistoryProtocolTests
{
    public static void Run()
    {
        TestHeader();
        TestRecord();
        TestInvalidRecord();
        TestEnd();

        Console.WriteLine("Quest history wire-format tests passed.");
    }

    private static void TestHeader()
    {
        int value = QuestHistoryWireFormat.HeaderBase +
            QuestHistoryWireFormat.MaxGeneration *
            QuestHistoryWireFormat.HeaderGenerationStride +
            QuestHistoryWireFormat.MaxSnapshotCount;

        Assert(
            QuestHistoryWireFormat.TryDecodeHeader(
                value,
                out int generation,
                out int count),
            "The highest valid history header should decode.");

        Assert(generation == QuestHistoryWireFormat.MaxGeneration,
            "The header generation should decode.");

        Assert(count == QuestHistoryWireFormat.MaxSnapshotCount,
            "The header count should decode.");

        Assert(
            !QuestHistoryWireFormat.TryDecodeHeader(
                QuestHistoryWireFormat.EndBase,
                out _,
                out _),
            "An end marker must not decode as a header.");
    }

    private static void TestRecord()
    {
        int incompleteValue = 6062 *
            QuestHistoryWireFormat.RecordStride;

        Assert(
            QuestHistoryWireFormat.TryDecodeRecord(
                incompleteValue,
                out int incompleteGeneration,
                out QuestCompletionRecord incomplete),
            "An incomplete history record should decode.");

        Assert(incompleteGeneration == 0,
            "The incomplete record generation should decode.");

        Assert(incomplete.QuestId == 6062,
            "The incomplete record quest ID should decode.");

        Assert(!incomplete.IsCompleted,
            "A completed bit of zero should mean incomplete.");

        int completedValue = 6081 *
            QuestHistoryWireFormat.RecordStride +
            QuestHistoryWireFormat.MaxGeneration *
            QuestHistoryWireFormat.CompletionStride +
            1;

        Assert(
            QuestHistoryWireFormat.TryDecodeRecord(
                completedValue,
                out int completedGeneration,
                out QuestCompletionRecord completed),
            "A completed history record should decode.");

        Assert(completedGeneration ==
            QuestHistoryWireFormat.MaxGeneration,
            "The completed record generation should decode.");

        Assert(completed.QuestId == 6081,
            "The completed record quest ID should decode.");

        Assert(completed.IsCompleted,
            "A completed bit of one should mean completed.");
    }

    private static void TestInvalidRecord()
    {
        Assert(
            !QuestHistoryWireFormat.TryDecodeRecord(
                0,
                out _,
                out _),
            "Zero is idle data, not a history record.");

        int invalidGeneration = 6062 *
            QuestHistoryWireFormat.RecordStride +
            6;

        Assert(
            !QuestHistoryWireFormat.TryDecodeRecord(
                invalidGeneration,
                out _,
                out _),
            "Generations above two must be rejected.");

        int aboveMaximumQuestId =
            (QuestHistoryWireFormat.MaxQuestId + 1) *
            QuestHistoryWireFormat.RecordStride;

        Assert(
            !QuestHistoryWireFormat.TryDecodeRecord(
                aboveMaximumQuestId,
                out _,
                out _),
            "Quest IDs outside the RGB-safe range must be rejected.");
    }

    private static void TestEnd()
    {
        int value = QuestHistoryWireFormat.EndBase +
            QuestHistoryWireFormat.MaxGeneration;

        Assert(
            QuestHistoryWireFormat.TryDecodeEnd(
                value,
                out int generation),
            "The highest valid end marker should decode.");

        Assert(generation == QuestHistoryWireFormat.MaxGeneration,
            "The end generation should decode.");

        Assert(
            !QuestHistoryWireFormat.TryDecodeEnd(
                value + 1,
                out _),
            "Reserved control values must not decode as end markers.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
