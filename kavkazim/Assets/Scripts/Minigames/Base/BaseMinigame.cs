using UnityEngine;
using UnityEngine.UI;
using Minigames.Base.Strategies;
using Minigames.Base.UI;

namespace Minigames.Base
{
    public abstract class BaseMinigame : MonoBehaviour, IMinigame
    {
        [Header("Popup Settings")]
        [SerializeField] protected int canvasSortingOrder = 200;
        [SerializeField] protected Color backgroundColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] protected bool showCloseButton = true;

        protected GameObject _popupWindow;
        protected Canvas _canvas;
        protected GameObject _backgroundPanel;
        protected GameObject _contentPanel;
        protected Button _closeButton;
        protected bool _wasCompletedSuccessfully = false;
        
        /// <summary>
        /// Strategy for checking win conditions. Can be set by derived classes.
        /// </summary>
        protected IWinConditionStrategy _winConditionStrategy;
        
        /// <summary>
        /// UI builder for creating popup windows. Can be set by derived classes.
        /// </summary>
        protected IPopupUIBuilder _uiBuilder;

        public bool IsActive => _popupWindow != null && _popupWindow.activeSelf;
        public bool WasCompletedSuccessfully => _wasCompletedSuccessfully;
        public GameObject PopupWindow => _popupWindow;

        /// <summary>
        /// Creates the popup window structure. Called automatically by StartGame().
        /// Uses UI builder pattern if available, otherwise falls back to default implementation.
        /// </summary>
        protected virtual void CreatePopupWindow()
        {
            // Use UI builder if set, otherwise use default builder
            if (_uiBuilder == null)
            {
                _uiBuilder = new DefaultPopupUIBuilder();
            }

            _popupWindow = _uiBuilder.BuildPopup(this);

            // Initialize minigame-specific UI
            InitializeGameUI();
        }

        /// <summary>
        /// Creates a close button in the top-right corner of the content panel.
        /// </summary>
        protected virtual void CreateCloseButton()
        {
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(_contentPanel.transform, false);
            _closeButton = closeBtnObj.AddComponent<Button>();
            Image btnImage = closeBtnObj.AddComponent<Image>();
            btnImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);

            RectTransform btnRect = closeBtnObj.GetComponent<RectTransform>();
            // Button size scaled for 2560x1440 reference resolution
            btnRect.sizeDelta = new Vector2(40, 40);
            btnRect.anchorMin = new Vector2(1, 1);
            btnRect.anchorMax = new Vector2(1, 1);
            // Position offset scaled for 2560x1440 reference resolution
            btnRect.anchoredPosition = new Vector2(-20, -20);

            // Add text to button
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(closeBtnObj.transform, false);
            Text txt = txtObj.AddComponent<Text>();
            txt.text = "X";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Font size scaled for 2560x1440 reference resolution
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            _closeButton.onClick.AddListener(CloseGame);
        }

        /// <summary>
        /// Override this method to initialize minigame-specific UI elements.
        /// The content panel is already created and available via _contentPanel.
        /// </summary>
        protected abstract void InitializeGameUI();

        /// <summary>
        /// Override this method to clean up minigame-specific resources.
        /// The popup window cleanup is handled automatically.
        /// </summary>
        protected virtual void CleanupGameUI() { }

        public virtual void StartGame()
        {
            if (IsActive)
            {
                Debug.LogWarning($"{GetType().Name} is already active!");
                return;
            }

            CreatePopupWindow();
            _popupWindow.SetActive(true);
        }

        public virtual void CloseGame()
        {
            if (!IsActive)
            {
                return;
            }

            CleanupGameUI();

            if (_popupWindow != null)
            {
                if (_uiBuilder != null)
                {
                    _uiBuilder.Cleanup(_popupWindow);
                }
                Destroy(_popupWindow);
                _popupWindow = null;
            }

            _canvas = null;
            _backgroundPanel = null;
            _contentPanel = null;
            _closeButton = null;
        }

        // Helper methods for UI builder
        public int GetCanvasSortingOrder() => canvasSortingOrder;
        public Color GetBackgroundColor() => backgroundColor;
        public bool ShouldShowCloseButton() => showCloseButton;
        
        public void SetPopupReferences(GameObject popupWindow, Canvas canvas, GameObject backgroundPanel, GameObject contentPanel)
        {
            _popupWindow = popupWindow;
            _canvas = canvas;
            _backgroundPanel = backgroundPanel;
            _contentPanel = contentPanel;
        }
        
        public void SetCloseButton(Button closeButton)
        {
            _closeButton = closeButton;
        }
        
        // Expose content panel for UI builders
        public GameObject GetContentPanel() => _contentPanel;
        
        /// <summary>
        /// Called when the minigame is completed successfully.
        /// Derived classes should override this and call base.OnGameComplete() to set the completion flag.
        /// Made public to allow win condition strategies to call it.
        /// </summary>
        public virtual void OnGameComplete()
        {
            _wasCompletedSuccessfully = true;
        }
        
        protected virtual System.Collections.IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseGame();
        }

        protected virtual void OnDestroy()
        {
            CloseGame();
        }
    }
}

