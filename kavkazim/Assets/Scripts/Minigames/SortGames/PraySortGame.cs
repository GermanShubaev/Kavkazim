using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Kavkazim.UI;
using Minigames;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minigames
{
    /// <summary>
    /// A minigame where players drag prayer images to order them correctly using SortGame's two-section layout.
    /// </summary>
    public class PraySortGame : SortGame, IMinigame
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

            numberOfElements = _targetOrder.Length;
            elementSize = 300f; // 4x original (100 * 4)
            cellSpacing = 30f; // Increased spacing for larger elements
            minDistanceBetweenElements = 450f; // Increased for larger elements
            snapProximityDistance = 200f; // Increased snap distance for larger cells
            
            SetupUpperSection();
            SetupLowerSection();
        }

        protected override void SetupUpperSection()
        {
            if (upperSection == null) return;

            cells.Clear();
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
                cells.Add(cell);

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
            List<Vector2> positions = GenerateRandomPositions(numberOfElements);

            for (int i = 0; i < numberOfElements; i++)
            {
                Sprite sprite = shuffledSprites[i];
                GameObject wordObj = new GameObject($"PrayImage_{i}");
                wordObj.transform.SetParent(lowerSection, false);
                RectTransform wordRect = wordObj.AddComponent<RectTransform>();
                
                wordRect.sizeDelta = new Vector2(elementSize, elementSize);
                wordRect.anchoredPosition = positions[i];
                wordRect.anchorMin = new Vector2(0.5f, 0f);
                wordRect.anchorMax = new Vector2(0.5f, 0f);
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
            if (cells == null || cells.Count != 6)
                return;

            // Check if all cells have elements
            bool allFilled = true;
            int[] placedValues = new int[6];
            
            for (int i = 0; i < cells.Count; i++)
            {
                Cell cell = cells[i];
                DraggableElement element = cell.GetElement();
                
                if (element == null)
                {
                    allFilled = false;
                    break;
                }

                PrayWordElement wordElement = element.GetComponent<PrayWordElement>();
                if (wordElement == null)
                {
                    allFilled = false;
                    break;
                }
                
                placedValues[i] = wordElement.OrderValue;
            }

            if (!allFilled)
            {
                if (_resultText != null)
                    _resultText.text = "";
                return;
            }

            // Check correct order (right to left: cell 0 = value 1, cell 1 = value 2, etc.)
            // Positions 3 and 5 (cell indices 2 and 4) are interchangeable for malachei images
            bool allCorrect = true;
            
            // Check fixed positions
            if (placedValues[0] != 1) allCorrect = false; // pray_shalom must be in cell 0
            if (placedValues[1] != 2) allCorrect = false; // pray_alechem must be in cell 1
            if (placedValues[3] != 4) allCorrect = false; // pray_hashalom must be in cell 3
            if (placedValues[5] != 6) allCorrect = false; // pray_elion must be in cell 5
            
            // Check interchangeable malachei positions (cells 2 and 4 should have values 3 and 5 in any order)
            bool malacheiCorrect = (placedValues[2] == 3 && placedValues[4] == 5) ||
                                   (placedValues[2] == 5 && placedValues[4] == 3);
            if (!malacheiCorrect) allCorrect = false;

            if (allCorrect)
            {
                if (_resultText != null)
                    _resultText.text = "Correct!";
                
                // Close the popup after a short delay
                StartCoroutine(CloseAfterDelay(1.5f));
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
        private int _orderValue; // The correct position value (1-6, right to left)

        public Sprite PraySprite => _praySprite;
        public int OrderValue => _orderValue;

        public void Initialize(Sprite sprite, int index, PraySortGame game)
        {
            _praySprite = sprite;
            _orderValue = GetOrderValueFromName(sprite.name);
            base.Initialize(index, game, sprite);
            
            // Cache the canvas from the popup window (element is a child of the popup)
            _canvas = GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// Gets the order value based on sprite name.
        /// Order is right to left: 1 = rightmost, 6 = leftmost
        /// </summary>
        private int GetOrderValueFromName(string spriteName)
        {
            string lowerName = spriteName.ToLower();
            
            if (lowerName.Contains("shalom") && !lowerName.Contains("hashalom"))
                return 1; // pray_shalom
            if (lowerName.Contains("alechem"))
                return 2; // pray_alechem
            if (lowerName.Contains("malachei_1"))
                return 3; // pray_malachei_1 (can also be 5)
            if (lowerName.Contains("hashalom"))
                return 4; // pray_hashalom
            if (lowerName.Contains("malachei_2"))
                return 5; // pray_malachei_2 (can also be 3)
            if (lowerName.Contains("elion"))
                return 6; // pray_elion
                
            Debug.LogWarning($"[PrayWordElement] Unknown sprite name: {spriteName}, defaulting to order 0");
            return 0;
        }

        public override void OnDrag(PointerEventData eventData)
        {
            // Get rectTransform if not cached
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }
            
            // Get canvas if not cached
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }
            
            if (game != null && rectTransform != null && _canvas != null)
            {
                RectTransform parentRect = rectTransform.parent as RectTransform;
                
                // Use the cached canvas instead of trying to get it from game component
                Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
                
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
    }
}
