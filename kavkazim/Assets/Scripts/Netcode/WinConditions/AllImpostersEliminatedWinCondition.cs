namespace Kavkazim.Netcode.WinConditions
{
    public class AllImpostersEliminatedWinCondition : IWinCondition
    {
        public string ConditionName => "All Imposters Eliminated";

        public bool TryGetWinner(GameSnapshot snapshot, out WinResult result)
        {
            result = null;
            
            if (snapshot.AliveKavkaziCount == 0 && snapshot.AliveInnocentCount > 0)
            {
                result = WinResult.FromSnapshot(
                    snapshot, 
                    TeamEnum.Innocent, 
                    "all_imposters_eliminated",
                    onlyAlive: false
                );
                return true;
            }
            
            return false;
        }
    }
}
