using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minigames
{
    /// <summary>
    /// A minigame where players click on anonymous silhouettes to reveal names.
    /// The game ends when both "amir" and "solomon" are found.
    /// </summary>
    public class WolfClickGame : BaseMinigame
    {
        [Header("Wolf Game Settings")]
        [SerializeField] private int numberOfPeople = 9;
        [SerializeField] private Vector2 personSize = new Vector2(300, 360);
        [SerializeField] private int gridColumns = 3;
        [SerializeField] private float gridSpacing = 40f;
        private const float nameLabelHeight = 80f; // Height for name label area

        private static readonly string[] AllNames = new string[]
        {
            "amir", "solomon", "ronit", "german", "villi", 
            "tamar", "nicole", "mishel", "rafik", "simon"
        };

        private static readonly string[] TargetNames = new string[] { "amir", "solomon" };

        private Sprite _anonymousSprite;
        private List<PersonCard> _personCards = new List<PersonCard>();
        private HashSet<string> _revealedTargets = new HashSet<string>();
        private GameObject _gridContainer;

        private void Awake()
        {
            LoadImages();
        }

        private void LoadImages()
        {
            #if UNITY_EDITOR
            string anonymousPath = "Assets/Art/Images/wolf/anonymous.png";
            _anonymousSprite = AssetDatabase.LoadAssetAtPath<Sprite>(anonymousPath);
            if (_anonymousSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(anonymousPath);
                if (tex != null)
                {
                    _anonymousSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            if (_anonymousSprite != null)
                Debug.Log("[WolfClickGame] Loaded anonymous.png (Editor mode)");
            #endif

            if (_anonymousSprite == null)
            {
                _anonymousSprite = Resources.Load<Sprite>("Art/Images/wolf/anonymous");
                if (_anonymousSprite == null)
                    _anonymousSprite = Resources.Load<Sprite>("wolf/anonymous");
            }

            if (_anonymousSprite == null)
            {
                Debug.LogError("[WolfClickGame] Failed to load anonymous.png");
            }
        }

        protected override void CreatePopupWindow()
        {
            base.CreatePopupWindow();
            
            // Resize content panel to 75% of screen
            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            contentRect.sizeDelta = new Vector2(screenWidth * 0.75f, screenHeight * 0.75f);
        }

        protected override void InitializeGameUI()
        {
            _personCards.Clear();
            _revealedTargets.Clear();

            // Create title
            CreateTitle();

            // Create grid container
            CreateGridContainer();

            // Generate random names for 9 people (must include amir and solomon)
            List<string> assignedNames = GenerateRandomNames();

            // Create person cards in a 3x3 grid
            CreatePersonCards(assignedNames);
        }

        private void CreateTitle()
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_contentPanel.transform, false);
            
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Find amir and solomon!";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 32;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -20);
            titleRect.sizeDelta = new Vector2(0, 50);
        }

        private void CreateGridContainer()
        {
            _gridContainer = new GameObject("GridContainer");
            _gridContainer.transform.SetParent(_contentPanel.transform, false);

            RectTransform gridRect = _gridContainer.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0, -20); // Slightly below center to account for title

            // Calculate grid size
            int rows = Mathf.CeilToInt((float)numberOfPeople / gridColumns);
            float gridWidth = gridColumns * personSize.x + (gridColumns - 1) * gridSpacing;
            float gridHeight = rows * (personSize.y + nameLabelHeight) + (rows - 1) * gridSpacing;
            gridRect.sizeDelta = new Vector2(gridWidth, gridHeight);
        }

        private List<string> GenerateRandomNames()
        {
            List<string> availableNames = new List<string>(AllNames);
            List<string> assignedNames = new List<string>();

            // Ensure amir and solomon are included
            assignedNames.Add("amir");
            assignedNames.Add("solomon");
            availableNames.Remove("amir");
            availableNames.Remove("solomon");

            // Fill remaining slots with random names
            while (assignedNames.Count < numberOfPeople && availableNames.Count > 0)
            {
                int randomIndex = Random.Range(0, availableNames.Count);
                assignedNames.Add(availableNames[randomIndex]);
                availableNames.RemoveAt(randomIndex);
            }

            // Shuffle the list so amir and solomon aren't always first
            ShuffleList(assignedNames);

            return assignedNames;
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private void CreatePersonCards(List<string> names)
        {
            int rows = Mathf.CeilToInt((float)numberOfPeople / gridColumns);
            float totalWidth = gridColumns * personSize.x + (gridColumns - 1) * gridSpacing;
            float totalHeight = rows * (personSize.y + nameLabelHeight) + (rows - 1) * gridSpacing;
            float startX = -totalWidth / 2 + personSize.x / 2;
            float startY = totalHeight / 2 - personSize.y / 2 - nameLabelHeight / 2;

            for (int i = 0; i < names.Count && i < numberOfPeople; i++)
            {
                int col = i % gridColumns;
                int row = i / gridColumns;

                float x = startX + col * (personSize.x + gridSpacing);
                float y = startY - row * (personSize.y + nameLabelHeight + gridSpacing);

                PersonCard card = CreatePersonCard(i, names[i], new Vector2(x, y));
                _personCards.Add(card);
            }
        }

        private PersonCard CreatePersonCard(int index, string personName, Vector2 position)
        {
            // Create card container
            GameObject cardObj = new GameObject($"PersonCard_{index}");
            cardObj.transform.SetParent(_gridContainer.transform, false);

            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = position;
            cardRect.sizeDelta = new Vector2(personSize.x, personSize.y + nameLabelHeight);

            // Create anonymous image
            GameObject imageObj = new GameObject("Image");
            imageObj.transform.SetParent(cardObj.transform, false);
            
            Image personImage = imageObj.AddComponent<Image>();
            personImage.sprite = _anonymousSprite;
            personImage.preserveAspect = true;

            RectTransform imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0, 0.2f);
            imageRect.anchorMax = new Vector2(1, 1);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            // Add button component for clicking
            Button button = imageObj.AddComponent<Button>();
            button.targetGraphic = personImage;

            // Create name label (hidden initially)
            GameObject labelObj = new GameObject("NameLabel");
            labelObj.transform.SetParent(cardObj.transform, false);

            Text nameText = labelObj.AddComponent<Text>();
            nameText.text = "???";
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 40;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;

            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 0.2f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // Create background for label
            GameObject labelBgObj = new GameObject("LabelBackground");
            labelBgObj.transform.SetParent(cardObj.transform, false);
            labelBgObj.transform.SetSiblingIndex(labelObj.transform.GetSiblingIndex());
            
            Image labelBg = labelBgObj.AddComponent<Image>();
            labelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            RectTransform labelBgRect = labelBgObj.GetComponent<RectTransform>();
            labelBgRect.anchorMin = new Vector2(0, 0);
            labelBgRect.anchorMax = new Vector2(1, 0.2f);
            labelBgRect.offsetMin = Vector2.zero;
            labelBgRect.offsetMax = Vector2.zero;

            // Move label to front
            labelObj.transform.SetAsLastSibling();

            // Create PersonCard component
            PersonCard card = cardObj.AddComponent<PersonCard>();
            card.Initialize(index, personName, this, nameText, button);

            return card;
        }

        public void OnPersonClicked(PersonCard card)
        {
            if (card.IsRevealed)
                return;

            card.RevealName();

            // Check if this is a target
            string name = card.PersonName.ToLower();
            if (System.Array.Exists(TargetNames, t => t == name))
            {
                _revealedTargets.Add(name);
                card.HighlightAsTarget();
                Debug.Log($"[WolfClickGame] Found {name}! {_revealedTargets.Count}/{TargetNames.Length} targets found.");

                // Check win condition
                if (_revealedTargets.Count >= TargetNames.Length)
                {
                    OnGameComplete();
                }
            }
        }

        private void OnGameComplete()
        {
            Debug.Log("[WolfClickGame] Both amir and solomon found! Game complete.");
            
            // Update title to show success
            Transform titleTransform = _contentPanel.transform.Find("Title");
            if (titleTransform != null)
            {
                Text titleText = titleTransform.GetComponent<Text>();
                if (titleText != null)
                {
                    titleText.text = "You found them!";
                    titleText.color = Color.green;
                }
            }

            StartCoroutine(CloseAfterDelay(2f));
        }

        private System.Collections.IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseGame();
        }

        protected override void CleanupGameUI()
        {
            _personCards.Clear();
            _revealedTargets.Clear();
            _gridContainer = null;
        }
    }

    /// <summary>
    /// Component for clickable person cards in the Wolf game.
    /// </summary>
    public class PersonCard : MonoBehaviour
    {
        private int _index;
        private string _personName;
        private WolfClickGame _game;
        private Text _nameText;
        private Button _button;
        private bool _isRevealed = false;

        public string PersonName => _personName;
        public bool IsRevealed => _isRevealed;

        public void Initialize(int index, string name, WolfClickGame game, Text nameText, Button button)
        {
            _index = index;
            _personName = name;
            _game = game;
            _nameText = nameText;
            _button = button;

            _button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (!_isRevealed && _game != null)
            {
                _game.OnPersonClicked(this);
            }
        }

        public void RevealName()
        {
            _isRevealed = true;
            if (_nameText != null)
            {
                _nameText.text = _personName;
            }
        }

        public void HighlightAsTarget()
        {
            if (_nameText != null)
            {
                _nameText.color = Color.green;
                _nameText.fontStyle = FontStyle.Bold;
            }

            // Also highlight the image
            Image image = GetComponentInChildren<Image>();
            if (image != null)
            {
                image.color = new Color(0.5f, 1f, 0.5f, 1f); // Light green tint
            }
        }
    }
}

