using Minigames.Base;
using Minigames.SortGames;
using UnityEngine;

namespace Minigames.Base.Strategies
{
    /// <summary>
    /// Default win condition strategy for sort games.
    /// Checks if all elements are placed in their correct cells.
    /// </summary>
    public class SortGameWinConditionStrategy : IWinConditionStrategy
    {
        public bool CheckWinCondition(BaseMinigame minigame)
        {
            if (minigame is not SortGame sortGame)
                return false;

            // Check if all cells have elements and they're in the correct positions
            var cells = sortGame.GetCells();
            foreach (var cell in cells)
            {
                var element = cell.GetElement();
                
                if (element == null)
                    return false;
                
                if (element.GetCorrectCellIndex() != cell.GetIndex())
                    return false;
            }

            return true;
        }

        public void OnWin(BaseMinigame minigame)
        {
            if (minigame is SortGame sortGame)
            {
                Debug.Log("SortGame: All elements correctly placed! Game complete.");
                sortGame.OnGameComplete();
            }
        }

        public void OnLose(BaseMinigame minigame)
        {
            // Sort games typically don't have a lose condition
            // Override in derived strategies if needed
        }
    }
}
