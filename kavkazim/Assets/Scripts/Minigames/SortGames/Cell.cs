using Minigames.Base.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Minigames.SortGames
{
    public class Cell : MonoBehaviour, IDropTarget, IHighlightable
    {
        private int _index;
        private SortGame _game;
        private RectTransform _rectTransform;
        private DraggableElement _currentElement;
        private Image _backgroundImage;
        private readonly Color _normalColor = new Color(1f, 1f, 1f, 0.2f);
        private readonly Color _highlightColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);

        public void Initialize(int idx, SortGame sortGame)
        {
            _index = idx;
            _game = sortGame;
            _rectTransform = GetComponent<RectTransform>();
            
            _backgroundImage = GetComponent<Image>();
            if (_backgroundImage == null)
                _backgroundImage = gameObject.AddComponent<Image>();
            
            _backgroundImage.color = _normalColor;
        }

        public RectTransform GetRectTransform() => _rectTransform;
        public DraggableElement GetElement() => _currentElement;
        public int GetIndex() => _index;

        public void SetElement(DraggableElement element)
        {
            _currentElement = element;
        }

        public void SetHighlight(bool highlighted)
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = highlighted ? _highlightColor : _normalColor;
            }
        }

        public bool CanAcceptDrop(Vector2 position)
        {
            return true;
        }

        public bool OnDrop(IDraggable draggable, Vector2 position)
        {
            if (draggable is DraggableElement element && _game != null)
            {
                _game.SnapToCell(element, this);
                return true;
            }
            return false;
        }
    }
}
