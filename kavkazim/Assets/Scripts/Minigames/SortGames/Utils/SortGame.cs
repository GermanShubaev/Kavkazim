using System.Collections.Generic;
using Minigames.Base;
using Minigames.Base.Strategies;
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
        [SerializeField] protected int numberOfElements;
        [SerializeField] protected float cellSpacing = 50f;
        [SerializeField] protected float elementSize = 1000f;
        [SerializeField] protected float minDistanceBetweenElements = 120f;
        [SerializeField] protected float snapProximityDistance = 120f; 

        protected readonly List<DraggableElement> Elements = new List<DraggableElement>();
        protected readonly List<Cell> Cells = new List<Cell>();
        protected DraggableElement CurrentlyDragging;

        public IReadOnlyList<Cell> GetCells() => Cells;

        protected virtual void Awake()
        {
            if (popupCanvas == null)
                popupCanvas = GetComponentInParent<Canvas>();
            
            if (popupWindow == null)
                popupWindow = GetComponent<RectTransform>();
            
            if (_winConditionStrategy == null)
            {
                _winConditionStrategy = new SortGameWinConditionStrategy();
            }
            
            if (_uiBuilder == null)
            {
                _uiBuilder = new Base.UI.SortGameUIBuilder();
            }
        }
        
        public void SetUpperSection(RectTransform section)
        {
            upperSection = section;
        }
        
        public void SetLowerSection(RectTransform section)
        {
            lowerSection = section;
        }
        
        public void SetPopupWindowReference(RectTransform window)
        {
            popupWindow = window;
            if (popupCanvas == null && _canvas != null)
            {
                popupCanvas = _canvas;
            }
        }

        protected virtual void Start()
        {
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

        public virtual void SnapToCell(DraggableElement element, Cell cell)
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
            if (_winConditionStrategy != null && _winConditionStrategy.CheckWinCondition(this))
            {
                _winConditionStrategy.OnWin(this);
            }
        }

        public override void OnGameComplete()
        {
            base.OnGameComplete();
        }

        protected override void InitializeGameUI()
        {
            InitializeGame();
        }
    }
}

