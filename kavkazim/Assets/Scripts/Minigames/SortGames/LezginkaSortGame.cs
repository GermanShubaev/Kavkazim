using System.Collections.Generic;
using Kavkazim.UI;
using Minigames.Base;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minigames.SortGames
{
    public class LezginkaSortGame : SortGame
    {
        [Header("Popup Settings")]
        [SerializeField] private int canvasSortingOrder = 200;
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private bool showCloseButton = true;

        [Header("Grid Settings")]
        [SerializeField] private int cellsPerRow = 4;
        [SerializeField] private int numberOfRows = 2;
        [SerializeField] private float rowSpacing = 20f;

        private Text _resultText;
        private Sprite[] _loadedImages;
        private List<LezginkaElement> _elements = new List<LezginkaElement>();
        private float cellSize = 150f;
        private RectTransform _cellSection;
        private RectTransform _elementSection;

        protected override void Awake()
        {
            LoadLezginkaImages();
        }

        protected override void Start()
        {
            if (IsActive)
            {
                Debug.LogWarning($"{GetType().Name} is already active!");
                return;
            }

            StartGame();
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
            elementSize = 300f;
            cellSize = 250f;
            cellSpacing = 30f;
            rowSpacing = 25f;
            minDistanceBetweenElements = 200f; 
            snapProximityDistance = 200f;

            SetupCellGrid();
            SetupElements();
        }

        private void SetupCellGrid()
        {
            if (upperSection == null) return;

            Cells.Clear();
            
            int totalCells = 8;
            int rowCount = 2;
            int colCount = 4;
            
            float totalRowWidth = (colCount * cellSize) + ((colCount - 1) * cellSpacing);
            float startX = -totalRowWidth / 2f + cellSize / 2f;
            
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
                    cellRect.anchorMin = new Vector2(0.5f, 1f);
                    cellRect.anchorMax = new Vector2(0.5f, 1f);
                    cellRect.pivot = new Vector2(0.5f, 0.5f);

                    Image bgImage = cellObj.AddComponent<Image>();
                    bgImage.color = new Color(1f, 1f, 1f, 0.2f);

                    Cell cell = cellObj.AddComponent<Cell>();
                    cell.Initialize(cellIndex, this);
                    Cells.Add(cell);

                    cellIndex++;
                }
            }
        }

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

        private List<Vector2> GenerateRandomPositionsForLezginka(int count)
        {
            List<Vector2> positions = new List<Vector2>();
            
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            
            float contentWidth = screenWidth * 0.75f;
            float contentHeight = screenHeight * 0.75f;
            
            float sectionWidth = contentWidth;
            float sectionHeight = contentHeight * 0.5f;
            
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
            if (Cells == null || Cells.Count != 8)
                return;

            string[] expectedTypes = new string[]
            {
                "hands_1",           
                "hands_2",           
                "hands_1",           
                "hands_3",           
                "right_foot_up",     
                "right_foot_forward",
                "left_foot_up",      
                "left_foot_forward"  
            };

            bool allCellsFilled = true;
            for (int i = 0; i < Cells.Count; i++)
            {
                Cell cell = Cells[i];
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
                OnGameComplete();
                StartCoroutine(CloseAfterDelay(2f));
            }
            else if (_resultText != null)
            {
                _resultText.text = "";
            }
        }

        public override void OnGameComplete()
        {
            base.OnGameComplete();
        }

        private System.Collections.IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseGame();
        }

        public override void CloseGame()
        {
            if (!IsActive)
            {
                return;
            }

            base.CloseGame();
        }

        protected override void CleanupGameUI()
        {
            foreach (var element in _elements)
            {
                if (element != null)
                {
                    Destroy(element.gameObject);
                }
            }
            _elements.Clear();

            _cellSection = null;
            _elementSection = null;
            upperSection = null;
            lowerSection = null;
        }

        protected override void InitializeGameUI()
        {
            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                contentRect.anchorMin = new Vector2(0.125f, 0.125f);
                contentRect.anchorMax = new Vector2(0.875f, 0.875f);
                contentRect.sizeDelta = Vector2.zero;
                contentRect.anchoredPosition = Vector2.zero;
                Image contentImage = _contentPanel.GetComponent<Image>();
                if (contentImage != null)
                {
                    contentImage.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
                }
            }

            if (_popupWindow != null)
            {
                popupWindow = _contentPanel.GetComponent<RectTransform>();
            }
            if (_canvas != null)
            {
                popupCanvas = _canvas;
            }

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

            if (upperSection == null)
            {
                GameObject upperSectionObj = new GameObject("UpperSection");
                upperSectionObj.transform.SetParent(_contentPanel.transform, false);
                upperSection = upperSectionObj.AddComponent<RectTransform>();
                upperSection.anchorMin = new Vector2(0, 0.5f);
                upperSection.anchorMax = new Vector2(1, 0.85f);
                upperSection.sizeDelta = Vector2.zero;
                upperSection.anchoredPosition = Vector2.zero;
            }
            _cellSection = upperSection;

            if (lowerSection == null)
            {
                GameObject lowerSectionObj = new GameObject("LowerSection");
                lowerSectionObj.transform.SetParent(_contentPanel.transform, false);
                lowerSection = lowerSectionObj.AddComponent<RectTransform>();
                lowerSection.anchorMin = new Vector2(0, 0f);
                lowerSection.anchorMax = new Vector2(1, 0.5f);
                lowerSection.sizeDelta = Vector2.zero;
                lowerSection.anchoredPosition = Vector2.zero;
            }
            _elementSection = lowerSection;

            InitializeGame();
        }

        protected virtual void OnDestroy()
        {
            CloseGame();
        }
    }

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

        private string GetSpriteTypeFromName(string spriteName)
        {
            string lowerName = spriteName.ToLower();

            if (lowerName.Contains("lezginka_hands_1_copy") || lowerName.Contains("lezginka_hands_1"))
            {
                if (!lowerName.Contains("hands_2") && !lowerName.Contains("hands_3"))
                    return "hands_1";
            }
            if (lowerName.Contains("lezginka_hands_2"))
                return "hands_2";
            if (lowerName.Contains("lezginka_hands_3"))
                return "hands_3";
            
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
            if (RectTransform == null)
            {
                RectTransform = GetComponent<RectTransform>();
            }

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            if (Game != null && RectTransform != null && _canvas != null)
            {
                RectTransform parentRect = RectTransform.parent as RectTransform;
                Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    cam,
                    out Vector2 localPoint);

                Vector2 parentSize = parentRect.rect.size;
                Vector2 anchorCenter = (RectTransform.anchorMin + RectTransform.anchorMax) / 2f;
                Vector2 anchorLocalPos = new Vector2(
                    (anchorCenter.x - 0.5f) * parentSize.x,
                    (anchorCenter.y - 0.5f) * parentSize.y
                );

                RectTransform.anchoredPosition = localPoint - anchorLocalPos;
                Game.OnElementDrag(this, eventData.position);
            }
        }
    }
}

