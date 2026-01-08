using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Minigames.Base.Interaction
{
    /// <summary>
    /// Manages drag-and-drop operations for sort games.
    /// Separates drag-and-drop logic from game-specific logic.
    /// </summary>
    public class DragAndDropManager
    {
        private readonly List<IDropTarget> _dropTargets = new List<IDropTarget>();
        private IDraggable _currentlyDragging;
        private float _snapProximityDistance = 120f;

        public void SetSnapProximityDistance(float distance)
        {
            _snapProximityDistance = distance;
        }

        public void RegisterDropTarget(IDropTarget target)
        {
            if (!_dropTargets.Contains(target))
            {
                _dropTargets.Add(target);
            }
        }

        public void UnregisterDropTarget(IDropTarget target)
        {
            _dropTargets.Remove(target);
        }

        public void OnBeginDrag(IDraggable draggable, PointerEventData eventData)
        {
            _currentlyDragging = draggable;
            if (draggable is MonoBehaviour mb)
            {
                mb.transform.SetAsLastSibling();
            }
        }

        public void OnDrag(IDraggable draggable, PointerEventData eventData, Canvas canvas)
        {
            if (draggable == null || draggable.GetRectTransform() == null)
                return;

            RectTransform rectTransform = draggable.GetRectTransform();
            RectTransform parentRect = rectTransform.parent as RectTransform;
            
            if (parentRect == null)
                return;

            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay 
                ? canvas.worldCamera : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                cam,
                out Vector2 localPoint);

            Vector2 parentSize = parentRect.rect.size;
            Vector2 anchorCenter = (rectTransform.anchorMin + rectTransform.anchorMax) / 2f;
            Vector2 anchorLocalPos = new Vector2(
                (anchorCenter.x - 0.5f) * parentSize.x,
                (anchorCenter.y - 0.5f) * parentSize.y
            );

            rectTransform.anchoredPosition = localPoint - anchorLocalPos;

            // Highlight closest drop target
            HighlightClosestTarget(eventData.position, canvas);
        }

        public void OnEndDrag(IDraggable draggable, PointerEventData eventData, Canvas canvas)
        {
            ClearHighlights();

            if (draggable == null)
            {
                _currentlyDragging = null;
                return;
            }

            IDropTarget closestTarget = FindClosestTarget(eventData.position, canvas);
            
            if (closestTarget != null && closestTarget.CanAcceptDrop(eventData.position))
            {
                closestTarget.OnDrop(draggable, eventData.position);
            }

            _currentlyDragging = null;
        }

        private void HighlightClosestTarget(Vector2 screenPosition, Canvas canvas)
        {
            ClearHighlights();

            IDropTarget closest = FindClosestTarget(screenPosition, canvas);
            if (closest != null && closest is IHighlightable highlightable)
            {
                highlightable.SetHighlight(true);
            }
        }

        private void ClearHighlights()
        {
            foreach (var target in _dropTargets)
            {
                if (target is IHighlightable highlightable)
                {
                    highlightable.SetHighlight(false);
                }
            }
        }

        private IDropTarget FindClosestTarget(Vector2 screenPosition, Canvas canvas)
        {
            IDropTarget closest = null;
            float minDistance = float.MaxValue;

            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay 
                ? canvas.worldCamera : null;

            foreach (var target in _dropTargets)
            {
                RectTransform targetRect = target.GetRectTransform();
                if (targetRect == null)
                    continue;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetRect,
                    screenPosition,
                    cam,
                    out Vector2 localPoint);

                float distance = Vector2.Distance(localPoint, targetRect.anchoredPosition);
                
                if (distance < minDistance && distance <= _snapProximityDistance)
                {
                    minDistance = distance;
                    closest = target;
                }
            }

            return closest;
        }

        public IDraggable GetCurrentlyDragging() => _currentlyDragging;
    }

    /// <summary>
    /// Interface for objects that can be highlighted.
    /// </summary>
    public interface IHighlightable
    {
        void SetHighlight(bool highlighted);
    }
}
