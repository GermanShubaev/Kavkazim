namespace Kavkazim.Netcode.WinConditions
{
    /// <summary>
    /// Win condition: Kavkazi win when they are at least 50% of alive players.
    /// Formula: AliveKavkaziCount >= TotalAliveCount / 2 (rounded down)
    /// Example: With 4 alive (2 Kavkazi, 2 Innocent) -> Kavkazi win
    /// Example: With 3 alive (1 Kavkazi, 2 Innocent) -> No win yet
    /// </summary>
    public class ImposterMajorityWinCondition : IWinCondition
    {
        public string ConditionName => "Imposter Majority";

        public bool TryGetWinner(GameSnapshot snapshot, out WinResult result)
        {
            result = null;
            
            // Need at least one player alive
            if (snapshot.TotalAliveCount == 0)
                return false;
            
            // Kavkazi win if they are >= 50% of alive players
            // Using >= for the "at least 50%" requirement
            // With 4 players: 2 kavkazi >= 2 (4/2) -> win
            // With 3 players: 1 kavkazi >= 1.5... but we use integer division so 1 >= 1 -> win
            // Actually need: kavkazi >= ceil(total/2) for strict majority
            // But spec says "at least 50%", so kavkazi * 2 >= total
            int kavkaziCount = snapshot.AliveKavkaziCount;
            int totalCount = snapshot.TotalAliveCount;
            
            // Kavkazi >= 50% means: kavkaziCount * 2 >= totalCount
            if (kavkaziCount * 2 >= totalCount && kavkaziCount > 0)
            {
                result = WinResult.FromSnapshot(
                    snapshot, 
                    TeamEnum.Kavkazi, 
                    "imposter_majority",
                    onlyAlive: false  // Show all Kavkazi as winners
                );
                return true;
            }
            
            return false;
        }
    }
}
