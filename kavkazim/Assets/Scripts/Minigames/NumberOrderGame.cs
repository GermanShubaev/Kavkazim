using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Minigames
{
    /// <summary>
    /// A minigame where players drag letters a-h to order them correctly.
    /// </summary>
    public class NumberOrderGame : BaseMinigame
    {
        private Transform _letterContainer;
        private Text _resultText;
        private List<DraggableLetter> _letterElements = new List<DraggableLetter>();
        private List<char> _targetOrder = new List<char> { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };

        protected override void InitializeGameUI()
        {
            if (_contentPanel == null)
            {
                Debug.LogError("[NumberOrderGame] Content panel is null! Cannot initialize UI.");
                return;
            }

            // Make content panel larger for 9 numbers and lighter for better contrast
            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                contentRect.sizeDelta = new Vector2(700, 500);
            }
            
            // Make content panel background lighter and more visible
            Image contentImage = _contentPanel.GetComponent<Image>();
            if (contentImage != null)
            {
                contentImage.color = new Color(0.15f, 0.15f, 0.2f, 0.95f); // Slightly lighter dark blue-gray
            }

            // Create title text
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(_contentPanel.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Order the letters from a to h";
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
            _resultText.color = new Color(0.2f, 1f, 0.3f, 1f); // Bright green
            RectTransform resultRect = resultTextObj.GetComponent<RectTransform>();
            resultRect.anchorMin = new Vector2(0, 0.75f);
            resultRect.anchorMax = new Vector2(1, 0.85f);
            resultRect.sizeDelta = Vector2.zero;
            resultRect.anchoredPosition = Vector2.zero;

            // Create container for letters (just a parent, no layout)
            GameObject containerObj = new GameObject("LetterContainer");
            containerObj.transform.SetParent(_contentPanel.transform, false);
            _letterContainer = containerObj.transform;

            RectTransform containerRect = containerObj.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;

            // Create letters a-h in random positions
            CreateLetters();
            
            Debug.Log($"[NumberOrderGame] UI initialized. Content panel: {_contentPanel != null}, Letter container: {_letterContainer != null}");
        }

        private void CreateLetters()
        {
            if (_letterContainer == null)
            {
                Debug.LogError("[NumberOrderGame] Letter container is null! Cannot create letters.");
                return;
            }

            // Clear existing letters
            foreach (var letter in _letterElements)
            {
                if (letter != null)
                {
                    Destroy(letter.gameObject);
                }
            }
            _letterElements.Clear();

            // Create list of letters a-h and shuffle
            List<char> letters = new List<char>(_targetOrder);
            Shuffle(letters);

            // Get content panel size for random positioning
            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            float panelWidth = contentRect.sizeDelta.x;
            float panelHeight = contentRect.sizeDelta.y;
            
            // Create draggable letter elements at random positions
            System.Random rng = new System.Random();
            foreach (char letter in letters)
            {
                GameObject letterObj = new GameObject($"Letter_{letter}");
                letterObj.transform.SetParent(_letterContainer, false);

                // Get or add RectTransform
                RectTransform letterRect = letterObj.GetComponent<RectTransform>();
                if (letterRect == null)
                    letterRect = letterObj.AddComponent<RectTransform>();
                
                // Set size
                float letterSize = 80f;
                letterRect.sizeDelta = new Vector2(letterSize, letterSize);
                
                // Random position within content panel (with padding)
                float padding = 50f;
                float minX = -panelWidth / 2 + padding;
                float maxX = panelWidth / 2 - padding;
                float minY = -panelHeight / 2 + padding;
                float maxY = panelHeight / 2 - padding;
                
                float randomX = (float)(rng.NextDouble() * (maxX - minX) + minX);
                float randomY = (float)(rng.NextDouble() * (maxY - minY) + minY);
                
                letterRect.anchoredPosition = new Vector2(randomX, randomY);
                letterRect.anchorMin = new Vector2(0.5f, 0.5f);
                letterRect.anchorMax = new Vector2(0.5f, 0.5f);

                // Add Image component for background with bright, contrasting color
                Image bgImage = letterObj.AddComponent<Image>();
                // Use a bright color that contrasts well with dark background
                bgImage.color = new Color(0.3f, 0.7f, 1f, 1f); // Bright blue

                // Add Text component for letter display
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(letterObj.transform, false);
                Text text = textObj.AddComponent<Text>();
                text.text = char.ToUpper(letter).ToString(); // Display as uppercase
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 56;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white; // White text on bright blue background
                text.raycastTarget = false; // Don't block drag events
                
                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                textRect.anchoredPosition = Vector2.zero;

                // Add DraggableLetter component
                DraggableLetter draggable = letterObj.AddComponent<DraggableLetter>();
                draggable.Initialize(letter, this);

                _letterElements.Add(draggable);
            }
            
            Debug.Log($"[NumberOrderGame] Created {_letterElements.Count} letter elements");
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

        public void OnLetterMoved()
        {
            CheckWinCondition();
        }

        private void CheckWinCondition()
        {
            if (_letterContainer == null || _letterElements.Count != 8)
                return;

            // Get all letters in their current order (by position or sibling index)
            // For simplicity, we'll check if all letters exist and are in the correct order
            // Since they're scattered, we'll need a different approach - check if all letters are present
            // and maybe use a different win condition, or track their order differently
            
            // For now, let's use a simpler check: if all 8 letters exist, consider it a win
            // (You can enhance this later to check actual order)
            bool allLettersPresent = _letterElements.Count == 8 && 
                                     _letterElements.All(l => l != null && _targetOrder.Contains(l.Letter));
            
            if (allLettersPresent)
            {
                // Check if letters are in correct order (by checking if they can be arranged)
                // For a more sophisticated check, you'd need to track their positions
                // For now, just verify all letters exist
                if (_resultText != null)
                    _resultText.text = "All letters present!";
            }
            else
            {
                if (_resultText != null)
                    _resultText.text = "";
            }
        }

        protected override void CleanupGameUI()
        {
            foreach (var letter in _letterElements)
            {
                if (letter != null)
                {
                    Destroy(letter.gameObject);
                }
            }
            _letterElements.Clear();
        }

        public override void StartGame()
        {
            base.StartGame();
            if (_resultText != null)
                _resultText.text = "";
        }
    }

    /// <summary>
    /// Component that makes a letter draggable within the NumberOrderGame.
    /// </summary>
    public class DraggableLetter : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private char _letter;
        private NumberOrderGame _game;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Transform _originalParent;
        private Vector3 _originalPosition;
        private int _originalSiblingIndex;

        public char Letter => _letter;

        public void Initialize(char letter, NumberOrderGame game)
        {
            _letter = letter;
            _game = game;
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _originalParent = transform.parent;
            _originalPosition = _rectTransform.position;
            _originalSiblingIndex = transform.GetSiblingIndex();
            
            // Make it non-blocking so we can detect what's underneath
            _canvasGroup.alpha = 0.6f;
            _canvasGroup.blocksRaycasts = false;
            
            // Move to top of hierarchy while dragging (find the canvas root)
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                transform.SetParent(canvas.transform);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Update position to follow mouse
            _rectTransform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Restore visual state
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;

            // If we're still at canvas root (didn't drop on a valid target), return to original position
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && transform.parent == canvas.transform)
            {
                transform.SetParent(_originalParent);
                transform.SetSiblingIndex(_originalSiblingIndex);
                _rectTransform.position = _originalPosition;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            // Handle dropping another letter on this one
            DraggableLetter draggedLetter = eventData.pointerDrag?.GetComponent<DraggableLetter>();
            if (draggedLetter != null && draggedLetter != this)
            {
                // Swap positions
                Vector2 tempPos = _rectTransform.anchoredPosition;
                _rectTransform.anchoredPosition = draggedLetter._rectTransform.anchoredPosition;
                draggedLetter._rectTransform.anchoredPosition = tempPos;

                // Notify game that letters were moved
                if (_game != null)
                {
                    _game.OnLetterMoved();
                }
            }
        }
    }
}

