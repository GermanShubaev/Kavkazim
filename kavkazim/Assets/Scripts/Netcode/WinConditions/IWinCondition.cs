namespace Kavkazim.Netcode.WinConditions
{
    public interface IWinCondition
    {
        bool TryGetWinner(GameSnapshot snapshot, out WinResult result);
        
        string ConditionName { get; }
    }
}
