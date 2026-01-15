using UnityEngine;

namespace Minigames.Base.Interaction
{
    public interface IDropTarget
    {
        bool CanAcceptDrop(Vector2 position);
        bool OnDrop(IDraggable draggable, Vector2 position);
        RectTransform GetRectTransform();
    }

    public interface IDraggable
    {
        RectTransform GetRectTransform();
        int GetCorrectTargetIndex();
    }

    public interface IHighlightable
    {
        void SetHighlight(bool highlighted);
    }
}
