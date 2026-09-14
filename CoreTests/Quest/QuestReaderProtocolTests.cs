using Core;

using Microsoft.Extensions.Logging;

using System;

namespace CoreTests;

internal static class QuestReaderProtocolTests
{
    public static void Run()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => { });

        TestEmptySnapshot(loggerFactory);
        TestRepeatedValues(loggerFactory);
        TestEqualSizeConsecutiveSnapshots(loggerFactory);
        TestDroppedHeader(loggerFactory);
        TestDroppedRecord(loggerFactory);
        TestInvalidValue(loggerFactory);
        TestReset(loggerFactory);

        Console.WriteLine("QuestReader protocol tests passed.");
    }

    private static void TestEmptySnapshot(ILoggerFactory loggerFactory)
    {
        QuestReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(120);

        Feed(reader, provider,
            Header(0, 0),
            End(0));

        Assert(reader.IsInitialized,
            "An empty snapshot should initialize the reader.");

        Assert(reader.ActiveQuests.Count == 0,
            "An empty snapshot should publish zero active quests.");
    }

    private static void TestRepeatedValues(ILoggerFactory loggerFactory)
    {
        QuestReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(120);

        int header = Header(0, 2);
        int firstRecord = Record(6062, 0, QuestStatus.Incomplete);
        int secondRecord = Record(6083, 0, QuestStatus.ReadyForTurnIn);
        int end = End(0);

        Feed(reader, provider,
            header,
            header,
            header,

            firstRecord,
            firstRecord,
            firstRecord,

            secondRecord,
            secondRecord,

            end,
            end,
            end);

        Assert(reader.IsInitialized,
            "A valid snapshot with repeated captured values should initialize.");

        Assert(reader.ActiveQuests.Count == 2,
            "Repeated values must not create duplicate quests.");

        AssertStatus(reader, 6062, QuestStatus.Incomplete);
        AssertStatus(reader, 6083, QuestStatus.ReadyForTurnIn);
    }

    private static void TestEqualSizeConsecutiveSnapshots(
        ILoggerFactory loggerFactory)
    {
        QuestReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(120);

        Feed(reader, provider,
            Header(0, 2),
            Record(6062, 0, QuestStatus.Incomplete),
            Record(6083, 0, QuestStatus.ReadyForTurnIn),
            End(0));

        Feed(reader, provider,
            Header(1, 2),
            Record(6062, 1, QuestStatus.Failed),
            Record(6082, 1, QuestStatus.Incomplete),
            End(1));

        Assert(reader.ActiveQuests.Count == 2,
            "The second snapshot should replace the first one.");

        AssertStatus(reader, 6062, QuestStatus.Failed);
        AssertStatus(reader, 6082, QuestStatus.Incomplete);

        Assert(!reader.IsActive(6083),
            "A quest absent from a committed later snapshot must be removed.");
    }

    private static void TestDroppedHeader(ILoggerFactory loggerFactory)
    {
        QuestReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(120);

        Feed(reader, provider,
            Header(0, 1),
            Record(6062, 0, QuestStatus.Incomplete),
            End(0));

        // Start generation 1, but never receive its end marker.
        Feed(reader, provider,
            Header(1, 1));

        // Simulate losing generation 2's header.
        Feed(reader, provider,
            Record(6083, 2, QuestStatus.ReadyForTurnIn),
            End(2));

        Assert(reader.ActiveQuests.Count == 1,
            "An incomplete or mismatched snapshot must not replace published data.");

        AssertStatus(reader, 6062, QuestStatus.Incomplete);

        Assert(!reader.IsActive(6083),
            "Records without their matching header must not be published.");
    }

    private static void TestDroppedRecord(ILoggerFactory loggerFactory)
    {
        QuestReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(120);

        Feed(reader, provider,
            Header(0, 1),
            Record(6062, 0, QuestStatus.Incomplete),
            End(0));

        // Header expects two records, but only one arrives.
        Feed(reader, provider,
            Header(1, 2),
            Record(6062, 1, QuestStatus.ReadyForTurnIn),
            End(1));

        Assert(reader.ActiveQuests.Count == 1,
            "A snapshot with a missing record must not be committed.");

        AssertStatus(reader, 6062, QuestStatus.Incomplete);
    }

    private static void TestInvalidValue(ILoggerFactory loggerFactory)
    {
        QuestReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(120);

        Feed(reader, provider,
            Header(0, 1),
            Record(6062, 0, QuestStatus.Incomplete),
            End(0));

        Feed(reader, provider,
            Header(1, 1),

            // 16,777,195 is in the reserved and invalid control range.
            QuestWireFormat.EndBase + QuestWireFormat.MaxGeneration + 1,

            Record(6083, 1, QuestStatus.ReadyForTurnIn),
            End(1));

        Assert(reader.ActiveQuests.Count == 1,
            "Invalid data must discard the pending snapshot.");

        AssertStatus(reader, 6062, QuestStatus.Incomplete);

        Assert(!reader.IsActive(6083),
            "Data after an invalid value must not be published without a new header.");
    }

    private static void TestReset(ILoggerFactory loggerFactory)
    {
        QuestReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(120);

        Feed(reader, provider,
            Header(0, 1),
            Record(6062, 0, QuestStatus.Incomplete),
            End(0));

        reader.Reset();

        Assert(!reader.IsInitialized,
            "Reset must make quest data unavailable until another snapshot commits.");

        Assert(reader.ActiveQuests.Count == 0,
            "Reset must clear published quests.");

        Feed(reader, provider,
            Header(1, 1),
            Record(6083, 1, QuestStatus.ReadyForTurnIn),
            End(1));

        Assert(reader.IsInitialized,
            "A valid snapshot after reset should initialize the reader again.");

        AssertStatus(reader, 6083, QuestStatus.ReadyForTurnIn);
    }

    private static QuestReader CreateReader(ILoggerFactory loggerFactory)
    {
        return new QuestReader(
            loggerFactory.CreateLogger<QuestReader>());
    }

    private static void Feed(
        QuestReader reader,
        NullAddonDataProvider provider,
        params int[] values)
    {
        foreach (int value in values)
        {
            provider.Data[QuestWireFormat.Cell] = value;
            reader.Update(provider);
        }
    }

    private static int Header(int generation, int count)
    {
        return QuestWireFormat.HeaderBase +
            generation * QuestWireFormat.HeaderGenerationStride +
            count;
    }

    private static int Record(
        int questId,
        int generation,
        QuestStatus status)
    {
        return questId * QuestWireFormat.RecordStride +
            generation * QuestWireFormat.StatusStride +
            (int)status;
    }

    private static int End(int generation)
    {
        return QuestWireFormat.EndBase + generation;
    }

    private static void AssertStatus(
        QuestReader reader,
        int questId,
        QuestStatus expectedStatus)
    {
        Assert(reader.TryGetStatus(questId, out QuestStatus actualStatus),
            $"Expected quest {questId} to be active.");

        Assert(actualStatus == expectedStatus,
            $"Quest {questId} expected {expectedStatus}, got {actualStatus}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
