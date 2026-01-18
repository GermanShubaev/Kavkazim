namespace Kavkazim.Netcode.WinConditions
{
    public class ImposterMajorityWinCondition : IWinCondition
    {
        public string ConditionName => "Imposter Majority";

        public bool TryGetWinner(GameSnapshot snapshot, out WinResult result)
        {
            result = null;
            
            if (snapshot.TotalAliveCount == 0)
                return false;
            
            int kavkaziCount = snapshot.AliveKavkaziCount;
            int totalCount = snapshot.TotalAliveCount;
            
            if (kavkaziCount * 2 >= totalCount && kavkaziCount > 0)
            {
                result = WinResult.FromSnapshot(
                    snapshot, 
                    TeamEnum.Kavkazi, 
                    "imposter_majority",
                    onlyAlive: false
                );
                return true;
            }
            
            return false;
        }
    }
}
