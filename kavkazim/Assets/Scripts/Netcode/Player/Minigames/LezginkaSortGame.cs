using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Minigames
{
    public class LezginkaSortGame : BaseMinigame
    {
        [Header("Setup")]
        [SerializeField] private Card cardPrefab;     // assign Card prefab
        [SerializeField] private Sprite[] numberSprites;    // size 8, in order 1..8

        private Transform _cardGridParent;
        private Text _resultText;
        private Card _firstSelected;
        private List<Card> _spawnedCards = new List<Card>();

        protected override void InitializeGameUI()
        {
            // Create result text at the top
            GameObject resultTextObj = new GameObject("ResultText");
            resultTextObj.transform.SetParent(_contentPanel.transform, false);
            _resultText = resultTextObj.AddComponent<Text>();
            _resultText.text = "";
            _resultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _resultText.fontSize = 24;
            _resultText.alignment = TextAnchor.UpperCenter;
            _resultText.color = Color.white;
            RectTransform resultRect = resultTextObj.GetComponent<RectTransform>();
            resultRect.anchorMin = new Vector2(0, 0.85f);
            resultRect.anchorMax = new Vector2(1, 1);
            resultRect.sizeDelta = Vector2.zero;
            resultRect.anchoredPosition = Vector2.zero;

            // Create card grid parent
            GameObject gridObj = new GameObject("CardGrid");
            gridObj.transform.SetParent(_contentPanel.transform, false);
            _cardGridParent = gridObj.transform;
            
            // Add GridLayoutGroup for automatic card arrangement
            GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(80, 80);
            gridLayout.spacing = new Vector2(10, 10);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4; // 4 columns for 8 cards (2 rows)

            RectTransform gridRect = gridObj.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.1f, 0.1f);
            gridRect.anchorMax = new Vector2(0.9f, 0.8f);
            gridRect.sizeDelta = Vector2.zero;
            gridRect.anchoredPosition = Vector2.zero;

            // Spawn cards
            SpawnCards();
        }

        protected override void CleanupGameUI()
        {
            // Clean up spawned cards
            foreach (var card in _spawnedCards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }
            _spawnedCards.Clear();
            _firstSelected = null;
        }

        public override void StartGame()
        {
            base.StartGame();
            if (_resultText != null)
                _resultText.text = "";
        }

        private void SpawnCards()
        {
            if (_cardGridParent == null) return;

            // Clear any existing cards
            foreach (var card in _spawnedCards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }
            _spawnedCards.Clear();

            // Numbers 1–8
            List<int> numbers = Enumerable.Range(1, 8).ToList();
            Shuffle(numbers);

            // Instantiate cards in random order
            for (int i = 0; i < numbers.Count; i++)
            {
                int num = numbers[i];

                // If no prefab assigned, create a basic card
                Card card;
                if (cardPrefab != null)
                {
                    card = Instantiate(cardPrefab, _cardGridParent);
                }
                else
                {
                    // Create a basic card if no prefab
                    GameObject cardObj = new GameObject($"Card_{num}");
                    cardObj.transform.SetParent(_cardGridParent, false);
                    card = cardObj.AddComponent<Card>();
                    Image cardImage = cardObj.AddComponent<Image>();
                    cardImage.color = Color.white;
                    Button cardButton = cardObj.AddComponent<Button>();
                }

                Sprite sprite = null;
                if (numberSprites != null && num - 1 < numberSprites.Length && numberSprites[num - 1] != null)
                {
                    sprite = numberSprites[num - 1]; // array assumed 0-based: index 0 = "1"
                }
                card.Init(num, sprite, this);
                _spawnedCards.Add(card);
            }
        }

    private void Shuffle<T>(IList<T> list)
    {
        // Fisher-Yates
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    public void OnCardClicked(Card clicked)
    {
        // First selection
        if (_firstSelected == null)
        {
            _firstSelected = clicked;
            _firstSelected.SetSelected(true);
            return;
        }

        // Clicked the same card again: deselect
        if (_firstSelected == clicked)
        {
            _firstSelected.SetSelected(false);
            _firstSelected = null;
            return;
        }

        // Second selection: swap
        SwapCards(_firstSelected, clicked);

        _firstSelected.SetSelected(false);
        _firstSelected = null;

        // Check if we won
        CheckWinCondition();
    }

    private void SwapCards(Card a, Card b)
    {
        int indexA = a.transform.GetSiblingIndex();
        int indexB = b.transform.GetSiblingIndex();

        a.transform.SetSiblingIndex(indexB);
        b.transform.SetSiblingIndex(indexA);
    }

        private void CheckWinCondition()
        {
            if (_cardGridParent == null) return;

            // correct order: child 0 -> number 1, child 1 -> number 2, ...
            for (int i = 0; i < _cardGridParent.childCount; i++)
            {
                var card = _cardGridParent.GetChild(i).GetComponent<Card>();
                if (card == null || card.Number != i + 1)
                {
                    // not yet solved
                    return;
                }
            }

            // If we get here, all in order
            if (_resultText != null)
                _resultText.text = "You win!";

            Debug.Log("Puzzle solved!");
        }
    }
}
