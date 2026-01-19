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
    public class PraySortGame : SortGame
    {
        [Header("Popup Settings")]
        [SerializeField] private int canvasSortingOrder = 200;
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private bool showCloseButton = true;

        private Text _resultText;
        private Sprite[] _targetOrder;
        private List<PrayWordElement> _wordElements = new List<PrayWordElement>();
        private float cellSize = 250f;

        protected override void Awake()
        {
            LoadPrayImages();
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

        private void LoadPrayImages()
        {
            #if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Images/pray" });
            if (guids != null && guids.Length > 0)
            {
                _targetOrder = new Sprite[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    _targetOrder[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                System.Array.Sort(_targetOrder, (a, b) => string.Compare(a.name, b.name));
                Debug.Log($"[PraySort] Loaded {_targetOrder.Length} images from Assets/Art/Images/pray (Editor mode)");
            }
            #endif
            
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
                Debug.LogError("[PraySort] Failed to load images. Make sure the images are either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/pray/");
                Debug.LogError("  2. Or in Assets/Art/Images/pray/ (editor only)");
                _targetOrder = new Sprite[0];
            }
            else
            {
                Debug.Log($"[PraySort] Loaded {_targetOrder.Length} images successfully");
            }
        }

        protected override void InitializeGame()
        {
            if (_targetOrder == null || _targetOrder.Length == 0)
            {
                Debug.LogError("[PraySort] No images loaded! Cannot initialize game.");
                return;
            }

            numberOfElements = 6;
            elementSize = 300f;
            cellSpacing = 30f;
            minDistanceBetweenElements = 200f;
            snapProximityDistance = 200f;
            
            SetupUpperSection();
            SetupLowerSection();
        }

        protected override void SetupUpperSection()
        {
            if (upperSection == null) return;

            Cells.Clear();
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

                Image bgImage = cellObj.AddComponent<Image>();
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

                Image image = wordObj.AddComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;

                PrayWordElement wordElement = wordObj.AddComponent<PrayWordElement>();
                wordElement.Initialize(sprite, i, this);
                _wordElements.Add(wordElement);
            }
        }

        private List<Vector2> GenerateRandomPositionsForPray(int count)
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
                Debug.Log($"[PraySort] Win check skipped: cells.Count = {Cells?.Count ?? 0}, expected 6");
                return;
            }

            string[] expectedTypes = new string[]
            {
                "elion",
                "malachei",
                "hashalom",
                "malachei",
                "alechem",
                "shalom"
            };

            for (int i = 0; i < Cells.Count; i++)
            {
                Cell cell = Cells[i];
                DraggableElement element = cell.GetElement();

                if (element == null)
                {
                    if (_resultText != null)
                        _resultText.text = "";
                    return;
                }
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

                PrayWordElement wordElement = element.GetComponent<PrayWordElement>();
                if (wordElement == null)
                {
                    allCorrect = false;
                    Debug.LogWarning($"[PraySort] Cell {i} element is not a PrayWordElement");
                    break;
                }

                string actualType = wordElement.GetSpriteType();
                string expectedType = expectedTypes[i];

                if (actualType != expectedType)
                {
                    allCorrect = false;
                    Debug.Log($"[PraySort] Cell {i} incorrect: expected '{expectedType}', got '{actualType}'");
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
                Debug.Log("[PraySort] All images correctly ordered! Game complete.");
                OnGameComplete();
                StartCoroutine(CloseAfterDelay(2f));
            }
            else
            {
                if (_resultText != null)
                {
                    _resultText.text = "";
                }
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
            foreach (var element in _wordElements)
            {
                if (element != null)
                {
                    Destroy(element.gameObject);
                }
            }
            _wordElements.Clear();

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

            InitializeGame();
        }

        protected virtual void OnDestroy()
        {
            CloseGame();
        }
    }

    public class PrayWordElement : DraggableElement
    {
        private Sprite _praySprite;
        private Canvas _canvas;

        public Sprite PraySprite => _praySprite;

        public void Initialize(Sprite sprite, int index, PraySortGame game)
        {
            _praySprite = sprite;
            base.Initialize(index, game, sprite);
            _canvas = GetComponentInParent<Canvas>();
        }

        private string GetSpriteTypeFromName(string spriteName)
        {
            string lowerName = spriteName.ToLower();
            
            if (lowerName.Contains("shalom") && !lowerName.Contains("hashalom"))
                return "shalom";
            if (lowerName.Contains("alechem"))
                return "alechem";
            if (lowerName.Contains("malachei"))
                return "malachei";
            if (lowerName.Contains("hashalom"))
                return "hashalom";
            if (lowerName.Contains("elion"))
                return "elion";
                
            Debug.LogWarning($"[PrayWordElement] Unknown sprite name: {spriteName}, defaulting to unknown");
            return "unknown";
        }

        public string GetSpriteType()
        {
            return GetSpriteTypeFromName(_praySprite.name);
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
