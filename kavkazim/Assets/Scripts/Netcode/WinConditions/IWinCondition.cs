namespace Kavkazim.Netcode.WinConditions
{
    /// <summary>
    /// Interface for all win condition implementations.
    /// Each condition checks if a specific win scenario has been met.
    /// </summary>
    public interface IWinCondition
    {
        /// <summary>
        /// Evaluates whether this win condition has been met.
        /// </summary>
        /// <param name="snapshot">Current game state snapshot.</param>
        /// <param name="result">The win result if condition is met.</param>
        /// <returns>True if this condition triggers a win, false otherwise.</returns>
        bool TryGetWinner(GameSnapshot snapshot, out WinResult result);
        
        /// <summary>
        /// Display name for this condition (for debugging/logging).
        /// </summary>
        string ConditionName { get; }
    }
}
