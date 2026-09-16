using Core;

using System;

namespace CoreTests;

internal static class QuestAcceptDecisionTests
{
    public static void Run()
    {
        ActiveQuestIsDone();
        OfferedQuestSelectsOffer();
        OfferDetailAcceptsQuest();
        OtherStateDoesNothing();
    }

    private static void ActiveQuestIsDone()
    {
        Assert(
            QuestAcceptDecision.Decide(
                true,
                QuestDialogState.Offered) == QuestAcceptAction.Done,
            "An active quest should finish acceptance.");
    }

    private static void OfferedQuestSelectsOffer()
    {
        Assert(
            QuestAcceptDecision.Decide(
                false,
                QuestDialogState.Offered) ==
                QuestAcceptAction.SelectOffer,
            "An offered quest should select its offer.");
    }

    private static void OfferDetailAcceptsQuest()
    {
        Assert(
            QuestAcceptDecision.Decide(
                false,
                QuestDialogState.OfferDetail) ==
                QuestAcceptAction.Accept,
            "An offer-detail page should accept its quest.");
    }

    private static void OtherStateDoesNothing()
    {
        Assert(
            QuestAcceptDecision.Decide(
                false,
                QuestDialogState.Progress) == QuestAcceptAction.None,
            "A progress page should not run an accept action.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
