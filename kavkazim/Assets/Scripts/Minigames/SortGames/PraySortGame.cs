using System.Collections.Generic;
using Kavkazim.UI;
using Minigames.Base;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigames.SortGames
{
    public class PraySortGame : SortGame
    {
        [Header("Popup Settings")]
        [SerializeField] private int canvasSortingOrder = 200;
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private bool showCloseButton = true;

        private GameObject _popupWindow;
        private Canvas _canvas;
        private GameObject _backgroundPanel;
        private GameObject _contentPanel;
        private Button _closeButton;
        private Text _resultText;
        private Sprite[] _targetOrder; // Array of images in correct order
        private List<PrayWordElement> _wordElements = new List<PrayWordElement>();
        private float cellSize = 250f; // Cell size (3x original 100)

        public bool IsActive => _popupWindow != null && _popupWindow.activeSelf;
        public GameObject PopupWindow => _popupWindow;

        protected override void Awake()
        {
            // Don't call base.Awake() as we'll set up our own popup structure
            // Load images early
            LoadPrayImages();
        }

        protected override void Start()
        {
            // Don't call base.Start() - we'll initialize when StartGame() is called
        }

        private void LoadPrayImages()
        {
            // Load all images from the pray folder
            // Note: For Resources.LoadAll to work, images need to be in a Resources folder
            // Path structure should be: Assets/Resources/Art/Images/pray/
            // If not in Resources, we'll try direct loading via UnityEditor (editor only)
            
            #if UNITY_EDITOR
            // Editor-only: Load directly from assets
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Images/pray" });
            if (guids != null && guids.Length > 0)
            {
                _targetOrder = new Sprite[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    _targetOrder[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                // Sort by name to ensure consistent order
                System.Array.Sort(_targetOrder, (a, b) => string.Compare(a.name, b.name));
                Debug.Log($"[PraySortGame] Loaded {_targetOrder.Length} images from Assets/Art/Images/pray (Editor mode)");
            }
            #endif
            
            // Try Resources loading (works in both editor and build)
            if (_targetOrder == null || _targetOrder.Length == 0)
            {
                _targetOrder = Resources.LoadAll<Sprite>("Art/Images/pray");
            }
            
            if (_targetOrder == null || _targetOrder.Length == 0)
            {
                _targetOrder = Resources.LoadAll<Sprite>("pray");
            }
            
            if (_targetOrder == null || _targetOrder.Length == 0)
            {
                Debug.LogError("[PraySortGame] Failed to load images. Make sure the images are either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/pray/");
                Debug.LogError("  2. Or in Assets/Art/Images/pray/ (editor only)");
                _targetOrder = new Sprite[0];
            }
            else
            {
                Debug.Log($"[PraySortGame] Loaded {_targetOrder.Length} images successfully");
            }
        }

        protected override void InitializeGame()
        {
            // This will be called after popup is created
            // Initialize game settings
            if (_targetOrder == null || _targetOrder.Length == 0)
            {
                Debug.LogError("[PraySortGame] No images loaded! Cannot initialize game.");
                return;
            }

            // Force exactly 6 cells for the prayer game
            numberOfElements = 6;
            elementSize = 300f; // 4x original (100 * 4)
            cellSpacing = 30f; // Increased spacing for larger elements
            minDistanceBetweenElements = 200f; // Reduced to fit elements in lower section
            snapProximityDistance = 200f; // Increased snap distance for larger cells
            
            SetupUpperSection();
            SetupLowerSection();
        }

        protected override void SetupUpperSection()
        {
            if (upperSection == null) return;

            Cells.Clear();
            // Use cellSize for cell dimensions (3x original = 300)
            float totalWidth = (numberOfElements * cellSize) + ((numberOfElements - 1) * cellSpacing);
            float startX = -totalWidth / 2f + cellSize / 2f;

            for (int i = 0; i < numberOfElements; i++)
            {
                GameObject cellObj = new GameObject($"Cell_{i}");
                cellObj.transform.SetParent(upperSection, false);
                RectTransform cellRect = cellObj.AddComponent<RectTransform>();
                
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.anchoredPosition = new Vector2(startX + i * (cellSize + cellSpacing), 0);
                cellRect.anchorMin = new Vector2(0.5f, 1f);
                cellRect.anchorMax = new Vector2(0.5f, 1f);
                cellRect.pivot = new Vector2(0.5f, 0.5f);

                Cell cell = cellObj.AddComponent<Cell>();
                cell.Initialize(i, this);
                Cells.Add(cell);

                // Add background image to show cell boundaries
                Image bgImage = cellObj.AddComponent<Image>();
                // bgImage.color = new Color(1f, 1f, 1f, 0.2f);
            }
        }

        protected override void SetupLowerSection()
        {
            if (lowerSection == null || _targetOrder == null || _targetOrder.Length == 0) return;

            _wordElements.Clear();
            List<Sprite> shuffledSprites = new List<Sprite>(_targetOrder);
            Shuffle(shuffledSprites);
            List<Vector2> positions = GenerateRandomPositionsForPray(numberOfElements);

            for (int i = 0; i < numberOfElements; i++)
            {
                Sprite sprite = shuffledSprites[i];
                GameObject wordObj = new GameObject($"PrayImage_{i}");
                wordObj.transform.SetParent(lowerSection, false);
                RectTransform wordRect = wordObj.AddComponent<RectTransform>();
                
                wordRect.sizeDelta = new Vector2(elementSize, elementSize);
                wordRect.anchoredPosition = positions[i];
                wordRect.anchorMin = new Vector2(0.5f, 0.5f);
                wordRect.anchorMax = new Vector2(0.5f, 0.5f);
                wordRect.pivot = new Vector2(0.5f, 0.5f);

                // Add Image component to display the sprite
                Image image = wordObj.AddComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;

                // Add custom draggable component
                PrayWordElement wordElement = wordObj.AddComponent<PrayWordElement>();
                wordElement.Initialize(sprite, i, this);
                _wordElements.Add(wordElement);
            }
        }

        /// <summary>
        /// Generate random positions within the lower section bounds.
        /// Uses screen-based calculation since anchors define the section size.
        /// </summary>
        private List<Vector2> GenerateRandomPositionsForPray(int count)
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

        protected override Sprite GetElementImage(int index)
        {
            if (_targetOrder != null && index >= 0 && index < _targetOrder.Length)
            {
                return _targetOrder[index];
            }
            return null;
        }

        public override void OnElementDragEnd(DraggableElement element, Vector2 position)
        {
            base.OnElementDragEnd(element, position);
            CheckWinCondition();
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

        private void CheckWinCondition()
        {
            if (Cells == null || Cells.Count != 6)
            {
                Debug.Log($"[PraySortGame] Win check skipped: cells.Count = {Cells?.Count ?? 0}, expected 6");
                return;
            }

            // Define the correct order from right to left (cell 0 is rightmost, cell 5 is leftmost)
            // 1st cell (cell 0): pray_shalom.png
            // 2nd cell (cell 1): pray_alechem.png
            // 3rd cell (cell 2): pray_malachei_2.png or pray_malachei_1.png
            // 4th cell (cell 3): pray_hashalom.png
            // 5th cell (cell 4): pray_malachei_2.png or pray_malachei_1.png
            // 6th cell (cell 5): pray_elion.png
            string[] expectedTypes = new string[]
            {
                "elion",        // Cell 5: 6th cell (leftmost)
                "malachei",    // Cell 4: 5th cell (malachei_1 or malachei_2)
                "hashalom",    // Cell 3: 4th cell
                "malachei",    // Cell 2: 3rd cell (malachei_1 or malachei_2)
                "alechem",     // Cell 1: 2nd cell
                "shalom"      // Cell 0: 1st cell (rightmost)
            };

            // Check if all cells have elements
            for (int i = 0; i < Cells.Count; i++)
            {
                Cell cell = Cells[i];
                DraggableElement element = cell.GetElement();

                if (element == null)
                {
                    // Not all cells filled yet
                    if (_resultText != null)
                        _resultText.text = "";
                    return;
                }
            }

            // All cells are filled, now check if they're in correct order
            bool allCorrect = true;
            for (int i = 0; i < Cells.Count && i < expectedTypes.Length; i++)
            {
                Cell cell = Cells[i];
                DraggableElement element = cell.GetElement();

                if (element == null)
                {
                    allCorrect = false;
                    break;
                }

                PrayWordElement wordElement = element.GetComponent<PrayWordElement>();
                if (wordElement == null)
                {
                    allCorrect = false;
                    Debug.LogWarning($"[PraySortGame] Cell {i} element is not a PrayWordElement");
                    break;
                }

                string actualType = wordElement.GetSpriteType();
                string expectedType = expectedTypes[i];

                if (actualType != expectedType)
                {
                    allCorrect = false;
                    Debug.Log($"[PraySortGame] Cell {i} incorrect: expected '{expectedType}', got '{actualType}'");
                    break;
                }
            }

            if (allCorrect)
            {
                if (_resultText != null)
                {
                    _resultText.text = "Correct! All prayer words in order!";
                    _resultText.color = Color.green;
                }
                Debug.Log("[PraySortGame] All images correctly ordered! Game complete.");
                OnGameComplete();
                StartCoroutine(CloseAfterDelay(2f));
            }
            else
            {
                // Clear result text if not all correct
                if (_resultText != null)
                {
                    _resultText.text = "";
                }
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

            // Clean up elements
            foreach (var element in _wordElements)
            {
                if (element != null)
                {
                    Destroy(element.gameObject);
                }
            }
            _wordElements.Clear();

            if (_popupWindow != null)
            {
                Destroy(_popupWindow);
                _popupWindow = null;
            }

            _canvas = null;
            _backgroundPanel = null;
            _contentPanel = null;
            _closeButton = null;
            upperSection = null;
            lowerSection = null;
        }

        private void CreatePopupWindow()
        {
            // Create root canvas object
            _popupWindow = new GameObject($"{GetType().Name}Popup");
            _popupWindow.transform.SetParent(null);

            // Add Canvas component
            _canvas = _popupWindow.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = canvasSortingOrder;
            _popupWindow.AddComponent<CanvasScaler>();
            _popupWindow.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists
            if (EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Create background overlay
            _backgroundPanel = new GameObject("Background");
            _backgroundPanel.transform.SetParent(_popupWindow.transform, false);
            Image bgImage = _backgroundPanel.AddComponent<Image>();
            bgImage.color = backgroundColor;
            RectTransform bgRect = _backgroundPanel.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Create content panel (centered, 75% of screen)
            _contentPanel = new GameObject("ContentPanel");
            _contentPanel.transform.SetParent(_popupWindow.transform, false);
            Image contentImage = _contentPanel.AddComponent<Image>();
            contentImage.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            // Use anchors for 75% screen coverage (12.5% margin on each side)
            contentRect.anchorMin = new Vector2(0.125f, 0.125f);
            contentRect.anchorMax = new Vector2(0.875f, 0.875f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;

            // Set popupWindow reference for SortGame
            popupWindow = contentRect;
            popupCanvas = _canvas;

            // Create title text
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(_contentPanel.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Order the prayer images";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.UpperCenter;
            titleText.color = Color.white;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.9f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = Vector2.zero;
            titleRect.anchoredPosition = Vector2.zero;

            // Create result text
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
            resultRect.anchorMin = new Vector2(0, 0.75f);
            resultRect.anchorMax = new Vector2(1, 0.85f);
            resultRect.sizeDelta = Vector2.zero;
            resultRect.anchoredPosition = Vector2.zero;

            // Create upper section (for ordered cells)
            GameObject upperSectionObj = new GameObject("UpperSection");
            upperSectionObj.transform.SetParent(_contentPanel.transform, false);
            upperSection = upperSectionObj.AddComponent<RectTransform>();
            upperSection.anchorMin = new Vector2(0, 0.5f);
            upperSection.anchorMax = new Vector2(1, 0.85f);
            upperSection.sizeDelta = Vector2.zero;
            upperSection.anchoredPosition = Vector2.zero;

            // Create lower section (for random placement)
            GameObject lowerSectionObj = new GameObject("LowerSection");
            lowerSectionObj.transform.SetParent(_contentPanel.transform, false);
            lowerSection = lowerSectionObj.AddComponent<RectTransform>();
            lowerSection.anchorMin = new Vector2(0, 0);
            lowerSection.anchorMax = new Vector2(1, 0.5f);
            lowerSection.sizeDelta = Vector2.zero;
            lowerSection.anchoredPosition = Vector2.zero;

            // Create close button if enabled
            if (showCloseButton)
            {
                CreateCloseButton();
            }

            // Initialize the game (sets up sections)
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
    /// Custom draggable element for prayer images that works with SortGame's system.
    /// </summary>
    public class PrayWordElement : DraggableElement
    {
        private Sprite _praySprite;
        private Canvas _canvas;

        public Sprite PraySprite => _praySprite;

        public void Initialize(Sprite sprite, int index, PraySortGame game)
        {
            _praySprite = sprite;
            base.Initialize(index, game, sprite);
            
            // Cache the canvas from the popup window (element is a child of the popup)
            _canvas = GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// Gets the sprite type identifier based on sprite name.
        /// Returns a string identifier for the sprite type.
        /// </summary>
        private string GetSpriteTypeFromName(string spriteName)
        {
            string lowerName = spriteName.ToLower();
            
            if (lowerName.Contains("shalom") && !lowerName.Contains("hashalom"))
                return "shalom"; // pray_shalom
            if (lowerName.Contains("alechem"))
                return "alechem"; // pray_alechem
            if (lowerName.Contains("malachei"))
                return "malachei"; // pray_malachei_1
            if (lowerName.Contains("hashalom"))
                return "hashalom"; // pray_hashalom
            if (lowerName.Contains("elion"))
                return "elion"; // pray_elion
                
            Debug.LogWarning($"[PrayWordElement] Unknown sprite name: {spriteName}, defaulting to unknown");
            return "unknown";
        }

        public string GetSpriteType()
        {
            return GetSpriteTypeFromName(_praySprite.name);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            // Get rectTransform if not cached
            if (RectTransform == null)
            {
                RectTransform = GetComponent<RectTransform>();
            }
            
            // Get canvas if not cached
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }
            
            if (Game != null && RectTransform != null && _canvas != null)
            {
                RectTransform parentRect = RectTransform.parent as RectTransform;
                
                // Use the cached canvas instead of trying to get it from game component
                Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
                
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    cam,
                    out Vector2 localPoint);
                
                // Calculate anchor position in parent's local space (relative to parent's center/pivot)
                Vector2 parentSize = parentRect.rect.size;
                Vector2 anchorCenter = (RectTransform.anchorMin + RectTransform.anchorMax) / 2f;
                Vector2 anchorLocalPos = new Vector2(
                    (anchorCenter.x - 0.5f) * parentSize.x,
                    (anchorCenter.y - 0.5f) * parentSize.y
                );
                
                // Set anchoredPosition so element center follows cursor exactly
                RectTransform.anchoredPosition = localPoint - anchorLocalPos;
                Game.OnElementDrag(this, eventData.position);
            }
        }
    }
}
