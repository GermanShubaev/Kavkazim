namespace Kavkazim.Netcode.WinConditions
{
    /// <summary>
    /// Win condition: Innocents win when all Kavkazi are eliminated.
    /// Requires: AliveKavkaziCount == 0 AND AliveInnocentCount > 0
    /// </summary>
    public class AllImpostersEliminatedWinCondition : IWinCondition
    {
        public string ConditionName => "All Imposters Eliminated";

        public bool TryGetWinner(GameSnapshot snapshot, out WinResult result)
        {
            result = null;
            
            // Innocents win if all Kavkazi are dead and at least one Innocent is alive
            if (snapshot.AliveKavkaziCount == 0 && snapshot.AliveInnocentCount > 0)
            {
                result = WinResult.FromSnapshot(
                    snapshot, 
                    TeamEnum.Innocent, 
                    "all_imposters_eliminated",
                    onlyAlive: false  // Show all Innocents as winners
                );
                return true;
            }
            
            return false;
        }
    }
}
