using UnityEngine;
using UnityEngine.UI;

namespace Minigames
{
    /// <summary>
    /// Abstract base class for all minigames. Handles common popup window creation and lifecycle.
    /// </summary>
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

        public bool IsActive => _popupWindow != null && _popupWindow.activeSelf;
        public GameObject PopupWindow => _popupWindow;

        /// <summary>
        /// Creates the popup window structure. Called automatically by StartGame().
        /// </summary>
        protected virtual void CreatePopupWindow()
        {
            // Create root canvas object
            _popupWindow = new GameObject($"{GetType().Name}Popup");
            _popupWindow.transform.SetParent(null); // Independent of scene hierarchy

            // Add Canvas component
            _canvas = _popupWindow.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = canvasSortingOrder;
            _popupWindow.AddComponent<CanvasScaler>();
            _popupWindow.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Create background overlay
            _backgroundPanel = new GameObject("Background");
            _backgroundPanel.transform.SetParent(_popupWindow.transform, false);
            Image bgImage = _backgroundPanel.AddComponent<Image>();
            bgImage.color = backgroundColor;
            RectTransform bgRect = _backgroundPanel.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Create content panel (centered)
            _contentPanel = new GameObject("ContentPanel");
            _contentPanel.transform.SetParent(_popupWindow.transform, false);
            Image contentImage = _contentPanel.AddComponent<Image>();
            contentImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(600, 400); // Default size, can be overridden
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);

            // Create close button if enabled
            if (showCloseButton)
            {
                CreateCloseButton();
            }

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
                Destroy(_popupWindow);
                _popupWindow = null;
            }

            _canvas = null;
            _backgroundPanel = null;
            _contentPanel = null;
            _closeButton = null;
        }

        protected virtual void OnDestroy()
        {
            CloseGame();
        }
    }
}

