using UnityEngine;

namespace Minigames.Base.UI
{
    public interface IPopupUIBuilder
    {
        GameObject BuildPopup(BaseMinigame minigame);
        void Cleanup(GameObject popup);
    }
}
