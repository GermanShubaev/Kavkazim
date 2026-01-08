using Minigames.Base;
using UnityEngine;

namespace Minigames.Base.UI
{
    /// <summary>
    /// UI builder for click games. Currently uses default builder,
    /// but can be extended for click-game-specific UI needs.
    /// </summary>
    public class ClickGameUIBuilder : DefaultPopupUIBuilder
    {
        public override GameObject BuildPopup(BaseMinigame minigame)
        {
            // Build base popup - click games can use default layout
            // but can be customized here if needed
            return base.BuildPopup(minigame);
        }
    }
}
