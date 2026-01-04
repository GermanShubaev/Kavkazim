using UnityEngine;

namespace Minigames
{
    public interface IMinigame
    {
        void StartGame();
        void CloseGame();
        bool IsActive { get; }
        GameObject PopupWindow { get; }
    }
}

