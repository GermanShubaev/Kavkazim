using System.Collections.Generic;
using Kavkazim.Netcode;
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
    /// <summary>
    /// Client-side UI controller for the Meeting Scene.
    /// Displays global meeting state (Timer, Results, Header).
    /// Delegating voting logic to <see cref="MeetingVoteUIController"/>.
    /// </summary>
    public class MeetingUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI centerText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI skipCountText;
        
        [Header("Sub-Controllers")]
        [SerializeField] private MeetingVoteUIController voteController;

        private MeetingManager _meetingManager;

        // Settings panel
        private GameObject _settingsPanel;
        private bool _isSettingsPanelOpen = false;

        private void Awake()
        {
            // CRITICAL: Ensure EventSystem exists for UI input
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                Debug.Log("[MeetingUIController] Creating EventSystem...");
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            
            // Ensure Canvas has GraphicRaycaster for button clicks
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            {
                Debug.Log("[MeetingUIController] Adding GraphicRaycaster to Canvas...");
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            
            // Auto-wire helper components
            if (voteController == null)
                voteController = GetComponentInChildren<MeetingVoteUIController>();

            // Auto-wire text references
            if (timerText == null)
            {
                GameObject timerObj = GameObject.Find("TimerText");
                if (timerObj != null) timerText = timerObj.GetComponent<TextMeshProUGUI>();
            }
            
            if (skipCountText == null)
            {
                GameObject skipCountObj = GameObject.Find("SkipCountText");
                if (skipCountObj != null) skipCountText = skipCountObj.GetComponent<TextMeshProUGUI>();
            }
            
            // Create settings panel with its own canvas
            CreateSettingsPanel();
            
            Debug.Log($"[MeetingUIController] Awake - voteController={voteController != null}");
        }
        
        // Canvas specifically for the settings UI (created by this controller)
        private Canvas _settingsCanvas;

        private void Start()
        {
            // Find MeetingManager
            _meetingManager = MeetingManager.Instance;
            if (_meetingManager == null)
            {
                Debug.LogError("[MeetingUIController] MeetingManager not found!");
                return;
            }

            // Get local client ID
            if (NetworkManager.Singleton != null)
            {
            }

            // Subscribe to MeetingManager events
            _meetingManager.TimeRemaining.OnValueChanged += OnTimerChanged;
            _meetingManager.SkipVoteCount.OnValueChanged += OnSkipCountChanged; 

            // Set center text
            if (centerText != null)
            {
                centerText.text = "WHO IS THE IMPOSTOR?";
            }

            // Update UI immediately
            UpdateTimerDisplay(_meetingManager.TimeRemaining.Value);
            
            // In new design, maybe we show skip count or hide it? 
            // Usually skip count is hidden or shows 0 until end/meeting update.
            // Keeping original logic:
            if (skipCountText != null)
            {
                skipCountText.text = "VOTING IN PROGRESS...";
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe
            if (_meetingManager != null)
            {
                _meetingManager.TimeRemaining.OnValueChanged -= OnTimerChanged;
                _meetingManager.SkipVoteCount.OnValueChanged -= OnSkipCountChanged;
            }
            
            // Clean up our settings canvas
            if (_settingsCanvas != null)
            {
                Destroy(_settingsCanvas.gameObject);
            }
        }

        // ========== EVENT HANDLERS ==========

        private void OnTimerChanged(float previousValue, float newValue)
        {
            UpdateTimerDisplay(newValue);
        }

        private void OnSkipCountChanged(int previousValue, int newValue)
        {
            // Only update if meeting ended? Or always?
            // "When a player selects a choice" requirements were for INPUT.
            // NetworkVariable SkipVoteCount usually updates live. 
            // But standard Among Us hides counts until end. 
            // MeetingManager updates it immediately on vote.
            // We'll trust existing design choice:
            UpdateSkipCountDisplay(newValue);
        }

        // ========== UI UPDATES ==========

        private void UpdateTimerDisplay(float timeRemaining)
        {
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(timeRemaining);
                timerText.text = $"TIME REMAINING: {seconds}s";
                
                // Visual urgency
                if (seconds <= 10) timerText.color = Color.red;
                else timerText.color = Color.white;
            }
        }

        private void UpdateSkipCountDisplay(int skipCount)
        {
            // Only show count if meeting ended, arguably? 
            // For now, let's just show what the server sends.
            if (skipCountText != null && _meetingManager.HasEnded.Value)
            {
                skipCountText.text = $"VOTES SKIPPED: {skipCount}";
            }
        }

        // ========== SETTINGS PANEL ==========

        private void CreateSettingsPanel()
        {
            // Create our own canvas for settings UI to avoid conflicts with persistent canvases
            GameObject settingsCanvasObj = new GameObject("MeetingSettingsCanvas");
            // Don't parent to anything - let it be a root object so positioning works correctly
            _settingsCanvas = settingsCanvasObj.AddComponent<Canvas>();
            _settingsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _settingsCanvas.sortingOrder = 100; // Ensure it's above other UI
            settingsCanvasObj.AddComponent<CanvasScaler>();
            settingsCanvasObj.AddComponent<GraphicRaycaster>();

            // Create Settings Button (Top Right)
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
            
            // Button text
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

            // Create Settings Panel (Center)
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(settingsCanvasObj.transform, false);
            
            Image panelBg = _settingsPanel.AddComponent<Image>();
            panelBg.color = new Color(0, 0, 0, 0.9f);
            
            RectTransform panelRect = _settingsPanel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(350, 200);
            panelRect.anchoredPosition = Vector2.zero;

            // Room Code Text
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

            // Leave Game Button
            GameObject leaveButtonObj = new GameObject("LeaveButton");
            leaveButtonObj.transform.SetParent(_settingsPanel.transform, false);
            
            Image leaveBg = leaveButtonObj.AddComponent<Image>();
            leaveBg.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            
            Button leaveButton = leaveButtonObj.AddComponent<Button>();
            leaveButton.onClick.AddListener(OnLeaveClicked);
            
            RectTransform leaveRect = leaveButtonObj.GetComponent<RectTransform>();
            leaveRect.sizeDelta = new Vector2(200, 50);
            leaveRect.anchoredPosition = new Vector2(0, -30);

            // Leave button text
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

            // Hide panel initially
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
