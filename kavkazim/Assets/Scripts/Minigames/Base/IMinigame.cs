using UnityEngine;

namespace Minigames.Base
{
    public interface IMinigame
    {
        void StartGame();
        void CloseGame();
        bool IsActive { get; }
        GameObject PopupWindow { get; }
    }
}

