
using Core;

using System;

namespace CoreTests;

internal static class QuestHistoryRequestTests
{
    public static void Run()
    {
        TestCommand();
        TestInvalidQuestId();
        TestTooManyQuestIds();

        Console.WriteLine("Quest history request tests passed.");
    }

    private static void TestCommand()
    {
        string command = QuestHistoryRequest.BuildCommand(
            "dominosold",
            [6083, 6062, 6083]);

        Assert(
            command ==
            "/run dominosold:SetQuestHistory({6062,6083})--",
            "The command should use sorted, unique quest IDs.");
    }

    private static void TestInvalidQuestId()
    {
        AssertThrows(() =>
            QuestHistoryRequest.BuildCommand("addon", [0]));
    }

    private static void TestTooManyQuestIds()
    {
        int[] ids = new int[QuestHistoryRequest.MaxCount + 1];

        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = i + 1;
        }

        AssertThrows(() =>
            QuestHistoryRequest.BuildCommand("addon", ids));
    }

    private static void AssertThrows(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        throw new InvalidOperationException(
            "The operation should throw ArgumentOutOfRangeException.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
