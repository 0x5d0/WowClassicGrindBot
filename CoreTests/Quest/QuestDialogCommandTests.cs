using Core;

using System;

namespace CoreTests;

internal static class QuestDialogCommandTests
{
    public static void Run()
    {
        SelectOfferedUsesQuestId();
        AcceptUsesAcceptQuest();
        SelectOfferedRejectsInvalidId();
    }

    private static void SelectOfferedUsesQuestId()
    {
        string command = QuestDialogCommand.BuildSelectOffered(6062);

        Assert(
            command == "/run C_GossipInfo.SelectAvailableQuest(6062)",
            "Selecting an offered quest should use its ID.");
    }

    private static void AcceptUsesAcceptQuest()
    {
        string command = QuestDialogCommand.BuildAccept();

        Assert(
            command == "/run AcceptQuest()",
            "Accepting should use the quest accept command.");
    }

    private static void SelectOfferedRejectsInvalidId()
    {
        try
        {
            QuestDialogCommand.BuildSelectOffered(0);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        throw new InvalidOperationException(
            "Selecting an offered quest should reject ID zero.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
