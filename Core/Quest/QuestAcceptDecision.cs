namespace Core;

public enum QuestAcceptAction
{
    None,
    SelectOffer,
    Accept,
    Done,
}

public static class QuestAcceptDecision
{
    public static QuestAcceptAction Decide(
        bool isActive,
        QuestDialogState? dialogState)
    {
        if (isActive)
        {
            return QuestAcceptAction.Done;
        }

        return dialogState switch
        {
            QuestDialogState.Offered => QuestAcceptAction.SelectOffer,
            QuestDialogState.OfferDetail => QuestAcceptAction.Accept,
            _ => QuestAcceptAction.None,
        };
    }
}
