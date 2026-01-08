using Minigames.Base;
using Minigames.ClickGames;
using UnityEngine;

namespace Minigames.Base.Strategies
{
    /// <summary>
    /// Default win condition strategy for click games.
    /// Checks if all stains/clickable elements have been removed.
    /// </summary>
    public class ClickGameWinConditionStrategy : IWinConditionStrategy
    {
        public bool CheckWinCondition(BaseMinigame minigame)
        {
            if (minigame is not ClickGame clickGame)
                return false;

            // Win condition is checked internally by ClickGame when stains are removed
            // This strategy just confirms the game state
            return clickGame.GetStainsRemaining() <= 0;
        }

        public void OnWin(BaseMinigame minigame)
        {
            if (minigame is ClickGame clickGame)
            {
                Debug.Log($"{clickGame.GetType().Name}: All stains removed! Game complete.");
                clickGame.OnGameComplete();
            }
        }

        public void OnLose(BaseMinigame minigame)
        {
            // Click games typically don't have a lose condition
            // Override in derived strategies if needed
        }
    }
}
