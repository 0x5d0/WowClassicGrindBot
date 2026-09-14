using Microsoft.Extensions.Logging;

namespace Core;

public sealed class QuestReader : IReader
{
    public const int QuestStateCell = 117;

    private readonly ILogger<QuestReader> logger;

    public RecordInt State { get; } = new(QuestStateCell);

    public bool IsActive => (State.Value & 1) != 0;
    public bool IsReadyForTurnIn => (State.Value & 2) != 0;
    public bool IsCompleted => (State.Value & 4) != 0;

    public QuestReader(ILogger<QuestReader> logger)
    {
        this.logger = logger;
        State.Changed += OnStateChanged;
    }

    public void Update(IAddonDataProvider reader)
    {
        State.Update(reader);
    }

    public void Reset()
    {
        State.Reset();
    }

    private void OnStateChanged()
    {
        logger.LogInformation(
            "Quest state updated: value={State}, active={Active}, ready={Ready}, completed={Completed}",
            State.Value,
            IsActive,
            IsReadyForTurnIn,
            IsCompleted);
    }
}
