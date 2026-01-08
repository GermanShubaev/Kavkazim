using UnityEngine;

namespace Minigames.Base.Strategies
{
    /// <summary>
    /// Strategy interface for checking win conditions in minigames.
    /// Allows different minigames to have different win condition logic
    /// without modifying the base classes.
    /// </summary>
    public interface IWinConditionStrategy
    {
        /// <summary>
        /// Checks if the win condition has been met.
        /// </summary>
        /// <param name="minigame">The minigame instance to check</param>
        /// <returns>True if win condition is met, false otherwise</returns>
        bool CheckWinCondition(BaseMinigame minigame);

        /// <summary>
        /// Called when the win condition is met.
        /// </summary>
        /// <param name="minigame">The minigame instance that won</param>
        void OnWin(BaseMinigame minigame);

        /// <summary>
        /// Called when the game is lost (if applicable).
        /// </summary>
        /// <param name="minigame">The minigame instance that lost</param>
        void OnLose(BaseMinigame minigame);
    }
}
