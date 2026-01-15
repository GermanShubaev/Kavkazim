using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigames.ClickGames
{
    public class TapachkiGame : ClickGame
    {
        [Header("Tapachki Settings")]
        [SerializeField] private Vector2 imageSize = new Vector2(900, 900);
        [SerializeField] private float imageSpacing = 50f;
        [SerializeField] private float outlineWidth = 10f;

        private Sprite _bootsSprite;
        private Sprite _slippersSprite;
        
        private GameObject _bootsObject;
        private GameObject _slippersObject;
        private Image _bootsImage;
        private Image _slippersImage;
        private Outline _bootsOutline;
        private Outline _slippersOutline;
        
        private bool _bootsClicked = false;
        private bool _slippersClicked = false;
        private bool _gameComplete = false;

        private void Awake()
        {
            useScreenPercentage = true;
            screenPercentage = 0.75f;
            
            LoadImages();
        }

        private void LoadImages()
        {
            #if UNITY_EDITOR
            string bootsPath = "Assets/Art/Images/tapachki/boots.png";
            _bootsSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bootsPath);
            if (_bootsSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(bootsPath);
                if (tex != null)
                {
                    _bootsSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            string slippersPath = "Assets/Art/Images/tapachki/slippers.png";
            _slippersSprite = AssetDatabase.LoadAssetAtPath<Sprite>(slippersPath);
            if (_slippersSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(slippersPath);
                if (tex != null)
                {
                    _slippersSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            if (_bootsSprite != null)
                Debug.Log("[TapachkiGame] Loaded boots.png (Editor mode)");
            if (_slippersSprite != null)
                Debug.Log("[TapachkiGame] Loaded slippers.png (Editor mode)");
            #endif

            if (_bootsSprite == null)
            {
                _bootsSprite = Resources.Load<Sprite>("Art/Images/tapachki/boots");
                if (_bootsSprite == null)
                    _bootsSprite = Resources.Load<Sprite>("tapachki/boots");
            }

            if (_slippersSprite == null)
            {
                _slippersSprite = Resources.Load<Sprite>("Art/Images/tapachki/slippers");
                if (_slippersSprite == null)
                    _slippersSprite = Resources.Load<Sprite>("tapachki/slippers");
            }

            if (_bootsSprite == null)
            {
                Debug.LogError("[TapachkiGame] Failed to load boots.png. Make sure the image is either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/tapachki/");
                Debug.LogError("  2. Or in Assets/Art/Images/tapachki/ (editor only)");
            }

            if (_slippersSprite == null)
            {
                Debug.LogError("[TapachkiGame] Failed to load slippers.png. Make sure the image is either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/tapachki/");
                Debug.LogError("  2. Or in Assets/Art/Images/tapachki/ (editor only)");
            }
        }

        protected override void InitializeGameUI()
        {
            if (useScreenPercentage)
            {
                ResizeContentPanelToScreenPercentage();
            }

            _bootsClicked = false;
            _slippersClicked = false;
            _gameComplete = false;

            CreateBootsImage();
            CreateSlippersImage();
        }

        private void CreateBootsImage()
        {
            _bootsObject = new GameObject("BootsImage");
            _bootsObject.transform.SetParent(_contentPanel.transform, false);

            _bootsImage = _bootsObject.AddComponent<Image>();
            _bootsImage.sprite = _bootsSprite;
            _bootsImage.preserveAspect = true;
            _bootsImage.raycastTarget = true;

            _bootsOutline = _bootsObject.AddComponent<Outline>();
            _bootsOutline.effectColor = Color.green;
            _bootsOutline.effectDistance = new Vector2(outlineWidth, outlineWidth);
            _bootsOutline.useGraphicAlpha = false;

            RectTransform rect = _bootsObject.GetComponent<RectTransform>();
            rect.sizeDelta = imageSize;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-(imageSize.x + imageSpacing) / 2f, 0);

            EventTrigger trigger = _bootsObject.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerClick;
            entry.callback.AddListener((data) => { OnBootsClicked(); });
            trigger.triggers.Add(entry);
        }

        private void CreateSlippersImage()
        {
            _slippersObject = new GameObject("SlippersImage");
            _slippersObject.transform.SetParent(_contentPanel.transform, false);

            _slippersImage = _slippersObject.AddComponent<Image>();
            _slippersImage.sprite = _slippersSprite;
            _slippersImage.preserveAspect = true;
            _slippersImage.raycastTarget = true;

            _slippersOutline = _slippersObject.AddComponent<Outline>();
            _slippersOutline.effectColor = Color.red;
            _slippersOutline.effectDistance = new Vector2(outlineWidth, outlineWidth);
            _slippersOutline.useGraphicAlpha = false;

            RectTransform rect = _slippersObject.GetComponent<RectTransform>();
            rect.sizeDelta = imageSize;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((imageSize.x + imageSpacing) / 2f, 0);

            EventTrigger trigger = _slippersObject.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerClick;
            entry.callback.AddListener((data) => { OnSlippersClicked(); });
            trigger.triggers.Add(entry);
        }

        private void OnBootsClicked()
        {
            if (_gameComplete || _bootsClicked) return;

            if (_bootsOutline != null)
            {
                _bootsOutline.effectColor = Color.red;
            }

            _bootsClicked = true;
            Debug.Log("[TapachkiGame] Boots clicked! Outline changed to red.");
        }

        private void OnSlippersClicked()
        {
            if (_gameComplete) return;

            if (!_bootsClicked)
            {
                Debug.Log("[TapachkiGame] Must click boots first!");
                return;
            }

            if (_slippersClicked) return;

            if (_slippersOutline != null)
            {
                _slippersOutline.effectColor = Color.green;
            }

            _slippersClicked = true;
            _gameComplete = true;
            Debug.Log("[TapachkiGame] Slippers clicked! Outline changed to green. Game complete!");

            OnGameComplete();
        }

        public override void OnGameComplete()
        {
            Debug.Log("[TapachkiGame] Congratulations! Game complete.");
            base.OnGameComplete();
        }

        protected override void CleanupGameUI()
        {
            base.CleanupGameUI();
            StopAllCoroutines();
            
            _bootsObject = null;
            _slippersObject = null;
            _bootsImage = null;
            _slippersImage = null;
            _bootsOutline = null;
            _slippersOutline = null;
        }

        protected override void CreateMainImage()
        {
        }

        protected override void CreateStains()
        {
        }
    }
}
