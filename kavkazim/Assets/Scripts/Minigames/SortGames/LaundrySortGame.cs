using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigames.SortGames
{
    public class LaundrySortGame : SortGame
    {
        [Header("Game Settings")]
        [SerializeField] private int totalClothes = 9;
        [SerializeField] private Vector2 basketSize = new Vector2(900, 900);
        [SerializeField] private Vector2 clothingSize = new Vector2(300, 300);
        [SerializeField] private float snapProximityDistance = 150f;

        private Sprite[] _basketSprites = new Sprite[5];
        private List<Sprite> _allClothesSprites = new List<Sprite>();

        private Image _mainBasketImage;
        private Image _leftBasketImage;
        private Image _rightBasketImage;
        private RectTransform _leftBasketRect;
        private RectTransform _rightBasketRect;
        private RectTransform _mainBasketRect;
        private Text _resultText;
        private Button _mainBasketButton;

        public class ClothingItem
        {
            public Sprite sprite;
            public bool isWhite; 
            public string fileName;
        }
        private List<ClothingItem> _clothingInventory = new List<ClothingItem>();
        private List<DraggableClothing> _activeClothes = new List<DraggableClothing>();
        
        private List<ClothingItem> _leftBasketItems = new List<ClothingItem>(); 
        private List<ClothingItem> _rightBasketItems = new List<ClothingItem>(); 

        private void Awake()
        {
            LoadImages();
        }

        private void LoadImages()
        {
            #if UNITY_EDITOR
            for (int i = 1; i <= 5; i++)
            {
                string path = $"Assets/Art/Images/laundry/basket/basket_{i}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null)
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                if (sprite != null)
                    _basketSprites[i - 1] = sprite;
            }

            var allClothesGuids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Images/laundry/clothes" });
            if (allClothesGuids != null && allClothesGuids.Length > 0)
            {
                foreach (var guid in allClothesGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Debug.Log($"[LaundrySortGame] path {path} sprite, {sprite} Amir");
                    
                    if (sprite == null)
                    {
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (tex != null)
                            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                    if (sprite != null)
                    {
                        if (string.IsNullOrEmpty(sprite.name) || sprite.name == "New Sprite")
                        {
                            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                            sprite.name = fileName;
                        }
                        _allClothesSprites.Add(sprite);
                    }
                }
            }
            #endif

            if (_basketSprites[0] == null)
            {
                for (int i = 1; i <= 5; i++)
                {
                    Sprite sprite = Resources.Load<Sprite>($"Art/Images/laundry/basket/basket_{i}");
                    if (sprite == null)
                        sprite = Resources.Load<Sprite>($"laundry/basket/basket_{i}");
                    if (sprite != null)
                        _basketSprites[i - 1] = sprite;
                }
            }

            if (_allClothesSprites.Count == 0)
            {
                var colorSprites = Resources.LoadAll<Sprite>("Art/Images/laundry/clothes/color");
                if (colorSprites == null || colorSprites.Length == 0)
                    colorSprites = Resources.LoadAll<Sprite>("laundry/clothes/color");
                if (colorSprites != null)
                    _allClothesSprites.AddRange(colorSprites);

                var whiteSprites = Resources.LoadAll<Sprite>("Art/Images/laundry/clothes/white");
                if (whiteSprites == null || whiteSprites.Length == 0)
                    whiteSprites = Resources.LoadAll<Sprite>("laundry/clothes/white");
                if (whiteSprites != null)
                    _allClothesSprites.AddRange(whiteSprites);
            }

            Debug.Log($"[LaundrySortGame] Loaded {_basketSprites.Length} basket sprites, {_allClothesSprites.Count} total clothes");
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
            }

            // Initialize clothing inventory
            InitializeInventory();

            // Create result text
            CreateResultText();

            // Create main basket in center
            CreateMainBasket();

            // Create left basket (for colored clothes)
            CreateLeftBasket();

            // Create right basket (for white clothes)
            CreateRightBasket();
        }

        private void InitializeInventory()
        {
            _clothingInventory.Clear();
            _leftBasketItems.Clear();
            _rightBasketItems.Clear();
            _activeClothes.Clear();

            List<ClothingItem> allClothes = new List<ClothingItem>();

            foreach (Sprite sprite in _allClothesSprites)
            {
                if (sprite == null) continue;

                string fileName = sprite.name.ToLower();
                bool isWhite = fileName.Contains("white");

                allClothes.Add(new ClothingItem
                {
                    sprite = sprite,
                    isWhite = isWhite,
                    fileName = fileName
                });
            }

            Shuffle(allClothes);

            int clothesToTake = Mathf.Min(totalClothes, allClothes.Count);
            for (int i = 0; i < clothesToTake; i++)
            {
                _clothingInventory.Add(allClothes[i]);
            }

            UpdateMainBasketImage();

            Debug.Log($"[LaundrySortGame] Initialized inventory with {_clothingInventory.Count} clothes");
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private void CreateResultText()
        {
            GameObject resultObj = new GameObject("ResultText");
            resultObj.transform.SetParent(_contentPanel.transform, false);

            _resultText = resultObj.AddComponent<Text>();
            _resultText.text = "Click the middle basket to get clothes!";
            _resultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _resultText.fontSize = 24;
            _resultText.fontStyle = FontStyle.Bold;
            _resultText.alignment = TextAnchor.MiddleCenter;
            _resultText.color = Color.white;

            RectTransform resultRect = resultObj.GetComponent<RectTransform>();
            resultRect.anchorMin = new Vector2(0, 0.85f);
            resultRect.anchorMax = new Vector2(1, 0.95f);
            resultRect.sizeDelta = Vector2.zero;
            resultRect.anchoredPosition = Vector2.zero;
        }

        private void CreateMainBasket()
        {
            GameObject mainBasketObj = new GameObject("MainBasket");
            mainBasketObj.transform.SetParent(_contentPanel.transform, false);

            _mainBasketImage = mainBasketObj.AddComponent<Image>();
            _mainBasketImage.sprite = _basketSprites[4];
            _mainBasketImage.preserveAspect = true;

            _mainBasketRect = mainBasketObj.GetComponent<RectTransform>();
            _mainBasketRect.sizeDelta = basketSize;
            _mainBasketRect.anchorMin = new Vector2(0.5f, 0.5f);
            _mainBasketRect.anchorMax = new Vector2(0.5f, 0.5f);
            _mainBasketRect.anchoredPosition = Vector2.zero;

            _mainBasketButton = mainBasketObj.AddComponent<Button>();
            _mainBasketButton.onClick.AddListener(OnMainBasketClicked);
        }

        private void CreateLeftBasket()
        {
            GameObject leftBasketObj = new GameObject("LeftBasket");
            leftBasketObj.transform.SetParent(_contentPanel.transform, false);

            _leftBasketImage = leftBasketObj.AddComponent<Image>();
            _leftBasketImage.sprite = _basketSprites[0]; // basket_1.png
            _leftBasketImage.preserveAspect = true;

            _leftBasketRect = leftBasketObj.GetComponent<RectTransform>();
            _leftBasketRect.sizeDelta = basketSize;
            _leftBasketRect.anchorMin = new Vector2(0.2f, 0.5f);
            _leftBasketRect.anchorMax = new Vector2(0.2f, 0.5f);
            _leftBasketRect.anchoredPosition = Vector2.zero;

            GameObject leftLabelObj = new GameObject("LeftBasketLabel");
            leftLabelObj.transform.SetParent(_contentPanel.transform, false);
            leftLabelObj.transform.SetAsLastSibling();

            Text leftLabelText = leftLabelObj.AddComponent<Text>();
            leftLabelText.text = "color";
            leftLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            leftLabelText.fontSize = 36;
            leftLabelText.fontStyle = FontStyle.Bold;
            leftLabelText.alignment = TextAnchor.MiddleCenter;
            leftLabelText.color = Color.black;

            Shadow leftShadow = leftLabelObj.AddComponent<Shadow>();
            leftShadow.effectColor = Color.white;
            leftShadow.effectDistance = new Vector2(2, -2);

            RectTransform leftLabelRect = leftLabelObj.GetComponent<RectTransform>();
            leftLabelRect.anchorMin = new Vector2(0.2f, 0.75f);
            leftLabelRect.anchorMax = new Vector2(0.2f, 0.75f);
            leftLabelRect.sizeDelta = new Vector2(300, 60);
            leftLabelRect.anchoredPosition = Vector2.zero;
        }

        private void CreateRightBasket()
        {
            GameObject rightBasketObj = new GameObject("RightBasket");
            rightBasketObj.transform.SetParent(_contentPanel.transform, false);

            _rightBasketImage = rightBasketObj.AddComponent<Image>();
            _rightBasketImage.sprite = _basketSprites[0]; // basket_1.png
            _rightBasketImage.preserveAspect = true;

            _rightBasketRect = rightBasketObj.GetComponent<RectTransform>();
            _rightBasketRect.sizeDelta = basketSize;
            _rightBasketRect.anchorMin = new Vector2(0.8f, 0.5f);
            _rightBasketRect.anchorMax = new Vector2(0.8f, 0.5f);
            _rightBasketRect.anchoredPosition = Vector2.zero;

            GameObject rightLabelObj = new GameObject("RightBasketLabel");
            rightLabelObj.transform.SetParent(_contentPanel.transform, false);
            rightLabelObj.transform.SetAsLastSibling();

            Text rightLabelText = rightLabelObj.AddComponent<Text>();
            rightLabelText.text = "white";
            rightLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rightLabelText.fontSize = 36;
            rightLabelText.fontStyle = FontStyle.Bold;
            rightLabelText.alignment = TextAnchor.MiddleCenter;
            rightLabelText.color = Color.black;

            Shadow rightShadow = rightLabelObj.AddComponent<Shadow>();
            rightShadow.effectColor = Color.white;
            rightShadow.effectDistance = new Vector2(2, -2);

            RectTransform rightLabelRect = rightLabelObj.GetComponent<RectTransform>();
            rightLabelRect.anchorMin = new Vector2(0.8f, 0.75f);
            rightLabelRect.anchorMax = new Vector2(0.8f, 0.75f);
            rightLabelRect.sizeDelta = new Vector2(300, 60);
            rightLabelRect.anchoredPosition = Vector2.zero;
        }

        private void OnMainBasketClicked()
        {
            if (_clothingInventory.Count > 0)
            {
                int randomIndex = Random.Range(0, _clothingInventory.Count);
                ClothingItem item = _clothingInventory[randomIndex];
                _clothingInventory.RemoveAt(randomIndex);

                SpawnClothing(item);
                UpdateMainBasketImage();
            }
        }

        private void SpawnClothing(ClothingItem item)
        {
            GameObject clothingObj = new GameObject($"Clothing_{item.fileName}");
            clothingObj.transform.SetParent(_contentPanel.transform, false);

            Image clothingImage = clothingObj.AddComponent<Image>();
            clothingImage.sprite = item.sprite;
            clothingImage.preserveAspect = true;

            RectTransform clothingRect = clothingObj.GetComponent<RectTransform>();
            clothingRect.sizeDelta = clothingSize;
            clothingRect.anchorMin = new Vector2(0.5f, 0.5f);
            clothingRect.anchorMax = new Vector2(0.5f, 0.5f);
            clothingRect.anchoredPosition = Vector2.zero;

            DraggableClothing draggable = clothingObj.AddComponent<DraggableClothing>();
            draggable.Initialize(item.sprite, item.isWhite, this, item);
            _activeClothes.Add(draggable);
        }

        private void UpdateMainBasketImage()
        {
            int unsortedCount = _clothingInventory.Count + _activeClothes.Count;
            int basketIndex = 0;

            if (unsortedCount >= 9)
                basketIndex = 4;
            else if (unsortedCount >= 6)
                basketIndex = 3;
            else if (unsortedCount >= 3)
                basketIndex = 2;
            else if (unsortedCount >= 1)
                basketIndex = 1;
            else
                basketIndex = 0;

            if (_mainBasketImage != null && basketIndex >= 0 && basketIndex < _basketSprites.Length)
            {
                _mainBasketImage.sprite = _basketSprites[basketIndex];
            }
        }

        public void OnClothingDropped(DraggableClothing clothing, Vector2 screenPosition)
        {
            if (clothing == null || !_activeClothes.Contains(clothing))
                return;

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            bool droppedOnLeft = _leftBasketRect != null && 
                RectTransformUtility.RectangleContainsScreenPoint(_leftBasketRect, screenPosition, cam);
            bool droppedOnRight = _rightBasketRect != null && 
                RectTransformUtility.RectangleContainsScreenPoint(_rightBasketRect, screenPosition, cam);

            if (droppedOnLeft || droppedOnRight)
            {
                ClothingItem item = clothing.GetClothingItem();
                
                if (droppedOnLeft)
                {
                    _leftBasketItems.Add(item);
                }
                else if (droppedOnRight)
                {
                    _rightBasketItems.Add(item);
                }

                _activeClothes.Remove(clothing);
                Destroy(clothing.gameObject);
                UpdateMainBasketImage();

                int totalSorted = _leftBasketItems.Count + _rightBasketItems.Count;
                if (totalSorted == totalClothes)
                {
                    CheckWinCondition();
                }
            }
        }

        private void CheckWinCondition()
        {
            int totalSorted = _leftBasketItems.Count + _rightBasketItems.Count;
            if (totalSorted == totalClothes)
            {
                bool allWhiteInRight = true;
                bool allColoredInLeft = true;

                foreach (var item in _rightBasketItems)
                {
                    if (!item.isWhite)
                    {
                        allWhiteInRight = false;
                        break;
                    }
                }

                foreach (var item in _leftBasketItems)
                {
                    if (item.isWhite)
                    {
                        allColoredInLeft = false;
                        break;
                    }
                }

                if (allWhiteInRight && allColoredInLeft)
                {
                    if (_resultText != null)
                    {
                        _resultText.text = "You won! All clothes sorted correctly!";
                        _resultText.color = Color.green;
                    }
                    Debug.Log("[LaundrySortGame] Player won! All clothes sorted correctly.");
                    OnGameComplete();
                    StartCoroutine(CloseAfterDelay(2f));
                }
                else
                {
                    if (_resultText != null)
                    {
                        _resultText.text = "You lost! Some clothes were sorted incorrectly. Try again!";
                        _resultText.color = Color.red;
                    }
                    Debug.Log("[LaundrySortGame] Player lost. Some clothes were sorted incorrectly.");
                    StartCoroutine(ReplayGame());
                }
            }
        }

        private System.Collections.IEnumerator ReplayGame()
        {
            yield return new WaitForSeconds(2f);

            _activeClothes.Clear();
            _leftBasketItems.Clear();
            _rightBasketItems.Clear();

            foreach (var clothing in FindObjectsOfType<DraggableClothing>())
            {
                if (clothing != null)
                    Destroy(clothing.gameObject);
            }

            InitializeInventory();

            if (_resultText != null)
            {
                _resultText.text = "Click the middle basket to get clothes!";
                _resultText.color = Color.white;
            }
        }

        private System.Collections.IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseGame();
        }

        protected override void CleanupGameUI()
        {
            StopAllCoroutines();
            _activeClothes.Clear();
            _clothingInventory.Clear();
            _leftBasketItems.Clear();
            _rightBasketItems.Clear();
            _mainBasketImage = null;
            _leftBasketImage = null;
            _rightBasketImage = null;
            _mainBasketButton = null;
            _mainBasketRect = null;
            _leftBasketRect = null;
            _rightBasketRect = null;
        }
    }

    public class DraggableClothing : MonoBehaviour, IBeginDragHandler, UnityEngine.EventSystems.IDragHandler, IEndDragHandler
    {
        private Sprite _clothingSprite;
        private bool _isWhite;
        private LaundrySortGame _game;
        private LaundrySortGame.ClothingItem _clothingItem;
        private RectTransform _rectTransform;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;

        public bool IsWhite => _isWhite;
        public Sprite ClothingSprite => _clothingSprite;
        public LaundrySortGame.ClothingItem GetClothingItem() => _clothingItem;

        public void Initialize(Sprite sprite, bool isWhite, LaundrySortGame game, LaundrySortGame.ClothingItem item)
        {
            _clothingSprite = sprite;
            _isWhite = isWhite;
            _game = game;
            _clothingItem = item;
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0.6f;
                _canvasGroup.blocksRaycasts = false;
            }
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_rectTransform != null && _canvas != null)
            {
                RectTransform parentRect = _rectTransform.parent as RectTransform;
                Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    cam,
                    out Vector2 localPoint);

                Vector2 parentSize = parentRect.rect.size;
                Vector2 anchorCenter = (_rectTransform.anchorMin + _rectTransform.anchorMax) / 2f;
                Vector2 anchorLocalPos = new Vector2(
                    (anchorCenter.x - 0.5f) * parentSize.x,
                    (anchorCenter.y - 0.5f) * parentSize.y
                );

                _rectTransform.anchoredPosition = localPoint - anchorLocalPos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_game != null)
            {
                _game.OnClothingDropped(this, eventData.position);
            }
        }
    }
}
