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
        protected IWinConditionStrategy _winConditionStrategy;
        protected IPopupUIBuilder _uiBuilder;

        public bool IsActive => _popupWindow != null && _popupWindow.activeSelf;
        public bool WasCompletedSuccessfully => _wasCompletedSuccessfully;
        public GameObject PopupWindow => _popupWindow;

        protected virtual void CreatePopupWindow()
        {
            if (_uiBuilder == null)
            {
                _uiBuilder = new DefaultPopupUIBuilder();
            }

            _popupWindow = _uiBuilder.BuildPopup(this);
            InitializeGameUI();
        }

        protected abstract void InitializeGameUI();
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
        
        public GameObject GetContentPanel() => _contentPanel;
        
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

