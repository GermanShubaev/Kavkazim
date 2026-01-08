namespace Kavkazim.Netcode.WinConditions
{
    /// <summary>
    /// Win condition: Innocents win when all tasks are completed.
    /// Requires: TasksLeft == 0 AND at least one Innocent is alive
    /// </summary>
    public class AllTasksCompletedWinCondition : IWinCondition
    {
        public string ConditionName => "All Tasks Completed";

        public bool TryGetWinner(GameSnapshot snapshot, out WinResult result)
        {
            result = null;
            
            // Check if TasksLeft is 0 (all tasks completed)
            if (GameSessionManager.Instance == null)
            {
                return false;
            }
            
            int tasksLeft = GameSessionManager.Instance.TasksLeft.Value;
            
            // Innocents win if all tasks are completed and at least one Innocent is alive
            if (tasksLeft == 0 && snapshot.AliveInnocentCount > 0)
            {
                result = WinResult.FromSnapshot(
                    snapshot, 
                    TeamEnum.Innocent, 
                    "all_tasks_completed",
                    onlyAlive: false  // Show all Innocents as winners
                );
                return true;
            }
            
            return false;
        }
    }
}
