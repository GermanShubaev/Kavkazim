using UnityEngine;

namespace Minigames.SortGames
{
    public interface IDropTarget
    {
        bool CanAcceptDrop(Vector2 position);
        bool OnDrop(IDraggable draggable, Vector2 position);
        RectTransform GetRectTransform();
    }

    

    
}
