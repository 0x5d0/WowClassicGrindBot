using System.Collections.Generic;

namespace Core;

public sealed class QuestHistorySender
{
    private readonly AddonConfigurator addon;
    private readonly ExecGameCommand commands;
    private readonly QuestHistoryReader reader;

    public QuestHistorySender(
        AddonConfigurator addon,
        ExecGameCommand commands,
        QuestHistoryReader reader)
    {
        this.addon = addon;
        this.commands = commands;
        this.reader = reader;
    }

    public void Send(IEnumerable<int> questIds)
    {
        string command = QuestHistoryRequest.BuildCommand(
            addon.Config.Title,
            questIds);

        reader.Reset();
        commands.Run(command, "Sending quest history watch list");
    }
}
