using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Kavkazim.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minigames
{
    /// <summary>
    /// A minigame where players drag lezginka dance images to order them correctly.
    /// Features 2 rows of 4 cells for ordering the dance moves.
    /// </summary>
    public class LezginkaSortGame : SortGame, IMinigame
    {
        [Header("Popup Settings")]
        [SerializeField] private int canvasSortingOrder = 200;
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private bool showCloseButton = true;

        [Header("Grid Settings")]
        [SerializeField] private int cellsPerRow = 4;
        [SerializeField] private int numberOfRows = 2;
        [SerializeField] private float rowSpacing = 20f;

        private GameObject _popupWindow;
        private Canvas _canvas;
        private GameObject _backgroundPanel;
        private GameObject _contentPanel;
        private Button _closeButton;
        private Text _resultText;
        private Sprite[] _loadedImages;
        private List<LezginkaElement> _elements = new List<LezginkaElement>();
        private float cellSize = 150f;
        private RectTransform _cellSection;
        private RectTransform _elementSection;

        public bool IsActive => _popupWindow != null && _popupWindow.activeSelf;
        public GameObject PopupWindow => _popupWindow;

        protected override void Awake()
        {
            LoadLezginkaImages();
        }

        protected override void Start()
        {
            // Don't call base.Start() - initialize when StartGame() is called
        }

        private void LoadLezginkaImages()
        {
            #if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Images/lezginka" });
            if (guids != null && guids.Length > 0)
            {
                _loadedImages = new Sprite[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    _loadedImages[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                // Sort by name to ensure consistent order
                System.Array.Sort(_loadedImages, (a, b) => string.Compare(a.name, b.name));
                Debug.Log($"[LezginkaSortGame] Loaded {_loadedImages.Length} images from Assets/Art/Images/lezginka (Editor mode)");
            }
            #endif

            if (_loadedImages == null || _loadedImages.Length == 0)
            {
                _loadedImages = Resources.LoadAll<Sprite>("Art/Images/lezginka");
            }

            if (_loadedImages == null || _loadedImages.Length == 0)
            {
                _loadedImages = Resources.LoadAll<Sprite>("lezginka");
            }

            if (_loadedImages == null || _loadedImages.Length == 0)
            {
                Debug.LogError("[LezginkaSortGame] Failed to load images. Make sure the images are either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/lezginka/");
                Debug.LogError("  2. Or in Assets/Art/Images/lezginka/ (editor only)");
                _loadedImages = new Sprite[0];
            }
            else
            {
                Debug.Log($"[LezginkaSortGame] Loaded {_loadedImages.Length} images successfully");
            }
        }

        protected override void InitializeGame()
        {
            if (_loadedImages == null || _loadedImages.Length == 0)
            {
                Debug.LogError("[LezginkaSortGame] No images loaded! Cannot initialize game.");
                return;
            }

            numberOfElements = _loadedImages.Length;
            // 5x larger images (similar to PraySortGame sizing)
            elementSize = 300f;
            cellSize = 250f;
            cellSpacing = 30f;
            rowSpacing = 25f;
            minDistanceBetweenElements = 200f; // Reduced to fit elements in lower section
            snapProximityDistance = 200f;

            SetupCellGrid();
            SetupElements();
        }

        /// <summary>
        /// Sets up exactly 8 cells in 2 rows of 4 (like PraySortGame but in grid layout)
        /// </summary>
        private void SetupCellGrid()
        {
            if (upperSection == null) return;

            cells.Clear();
            
            // Always create 8 cells: 2 rows × 4 columns
            int totalCells = 8;
            int rowCount = 2;
            int colCount = 4;
            
            // Calculate total dimensions for centering
            float totalRowWidth = (colCount * cellSize) + ((colCount - 1) * cellSpacing);
            float startX = -totalRowWidth / 2f + cellSize / 2f;
            
            // Calculate vertical spacing - position rows from top of section
            float topOffset = -cellSize / 2f - 10f;

            int cellIndex = 0;
            for (int row = 0; row < rowCount; row++)
            {
                for (int col = 0; col < colCount; col++)
                {
                    GameObject cellObj = new GameObject($"Cell_{cellIndex}");
                    cellObj.transform.SetParent(upperSection, false);
                    RectTransform cellRect = cellObj.AddComponent<RectTransform>();

                    cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                    float xPos = startX + col * (cellSize + cellSpacing);
                    float yPos = topOffset - row * (cellSize + rowSpacing);
                    cellRect.anchoredPosition = new Vector2(xPos, yPos);
                    // Use top-center anchor like PraySortGame
                    cellRect.anchorMin = new Vector2(0.5f, 1f);
                    cellRect.anchorMax = new Vector2(0.5f, 1f);
                    cellRect.pivot = new Vector2(0.5f, 0.5f);

                    // Add background image first (Cell.Initialize will use it)
                    Image bgImage = cellObj.AddComponent<Image>();
                    bgImage.color = new Color(1f, 1f, 1f, 0.2f);

                    Cell cell = cellObj.AddComponent<Cell>();
                    cell.Initialize(cellIndex, this);
                    cells.Add(cell);

                    cellIndex++;
                }
            }
        }

        /// <summary>
        /// Sets up the draggable elements with random positions in the lower section
        /// </summary>
        private void SetupElements()
        {
            if (lowerSection == null || _loadedImages == null || _loadedImages.Length == 0) return;

            _elements.Clear();

            List<Sprite> shuffledSprites = new List<Sprite>(_loadedImages);
            Shuffle(shuffledSprites);
            List<Vector2> positions = GenerateRandomPositionsForLezginka(numberOfElements);

            for (int i = 0; i < numberOfElements; i++)
            {
                Sprite sprite = shuffledSprites[i];
                GameObject elementObj = new GameObject($"LezginkaImage_{i}");
                elementObj.transform.SetParent(lowerSection, false);
                RectTransform elementRect = elementObj.AddComponent<RectTransform>();

                elementRect.sizeDelta = new Vector2(elementSize, elementSize);
                elementRect.anchoredPosition = positions[i];
                // Use center anchor so positions are relative to section center
                elementRect.anchorMin = new Vector2(0.5f, 0.5f);
                elementRect.anchorMax = new Vector2(0.5f, 0.5f);
                elementRect.pivot = new Vector2(0.5f, 0.5f);

                Image image = elementObj.AddComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;

                LezginkaElement element = elementObj.AddComponent<LezginkaElement>();
                element.Initialize(sprite, i, this);
                _elements.Add(element);
            }
        }

        /// <summary>
        /// Generate random positions within the lower section bounds.
        /// Uses screen-based calculation since anchors define the section size.
        /// </summary>
        private List<Vector2> GenerateRandomPositionsForLezginka(int count)
        {
            List<Vector2> positions = new List<Vector2>();
            
            // Calculate bounds based on screen size and anchors
            // Lower section is anchors (0,0) to (1, 0.5) of content panel
            // Content panel is anchors (0.125, 0.125) to (0.875, 0.875) of screen
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            
            // Content panel size (75% of screen)
            float contentWidth = screenWidth * 0.75f;
            float contentHeight = screenHeight * 0.75f;
            
            // Lower section is bottom half of content panel
            float sectionWidth = contentWidth;
            float sectionHeight = contentHeight * 0.5f;
            
            // Calculate bounds with margin for element size
            float margin = elementSize / 2f + 20f;
            float halfWidth = sectionWidth / 2f - margin;
            float halfHeight = sectionHeight / 2f - margin;

            int maxAttempts = 1000;
            for (int i = 0; i < count; i++)
            {
                Vector2 position = Vector2.zero;
                bool validPosition = false;
                int attempts = 0;

                while (!validPosition && attempts < maxAttempts)
                {
                    position = new Vector2(
                        Random.Range(-halfWidth, halfWidth),
                        Random.Range(-halfHeight, halfHeight)
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

        private void Shuffle<T>(IList<T> list)
        {
            System.Random rng = new System.Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        public override void OnElementDragEnd(DraggableElement element, Vector2 position)
        {
            base.OnElementDragEnd(element, position);
            CheckWinCondition();
        }

        private void CheckWinCondition()
        {
            if (cells == null || cells.Count != 8)
                return;

            // Define the correct order for each cell
            // Upper row (cells 0-3): hands_1, hands_2, hands_1, hands_3
            // Lower row (cells 4-7): right_foot_up, right_foot_forward, left_foot_up, left_foot_forward
            string[] expectedTypes = new string[]
            {
                "hands_1",           // Cell 0: Upper row, 1st cell
                "hands_2",           // Cell 1: Upper row, 2nd cell
                "hands_1",           // Cell 2: Upper row, 3rd cell
                "hands_3",           // Cell 3: Upper row, 4th cell
                "right_foot_up",     // Cell 4: Lower row, 1st cell
                "right_foot_forward", // Cell 5: Lower row, 2nd cell
                "left_foot_up",      // Cell 6: Lower row, 3rd cell
                "left_foot_forward"  // Cell 7: Lower row, 4th cell
            };

            // Check if all cells have elements
            bool allCellsFilled = true;
            for (int i = 0; i < cells.Count; i++)
            {
                Cell cell = cells[i];
                DraggableElement element = cell.GetElement();

                if (element == null)
                {
                    allCellsFilled = false;
                    break;
                }
            }

            if (!allCellsFilled)
            {
                if (_resultText != null)
                    _resultText.text = "";
                return;
            }

            // Check if all elements are in their correct positions
            bool allCorrect = true;
            for (int i = 0; i < cells.Count && i < expectedTypes.Length; i++)
            {
                Cell cell = cells[i];
                DraggableElement element = cell.GetElement();

                if (element == null)
                {
                    allCorrect = false;
                    break;
                }

                LezginkaElement lezginkaElement = element.GetComponent<LezginkaElement>();
                if (lezginkaElement == null)
                {
                    allCorrect = false;
                    break;
                }

                string actualType = lezginkaElement.GetSpriteType();
                string expectedType = expectedTypes[i];

                if (actualType != expectedType)
                {
                    allCorrect = false;
                    Debug.Log($"[LezginkaSortGame] Cell {i} incorrect: expected {expectedType}, got {actualType}");
                    break;
                }
            }

            if (allCorrect)
            {
                if (_resultText != null)
                {
                    _resultText.text = "Correct! All dance moves in order!";
                    _resultText.color = Color.green;
                }
                Debug.Log("[LezginkaSortGame] All images correctly ordered! Game complete.");
                StartCoroutine(CloseAfterDelay(2f));
            }
            else if (_resultText != null)
            {
                _resultText.text = "";
            }
        }

        private System.Collections.IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseGame();
        }

        // IMinigame implementation
        public void StartGame()
        {
            if (IsActive)
            {
                Debug.LogWarning($"{GetType().Name} is already active!");
                return;
            }

            CreatePopupWindow();
            _popupWindow.SetActive(true);
        }

        public void CloseGame()
        {
            if (!IsActive)
            {
                return;
            }

            foreach (var element in _elements)
            {
                if (element != null)
                {
                    Destroy(element.gameObject);
                }
            }
            _elements.Clear();

            if (_popupWindow != null)
            {
                Destroy(_popupWindow);
                _popupWindow = null;
            }

            _canvas = null;
            _backgroundPanel = null;
            _contentPanel = null;
            _closeButton = null;
            _cellSection = null;
            _elementSection = null;
            upperSection = null;
            lowerSection = null;
        }

        private void CreatePopupWindow()
        {
            _popupWindow = new GameObject($"{GetType().Name}Popup");
            _popupWindow.transform.SetParent(null);

            _canvas = _popupWindow.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = canvasSortingOrder;
            _popupWindow.AddComponent<CanvasScaler>();
            _popupWindow.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Background overlay
            _backgroundPanel = new GameObject("Background");
            _backgroundPanel.transform.SetParent(_popupWindow.transform, false);
            Image bgImage = _backgroundPanel.AddComponent<Image>();
            bgImage.color = backgroundColor;
            RectTransform bgRect = _backgroundPanel.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Content panel (75% of screen)
            _contentPanel = new GameObject("ContentPanel");
            _contentPanel.transform.SetParent(_popupWindow.transform, false);
            Image contentImage = _contentPanel.AddComponent<Image>();
            contentImage.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.125f, 0.125f);
            contentRect.anchorMax = new Vector2(0.875f, 0.875f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;

            popupWindow = contentRect;
            popupCanvas = _canvas;

            // Title text
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(_contentPanel.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Order the Lezginka dance moves";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.UpperCenter;
            titleText.color = Color.white;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.92f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = Vector2.zero;
            titleRect.anchoredPosition = Vector2.zero;

            // Result text
            GameObject resultTextObj = new GameObject("ResultText");
            resultTextObj.transform.SetParent(_contentPanel.transform, false);
            _resultText = resultTextObj.AddComponent<Text>();
            _resultText.text = "";
            _resultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _resultText.fontSize = 32;
            _resultText.fontStyle = FontStyle.Bold;
            _resultText.alignment = TextAnchor.MiddleCenter;
            _resultText.color = new Color(0.2f, 1f, 0.3f, 1f);
            RectTransform resultRect = resultTextObj.GetComponent<RectTransform>();
            resultRect.anchorMin = new Vector2(0, 0.85f);
            resultRect.anchorMax = new Vector2(1, 0.92f);
            resultRect.sizeDelta = Vector2.zero;
            resultRect.anchoredPosition = Vector2.zero;

            // Upper section (for the 2 rows of cells) - same structure as PraySortGame
            GameObject upperSectionObj = new GameObject("UpperSection");
            upperSectionObj.transform.SetParent(_contentPanel.transform, false);
            _cellSection = upperSectionObj.AddComponent<RectTransform>();
            _cellSection.anchorMin = new Vector2(0, 0.5f);
            _cellSection.anchorMax = new Vector2(1, 0.85f);
            _cellSection.sizeDelta = Vector2.zero;
            _cellSection.anchoredPosition = Vector2.zero;
            upperSection = _cellSection; // Set reference for SortGame

            // Lower section (for randomly scattered elements) - same structure as PraySortGame
            GameObject lowerSectionObj = new GameObject("LowerSection");
            lowerSectionObj.transform.SetParent(_contentPanel.transform, false);
            _elementSection = lowerSectionObj.AddComponent<RectTransform>();
            _elementSection.anchorMin = new Vector2(0, 0);
            _elementSection.anchorMax = new Vector2(1, 0.5f);
            _elementSection.sizeDelta = Vector2.zero;
            _elementSection.anchoredPosition = Vector2.zero;
            lowerSection = _elementSection; // Set reference for SortGame

            if (showCloseButton)
            {
                CreateCloseButton();
            }

            InitializeGame();
        }

        private void CreateCloseButton()
        {
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(_contentPanel.transform, false);
            _closeButton = closeBtnObj.AddComponent<Button>();
            Image btnImage = closeBtnObj.AddComponent<Image>();
            btnImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);

            RectTransform btnRect = closeBtnObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(40, 40);
            btnRect.anchorMin = new Vector2(1, 1);
            btnRect.anchorMax = new Vector2(1, 1);
            btnRect.anchoredPosition = new Vector2(-20, -20);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(closeBtnObj.transform, false);
            Text txt = txtObj.AddComponent<Text>();
            txt.text = "X";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            _closeButton.onClick.AddListener(CloseGame);
        }

        protected virtual void OnDestroy()
        {
            CloseGame();
        }
    }

    /// <summary>
    /// Custom draggable element for lezginka dance images.
    /// </summary>
    public class LezginkaElement : DraggableElement
    {
        private Sprite _sprite;
        private Canvas _canvas;

        public Sprite LezginkaSprite => _sprite;

        public void Initialize(Sprite sprite, int index, LezginkaSortGame game)
        {
            _sprite = sprite;
            base.Initialize(index, game, sprite);
            _canvas = GetComponentInParent<Canvas>();
        }

        public string GetSpriteType()
        {
            return GetSpriteTypeFromName(_sprite.name);
        }

        /// <summary>
        /// Gets the sprite type identifier based on sprite name.
        /// Returns a string identifier for the sprite type.
        /// </summary>
        private string GetSpriteTypeFromName(string spriteName)
        {
            string lowerName = spriteName.ToLower();

            // Check for hands images
            if (lowerName.Contains("lezginka_hands_1_copy") || lowerName.Contains("lezginka_hands_1"))
            {
                // Make sure it's not hands_2 or hands_3
                if (!lowerName.Contains("hands_2") && !lowerName.Contains("hands_3"))
                    return "hands_1";
            }
            if (lowerName.Contains("lezginka_hands_2"))
                return "hands_2";
            if (lowerName.Contains("lezginka_hands_3"))
                return "hands_3";
            
            // Check for foot images
            if (lowerName.Contains("right_foot_up"))
                return "right_foot_up";
            if (lowerName.Contains("right_foot_forward"))
                return "right_foot_forward";
            if (lowerName.Contains("left_foot_up"))
                return "left_foot_up";
            if (lowerName.Contains("left_foot_forward"))
                return "left_foot_forward";

            Debug.LogWarning($"[LezginkaElement] Unknown sprite name: {spriteName}, defaulting to unknown");
            return "unknown";
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            if (game != null && rectTransform != null && _canvas != null)
            {
                RectTransform parentRect = rectTransform.parent as RectTransform;
                Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

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
                game.OnElementDrag(this, eventData.position);
            }
        }
    }
}

