using UnityEngine;

namespace Minigames
{
    /// <summary>
    /// Interface defining the contract for all minigames.
    /// </summary>
    public interface IMinigame
    {
        /// <summary>
        /// Initialize and show the minigame.
        /// </summary>
        void StartGame();

        /// <summary>
        /// Clean up and hide the minigame.
        /// </summary>
        void CloseGame();

        /// <summary>
        /// Whether the minigame is currently active/running.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Reference to the popup window GameObject.
        /// </summary>
        GameObject PopupWindow { get; }
    }
}

