using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Kavkazim.UI
{
    public class SortGame : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] protected Canvas popupCanvas;
        [SerializeField] protected RectTransform popupWindow;
        [SerializeField] protected RectTransform lowerSection; // Where elements are randomly placed
        [SerializeField] protected RectTransform upperSection; // Row of cells for ordering
        [SerializeField] protected GameObject cellPrefab; // Prefab for cells in upper section
        [SerializeField] protected GameObject elementPrefab; // Prefab for draggable photo elements

        [Header("Game Settings")]
        [SerializeField] protected int numberOfElements = 5;
        [SerializeField] protected float cellSpacing = 10f;
        [SerializeField] protected float elementSize = 100f;
        [SerializeField] protected float minDistanceBetweenElements = 120f; // To prevent overlap

        protected List<DraggableElement> elements = new List<DraggableElement>();
        protected List<Cell> cells = new List<Cell>();
        protected DraggableElement currentlyDragging;

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

            cells.Clear();
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
                cells.Add(cell);
            }
        }

        protected virtual void SetupLowerSection()
        {
            if (lowerSection == null || elementPrefab == null) return;

            elements.Clear();
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

                DraggableElement element = elementObj.GetComponent<DraggableElement>();
                if (element == null)
                    element = elementObj.AddComponent<DraggableElement>();
                
                element.Initialize(i, this, GetElementImage(i)); // Override GetElementImage in derived classes
                elements.Add(element);
            }
        }

        protected virtual List<Vector2> GenerateRandomPositions(int count)
        {
            List<Vector2> positions = new List<Vector2>();
            Rect bounds = lowerSection.rect;
            
            // Account for element size to keep elements within bounds
            float minX = bounds.xMin + elementSize / 2f;
            float maxX = bounds.xMax - elementSize / 2f;
            float minY = bounds.yMin + elementSize / 2f;
            float maxY = bounds.yMax - elementSize / 2f;

            int maxAttempts = 1000;
            for (int i = 0; i < count; i++)
            {
                Vector2 position = Vector2.zero;
                bool validPosition = false;
                int attempts = 0;

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
            // Override in derived classes to provide specific images
            return null;
        }

        public virtual void OnElementDragStart(DraggableElement element)
        {
            currentlyDragging = element;
            element.transform.SetAsLastSibling(); // Bring to front
        }

        public virtual void OnElementDrag(DraggableElement element, Vector2 position)
        {
            // Convert screen position to local position in upper section
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                upperSection, position, popupCanvas.worldCamera, out Vector2 localPoint);

            // Find closest cell
            Cell closestCell = FindClosestCell(localPoint);
            if (closestCell != null)
            {
                // Visual feedback could be added here
            }
        }

        public virtual void OnElementDragEnd(DraggableElement element, Vector2 position)
        {
            // Check if position is within upper section bounds
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                upperSection, position, popupCanvas.worldCamera, out Vector2 upperLocalPoint);
            
            bool isInUpperSection = RectTransformUtility.RectangleContainsScreenPoint(
                upperSection, position, popupCanvas.worldCamera);

            if (isInUpperSection)
            {
                Cell closestCell = FindClosestCell(upperLocalPoint);
                if (closestCell != null && IsWithinCellBounds(upperLocalPoint, closestCell))
                {
                    SnapToCell(element, closestCell);
                }
                else
                {
                    // If in upper section but not in a cell, return to lower section
                    ReturnToLowerSection(element);
                }
            }
            else
            {
                // Return to lower section if dropped outside upper section
                ReturnToLowerSection(element);
            }

            currentlyDragging = null;
        }

        protected virtual Cell FindClosestCell(Vector2 position)
        {
            Cell closest = null;
            float minDistance = float.MaxValue;

            foreach (Cell cell in cells)
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

        protected virtual bool IsWithinCellBounds(Vector2 position, Cell cell)
        {
            RectTransform cellRect = cell.GetRectTransform();
            Vector2 cellPos = cellRect.anchoredPosition;
            float halfSize = elementSize / 2f;

            return position.x >= cellPos.x - halfSize &&
                   position.x <= cellPos.x + halfSize &&
                   position.y >= cellPos.y - halfSize &&
                   position.y <= cellPos.y + halfSize;
        }

        protected virtual void SnapToCell(DraggableElement element, Cell cell)
        {
            // Remove element from previous cell if any
            foreach (Cell c in cells)
            {
                if (c.GetElement() == element)
                {
                    c.SetElement(null);
                    break;
                }
            }

            // If cell already has an element, swap them
            DraggableElement existingElement = cell.GetElement();
            if (existingElement != null)
            {
                // Swap positions
                Vector2 tempPos = element.GetRectTransform().anchoredPosition;
                element.GetRectTransform().anchoredPosition = existingElement.GetRectTransform().anchoredPosition;
                existingElement.GetRectTransform().anchoredPosition = tempPos;
            }

            // Place element in cell
            element.GetRectTransform().SetParent(upperSection);
            element.GetRectTransform().anchoredPosition = cell.GetRectTransform().anchoredPosition;
            cell.SetElement(element);
        }

        protected virtual void ReturnToLowerSection(DraggableElement element)
        {
            // Remove from cell if it was in one
            foreach (Cell cell in cells)
            {
                if (cell.GetElement() == element)
                {
                    cell.SetElement(null);
                    break;
                }
            }

            // Return to lower section with a random position
            element.GetRectTransform().SetParent(lowerSection);
            Vector2 newPos = GenerateRandomPositions(1)[0];
            element.GetRectTransform().anchoredPosition = newPos;
        }

        public virtual void ShowPopup()
        {
            if (popupCanvas != null)
                popupCanvas.gameObject.SetActive(true);
        }

        public virtual void HidePopup()
        {
            if (popupCanvas != null)
                popupCanvas.gameObject.SetActive(false);
        }
    }

    // Draggable Element Component
    public class DraggableElement : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private int index;
        protected SortGame game;
        protected RectTransform rectTransform;
        private Image image;

        public void Initialize(int idx, SortGame sortGame, Sprite sprite)
        {
            index = idx;
            game = sortGame;
            rectTransform = GetComponent<RectTransform>();
            
            image = GetComponent<Image>();
            if (image == null)
                image = gameObject.AddComponent<Image>();
            
            if (sprite != null)
                image.sprite = sprite;
        }

        public RectTransform GetRectTransform() => rectTransform;
        public int GetIndex() => index;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (game != null)
                game.OnElementDragStart(this);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (game != null && rectTransform != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform.parent as RectTransform,
                    eventData.position,
                    game.GetComponentInParent<Canvas>().worldCamera,
                    out Vector2 localPoint);
                
                rectTransform.anchoredPosition = localPoint;
                game.OnElementDrag(this, eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (game != null)
                game.OnElementDragEnd(this, eventData.position);
        }
    }

    // Cell Component
    public class Cell : MonoBehaviour
    {
        private int index;
        private SortGame game;
        private RectTransform rectTransform;
        private DraggableElement currentElement;
        private Image backgroundImage;

        public void Initialize(int idx, SortGame sortGame)
        {
            index = idx;
            game = sortGame;
            rectTransform = GetComponent<RectTransform>();
            
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
                backgroundImage = gameObject.AddComponent<Image>();
            
            // Set a semi-transparent background to show cell boundaries
            backgroundImage.color = new Color(1f, 1f, 1f, 0.2f);
        }

        public RectTransform GetRectTransform() => rectTransform;
        public DraggableElement GetElement() => currentElement;
        public int GetIndex() => index;

        public void SetElement(DraggableElement element)
        {
            currentElement = element;
        }
    }
}

