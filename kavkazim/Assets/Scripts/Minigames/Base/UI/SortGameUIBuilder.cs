using Minigames.Base;
using Minigames.SortGames;
using UnityEngine;
using UnityEngine.UI;

namespace Minigames.Base.UI
{
    /// <summary>
    /// UI builder for sort games that creates a popup with upper and lower sections
    /// for cells and draggable elements.
    /// </summary>
    public class SortGameUIBuilder : DefaultPopupUIBuilder
    {
        public override GameObject BuildPopup(BaseMinigame minigame)
        {
            // Build base popup
            GameObject popupWindow = base.BuildPopup(minigame);
            
            if (minigame is SortGame sortGame)
            {
                // Get content panel and adjust it
                GameObject contentPanel = minigame.GetContentPanel();
                RectTransform contentRect = contentPanel.GetComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0.125f, 0.125f);
                contentRect.anchorMax = new Vector2(0.875f, 0.875f);
                contentRect.sizeDelta = Vector2.zero;
                contentRect.anchoredPosition = Vector2.zero;

                // Create upper section for cells
                GameObject upperSectionObj = new GameObject("UpperSection");
                upperSectionObj.transform.SetParent(contentPanel.transform, false);
                RectTransform upperSection = upperSectionObj.AddComponent<RectTransform>();
                upperSection.anchorMin = new Vector2(0, 0.5f);
                upperSection.anchorMax = new Vector2(1, 1f);
                upperSection.sizeDelta = Vector2.zero;
                upperSection.anchoredPosition = Vector2.zero;
                sortGame.SetUpperSection(upperSection);

                // Create lower section for elements
                GameObject lowerSectionObj = new GameObject("LowerSection");
                lowerSectionObj.transform.SetParent(contentPanel.transform, false);
                RectTransform lowerSection = lowerSectionObj.AddComponent<RectTransform>();
                lowerSection.anchorMin = new Vector2(0, 0f);
                lowerSection.anchorMax = new Vector2(1, 0.5f);
                lowerSection.sizeDelta = Vector2.zero;
                lowerSection.anchoredPosition = Vector2.zero;
                sortGame.SetLowerSection(lowerSection);

                // Set popup window reference
                sortGame.SetPopupWindowReference(upperSection);
            }

            return popupWindow;
        }
    }
}
