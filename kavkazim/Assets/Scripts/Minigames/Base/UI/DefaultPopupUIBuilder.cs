using Minigames.Base;
using UnityEngine;
using UnityEngine.UI;

namespace Minigames.Base.UI
{
    /// <summary>
    /// Default popup UI builder that creates a standard popup window
    /// with background, content panel, and optional close button.
    /// </summary>
    public class DefaultPopupUIBuilder : IPopupUIBuilder
    {
        public virtual GameObject BuildPopup(BaseMinigame minigame)
        {
            // Create root canvas object
            GameObject popupWindow = new GameObject($"{minigame.GetType().Name}Popup");
            popupWindow.transform.SetParent(null); // Independent of scene hierarchy

            // Add Canvas component
            Canvas canvas = popupWindow.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = minigame.GetCanvasSortingOrder();
            popupWindow.AddComponent<CanvasScaler>();
            popupWindow.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Create background overlay
            GameObject backgroundPanel = new GameObject("Background");
            backgroundPanel.transform.SetParent(popupWindow.transform, false);
            Image bgImage = backgroundPanel.AddComponent<Image>();
            bgImage.color = minigame.GetBackgroundColor();
            RectTransform bgRect = backgroundPanel.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Create content panel (centered)
            GameObject contentPanel = new GameObject("ContentPanel");
            contentPanel.transform.SetParent(popupWindow.transform, false);
            Image contentImage = contentPanel.AddComponent<Image>();
            contentImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            RectTransform contentRect = contentPanel.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(1000, 700); // Default size, can be overridden
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);

            // Store references in minigame
            minigame.SetPopupReferences(popupWindow, canvas, backgroundPanel, contentPanel);

            // Create close button if enabled
            if (minigame.ShouldShowCloseButton())
            {
                CreateCloseButton(minigame, contentPanel);
            }

            return popupWindow;
        }

        private void CreateCloseButton(BaseMinigame minigame, GameObject contentPanel)
        {
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(contentPanel.transform, false);
            Button closeButton = closeBtnObj.AddComponent<Button>();
            Image btnImage = closeBtnObj.AddComponent<Image>();
            btnImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);

            RectTransform btnRect = closeBtnObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(40, 40);
            btnRect.anchorMin = new Vector2(1, 1);
            btnRect.anchorMax = new Vector2(1, 1);
            btnRect.anchoredPosition = new Vector2(-20, -20);

            // Add text to button
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(closeBtnObj.transform, false);
            Text txt = txtObj.AddComponent<Text>();
            txt.text = "X";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            closeButton.onClick.AddListener(() => minigame.CloseGame());
            minigame.SetCloseButton(closeButton);
        }

        public void Cleanup(GameObject popup)
        {
            // Default implementation - popup destruction is handled by BaseMinigame
        }
    }
}
