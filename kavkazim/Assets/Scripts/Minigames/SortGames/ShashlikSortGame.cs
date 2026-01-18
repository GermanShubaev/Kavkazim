using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigames.SortGames
{
    public class ShashlikSortGame : SortGame
    {
        [Header("Popup Settings")]
        [SerializeField] private int canvasSortingOrder = 200;
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private bool showCloseButton = true;

        [Header("Game Settings")]
        [SerializeField] private int numberOfSlots = 5;
        [SerializeField] private Vector2 ingredientSize = new Vector2(100, 100);
        [SerializeField] private float ingredientSpacing = 120f;
        [SerializeField] private int totalRounds = 3;

        private Text _resultText;

        private Sprite _skewerSprite;
        private Dictionary<string, Sprite> _ingredientSprites = new Dictionary<string, Sprite>();
        private string[] _ingredientNames = { "meat", "tomato", "onion", "tofu", "fried_chicken" };

        private List<string> _targetSequence = new List<string>();
        private List<string> _playerSequence = new List<string>();
        private List<Image> _playerSkewerSlots = new List<Image>();
        private List<Button> _ingredientButtons = new List<Button>();
        
        private bool _isMemorizationPhase = true;
        private GameObject _memorizationPanel;
        private GameObject _gameplayPanel;
        
        private int _currentRound = 1;
        private Text _roundText;
        private Text _memorizationRoundText;

        private void Awake()
        {
            LoadImages();
        }

        private void LoadImages()
        {
            #if UNITY_EDITOR
            string skewerPath = "Assets/Art/Images/shashlik/shashlik_skewer.png";
            _skewerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(skewerPath);
            if (_skewerSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(skewerPath);
                if (tex != null)
                    _skewerSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            foreach (string ingredient in _ingredientNames)
            {
                string path = $"Assets/Art/Images/shashlik/shashlik_{ingredient}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null)
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                if (sprite != null)
                {
                    _ingredientSprites[ingredient] = sprite;
                    Debug.Log($"[ShashlikSortGame] Loaded shashlik_{ingredient}.png");
                }
            }
            #endif

            if (_skewerSprite == null)
            {
                _skewerSprite = Resources.Load<Sprite>("Art/Images/shashlik/shashlik_skewer");
                if (_skewerSprite == null)
                    _skewerSprite = Resources.Load<Sprite>("shashlik/shashlik_skewer");
            }

            foreach (string ingredient in _ingredientNames)
            {
                if (!_ingredientSprites.ContainsKey(ingredient))
                {
                    Sprite sprite = Resources.Load<Sprite>($"Art/Images/shashlik/shashlik_{ingredient}");
                    if (sprite == null)
                        sprite = Resources.Load<Sprite>($"shashlik/shashlik_{ingredient}");
                    if (sprite != null)
                        _ingredientSprites[ingredient] = sprite;
                }
            }

            if (_skewerSprite == null)
                Debug.LogError("[ShashlikSortGame] Failed to load shashlik_skewer.png");
            
            Debug.Log($"[ShashlikSortGame] Loaded {_ingredientSprites.Count} ingredient sprites");
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
        
        public override void CloseGame()
        {
            if (!IsActive) return;

            base.CloseGame();
        }

        protected override void CleanupGameUI()
        {
            StopAllCoroutines();
            _targetSequence.Clear();
            _playerSequence.Clear();
            _playerSkewerSlots.Clear();
            _ingredientButtons.Clear();

            _resultText = null;
            _memorizationPanel = null;
            _gameplayPanel = null;
            _isMemorizationPhase = true;
            _currentRound = 1;
            _roundText = null;
            _memorizationRoundText = null;
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

            GenerateTargetSequence();

            _isMemorizationPhase = true;
            CreateMemorizationPhase();
            CreateGameplayPhase();

            _memorizationPanel.SetActive(true);
            _gameplayPanel.SetActive(false);
        }

        private void CreateMemorizationPhase()
        {
            _memorizationPanel = new GameObject("MemorizationPanel");
            _memorizationPanel.transform.SetParent(_contentPanel.transform, false);
            RectTransform panelRect = _memorizationPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            GameObject roundObj = new GameObject("RoundText");
            roundObj.transform.SetParent(_memorizationPanel.transform, false);
            _memorizationRoundText = roundObj.AddComponent<Text>();
            _memorizationRoundText.text = $"Round {_currentRound} of {totalRounds}";
            _memorizationRoundText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _memorizationRoundText.fontSize = 24;
            _memorizationRoundText.alignment = TextAnchor.MiddleCenter;
            _memorizationRoundText.color = Color.cyan;
            RectTransform roundRect = roundObj.GetComponent<RectTransform>();
            roundRect.anchorMin = new Vector2(0, 0.92f);
            roundRect.anchorMax = new Vector2(1, 1);
            roundRect.sizeDelta = Vector2.zero;

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_memorizationPanel.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Memorize the Shashlik!";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 36;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.yellow;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.82f);
            titleRect.anchorMax = new Vector2(1, 0.92f);
            titleRect.sizeDelta = Vector2.zero;

            CreateTargetSkewerInPanel(_memorizationPanel, 0.35f, 0.75f);

            CreateGoButton();
        }

        private void CreateGameplayPhase()
        {
            _gameplayPanel = new GameObject("GameplayPanel");
            _gameplayPanel.transform.SetParent(_contentPanel.transform, false);
            RectTransform panelRect = _gameplayPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            GameObject roundObj = new GameObject("RoundText");
            roundObj.transform.SetParent(_gameplayPanel.transform, false);
            _roundText = roundObj.AddComponent<Text>();
            _roundText.text = $"Round {_currentRound} of {totalRounds}";
            _roundText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _roundText.fontSize = 24;
            _roundText.alignment = TextAnchor.MiddleCenter;
            _roundText.color = Color.cyan;
            RectTransform roundRect = roundObj.GetComponent<RectTransform>();
            roundRect.anchorMin = new Vector2(0, 0.92f);
            roundRect.anchorMax = new Vector2(1, 1);
            roundRect.sizeDelta = Vector2.zero;

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_gameplayPanel.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Recreate the Shashlik!";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.82f);
            titleRect.anchorMax = new Vector2(1, 0.92f);
            titleRect.sizeDelta = Vector2.zero;

            CreateResultText();

            CreatePlayerSkewer();

            CreateIngredientButtons();

            CreateGoBackButton();
        }

        private void CreateGoButton()
        {
            GameObject goBtnObj = new GameObject("GoButton");
            goBtnObj.transform.SetParent(_memorizationPanel.transform, false);

            Image btnImage = goBtnObj.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.7f, 0.3f, 1f);

            Button goButton = goBtnObj.AddComponent<Button>();
            goButton.targetGraphic = btnImage;

            ColorBlock colors = goButton.colors;
            colors.normalColor = new Color(0.2f, 0.7f, 0.3f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.8f, 0.4f, 1f);
            colors.pressedColor = new Color(0.15f, 0.5f, 0.2f, 1f);
            goButton.colors = colors;

            RectTransform btnRect = goBtnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.35f, 0.1f);
            btnRect.anchorMax = new Vector2(0.65f, 0.25f);
            btnRect.sizeDelta = Vector2.zero;
            btnRect.anchoredPosition = Vector2.zero;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(goBtnObj.transform, false);
            Text goText = textObj.AddComponent<Text>();
            goText.text = "GO!";
            goText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goText.fontSize = 48;
            goText.fontStyle = FontStyle.Bold;
            goText.alignment = TextAnchor.MiddleCenter;
            goText.color = Color.white;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            goButton.onClick.AddListener(OnGoButtonClicked);
        }

        private void OnGoButtonClicked()
        {
            _isMemorizationPhase = false;
            _memorizationPanel.SetActive(false);
            _gameplayPanel.SetActive(true);
        }

        private void CreateGoBackButton()
        {
            GameObject goBackObj = new GameObject("GoBackButton");
            goBackObj.transform.SetParent(_gameplayPanel.transform, false);

            Image btnImage = goBackObj.AddComponent<Image>();
            btnImage.color = new Color(0.3f, 0.5f, 0.8f, 1f);

            Button goBackButton = goBackObj.AddComponent<Button>();
            goBackButton.targetGraphic = btnImage;

            ColorBlock colors = goBackButton.colors;
            colors.normalColor = new Color(0.3f, 0.5f, 0.8f, 1f);
            colors.highlightedColor = new Color(0.4f, 0.6f, 0.9f, 1f);
            colors.pressedColor = new Color(0.2f, 0.4f, 0.6f, 1f);
            goBackButton.colors = colors;

            RectTransform btnRect = goBackObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.02f, 0.92f);
            btnRect.anchorMax = new Vector2(0.15f, 0.98f);
            btnRect.sizeDelta = Vector2.zero;
            btnRect.anchoredPosition = Vector2.zero;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(goBackObj.transform, false);
            Text goBackText = textObj.AddComponent<Text>();
            goBackText.text = "← Back";
            goBackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goBackText.fontSize = 18;
            goBackText.fontStyle = FontStyle.Bold;
            goBackText.alignment = TextAnchor.MiddleCenter;
            goBackText.color = Color.white;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            goBackButton.onClick.AddListener(OnGoBackClicked);
        }

        private void OnGoBackClicked()
        {
            _isMemorizationPhase = true;
            _gameplayPanel.SetActive(false);
            _memorizationPanel.SetActive(true);
        }

        private void CreateTargetSkewerInPanel(GameObject parent, float yMin, float yMax)
        {
            GameObject targetSkewerContainer = new GameObject("TargetSkewerContainer");
            targetSkewerContainer.transform.SetParent(parent.transform, false);
            RectTransform containerRect = targetSkewerContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, yMin);
            containerRect.anchorMax = new Vector2(1, yMax);
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;

            GameObject skewerObj = new GameObject("TargetSkewer");
            skewerObj.transform.SetParent(targetSkewerContainer.transform, false);
            Image skewerImage = skewerObj.AddComponent<Image>();
            skewerImage.sprite = _skewerSprite;
            skewerImage.preserveAspect = true;
            RectTransform skewerRect = skewerObj.GetComponent<RectTransform>();
            skewerRect.anchorMin = new Vector2(0.1f, 0.1f);
            skewerRect.anchorMax = new Vector2(0.9f, 0.9f);
            skewerRect.sizeDelta = Vector2.zero;

            float startX = -ingredientSpacing * 2;
            for (int i = 0; i < numberOfSlots; i++)
            {
                string ingredient = _targetSequence[i];
                if (!_ingredientSprites.ContainsKey(ingredient)) continue;

                GameObject ingredientObj = new GameObject($"TargetIngredient_{i}");
                ingredientObj.transform.SetParent(skewerObj.transform, false);
                Image ingredientImage = ingredientObj.AddComponent<Image>();
                ingredientImage.sprite = _ingredientSprites[ingredient];
                ingredientImage.preserveAspect = true;

                RectTransform ingredientRect = ingredientObj.GetComponent<RectTransform>();
                ingredientRect.anchorMin = new Vector2(0.5f, 0.5f);
                ingredientRect.anchorMax = new Vector2(0.5f, 0.5f);
                ingredientRect.sizeDelta = ingredientSize;
                ingredientRect.anchoredPosition = new Vector2(startX + i * ingredientSpacing, 0);
            }
        }

        private void CreateResultText()
        {
            GameObject resultObj = new GameObject("ResultText");
            resultObj.transform.SetParent(_gameplayPanel.transform, false);

            _resultText = resultObj.AddComponent<Text>();
            _resultText.text = "";
            _resultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _resultText.fontSize = 28;
            _resultText.fontStyle = FontStyle.Bold;
            _resultText.alignment = TextAnchor.MiddleCenter;
            _resultText.color = Color.green;

            RectTransform resultRect = resultObj.GetComponent<RectTransform>();
            resultRect.anchorMin = new Vector2(0, 0.85f);
            resultRect.anchorMax = new Vector2(1, 0.92f);
            resultRect.sizeDelta = Vector2.zero;
            resultRect.anchoredPosition = Vector2.zero;
        }

        private void GenerateTargetSequence()
        {
            _targetSequence.Clear();
            List<string> availableIngredients = new List<string>(_ingredientNames);

            for (int i = 0; i < numberOfSlots; i++)
            {
                int randomIndex = Random.Range(0, availableIngredients.Count);
                _targetSequence.Add(availableIngredients[randomIndex]);
            }

            Debug.Log($"[ShashlikSortGame] Target sequence: {string.Join(", ", _targetSequence)}");
        }

        private void CreatePlayerSkewer()
        {
            GameObject playerSkewerContainer = new GameObject("PlayerSkewerContainer");
            playerSkewerContainer.transform.SetParent(_gameplayPanel.transform, false);
            RectTransform containerRect = playerSkewerContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0.45f);
            containerRect.anchorMax = new Vector2(1, 0.85f);
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(playerSkewerContainer.transform, false);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = "Your Skewer:";
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 24;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.85f);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.sizeDelta = Vector2.zero;

            GameObject skewerObj = new GameObject("PlayerSkewer");
            skewerObj.transform.SetParent(playerSkewerContainer.transform, false);
            Image skewerImage = skewerObj.AddComponent<Image>();
            skewerImage.sprite = _skewerSprite;
            skewerImage.preserveAspect = true;
            RectTransform skewerRect = skewerObj.GetComponent<RectTransform>();
            skewerRect.anchorMin = new Vector2(0.1f, 0.1f);
            skewerRect.anchorMax = new Vector2(0.9f, 0.8f);
            skewerRect.sizeDelta = Vector2.zero;

            _playerSkewerSlots.Clear();
            float startX = -ingredientSpacing * 2;
            for (int i = 0; i < numberOfSlots; i++)
            {
                GameObject slotObj = new GameObject($"PlayerSlot_{i}");
                slotObj.transform.SetParent(skewerObj.transform, false);
                Image slotImage = slotObj.AddComponent<Image>();
                slotImage.color = new Color(1, 1, 1, 0);
                slotImage.preserveAspect = true;

                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0.5f, 0.5f);
                slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotRect.sizeDelta = ingredientSize;
                slotRect.anchoredPosition = new Vector2(startX + i * ingredientSpacing, 0);

                _playerSkewerSlots.Add(slotImage);
            }
        }

        private void CreateIngredientButtons()
        {
            GameObject buttonContainer = new GameObject("ButtonContainer");
            buttonContainer.transform.SetParent(_gameplayPanel.transform, false);
            RectTransform containerRect = buttonContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0.05f);
            containerRect.anchorMax = new Vector2(1, 0.40f);
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(buttonContainer.transform, false);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = "Click ingredients in order:";
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 22;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.85f);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.sizeDelta = Vector2.zero;

            List<string> buttonIngredients = new List<string>(_ingredientNames);
            
            for (int i = buttonIngredients.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                string temp = buttonIngredients[i];
                buttonIngredients[i] = buttonIngredients[j];
                buttonIngredients[j] = temp;
            }

            _ingredientButtons.Clear();
            float buttonSize = 100f;
            float buttonSpacing = 20f;
            float totalWidth = buttonIngredients.Count * buttonSize + (buttonIngredients.Count - 1) * buttonSpacing;
            float startX = -totalWidth / 2f + buttonSize / 2f;

            for (int i = 0; i < buttonIngredients.Count; i++)
            {
                string ingredient = buttonIngredients[i];
                if (!_ingredientSprites.ContainsKey(ingredient)) continue;

                GameObject buttonObj = new GameObject($"Button_{ingredient}");
                buttonObj.transform.SetParent(buttonContainer.transform, false);

                Image buttonBg = buttonObj.AddComponent<Image>();
                buttonBg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

                Button button = buttonObj.AddComponent<Button>();
                button.targetGraphic = buttonBg;

                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
                colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
                colors.pressedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                button.colors = colors;

                RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0.5f, 0.45f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.45f);
                buttonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
                buttonRect.anchoredPosition = new Vector2(startX + i * (buttonSize + buttonSpacing), 0);

                GameObject imgObj = new GameObject("Image");
                imgObj.transform.SetParent(buttonObj.transform, false);
                Image ingredientImg = imgObj.AddComponent<Image>();
                ingredientImg.sprite = _ingredientSprites[ingredient];
                ingredientImg.preserveAspect = true;
                ingredientImg.raycastTarget = false;
                RectTransform imgRect = imgObj.GetComponent<RectTransform>();
                imgRect.anchorMin = new Vector2(0.1f, 0.1f);
                imgRect.anchorMax = new Vector2(0.9f, 0.9f);
                imgRect.sizeDelta = Vector2.zero;

                string capturedIngredient = ingredient;
                button.onClick.AddListener(() => OnIngredientButtonClicked(capturedIngredient));

                _ingredientButtons.Add(button);
            }

            CreateUndoButton(buttonContainer);
        }

        private void CreateUndoButton(GameObject parent)
        {
            GameObject undoObj = new GameObject("UndoButton");
            undoObj.transform.SetParent(parent.transform, false);

            Image undoBg = undoObj.AddComponent<Image>();
            undoBg.color = new Color(0.8f, 0.4f, 0.2f, 1f);

            Button undoButton = undoObj.AddComponent<Button>();
            undoButton.targetGraphic = undoBg;

            ColorBlock colors = undoButton.colors;
            colors.normalColor = new Color(0.8f, 0.4f, 0.2f, 1f);
            colors.highlightedColor = new Color(0.9f, 0.5f, 0.3f, 1f);
            colors.pressedColor = new Color(0.6f, 0.3f, 0.15f, 1f);
            undoButton.colors = colors;

            RectTransform undoRect = undoObj.GetComponent<RectTransform>();
            undoRect.anchorMin = new Vector2(0.5f, 0.1f);
            undoRect.anchorMax = new Vector2(0.5f, 0.1f);
            undoRect.sizeDelta = new Vector2(100, 40);
            undoRect.anchoredPosition = Vector2.zero;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(undoObj.transform, false);
            Text undoText = textObj.AddComponent<Text>();
            undoText.text = "Undo";
            undoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            undoText.fontSize = 20;
            undoText.alignment = TextAnchor.MiddleCenter;
            undoText.color = Color.white;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            undoButton.onClick.AddListener(OnUndoClicked);
        }

        private void OnIngredientButtonClicked(string ingredient)
        {
            if (_playerSequence.Count >= numberOfSlots) return;

            _playerSequence.Add(ingredient);

            int slotIndex = _playerSequence.Count - 1;
            if (slotIndex < _playerSkewerSlots.Count && _ingredientSprites.ContainsKey(ingredient))
            {
                _playerSkewerSlots[slotIndex].sprite = _ingredientSprites[ingredient];
                _playerSkewerSlots[slotIndex].color = Color.white;
            }

            Debug.Log($"[ShashlikSortGame] Added {ingredient}, sequence: {string.Join(", ", _playerSequence)}");

            if (_playerSequence.Count == numberOfSlots)
            {
                CheckWinCondition();
            }
        }

        private void OnUndoClicked()
        {
            if (_playerSequence.Count == 0) return;

            int lastIndex = _playerSequence.Count - 1;
            _playerSequence.RemoveAt(lastIndex);

            if (lastIndex < _playerSkewerSlots.Count)
            {
                _playerSkewerSlots[lastIndex].sprite = null;
                _playerSkewerSlots[lastIndex].color = new Color(1, 1, 1, 0);
            }

            if (_resultText != null)
                _resultText.text = "";

            Debug.Log($"[ShashlikSortGame] Undo, sequence: {string.Join(", ", _playerSequence)}");
        }

        private void CheckWinCondition()
        {
            bool correct = true;
            for (int i = 0; i < numberOfSlots; i++)
            {
                if (_playerSequence[i] != _targetSequence[i])
                {
                    correct = false;
                    break;
                }
            }

            if (correct)
            {
                if (_currentRound >= totalRounds)
                {
                    if (_resultText != null)
                    {
                        _resultText.text = "All rounds complete! Master Chef!";
                        _resultText.color = Color.green;
                    }
                    OnGameComplete();
                    StartCoroutine(CloseAfterDelay(2f));
                }
                else
                {
                    if (_resultText != null)
                    {
                        _resultText.text = $"Round {_currentRound} complete! Get ready...";
                        _resultText.color = Color.green;
                    }
                    StartCoroutine(StartNextRound());
                }
            }
            else
            {
                if (_resultText != null)
                {
                    _resultText.text = "Not quite right... Try again!";
                    _resultText.color = Color.red;
                }
            }
        }

        private System.Collections.IEnumerator StartNextRound()
        {
            yield return new WaitForSeconds(1.5f);

            _currentRound++;
            
            _playerSequence.Clear();
            foreach (var slot in _playerSkewerSlots)
            {
                if (slot != null)
                {
                    slot.sprite = null;
                    slot.color = new Color(1, 1, 1, 0);
                }
            }

            if (_resultText != null)
                _resultText.text = "";

            UpdateRoundIndicators();
            GenerateTargetSequence();
            RebuildMemorizationSkewer();

            _isMemorizationPhase = true;
            _gameplayPanel.SetActive(false);
            _memorizationPanel.SetActive(true);
        }

        private void UpdateRoundIndicators()
        {
            if (_roundText != null)
                _roundText.text = $"Round {_currentRound} of {totalRounds}";
            if (_memorizationRoundText != null)
                _memorizationRoundText.text = $"Round {_currentRound} of {totalRounds}";
        }

        private void RebuildMemorizationSkewer()
        {
            Transform oldSkewer = _memorizationPanel.transform.Find("TargetSkewerContainer");
            if (oldSkewer != null)
                Destroy(oldSkewer.gameObject);

            CreateTargetSkewerInPanel(_memorizationPanel, 0.35f, 0.75f);
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

        private void OnDestroy()
        {
            CloseGame();
        }
    }
}

