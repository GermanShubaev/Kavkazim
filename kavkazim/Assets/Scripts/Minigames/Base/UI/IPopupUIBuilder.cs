using UnityEngine;

namespace Minigames.Base.UI
{
    /// <summary>
    /// Interface for building popup UI for minigames.
    /// Allows different minigames to have different UI structures
    /// without modifying the base classes.
    /// </summary>
    public interface IPopupUIBuilder
    {
        /// <summary>
        /// Builds the popup window structure for a minigame.
        /// </summary>
        /// <param name="minigame">The minigame instance that needs a popup</param>
        /// <returns>The created popup window GameObject</returns>
        GameObject BuildPopup(BaseMinigame minigame);

        /// <summary>
        /// Cleans up any resources created by the builder.
        /// </summary>
        /// <param name="popup">The popup window to clean up</param>
        void Cleanup(GameObject popup);
    }
}
