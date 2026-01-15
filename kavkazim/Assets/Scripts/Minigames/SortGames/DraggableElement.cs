using Minigames.Base.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigames.SortGames
{
    public class DraggableElement : MonoBehaviour, IBeginDragHandler, UnityEngine.EventSystems.IDragHandler, IEndDragHandler, IDraggable
    {
        private int _index;
        private int _correctCellIndex;
        protected SortGame Game;
        protected RectTransform RectTransform;
        private Image _image;

        protected void Initialize(int idx, SortGame sortGame, Sprite sprite)
        {
            Initialize(idx, sortGame, sprite, idx);
        }

        public void Initialize(int idx, SortGame sortGame, Sprite sprite, int correctCell)
        {
            _index = idx;
            _correctCellIndex = correctCell;
            Game = sortGame;
            RectTransform = GetComponent<RectTransform>();
            
            _image = GetComponent<Image>();
            if (_image == null)
                _image = gameObject.AddComponent<Image>();
            
            if (sprite != null)
                _image.sprite = sprite;
        }

        public RectTransform GetRectTransform() => RectTransform;
        public int GetIndex() => _index;
        public int GetCorrectCellIndex() => _correctCellIndex;
        int IDraggable.GetCorrectTargetIndex() => _correctCellIndex;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Game != null)
                Game.OnElementDragStart(this);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (Game != null && RectTransform != null)
            {
                var parentRect = RectTransform.parent as RectTransform;
                var canvas = Game.GetComponentInParent<Canvas>();
                var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay 
                    ? canvas.worldCamera : null;
                
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    cam,
                    out Vector2 localPoint);
                
                var parentSize = parentRect.rect.size;
                var anchorCenter = (RectTransform.anchorMin + RectTransform.anchorMax) / 2f;
                var anchorLocalPos = new Vector2(
                    (anchorCenter.x - 0.5f) * parentSize.x,
                    (anchorCenter.y - 0.5f) * parentSize.y
                );
                
                RectTransform.anchoredPosition = localPoint - anchorLocalPos;
                Game.OnElementDrag(this, eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Game != null)
                Game.OnElementDragEnd(this, eventData.position);
        }
    }
}
