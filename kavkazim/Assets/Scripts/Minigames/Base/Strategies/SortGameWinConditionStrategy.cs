using System.Linq;
using Minigames.SortGames;
using UnityEngine;

namespace Minigames.Base.Strategies
{
    public class SortGameWinConditionStrategy : IWinConditionStrategy
    {
        public bool CheckWinCondition(BaseMinigame minigame)
        {
            if (minigame is not SortGame sortGame)
                return false;

            var cells = sortGame.GetCells();
            return cells.All(cell =>
            {
                var element = cell.GetElement();
                return element != null &&
                       element.GetCorrectCellIndex() == cell.GetIndex();
            });
        }

        public void OnWin(BaseMinigame minigame)
        {
            if (minigame is SortGame sortGame)
            {
                Debug.Log("SortGame: All elements correctly placed! Game complete.");
                sortGame.OnGameComplete();
            }
        }
    }
}
