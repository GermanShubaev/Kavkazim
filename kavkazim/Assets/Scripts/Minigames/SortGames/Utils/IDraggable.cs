using UnityEngine;

namespace Minigames.SortGames
{
    public interface IDraggable
    {
        RectTransform GetRectTransform();
        int GetCorrectTargetIndex();
    }
}