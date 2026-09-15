using Core;

using Microsoft.Extensions.Logging;

using System;

namespace CoreTests;

internal static class QuestHistoryReaderTests
{
    public static void Run()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(
            builder => { });

        TestCompleteSnapshot(loggerFactory);
        TestUnknownUntilCommitted(loggerFactory);
        TestCompletionPredicates(loggerFactory);
        TestDroppedRecord(loggerFactory);
        TestRepeatedValues(loggerFactory);
        TestReset(loggerFactory);

        Console.WriteLine("Quest history reader tests passed.");
    }

    private static void TestCompleteSnapshot(
        ILoggerFactory loggerFactory)
    {
        QuestHistoryReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(121);

        Feed(reader, provider,
            Header(0, 2),
            Record(6062, 0, false),
            Record(6081, 0, true),
            End(0));

        Assert(reader.IsInitialized,
            "A complete history snapshot should initialize the reader.");

        AssertCompletion(reader, 6062, false);
        AssertCompletion(reader, 6081, true);
    }

    private static void TestUnknownUntilCommitted(
        ILoggerFactory loggerFactory)
    {
        QuestHistoryReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(121);

        Feed(reader, provider,
            Header(0, 1),
            Record(6062, 0, false));

        Assert(!reader.IsInitialized,
            "The reader must remain uninitialized before the end marker.");

        Assert(
            !reader.TryGetCompletion(6062, out _),
            "A record is unknown until its full snapshot commits.");

        Feed(reader, provider, End(0));

        AssertCompletion(reader, 6062, false);
    }

    private static void TestCompletionPredicates(
        ILoggerFactory loggerFactory)
    {
        QuestHistoryReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(121);

        Assert(!reader.IsCompleted(6062),
            "Unknown completion must not be reported as completed.");

        Assert(!reader.IsNotCompleted(6062),
            "Unknown completion must not be reported as not completed.");

        Feed(reader, provider,
            Header(0, 2),
            Record(6062, 0, false),
            Record(6081, 0, true),
            End(0));

        Assert(reader.IsNotCompleted(6062),
            "A confirmed false record must be not completed.");

        Assert(!reader.IsCompleted(6062),
            "A confirmed false record must not be completed.");

        Assert(reader.IsCompleted(6081),
            "A confirmed true record must be completed.");

        Assert(!reader.IsNotCompleted(6081),
            "A confirmed true record must not be not completed.");
    }

    private static void TestDroppedRecord(
        ILoggerFactory loggerFactory)
    {
        QuestHistoryReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(121);

        Feed(reader, provider,
            Header(0, 1),
            Record(6062, 0, false),
            End(0));

        Feed(reader, provider,
            Header(1, 2),
            Record(6062, 1, true),
            End(1));

        AssertCompletion(reader, 6062, false);

        Assert(
            !reader.TryGetCompletion(6081, out _),
            "An incomplete later snapshot must not replace published data.");
    }

    private static void TestRepeatedValues(
        ILoggerFactory loggerFactory)
    {
        QuestHistoryReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(121);

        int header = Header(2, 1);
        int record = Record(6081, 2, true);
        int end = End(2);

        Feed(reader, provider,
            header,
            header,
            record,
            record,
            end,
            end);

        AssertCompletion(reader, 6081, true);
    }

    private static void TestReset(ILoggerFactory loggerFactory)
    {
        QuestHistoryReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(121);

        Feed(reader, provider,
            Header(0, 1),
            Record(6062, 0, false),
            End(0));

        reader.Reset();

        Assert(!reader.IsInitialized,
            "Reset must make history data unavailable.");

        Assert(
            !reader.TryGetCompletion(6062, out _),
            "Reset must clear published completion data.");

        Feed(reader, provider,
            Header(1, 1),
            Record(6081, 1, true),
            End(1));

        AssertCompletion(reader, 6081, true);
    }

    private static QuestHistoryReader CreateReader(
        ILoggerFactory loggerFactory)
    {
        return new QuestHistoryReader(
            loggerFactory.CreateLogger<QuestHistoryReader>());
    }

    private static void Feed(
        QuestHistoryReader reader,
        NullAddonDataProvider provider,
        params int[] values)
    {
        foreach (int value in values)
        {
            provider.Data[QuestHistoryWireFormat.Cell] = value;
            reader.Update(provider);
        }
    }

    private static int Header(int generation, int count)
    {
        return QuestHistoryWireFormat.HeaderBase +
            generation * QuestHistoryWireFormat.HeaderGenerationStride +
            count;
    }

    private static int Record(
        int questId,
        int generation,
        bool isCompleted)
    {
        return questId * QuestHistoryWireFormat.RecordStride +
            generation * QuestHistoryWireFormat.CompletionStride +
            (isCompleted ? 1 : 0);
    }

    private static int End(int generation)
    {
        return QuestHistoryWireFormat.EndBase + generation;
    }

    private static void AssertCompletion(
        QuestHistoryReader reader,
        int questId,
        bool expectedCompletion)
    {
        Assert(
            reader.TryGetCompletion(
                questId,
                out bool actualCompletion),
            $"Expected quest {questId} to have a completion response.");

        Assert(actualCompletion == expectedCompletion,
            $"Quest {questId} expected completed={expectedCompletion}, " +
            $"got completed={actualCompletion}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
