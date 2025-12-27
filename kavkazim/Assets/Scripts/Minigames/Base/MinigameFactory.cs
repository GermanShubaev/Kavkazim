using UnityEngine;

namespace Minigames
{
    /// <summary>
    /// Factory for creating minigame instances based on MinigameType.
    /// </summary>
    public static class MinigameFactory
    {
        /// <summary>
        /// Creates and returns a minigame instance based on the specified type.
        /// </summary>
        public static IMinigame CreateMinigame(MinigameType gameType)
        {
            GameObject minigameObj = new GameObject($"{gameType}Instance");
            
            IMinigame minigame = gameType switch
            {
                MinigameType.LezginkaSort => minigameObj.AddComponent<LezginkaSortGame>(),
                MinigameType.EmptyPopup => minigameObj.AddComponent<EmptyPopupMinigame>(),
                MinigameType.PraySortGame => minigameObj.AddComponent<PraySortGame>(),
                MinigameType.PapakhaClick => minigameObj.AddComponent<PapakhaClickGame>(),
                MinigameType.DishClick => minigameObj.AddComponent<DishClickGame>(),
                MinigameType.Game4 => minigameObj.AddComponent<PlaceholderMinigame>(),
                MinigameType.Game5 => minigameObj.AddComponent<PlaceholderMinigame>(),
                MinigameType.Game6 => minigameObj.AddComponent<PlaceholderMinigame>(),
                MinigameType.Game7 => minigameObj.AddComponent<PlaceholderMinigame>(),
                MinigameType.Game8 => minigameObj.AddComponent<PlaceholderMinigame>(),
                MinigameType.Game9 => minigameObj.AddComponent<PlaceholderMinigame>(),
                MinigameType.Game10 => minigameObj.AddComponent<PlaceholderMinigame>(),
                _ => minigameObj.AddComponent<PlaceholderMinigame>()
            };

            return minigame;
        }
    }

    /// <summary>
    /// Placeholder minigame for games 2-10 that haven't been implemented yet.
    /// </summary>
    public class PlaceholderMinigame : BaseMinigame
    {
        protected override void InitializeGameUI()
        {
            // Create a simple placeholder text
            GameObject textObj = new GameObject("PlaceholderText");
            textObj.transform.SetParent(_contentPanel.transform, false);
            UnityEngine.UI.Text text = textObj.AddComponent<UnityEngine.UI.Text>();
            text.text = $"Placeholder for {GetType().Name}\n\nThis minigame is not yet implemented.";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }
    }
}

