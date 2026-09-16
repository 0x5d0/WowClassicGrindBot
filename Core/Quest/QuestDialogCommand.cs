using System;

namespace Core;

public static class QuestDialogCommand
{
    public static string BuildSelectOffered(int questId)
    {
        return $"/run C_GossipInfo.SelectAvailableQuest({QuestId(questId)})";
    }

    public static string BuildAccept()
    {
        return "/run AcceptQuest()";
    }

    private static int QuestId(int questId)
    {
        if (questId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questId));
        }

        return questId;
    }
}
