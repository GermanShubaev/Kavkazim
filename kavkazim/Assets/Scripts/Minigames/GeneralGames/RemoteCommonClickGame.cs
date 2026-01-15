using Minigames.Base;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Minigames.GeneralGames
{
    public class RemoteCommonClickGame : BaseMinigame
    {
        private const string RemotePath = "Assets/Art/Images/temperature/common_remote.png";

        [Header("Remote Settings")]
        [SerializeField] private int minTemperature = 16;
        [SerializeField] private int maxTemperature = 30;
        [SerializeField] private int startTemperature = 18;
        [SerializeField] private int targetTemperature = 25;
        
        [SerializeField] private int minFanSpeed = 1;
        [SerializeField] private int maxFanSpeed = 4;
        [SerializeField] private int startFanSpeed = 4;
        [SerializeField] private int targetFanSpeed = 1;

        [Header("Button Positions (normalized 0-1)")]
        [SerializeField] private Vector2 tempUpButtonPos = new Vector2(0.25f, 0.45f);
        [SerializeField] private Vector2 tempDownButtonPos = new Vector2(0.25f, 0.30f);
        [SerializeField] private Vector2 fanUpButtonPos = new Vector2(0.75f, 0.45f);
        [SerializeField] private Vector2 fanDownButtonPos = new Vector2(0.75f, 0.30f);

        [Header("Display Positions (normalized 0-1)")]
        [SerializeField] private Vector2 tempDisplayPos = new Vector2(0.5f, 0.68f);
        [SerializeField] private Vector2 fanDisplayPos = new Vector2(0.5f, 0.55f);

        private Sprite _remoteSprite;
        private Image _remoteImage;
        private Text _temperatureText;
        private GameObject _fanSpeedContainer;
        private Image[] _fanSpeedBars;
        private Text _instructionText;
        private Text _resultText;

        private int _currentTemperature;
        private int _currentFanSpeed;
        private bool _gameWon;

        private void Awake()
        {
            LoadImages();
        }

        private void LoadImages()
        {
            #if UNITY_EDITOR
            
            _remoteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RemotePath);
            if (_remoteSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RemotePath);
                if (tex != null)
                {
                    _remoteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            if (_remoteSprite != null)
                Debug.Log("[RemoteCommonClickGame] Loaded common_remote.png (Editor mode)");
            #endif

            if (_remoteSprite == null)
            {
                _remoteSprite = Resources.Load<Sprite>("Art/Images/temperature/common_remote");
                if (_remoteSprite == null)
                    _remoteSprite = Resources.Load<Sprite>("temperature/common_remote");
            }

            if (_remoteSprite == null)
                Debug.LogError("[RemoteCommonClickGame] Failed to load common_remote.png");
        }

        protected override void CreatePopupWindow()
        {
            base.CreatePopupWindow();
            
            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            const float referenceWidth = 2560f;
            const float referenceHeight = 1440f;
            contentRect.sizeDelta = new Vector2(referenceWidth * 0.75f, referenceHeight * 0.75f);
        }

        protected override void InitializeGameUI()
        {
            _currentTemperature = startTemperature;
            _currentFanSpeed = startFanSpeed;
            _gameWon = false;

            CreateInstructionText();
            CreateRemoteImage();
            CreateTemperatureDisplay();
            CreateFanSpeedDisplay();
            CreateControlButtons();
            CreateResultText();

            UpdateDisplays();
        }

        private void CreateInstructionText()
        {
            GameObject instructionObj = new GameObject("Instructions");
            instructionObj.transform.SetParent(_contentPanel.transform, false);

            _instructionText = instructionObj.AddComponent<Text>();
            _instructionText.text = $"Set temperature to {targetTemperature}°C and fan speed to {targetFanSpeed}";
            _instructionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _instructionText.fontSize = 24;
            _instructionText.alignment = TextAnchor.MiddleCenter;
            _instructionText.color = Color.white;

            RectTransform instructionRect = instructionObj.GetComponent<RectTransform>();
            instructionRect.anchorMin = new Vector2(0, 0.92f);
            instructionRect.anchorMax = new Vector2(1, 1);
            instructionRect.offsetMin = Vector2.zero;
            instructionRect.offsetMax = Vector2.zero;
        }

        private void CreateRemoteImage()
        {
            GameObject imageContainer = new GameObject("RemoteContainer");
            imageContainer.transform.SetParent(_contentPanel.transform, false);
            
            RectTransform containerRect = imageContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.15f, 0.1f);
            containerRect.anchorMax = new Vector2(0.85f, 0.9f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            GameObject imageObj = new GameObject("RemoteImage");
            imageObj.transform.SetParent(imageContainer.transform, false);

            _remoteImage = imageObj.AddComponent<Image>();
            _remoteImage.sprite = _remoteSprite;
            _remoteImage.preserveAspect = true;
            _remoteImage.raycastTarget = false;

            RectTransform imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
        }

        private void CreateTemperatureDisplay()
        {
            GameObject textObj = new GameObject("TemperatureDisplay");
            textObj.transform.SetParent(_contentPanel.transform, false);

            _temperatureText = textObj.AddComponent<Text>();
            _temperatureText.text = $"{_currentTemperature}°C";
            _temperatureText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _temperatureText.fontSize = 80;
            _temperatureText.fontStyle = FontStyle.Bold;
            _temperatureText.alignment = TextAnchor.MiddleCenter;
            _temperatureText.color = Color.black;

            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1, 1);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(tempDisplayPos.x - 0.1f, tempDisplayPos.y - 0.05f);
            textRect.anchorMax = new Vector2(tempDisplayPos.x + 0.1f, tempDisplayPos.y + 0.05f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void CreateFanSpeedDisplay()
        {
            _fanSpeedContainer = new GameObject("FanSpeedDisplay");
            _fanSpeedContainer.transform.SetParent(_contentPanel.transform, false);

            RectTransform containerRect = _fanSpeedContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(fanDisplayPos.x - 0.06f, fanDisplayPos.y - 0.03f);
            containerRect.anchorMax = new Vector2(fanDisplayPos.x + 0.06f, fanDisplayPos.y + 0.03f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            _fanSpeedBars = new Image[maxFanSpeed];
            float barWidth = 0.04f;
            float spacing = 0.24f;
            float startX = 0.02f;

            for (int i = 0; i < maxFanSpeed; i++)
            {
                GameObject barObj = new GameObject($"FanBar_{i}");
                barObj.transform.SetParent(_fanSpeedContainer.transform, false);

                Image barImage = barObj.AddComponent<Image>();
                barImage.color = new Color(0.7f, 0.7f, 0.7f); // Light gray when inactive
                barImage.raycastTarget = false;

                RectTransform barRect = barObj.GetComponent<RectTransform>();
                float xPos = startX + i * spacing;
                float height = 0.4f + (i * 0.15f);
                barRect.anchorMin = new Vector2(xPos, 0.1f);
                barRect.anchorMax = new Vector2(xPos + barWidth, 0.1f + height);
                barRect.offsetMin = Vector2.zero;
                barRect.offsetMax = Vector2.zero;

                _fanSpeedBars[i] = barImage;
            }
        }

        private void CreateControlButtons()
        {
            CreateButton("TempUp", "+", tempUpButtonPos, new Color(0.9f, 0.3f, 0.3f), OnTempUp);
            CreateButton("TempDown", "-", tempDownButtonPos, new Color(0.3f, 0.5f, 0.9f), OnTempDown);
            CreateButton("FanUp", "▲", fanUpButtonPos, new Color(0.4f, 0.8f, 0.4f), OnFanUp);
            CreateButton("FanDown", "▼", fanDownButtonPos, new Color(0.8f, 0.6f, 0.2f), OnFanDown);
        }

        private void CreateButton(string name, string label, Vector2 normalizedPos, Color color, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(_contentPanel.transform, false);

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = color;

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(color.r + 0.15f, color.g + 0.15f, color.b + 0.15f);
            colors.pressedColor = new Color(color.r - 0.15f, color.g - 0.15f, color.b - 0.15f);
            button.colors = colors;

            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(normalizedPos.x - 0.05f, normalizedPos.y - 0.03f);
            buttonRect.anchorMax = new Vector2(normalizedPos.x + 0.05f, normalizedPos.y + 0.03f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(buttonObj.transform, false);

            Text buttonText = textObj.AddComponent<Text>();
            buttonText.text = label;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 28;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void CreateResultText()
        {
            GameObject resultObj = new GameObject("ResultText");
            resultObj.transform.SetParent(_contentPanel.transform, false);

            _resultText = resultObj.AddComponent<Text>();
            _resultText.text = "";
            _resultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _resultText.fontSize = 28;
            _resultText.fontStyle = FontStyle.Bold;
            _resultText.alignment = TextAnchor.MiddleCenter;
            _resultText.color = Color.green;

            RectTransform resultRect = resultObj.GetComponent<RectTransform>();
            resultRect.anchorMin = new Vector2(0, 0.02f);
            resultRect.anchorMax = new Vector2(1, 0.08f);
            resultRect.offsetMin = Vector2.zero;
            resultRect.offsetMax = Vector2.zero;
        }

        private void OnTempUp()
        {
            if (_gameWon) return;
            if (_currentTemperature < maxTemperature)
            {
                _currentTemperature++;
                UpdateDisplays();
                CheckWinCondition();
            }
        }

        private void OnTempDown()
        {
            if (_gameWon) return;
            if (_currentTemperature > minTemperature)
            {
                _currentTemperature--;
                UpdateDisplays();
                CheckWinCondition();
            }
        }

        private void OnFanUp()
        {
            if (_gameWon) return;
            if (_currentFanSpeed < maxFanSpeed)
            {
                _currentFanSpeed++;
                UpdateDisplays();
                CheckWinCondition();
            }
        }

        private void OnFanDown()
        {
            if (_gameWon) return;
            if (_currentFanSpeed > minFanSpeed)
            {
                _currentFanSpeed--;
                UpdateDisplays();
                CheckWinCondition();
            }
        }

        private void UpdateDisplays()
        {
            if (_temperatureText != null)
            {
                _temperatureText.text = $"{_currentTemperature}°C";
                
                if (_currentTemperature == targetTemperature)
                    _temperatureText.color = Color.green;
                else
                    _temperatureText.color = Color.black;
            }

            if (_fanSpeedBars != null)
            {
                for (int i = 0; i < _fanSpeedBars.Length; i++)
                {
                    if (_fanSpeedBars[i] != null)
                    {
                        if (i < _currentFanSpeed)
                        {
                            if (_currentFanSpeed == targetFanSpeed)
                                _fanSpeedBars[i].color = Color.green;
                            else
                                _fanSpeedBars[i].color = Color.black;
                        }
                        else
                        {
                            _fanSpeedBars[i].color = new Color(0.7f, 0.7f, 0.7f);
                        }
                    }
                }
            }
        }

        private void CheckWinCondition()
        {
            if (_currentTemperature == targetTemperature && _currentFanSpeed == targetFanSpeed)
            {
                OnGameWon();
            }
        }

        private void OnGameWon()
        {
            _gameWon = true;
            Debug.Log("[RemoteCommonClickGame] Settings correct! Game won!");

            if (_instructionText != null)
            {
                _instructionText.text = "Perfect! AC is set correctly!";
                _instructionText.color = Color.green;
            }

            if (_resultText != null)
            {
                _resultText.text = "✓ Temperature and Fan Speed set!";
                _resultText.color = Color.green;
            }

            OnGameComplete();
            StartCoroutine(CloseAfterDelay(2f));
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

        protected override void CleanupGameUI()
        {
            StopAllCoroutines();
            _remoteImage = null;
            _temperatureText = null;
            _fanSpeedContainer = null;
            _fanSpeedBars = null;
            _instructionText = null;
            _resultText = null;
        }
    }
}

