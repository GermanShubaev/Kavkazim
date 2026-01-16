using Minigames.Base;
using UnityEngine;
using UnityEngine.UI;

namespace Minigames.Base.UI
{
    public class DefaultPopupUIBuilder : IPopupUIBuilder
    {
        public virtual GameObject BuildPopup(BaseMinigame minigame)
        {
            GameObject popupWindow = new GameObject($"{minigame.GetType().Name}Popup");
            popupWindow.transform.SetParent(null);

            Canvas canvas = popupWindow.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = minigame.GetCanvasSortingOrder();
            
            CanvasScaler scaler = popupWindow.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            popupWindow.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            GameObject backgroundPanel = new GameObject("Background");
            backgroundPanel.transform.SetParent(popupWindow.transform, false);
            Image bgImage = backgroundPanel.AddComponent<Image>();
            bgImage.color = minigame.GetBackgroundColor();
            RectTransform bgRect = backgroundPanel.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            GameObject contentPanel = new GameObject("ContentPanel");
            contentPanel.transform.SetParent(popupWindow.transform, false);
            Image contentImage = contentPanel.AddComponent<Image>();
            contentImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            RectTransform contentRect = contentPanel.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(1000, 700);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);

            minigame.SetPopupReferences(popupWindow, canvas, backgroundPanel, contentPanel);

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
        }
    }
}
