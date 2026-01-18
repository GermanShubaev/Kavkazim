namespace Kavkazim.Netcode.WinConditions
{
    public class AllTasksCompletedWinCondition : IWinCondition
    {
        public string ConditionName => "All Tasks Completed";

        public bool TryGetWinner(GameSnapshot snapshot, out WinResult result)
        {
            result = null;
            
            if (GameSessionManager.Instance == null)
            {
                return false;
            }
            
            int tasksLeft = GameSessionManager.Instance.TasksLeft.Value;
            
            if (tasksLeft == 0 && snapshot.AliveInnocentCount > 0)
            {
                result = WinResult.FromSnapshot(
                    snapshot, 
                    TeamEnum.Innocent, 
                    "all_tasks_completed",
                    onlyAlive: false
                );
                return true;
            }
            
            return false;
        }
    }
}
