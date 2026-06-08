namespace BetterBTD.Models.AutoTasks;

public enum RaceAutoTaskScriptRunState
{
    NotStarted,
    Running,
    FinishedCurrentStage
}

public static class RaceAutoTaskStateKeys
{
    public const string ScriptRunState = "Race.ScriptRunState";
    public const string HasSeenRaceUi = "Race.HasSeenRaceUi";
    public const string UnknownUiAttempts = "Race.UnknownUiAttempts";
}
