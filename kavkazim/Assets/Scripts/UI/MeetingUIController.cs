using System.Collections.Generic;
using Kavkazim.Netcode;
using Kavkazim.Utils;
using Kavkazim.Netcode.Meeting;
using Netcode;
using Netcode.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kavkazim.UI.Meeting
{
    public class MeetingUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI timerText;
        
        [Header("Sub-Controllers")]
        [SerializeField] private MeetingVoteUIController voteController;

        private MeetingManager _meetingManager;

        private GameObject _settingsPanel;
        private bool _isSettingsPanelOpen = false;

        private void Awake()
        {
            UIUtils.EnsureEventSystem();
            
            Canvas canvas = FindFirstObjectByType<Canvas>();
            UIUtils.EnsureGraphicRaycaster(canvas);
            
            CreateSettingsPanel();
            
            Debug.Log($"[MeetingUIController] Awake - voteController={voteController != null}");
        }
        
        private Canvas _settingsCanvas;

        private void Start()
        {
            _meetingManager = MeetingManager.Instance;
            if (_meetingManager == null)
            {
                Debug.LogError("[MeetingUIController] MeetingManager not found!");
                return;
            }

            if (NetworkManager.Singleton != null)
            {
            }

            _meetingManager.TimeRemaining.OnValueChanged += OnTimerChanged;
            UpdateTimerDisplay(_meetingManager.TimeRemaining.Value);
        }

        private void OnDestroy()
        {
            if (_meetingManager != null)
            {
                _meetingManager.TimeRemaining.OnValueChanged -= OnTimerChanged;
            }
            
            if (_settingsCanvas != null)
            {
                Destroy(_settingsCanvas.gameObject);
            }
        }

        private void OnTimerChanged(float previousValue, float newValue)
        {
            UpdateTimerDisplay(newValue);
        }

        private void UpdateTimerDisplay(float timeRemaining)
        {
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(timeRemaining);
                timerText.text = $"TIME REMAINING: {seconds}s";
                
                if (seconds <= 10) timerText.color = Color.red;
                else timerText.color = Color.white;
            }
        }

        private void CreateSettingsPanel()
        {
            GameObject settingsCanvasObj = new GameObject("MeetingSettingsCanvas");
            _settingsCanvas = settingsCanvasObj.AddComponent<Canvas>();
            _settingsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _settingsCanvas.sortingOrder = 100;
            CanvasScaler scaler = settingsCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            settingsCanvasObj.AddComponent<GraphicRaycaster>();

            GameObject settingsButtonObj = new GameObject("SettingsButton");
            settingsButtonObj.transform.SetParent(settingsCanvasObj.transform, false);
            
            Image buttonBg = settingsButtonObj.AddComponent<Image>();
            buttonBg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            
            Button settingsButton = settingsButtonObj.AddComponent<Button>();
            settingsButton.onClick.AddListener(ToggleSettingsPanel);
            
            RectTransform buttonRect = settingsButtonObj.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(160, 60);
            buttonRect.anchorMin = new Vector2(1, 1);
            buttonRect.anchorMax = new Vector2(1, 1);
            buttonRect.pivot = new Vector2(1, 1);
            buttonRect.anchoredPosition = new Vector2(-20, -20);
            
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(settingsButtonObj.transform, false);
            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "Settings";
            buttonText.fontSize = 24;
            buttonText.color = Color.black;
            buttonText.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = buttonTextObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(settingsCanvasObj.transform, false);
            
            Image panelBg = _settingsPanel.AddComponent<Image>();
            panelBg.color = new Color(0, 0, 0, 0.9f);
            
            RectTransform panelRect = _settingsPanel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(350, 200);
            panelRect.anchoredPosition = Vector2.zero;

            GameObject codeTextObj = new GameObject("RoomCodeText");
            codeTextObj.transform.SetParent(_settingsPanel.transform, false);
            TextMeshProUGUI codeText = codeTextObj.AddComponent<TextMeshProUGUI>();
            
            string code = "Unknown";
            if (NetworkBootstrap.Instance != null)
            {
                code = NetworkBootstrap.Instance.LobbyCode ?? "None";
            }
            codeText.text = $"Room Code: {code}";
            codeText.fontSize = 28;
            codeText.color = Color.yellow;
            codeText.alignment = TextAlignmentOptions.Center;
            
            RectTransform codeRect = codeTextObj.GetComponent<RectTransform>();
            codeRect.sizeDelta = new Vector2(320, 50);
            codeRect.anchoredPosition = new Vector2(0, 50);

            GameObject leaveButtonObj = new GameObject("LeaveButton");
            leaveButtonObj.transform.SetParent(_settingsPanel.transform, false);
            
            Image leaveBg = leaveButtonObj.AddComponent<Image>();
            leaveBg.color = UIUtils.ColorNotReady;
            
            Button leaveButton = leaveButtonObj.AddComponent<Button>();
            leaveButton.onClick.AddListener(OnLeaveClicked);
            
            RectTransform leaveRect = leaveButtonObj.GetComponent<RectTransform>();
            leaveRect.sizeDelta = new Vector2(200, 50);
            leaveRect.anchoredPosition = new Vector2(0, -30);

            GameObject leaveTextObj = new GameObject("Text");
            leaveTextObj.transform.SetParent(leaveButtonObj.transform, false);
            TextMeshProUGUI leaveText = leaveTextObj.AddComponent<TextMeshProUGUI>();
            leaveText.text = "Leave Game";
            leaveText.fontSize = 22;
            leaveText.color = Color.white;
            leaveText.alignment = TextAlignmentOptions.Center;
            RectTransform leaveTextRect = leaveTextObj.GetComponent<RectTransform>();
            leaveTextRect.anchorMin = Vector2.zero;
            leaveTextRect.anchorMax = Vector2.one;
            leaveTextRect.sizeDelta = Vector2.zero;

            _settingsPanel.SetActive(false);
        }

        private void ToggleSettingsPanel()
        {
            _isSettingsPanelOpen = !_isSettingsPanelOpen;
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(_isSettingsPanelOpen);
            }
        }

        private void OnLeaveClicked()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            SceneManager.LoadScene("MainMenu");
        }
    }
}
