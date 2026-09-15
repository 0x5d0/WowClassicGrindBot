using System;
using System.Collections.Generic;
using System.Linq;

namespace Core;

public static class QuestHistoryRequest
{
    public const int MaxCount = 63;
    public const int MaxQuestId = 2_097_124;

    public static string BuildCommand(
        string addonTitle,
        IEnumerable<int> questIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonTitle);

        int[] ids = Normalize(questIds);
        string values = string.Join(',', ids);

        return $"/run {addonTitle}:SetQuestHistory({{{values}}})--";
    }

    public static int[] Normalize(IEnumerable<int> questIds)
    {
        ArgumentNullException.ThrowIfNull(questIds);

        int[] ids = questIds
            .Distinct()
            .Order()
            .ToArray();

        if (ids.Length > MaxCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(questIds),
                $"A watch list can contain at most {MaxCount} quest IDs.");
        }

        foreach (int id in ids)
        {
            if (id <= 0 || id > MaxQuestId)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(questIds),
                    $"Quest IDs must be between 1 and {MaxQuestId}.");
            }
        }

        return ids;
    }
}
