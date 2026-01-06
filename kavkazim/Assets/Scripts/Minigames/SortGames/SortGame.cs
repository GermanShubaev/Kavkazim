using System.Collections.Generic;
using Minigames.Base;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigames.SortGames
{
    public class SortGame : BaseMinigame
    {
        [Header("UI References")]
        [SerializeField] protected Canvas popupCanvas;
        [SerializeField] protected RectTransform popupWindow;
        [SerializeField] protected RectTransform lowerSection; 
        [SerializeField] protected RectTransform upperSection; 
        [SerializeField] protected GameObject cellPrefab; 
        [SerializeField] protected GameObject elementPrefab; 

        [Header("Game Settings")]
        [SerializeField] protected int numberOfElements = 6;
        [SerializeField] protected float cellSpacing = 50f;
        [SerializeField] protected float elementSize = 1000f;
        [SerializeField] protected float minDistanceBetweenElements = 120f; 
        [SerializeField] protected float snapProximityDistance = 120f; 

        protected readonly List<DraggableElement> Elements = new List<DraggableElement>();
        protected readonly List<Cell> Cells = new List<Cell>();
        protected DraggableElement CurrentlyDragging;

        protected virtual void Awake()
        {
            if (popupCanvas == null)
                popupCanvas = GetComponentInParent<Canvas>();
            
            if (popupWindow == null)
                popupWindow = GetComponent<RectTransform>();
        }

        protected virtual void Start()
        {
            InitializeGame();
        }

        protected virtual void InitializeGame()
        {
            SetupUpperSection();
            SetupLowerSection();
        }

        protected virtual void SetupUpperSection()
        {
            if (upperSection == null || cellPrefab == null) return;

            Cells.Clear();
            float totalWidth = (numberOfElements * elementSize) + ((numberOfElements - 1) * cellSpacing);
            float startX = -totalWidth / 2f + elementSize / 2f;

            for (int i = 0; i < numberOfElements; i++)
            {
                GameObject cellObj = Instantiate(cellPrefab, upperSection);
                RectTransform cellRect = cellObj.GetComponent<RectTransform>();
                
                if (cellRect == null)
                    cellRect = cellObj.AddComponent<RectTransform>();

                cellRect.sizeDelta = new Vector2(elementSize, elementSize);
                cellRect.anchoredPosition = new Vector2(startX + i * (elementSize + cellSpacing), 0);
                cellRect.anchorMin = new Vector2(0.5f, 1f);
                cellRect.anchorMax = new Vector2(0.5f, 1f);
                cellRect.pivot = new Vector2(0.5f, 0.5f);

                Cell cell = cellObj.GetComponent<Cell>();
                if (cell == null)
                    cell = cellObj.AddComponent<Cell>();
                
                cell.Initialize(i, this);
                Cells.Add(cell);
            }
        }

        protected virtual void SetupLowerSection()
        {
            if (lowerSection == null || elementPrefab == null) return;

            Elements.Clear();
            List<Vector2> positions = GenerateRandomPositions(numberOfElements);

            for (int i = 0; i < numberOfElements; i++)
            {
                GameObject elementObj = Instantiate(elementPrefab, lowerSection);
                RectTransform elementRect = elementObj.GetComponent<RectTransform>();
                
                if (elementRect == null)
                    elementRect = elementObj.AddComponent<RectTransform>();

                elementRect.sizeDelta = new Vector2(elementSize, elementSize);
                elementRect.anchoredPosition = positions[i];
                elementRect.anchorMin = new Vector2(0.5f, 0f);
                elementRect.anchorMax = new Vector2(0.5f, 0f);
                elementRect.pivot = new Vector2(0.5f, 0.5f);

                var element = elementObj.GetComponent<DraggableElement>();
                if (element == null)
                    element = elementObj.AddComponent<DraggableElement>();
                
                element.Initialize(i, this, GetElementImage(i), GetCorrectCellForElement(i));
                Elements.Add(element);
            }
        }

        protected virtual List<Vector2> GenerateRandomPositions(int count)
        {
            var positions = new List<Vector2>();
            var bounds = lowerSection.rect;
            
            var minX = bounds.xMin + elementSize / 2f;
            var maxX = bounds.xMax - elementSize / 2f;
            var minY = bounds.yMin + elementSize / 2f;
            var maxY = bounds.yMax - elementSize / 2f;

            var maxAttempts = 1000;
            for (var i = 0; i < count; i++)
            {
                var position = Vector2.zero;
                var validPosition = false;
                var attempts = 0;

                while (!validPosition && attempts < maxAttempts)
                {
                    position = new Vector2(
                        Random.Range(minX, maxX),
                        Random.Range(minY, maxY)
                    );

                    validPosition = true;
                    foreach (Vector2 existingPos in positions)
                    {
                        if (Vector2.Distance(position, existingPos) < minDistanceBetweenElements)
                        {
                            validPosition = false;
                            break;
                        }
                    }
                    attempts++;
                }

                positions.Add(position);
            }

            return positions;
        }

        protected virtual Sprite GetElementImage(int index)
        {
            return null;
        }

        protected virtual int GetCorrectCellForElement(int elementIndex)
        {
            return elementIndex;
        }

        public virtual void OnElementDragStart(DraggableElement element)
        {
            CurrentlyDragging = element;
            element.transform.SetAsLastSibling();
        }

        public virtual void OnElementDrag(DraggableElement element, Vector2 position)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                upperSection, position, popupCanvas.worldCamera, out Vector2 localPoint);

            foreach (var cell in Cells)
            {
                cell.SetHighlight(false);
            }

            var closestCell = FindClosestCell(localPoint);
            if (closestCell != null && IsWithinSnapProximity(localPoint, closestCell))
            {
                closestCell.SetHighlight(true);
            }
        }

        public virtual void OnElementDragEnd(DraggableElement element, Vector2 position)
        {
            foreach (var cell in Cells)
            {
                cell.SetHighlight(false);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                upperSection, position, popupCanvas.worldCamera, out Vector2 upperLocalPoint);
            
            Cell closestCell = FindClosestCell(upperLocalPoint);
            if (closestCell != null && IsWithinSnapProximity(upperLocalPoint, closestCell))
            {
                SnapToCell(element, closestCell);
            }

            CurrentlyDragging = null;
        }

        protected virtual Cell FindClosestCell(Vector2 position)
        {
            Cell closest = null;
            float minDistance = float.MaxValue;

            foreach (Cell cell in Cells)
            {
                float distance = Vector2.Distance(position, cell.GetRectTransform().anchoredPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = cell;
                }
            }

            return closest;
        }

        protected virtual bool IsWithinSnapProximity(Vector2 position, Cell cell)
        {
            var cellRect = cell.GetRectTransform();
            var cellPos = cellRect.anchoredPosition;
            float distance = Vector2.Distance(position, cellPos);
            return distance <= snapProximityDistance;
        }

        protected virtual void SnapToCell(DraggableElement element, Cell cell)
        {
            foreach (Cell c in Cells)
            {
                if (c.GetElement() == element)
                {
                    c.SetElement(null);
                    break;
                }
            }

            var existingElement = cell.GetElement();
            if (existingElement != null)
            {
                var existingRect = existingElement.GetRectTransform();
                existingRect.SetParent(lowerSection);
                existingRect.anchorMin = new Vector2(0.5f, 0f);
                existingRect.anchorMax = new Vector2(0.5f, 0f);
                var randomPos = GenerateRandomPositions(1)[0];
                existingRect.anchoredPosition = randomPos;
                cell.SetElement(null);
            }

            RectTransform elementRect = element.GetRectTransform();
            RectTransform cellRect = cell.GetRectTransform();
            
            elementRect.SetParent(upperSection);
            elementRect.anchorMin = cellRect.anchorMin;
            elementRect.anchorMax = cellRect.anchorMax;
            elementRect.anchoredPosition = cellRect.anchoredPosition;
            cell.SetElement(element);

            CheckWinCondition();
        }

        protected virtual void CheckWinCondition()
        {
            foreach (Cell cell in Cells)
            {
                DraggableElement element = cell.GetElement();
                
                if (element == null)
                    return;
                
                if (element.GetCorrectCellIndex() != cell.GetIndex())
                    return;
            }

            OnGameComplete();
        }

        protected override void OnGameComplete()
        {
            base.OnGameComplete(); // Mark as completed successfully
            Debug.Log("SortGame: All elements correctly placed! Game complete.");
            HidePopup();
        }

        protected virtual void ReturnToLowerSection(DraggableElement element)
        {
            foreach (var cell in Cells)
            {
                if (cell.GetElement() == element)
                {
                    cell.SetElement(null);
                    break;
                }
            }

            var elementRect = element.GetRectTransform();
            elementRect.SetParent(lowerSection);
            elementRect.anchorMin = new Vector2(0.5f, 0f);
            elementRect.anchorMax = new Vector2(0.5f, 0f);
            var newPos = GenerateRandomPositions(1)[0];
            elementRect.anchoredPosition = newPos;
        }

        public virtual void ShowPopup()
        {
            if (popupCanvas != null)
                popupCanvas.gameObject.SetActive(true);
        }

        protected virtual void HidePopup()
        {
            if (popupCanvas != null)
                popupCanvas.gameObject.SetActive(false);
        }

        protected override void InitializeGameUI()
        {
            throw new System.NotImplementedException();
        }
    }

    public class DraggableElement : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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

    public class Cell : MonoBehaviour
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
    }
}

