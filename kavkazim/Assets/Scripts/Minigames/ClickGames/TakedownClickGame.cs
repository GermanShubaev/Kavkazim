using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Minigames.ClickGames
{
    public class TakedownClickGame : ClickGame
    {
        private const string NoSubPath = "Assets/Art/Images/ufc/ufc_no_sub.png";
        private const string SubPath = "Assets/Art/Images/ufc/ufc_sub.png";

        [Header("Takedown Settings")]
        [SerializeField] private int requiredClicks = 30;
        [SerializeField] private float timeWindow = 5f;
        [SerializeField] private float imageFlashDuration = 0.1f;
        [SerializeField] private Vector2 buttonSize = new Vector2(120, 120);

        private Sprite _noSubSprite;
        private Sprite _subSprite;
        private Image _mainImage;
        private Button _clickButton;
        private Text _clickCountText;
        private Text _instructionText;
        private List<float> _clickTimestamps = new List<float>();
        private bool _isFlashing = false;
        private bool _gameWon = false;

        private void Awake()
        {
            LoadImages();
        }

        private void LoadImages()
        {
            #if UNITY_EDITOR
            _noSubSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NoSubPath);
            if (_noSubSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(NoSubPath);
                if (tex != null)
                {
                    _noSubSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            _subSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SubPath);
            if (_subSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SubPath);
                if (tex != null)
                {
                    _subSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            if (_noSubSprite != null)
                Debug.Log("[TakedownClickGame] Loaded ufc_no_sub.png (Editor mode)");
            if (_subSprite != null)
                Debug.Log("[TakedownClickGame] Loaded ufc_sub.png (Editor mode)");
            #endif

            if (_noSubSprite == null)
            {
                _noSubSprite = Resources.Load<Sprite>("Art/Images/ufc/ufc_no_sub");
                if (_noSubSprite == null)
                    _noSubSprite = Resources.Load<Sprite>("ufc/ufc_no_sub");
            }

            if (_subSprite == null)
            {
                _subSprite = Resources.Load<Sprite>("Art/Images/ufc/ufc_sub");
                if (_subSprite == null)
                    _subSprite = Resources.Load<Sprite>("ufc/ufc_sub");
            }

            if (_noSubSprite == null)
                Debug.LogError("[TakedownClickGame] Failed to load ufc_no_sub.png");
            if (_subSprite == null)
                Debug.LogError("[TakedownClickGame] Failed to load ufc_sub.png");
        }

        protected override void CreatePopupWindow()
        {
            base.CreatePopupWindow();
            
            // Resize content panel relative to reference resolution (2560x1440)
            // CanvasScaler will handle scaling to different screen sizes
            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            const float referenceWidth = 2560f;
            const float referenceHeight = 1440f;
            contentRect.sizeDelta = new Vector2(referenceWidth * 0.75f, referenceHeight * 0.75f);
        }

        protected override void InitializeGameUI()
        {
            _clickTimestamps.Clear();
            _gameWon = false;

            CreateInstructionText();
            CreateMainImage();
            CreateClickButton();
            CreateClickCounter();
        }

        private void CreateInstructionText()
        {
            GameObject instructionObj = new GameObject("Instructions");
            instructionObj.transform.SetParent(_contentPanel.transform, false);

            _instructionText = instructionObj.AddComponent<Text>();
            _instructionText.text = $"Mash the button! {requiredClicks} clicks in {timeWindow} seconds!";
            _instructionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _instructionText.fontSize = 28;
            _instructionText.alignment = TextAnchor.MiddleCenter;
            _instructionText.color = Color.white;

            RectTransform instructionRect = instructionObj.GetComponent<RectTransform>();
            instructionRect.anchorMin = new Vector2(0, 1);
            instructionRect.anchorMax = new Vector2(1, 1);
            instructionRect.pivot = new Vector2(0.5f, 1);
            instructionRect.anchoredPosition = new Vector2(0, -20);
            instructionRect.sizeDelta = new Vector2(0, 50);
        }

        private void CreateMainImage()
        {
            GameObject imageObj = new GameObject("MainImage");
            imageObj.transform.SetParent(_contentPanel.transform, false);

            _mainImage = imageObj.AddComponent<Image>();
            _mainImage.sprite = _noSubSprite;
            _mainImage.preserveAspect = true;
            _mainImage.raycastTarget = false;

            RectTransform imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.1f, 0.15f);
            imageRect.anchorMax = new Vector2(0.9f, 0.85f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
        }

        private void CreateClickButton()
        {
            GameObject buttonObj = new GameObject("ClickButton");
            buttonObj.transform.SetParent(_contentPanel.transform, false);

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.9f, 0.2f, 0.2f, 1f);

            _clickButton = buttonObj.AddComponent<Button>();
            _clickButton.targetGraphic = buttonImage;
            _clickButton.onClick.AddListener(OnButtonClicked);

            ColorBlock colors = _clickButton.colors;
            colors.normalColor = new Color(0.9f, 0.2f, 0.2f, 1f);
            colors.highlightedColor = new Color(1f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.7f, 0.1f, 0.1f, 1f);
            _clickButton.colors = colors;

            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0);
            buttonRect.anchorMax = new Vector2(0.5f, 0);
            buttonRect.pivot = new Vector2(0.5f, 0);
            buttonRect.anchoredPosition = new Vector2(0, 30);
            buttonRect.sizeDelta = buttonSize;

            GameObject textObj = new GameObject("ButtonText");
            textObj.transform.SetParent(buttonObj.transform, false);

            Text buttonText = textObj.AddComponent<Text>();
            buttonText.text = "TAP!";
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 24;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void CreateClickCounter()
        {
            GameObject counterObj = new GameObject("ClickCounter");
            counterObj.transform.SetParent(_contentPanel.transform, false);

            _clickCountText = counterObj.AddComponent<Text>();
            _clickCountText.text = "Clicks: 0 / " + requiredClicks;
            _clickCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _clickCountText.fontSize = 36;
            _clickCountText.fontStyle = FontStyle.Bold;
            _clickCountText.alignment = TextAnchor.MiddleCenter;
            _clickCountText.color = Color.yellow;

            RectTransform counterRect = counterObj.GetComponent<RectTransform>();
            counterRect.anchorMin = new Vector2(0, 0);
            counterRect.anchorMax = new Vector2(1, 0);
            counterRect.pivot = new Vector2(0.5f, 0);
            counterRect.anchoredPosition = new Vector2(0, buttonSize.y + 50);
            counterRect.sizeDelta = new Vector2(0, 50);
        }

        private void OnButtonClicked()
        {
            if (_gameWon) return;

            var currentTime = Time.time;
            _clickTimestamps.Add(currentTime);

            CleanupOldClicks(currentTime);

            if (!_isFlashing)
            {
                StartCoroutine(FlashSubmissionImage());
            }

            UpdateClickCounter();

            if (_clickTimestamps.Count >= requiredClicks)
            {
                OnGameWon();
            }
        }

        private void CleanupOldClicks(float currentTime)
        {
            float cutoffTime = currentTime - timeWindow;
            _clickTimestamps.RemoveAll(t => t < cutoffTime);
        }

        private void Update()
        {
            if (_gameWon) return;

            CleanupOldClicks(Time.time);
            UpdateClickCounter();
        }

        private void UpdateClickCounter()
        {
            if (_clickCountText != null)
            {
                int currentClicks = _clickTimestamps.Count;
                _clickCountText.text = $"Clicks: {currentClicks} / {requiredClicks}";

                var progress = (float)currentClicks / requiredClicks;
                if (progress >= 1f)
                    _clickCountText.color = Color.green;
                else if (progress >= 0.7f)
                    _clickCountText.color = Color.yellow;
                else if (progress >= 0.4f)
                    _clickCountText.color = new Color(1f, 0.5f, 0f);
                else
                    _clickCountText.color = Color.white;
            }
        }

        private System.Collections.IEnumerator FlashSubmissionImage()
        {
            _isFlashing = true;
            
            if (_mainImage != null && _subSprite != null)
            {
                _mainImage.sprite = _subSprite;
                yield return new WaitForSeconds(imageFlashDuration);
                
                if (_mainImage != null && !_gameWon)
                {
                    _mainImage.sprite = _noSubSprite;
                }
            }

            _isFlashing = false;
        }

        private void OnGameWon()
        {
            _gameWon = true;
            Debug.Log("[TakedownClickGame] Player achieved takedown! Game won!");

            if (_mainImage != null && _subSprite != null)
            {
                _mainImage.sprite = _subSprite;
            }

            if (_instructionText != null)
            {
                _instructionText.text = "SUBMISSION! You got the takedown!";
                _instructionText.color = Color.green;
                _instructionText.fontSize = 36;
            }

            if (_clickCountText != null)
            {
                _clickCountText.text = "SUCCESS!";
                _clickCountText.color = Color.green;
            }

            if (_clickButton != null)
            {
                _clickButton.interactable = false;
            }

            OnGameComplete(); // Mark as completed successfully
            StartCoroutine(CloseAfterDelay(2f));
        }

        public override void OnGameComplete()
        {
            base.OnGameComplete(); // Mark as completed successfully
        }

        private System.Collections.IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseGame();
        }

        protected override void CleanupGameUI()
        {
            StopAllCoroutines();
            _clickTimestamps.Clear();
            _mainImage = null;
            _clickButton = null;
            _clickCountText = null;
            _instructionText = null;
        }
    }
}

