namespace BetterBTD.Models.AutoTasks;

public enum OdysseyAutoTaskScriptRunState
{
    NotStarted,
    Running,
    FinishedCurrentStage
}

public static class OdysseyAutoTaskStateKeys
{
    public const string ScriptQueue = "Odyssey.ScriptQueue";
    public const string CurrentStageIndex = "Odyssey.CurrentStageIndex";
    public const string ScriptRunState = "Odyssey.ScriptRunState";
}
