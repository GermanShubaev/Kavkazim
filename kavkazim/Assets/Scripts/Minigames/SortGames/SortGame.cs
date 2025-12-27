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
        [SerializeField] protected int numberOfElements = 6;
        [SerializeField] protected float cellSpacing = 50f;
        [SerializeField] protected float elementSize = 1000f;
        [SerializeField] protected float minDistanceBetweenElements = 120f; // To prevent overlap
        [SerializeField] protected float snapProximityDistance = 120f; // Distance within which element snaps to cell

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
                
                // Initialize with index, image, and correct cell index
                // Override GetElementImage and GetCorrectCellForElement in derived classes
                element.Initialize(i, this, GetElementImage(i), GetCorrectCellForElement(i));
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

        /// <summary>
        /// Returns the correct cell index for an element at the given index.
        /// Override in derived classes to customize element-to-cell mapping.
        /// By default, element index matches its correct cell index (element 0 goes to cell 0, etc.).
        /// </summary>
        protected virtual int GetCorrectCellForElement(int elementIndex)
        {
            return elementIndex;
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

            // Reset all cell highlights
            foreach (Cell cell in cells)
            {
                cell.SetHighlight(false);
            }

            // Find closest cell and highlight if within proximity
            Cell closestCell = FindClosestCell(localPoint);
            if (closestCell != null && IsWithinSnapProximity(localPoint, closestCell))
            {
                closestCell.SetHighlight(true);
            }
        }

        public virtual void OnElementDragEnd(DraggableElement element, Vector2 position)
        {
            // Reset all cell highlights
            foreach (Cell cell in cells)
            {
                cell.SetHighlight(false);
            }

            // Check if position is within upper section bounds
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                upperSection, position, popupCanvas.worldCamera, out Vector2 upperLocalPoint);
            
            // Find closest cell and check proximity
            Cell closestCell = FindClosestCell(upperLocalPoint);
            if (closestCell != null && IsWithinSnapProximity(upperLocalPoint, closestCell))
            {
                SnapToCell(element, closestCell);
            }
            else
            {
                // Keep element at current position if dropped outside snap range
                // Element stays where it was dropped (no automatic return)
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

        /// <summary>
        /// Checks if a position is within snap proximity of a cell (distance-based).
        /// </summary>
        protected virtual bool IsWithinSnapProximity(Vector2 position, Cell cell)
        {
            RectTransform cellRect = cell.GetRectTransform();
            Vector2 cellPos = cellRect.anchoredPosition;
            float distance = Vector2.Distance(position, cellPos);
            return distance <= snapProximityDistance;
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

            // If cell already has an element, return it to lower section
            DraggableElement existingElement = cell.GetElement();
            if (existingElement != null)
            {
                // Return existing element to lower section with random position
                RectTransform existingRect = existingElement.GetRectTransform();
                existingRect.SetParent(lowerSection);
                // Restore anchors for lower section (center-bottom)
                existingRect.anchorMin = new Vector2(0.5f, 0f);
                existingRect.anchorMax = new Vector2(0.5f, 0f);
                Vector2 randomPos = GenerateRandomPositions(1)[0];
                existingRect.anchoredPosition = randomPos;
                cell.SetElement(null);
            }

            // Place element in cell - match anchors to cell's anchors
            RectTransform elementRect = element.GetRectTransform();
            RectTransform cellRect = cell.GetRectTransform();
            
            elementRect.SetParent(upperSection);
            // Set element's anchors to match the cell's anchors so positioning works correctly
            elementRect.anchorMin = cellRect.anchorMin;
            elementRect.anchorMax = cellRect.anchorMax;
            elementRect.anchoredPosition = cellRect.anchoredPosition;
            cell.SetElement(element);

            // Check if all elements are correctly placed
            CheckWinCondition();
        }

        /// <summary>
        /// Checks if all cells contain their correct elements.
        /// </summary>
        protected virtual void CheckWinCondition()
        {
            // All cells must have an element with matching correctCellIndex
            foreach (Cell cell in cells)
            {
                DraggableElement element = cell.GetElement();
                
                // If any cell is empty, game is not complete
                if (element == null)
                    return;
                
                // If element's correctCellIndex doesn't match cell's index, game is not complete
                if (element.GetCorrectCellIndex() != cell.GetIndex())
                    return;
            }

            // All elements are correctly placed - game complete!
            OnGameComplete();
        }

        /// <summary>
        /// Called when all elements are correctly placed in their matching cells.
        /// Override in derived classes for custom completion behavior.
        /// </summary>
        protected virtual void OnGameComplete()
        {
            Debug.Log("SortGame: All elements correctly placed! Game complete.");
            HidePopup();
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
            RectTransform elementRect = element.GetRectTransform();
            elementRect.SetParent(lowerSection);
            // Restore anchors for lower section (center-bottom)
            elementRect.anchorMin = new Vector2(0.5f, 0f);
            elementRect.anchorMax = new Vector2(0.5f, 0f);
            Vector2 newPos = GenerateRandomPositions(1)[0];
            elementRect.anchoredPosition = newPos;
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
        private int correctCellIndex; // The cell index where this element should be placed
        protected SortGame game;
        protected RectTransform rectTransform;
        private Image image;

        public void Initialize(int idx, SortGame sortGame, Sprite sprite)
        {
            Initialize(idx, sortGame, sprite, idx); // Default: correctCellIndex matches index
        }

        public void Initialize(int idx, SortGame sortGame, Sprite sprite, int correctCell)
        {
            index = idx;
            correctCellIndex = correctCell;
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
        public int GetCorrectCellIndex() => correctCellIndex;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (game != null)
                game.OnElementDragStart(this);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (game != null && rectTransform != null)
            {
                RectTransform parentRect = rectTransform.parent as RectTransform;
                Canvas canvas = game.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay 
                    ? canvas.worldCamera : null;
                
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    cam,
                    out Vector2 localPoint);
                
                // Calculate anchor position in parent's local space (relative to parent's center/pivot)
                Vector2 parentSize = parentRect.rect.size;
                Vector2 anchorCenter = (rectTransform.anchorMin + rectTransform.anchorMax) / 2f;
                Vector2 anchorLocalPos = new Vector2(
                    (anchorCenter.x - 0.5f) * parentSize.x,
                    (anchorCenter.y - 0.5f) * parentSize.y
                );
                
                // Set anchoredPosition so element center follows cursor exactly
                rectTransform.anchoredPosition = localPoint - anchorLocalPos;
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
        private Color normalColor = new Color(1f, 1f, 1f, 0.2f);
        private Color highlightColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);

        public void Initialize(int idx, SortGame sortGame)
        {
            index = idx;
            game = sortGame;
            rectTransform = GetComponent<RectTransform>();
            
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
                backgroundImage = gameObject.AddComponent<Image>();
            
            // Set a semi-transparent background to show cell boundaries
            backgroundImage.color = normalColor;
        }

        public RectTransform GetRectTransform() => rectTransform;
        public DraggableElement GetElement() => currentElement;
        public int GetIndex() => index;

        public void SetElement(DraggableElement element)
        {
            currentElement = element;
        }

        /// <summary>
        /// Sets visual highlight state for the cell (used during drag proximity feedback).
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = highlighted ? highlightColor : normalColor;
            }
        }
    }
}

