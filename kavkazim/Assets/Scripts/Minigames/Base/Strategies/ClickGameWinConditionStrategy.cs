using Minigames.Base;
using Minigames.ClickGames;
using UnityEngine;

namespace Minigames.Base.Strategies
{
    public class ClickGameWinConditionStrategy : IWinConditionStrategy
    {
        public bool CheckWinCondition(BaseMinigame minigame)
        {
            if (minigame is not ClickGame clickGame)
                return false;

            return clickGame.GetStainsRemaining() == 0;
        }

        public void OnWin(BaseMinigame minigame)
        {
            if (minigame is ClickGame clickGame)
            {
                Debug.Log($"{clickGame.GetType().Name}: All stains removed! Game complete.");
                clickGame.OnGameComplete();
            }
        }
    }
}
