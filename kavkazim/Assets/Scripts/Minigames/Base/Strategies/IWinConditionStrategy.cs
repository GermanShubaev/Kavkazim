using UnityEngine;

namespace Minigames.Base.Strategies
{
    public interface IWinConditionStrategy
    {
        bool CheckWinCondition(BaseMinigame minigame);
        void OnWin(BaseMinigame minigame);
    }
}
