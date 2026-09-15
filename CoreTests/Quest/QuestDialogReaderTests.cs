using Core;

using Microsoft.Extensions.Logging;

using System;

namespace CoreTests;

internal static class QuestDialogReaderTests
{
    public static void Run()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(
            builder => { });

        TestEmptySnapshot(loggerFactory);
        TestRepeatedValues(loggerFactory);
        TestDroppedRecord(loggerFactory);
        TestReset(loggerFactory);

        Console.WriteLine("Quest dialog reader tests passed.");
    }

    private static void TestEmptySnapshot(
        ILoggerFactory loggerFactory)
    {
        QuestDialogReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(122);

        Feed(reader, provider, Header(0, 0), End(0));

        Assert(reader.IsInitialized,
            "An empty dialog snapshot should initialize the reader.");

        Assert(reader.Quests.Count == 0,
            "An empty dialog snapshot should clear visible quests.");
    }

    private static void TestRepeatedValues(
        ILoggerFactory loggerFactory)
    {
        QuestDialogReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(122);

        int header = Header(0, 2);
        int offered = Record(6062, 0, QuestDialogState.Offered);
        int active = Record(6083, 0, QuestDialogState.Active);
        int end = End(0);

        Feed(reader, provider,
            header,
            header,
            offered,
            offered,
            active,
            active,
            end,
            end);

        Assert(reader.Quests.Count == 2,
            "Repeated values must not create duplicate dialog entries.");

        AssertState(reader, 6062, QuestDialogState.Offered);
        AssertState(reader, 6083, QuestDialogState.Active);

        Assert(reader.IsOffered(6062),
            "An offered quest should be reported as offered.");

        Assert(reader.IsActive(6083),
            "An active quest should be reported as active.");
    }

    private static void TestDroppedRecord(
        ILoggerFactory loggerFactory)
    {
        QuestDialogReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(122);

        Feed(reader, provider,
            Header(0, 1),
            Record(6062, 0, QuestDialogState.Offered),
            End(0));

        Feed(reader, provider,
            Header(1, 2),
            Record(6062, 1, QuestDialogState.OfferDetail),
            End(1));

        AssertState(reader, 6062, QuestDialogState.Offered);

        Assert(!reader.TryGetState(6083, out _),
            "An incomplete snapshot must not replace published data.");
    }

    private static void TestReset(ILoggerFactory loggerFactory)
    {
        QuestDialogReader reader = CreateReader(loggerFactory);
        using NullAddonDataProvider provider = new(122);

        Feed(reader, provider,
            Header(0, 1),
            Record(6062, 0, QuestDialogState.Active),
            End(0));

        reader.Reset();

        Assert(!reader.IsInitialized,
            "Reset must make dialog data unavailable.");

        Assert(reader.Quests.Count == 0,
            "Reset must clear visible quests.");

        Feed(reader, provider,
            Header(1, 1),
            Record(6062, 1, QuestDialogState.Progress),
            End(1));

        AssertState(reader, 6062, QuestDialogState.Progress);
    }

    private static QuestDialogReader CreateReader(
        ILoggerFactory loggerFactory)
    {
        return new QuestDialogReader(
            loggerFactory.CreateLogger<QuestDialogReader>());
    }

    private static void Feed(
        QuestDialogReader reader,
        NullAddonDataProvider provider,
        params int[] values)
    {
        foreach (int value in values)
        {
            provider.Data[QuestDialogWireFormat.Cell] = value;
            reader.Update(provider);
        }
    }

    private static int Header(int generation, int count)
    {
        return QuestDialogWireFormat.HeaderBase +
            generation * QuestDialogWireFormat.HeaderGenerationStride +
            count;
    }

    private static int Record(
        int questId,
        int generation,
        QuestDialogState state)
    {
        return questId * QuestDialogWireFormat.RecordStride +
            generation * QuestDialogWireFormat.StateStride +
            (int)state;
    }

    private static int End(int generation)
    {
        return QuestDialogWireFormat.EndBase + generation;
    }

    private static void AssertState(
        QuestDialogReader reader,
        int questId,
        QuestDialogState expected)
    {
        Assert(reader.TryGetState(questId, out var actual),
            $"Expected quest {questId} to be visible.");

        Assert(actual == expected,
            $"Quest {questId} expected {expected}, got {actual}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}