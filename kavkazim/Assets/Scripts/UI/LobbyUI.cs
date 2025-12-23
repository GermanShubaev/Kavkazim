using System.Collections.Generic;
using Kavkazim.Netcode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Lobby UI controller. Creates and manages the lobby interface.
    /// Subscribes to GameSessionManager events for reactive updates.
    /// 
    /// Features:
    /// - Player list with ready indicators
    /// - Settings panel (host can edit, clients read-only)
    /// - Start Game / Ready / Leave buttons
    /// - "Match in Progress" waiting message for late joiners
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private Color hostColor = new Color(1f, 0.84f, 0f); // Gold
        [SerializeField] private Color readyColor = new Color(0.2f, 0.8f, 0.2f); // Green
        [SerializeField] private Color notReadyColor = new Color(0.8f, 0.2f, 0.2f); // Red
        [SerializeField] private Color waitingColor = new Color(0.5f, 0.5f, 0.5f); // Gray

        // UI Elements (created dynamically)
        private GameObject _canvasObj;
        private GameObject _lobbyPanel;
        private GameObject _waitingPanel;
        private Transform _playerListContent;
        private Text _roomCodeText;
        private Text _playerCountText;
        private Text _phaseText;
        
        // Settings UI
        private Slider _maxPlayersSlider;
        private Slider _kavkaziCountSlider;
        private Slider _votingTimeSlider;
        private Slider _moveSpeedSlider;
        private Slider _killCooldownSlider;
        private Slider _missionsSlider;
        private Text _maxPlayersValue;
        private Text _kavkaziCountValue;
        private Text _votingTimeValue;
        private Text _moveSpeedValue;
        private Text _killCooldownValue;
        private Text _missionsValue;
        
        // Buttons
        private Button _startGameButton;
        private Button _readyButton;
        private Button _leaveButton;
        private Text _readyButtonText;
        
        // Player list items (pooled)
        private List<GameObject> _playerListItems = new List<GameObject>();
        
        private Sprite _roundedSprite;

        // State
        private bool _isReady = false;
        private bool _isHost = false;

        private void Start()
        {
            CreateUI();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.OnPlayersChanged += OnPlayersListUpdated;
                GameSessionManager.Instance.OnSettingsChanged += RefreshSettings;
                GameSessionManager.Instance.OnPhaseChanged += OnPhaseChanged;
                
                // Initial refresh
                RefreshPlayerList();
                RefreshSettings();
                OnPhaseChanged(GameSessionManager.Instance.CurrentPhase.Value);
            }
            else
            {
                Debug.LogWarning("[LobbyUI] GameSessionManager.Instance is null");
                // Try again after a delay
                Invoke(nameof(RetrySubscribe), 0.5f);
            }
        }

        private void RetrySubscribe()
        {
            if (GameSessionManager.Instance != null)
            {
                SubscribeToEvents();
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.OnPlayersChanged -= OnPlayersListUpdated;
                GameSessionManager.Instance.OnSettingsChanged -= RefreshSettings;
                GameSessionManager.Instance.OnPhaseChanged -= OnPhaseChanged;
            }
        }

        private void OnPlayersListUpdated()
        {
            RefreshPlayerList();
            // Re-evaluate phase logic because "IsWaiting" status depends on finding local player in the list
            // This fixes the issue where late joiners initially see empty screen because their player entry wasn't in list yet
            if (GameSessionManager.Instance != null)
            {
                OnPhaseChanged(GameSessionManager.Instance.CurrentPhase.Value);
            }
        }

        private void CreateUI()
        {
            _roundedSprite = CreateRoundedRectSprite(64, 8);

            // Ensure EventSystem exists
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Create Canvas
            _canvasObj = new GameObject("LobbyCanvas");
            _canvasObj.transform.SetParent(transform, false);
            Canvas canvas = _canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            _canvasObj.AddComponent<CanvasScaler>();
            _canvasObj.AddComponent<GraphicRaycaster>();

            // Create main lobby panel
            CreateLobbyPanel();
            
            // Create waiting panel (for late joiners)
            CreateWaitingPanel();
        }

        private void CreateLobbyPanel()
        {
            _lobbyPanel = CreatePanel(_canvasObj.transform, "LobbyPanel", new Color(0.1f, 0.1f, 0.15f, 0.95f));
            RectTransform lobbyRect = _lobbyPanel.GetComponent<RectTransform>();
            lobbyRect.anchorMin = new Vector2(0.1f, 0.1f);
            lobbyRect.anchorMax = new Vector2(0.9f, 0.9f);
            lobbyRect.offsetMin = Vector2.zero;
            lobbyRect.offsetMax = Vector2.zero;

            // Title - top left
            var titleObj = CreateText(_lobbyPanel.transform, "Title", "LOBBY", 28, FontStyle.Bold, 
                new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(80, -25));
            titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 40);

            // Room Code - top right with padding from edge
            _roomCodeText = CreateText(_lobbyPanel.transform, "RoomCode", "Code: ----", 18, FontStyle.Bold,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-130, -25)).GetComponent<Text>();
            _roomCodeText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 35);
            _roomCodeText.color = Color.yellow;
            _roomCodeText.alignment = TextAnchor.MiddleRight;

            // Phase indicator - below title, small
            _phaseText = CreateText(_lobbyPanel.transform, "Phase", "Lobby Open", 12, FontStyle.Italic,
                new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(80, -55)).GetComponent<Text>();
            _phaseText.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 20);
            _phaseText.color = Color.cyan;

            // Left side: Player List
            CreatePlayerListPanel();

            // Right side: Settings
            CreateSettingsPanel();

            // Bottom: Buttons
            CreateButtonPanel();

            // Update room code display
            UpdateRoomCode();
        }

        private void CreatePlayerListPanel()
        {
            GameObject listPanel = CreatePanel(_lobbyPanel.transform, "PlayerListPanel", new Color(0.15f, 0.15f, 0.2f, 1f));
            RectTransform listRect = listPanel.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.02f, 0.10f);  // Same height as Settings panel
            listRect.anchorMax = new Vector2(0.48f, 0.85f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;

            // Header
            _playerCountText = CreateText(listPanel.transform, "Header", "Players (0/10)", 20, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20)).GetComponent<Text>();

            // Simple content container with vertical layout (no scroll view - simpler)
            GameObject content = new GameObject("PlayerListContent");
            content.transform.SetParent(listPanel.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.03f, 0.02f);
            contentRect.anchorMax = new Vector2(0.97f, 0.88f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;
            
            _playerListContent = content.transform;
        }

        private void CreateSettingsPanel()
        {
            GameObject settingsPanel = CreatePanel(_lobbyPanel.transform, "SettingsPanel", new Color(0.15f, 0.15f, 0.2f, 1f));
            RectTransform settingsRect = settingsPanel.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(0.52f, 0.10f);  // Lower bottom to fit all settings
            settingsRect.anchorMax = new Vector2(0.98f, 0.85f);
            settingsRect.offsetMin = Vector2.zero;
            settingsRect.offsetMax = Vector2.zero;

            // Header
            CreateText(settingsPanel.transform, "Header", "Settings", 20, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -15));

            float yOffset = -40;
            float yStep = -43;  // Compact spacing for 6 settings

            // Max Players
            (_maxPlayersSlider, _maxPlayersValue) = CreateSettingSlider(settingsPanel.transform, "Max Players", 
                4, 15, 10, yOffset);
            _maxPlayersSlider.wholeNumbers = true;
            _maxPlayersSlider.onValueChanged.AddListener(v => OnSettingChanged());
            yOffset += yStep;

            // Kavkazi Count
            (_kavkaziCountSlider, _kavkaziCountValue) = CreateSettingSlider(settingsPanel.transform, "Kavkazi Count", 
                1, 3, 2, yOffset);
            _kavkaziCountSlider.wholeNumbers = true;
            _kavkaziCountSlider.onValueChanged.AddListener(v => OnSettingChanged());
            yOffset += yStep;

            // Voting Time
            (_votingTimeSlider, _votingTimeValue) = CreateSettingSlider(settingsPanel.transform, "Voting Time (s)", 
                30, 180, 60, yOffset);
            _votingTimeSlider.wholeNumbers = true;
            _votingTimeSlider.onValueChanged.AddListener(v => OnSettingChanged());
            yOffset += yStep;

            // Move Speed
            (_moveSpeedSlider, _moveSpeedValue) = CreateSettingSlider(settingsPanel.transform, "Move Speed", 
                0.5f, 5f, 3.5f, yOffset);
            _moveSpeedSlider.onValueChanged.AddListener(v => OnSettingChanged());
            yOffset += yStep;

            // Kill Cooldown
            (_killCooldownSlider, _killCooldownValue) = CreateSettingSlider(settingsPanel.transform, "Kill Cooldown (s)", 
                5, 60, 15, yOffset);
            _killCooldownSlider.wholeNumbers = true;
            _killCooldownSlider.onValueChanged.AddListener(v => OnSettingChanged());
            yOffset += yStep;

            // Missions Count
            (_missionsSlider, _missionsValue) = CreateSettingSlider(settingsPanel.transform, "Missions Count", 
                1, 10, 3, yOffset);
            _missionsSlider.wholeNumbers = true;
            _missionsSlider.onValueChanged.AddListener(v => OnSettingChanged());
        }

        private (Slider, Text) CreateSettingSlider(Transform parent, string label, float min, float max, float defaultVal, float yPos)
        {
            GameObject container = new GameObject(label + "Container");
            container.transform.SetParent(parent, false);
            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.05f, 1);
            containerRect.anchorMax = new Vector2(0.95f, 1);
            containerRect.pivot = new Vector2(0.5f, 1);
            containerRect.anchoredPosition = new Vector2(0, yPos);
            containerRect.sizeDelta = new Vector2(0, 50);

            // Label
            CreateText(container.transform, "Label", label, 16, FontStyle.Normal,
                new Vector2(0, 0.5f), new Vector2(0.4f, 0.5f), Vector2.zero);

            // Slider
            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(container.transform, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.40f, 0.3f);
            sliderRect.anchorMax = new Vector2(0.84f, 0.7f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            Image sliderBg = sliderObj.AddComponent<Image>();
            sliderBg.sprite = _roundedSprite;
            sliderBg.type = Image.Type.Sliced;
            sliderBg.color = new Color(0.3f, 0.3f, 0.35f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultVal;

            // Fill area
            GameObject fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = _roundedSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(0.3f, 0.6f, 0.9f);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slider.fillRect = fillRect;

            // Handle
            GameObject handleArea = new GameObject("HandleArea");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.sprite = _roundedSprite;
            handleImg.type = Image.Type.Sliced;
            handleImg.color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(15, 0);
            slider.handleRect = handleRect;

            // Value text - positioned inside the panel
            Text valueText = CreateText(container.transform, "Value", defaultVal.ToString("F1"), 14, FontStyle.Bold,
                new Vector2(0.86f, 0.5f), new Vector2(0.98f, 0.5f), Vector2.zero).GetComponent<Text>();
            valueText.alignment = TextAnchor.MiddleCenter;

            // Update value text on slider change
            slider.onValueChanged.AddListener(v => {
                valueText.text = slider.wholeNumbers ? v.ToString("F0") : v.ToString("F1");
            });

            return (slider, valueText);
        }

        private void CreateButtonPanel()
        {
            // Start Game button (Host only)
            _startGameButton = CreateButton(_lobbyPanel.transform, "StartGame", "START GAME", 
                new Color(0.2f, 0.7f, 0.2f), new Vector2(0.15f, 0.02f), new Vector2(0.35f, 0.1f));
            _startGameButton.onClick.AddListener(OnStartGameClicked);

            // Ready button (Clients)
            _readyButton = CreateButton(_lobbyPanel.transform, "Ready", "READY", 
                new Color(0.3f, 0.5f, 0.8f), new Vector2(0.4f, 0.02f), new Vector2(0.6f, 0.1f));
            _readyButton.onClick.AddListener(OnReadyClicked);
            _readyButtonText = _readyButton.GetComponentInChildren<Text>();

            // Leave button
            _leaveButton = CreateButton(_lobbyPanel.transform, "Leave", "LEAVE", 
                new Color(0.7f, 0.2f, 0.2f), new Vector2(0.65f, 0.02f), new Vector2(0.85f, 0.1f));
            _leaveButton.onClick.AddListener(OnLeaveClicked);
        }

        private void CreateWaitingPanel()
        {
            _waitingPanel = CreatePanel(_canvasObj.transform, "WaitingPanel", new Color(0.1f, 0.1f, 0.15f, 0.95f));
            RectTransform waitRect = _waitingPanel.GetComponent<RectTransform>();
            waitRect.anchorMin = new Vector2(0.2f, 0.3f);
            waitRect.anchorMax = new Vector2(0.8f, 0.7f);
            waitRect.offsetMin = Vector2.zero;
            waitRect.offsetMax = Vector2.zero;

            CreateText(_waitingPanel.transform, "Title", "Match in play", 32, FontStyle.Bold,
                new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), Vector2.zero);

            CreateText(_waitingPanel.transform, "Message", "You will join next game", 20, FontStyle.Italic,
                new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.4f), Vector2.zero);

            Button leaveBtn = CreateButton(_waitingPanel.transform, "Leave", "Leave", 
                new Color(0.7f, 0.2f, 0.2f), new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.25f));
            leaveBtn.onClick.AddListener(OnLeaveClicked);

            _waitingPanel.SetActive(false);
        }

        private void UpdateRoomCode()
        {
            if (_roomCodeText != null && Netcode.NetworkBootstrap.Instance != null)
            {
                string code = Netcode.NetworkBootstrap.Instance.LobbyCode ?? "----";
                _roomCodeText.text = $"Room Code: {code}";
            }
        }

        // ========== EVENT HANDLERS ==========

        private void RefreshPlayerList()
        {
            if (GameSessionManager.Instance == null) return;

            var players = GameSessionManager.Instance.Players;
            int maxPlayers = GameSessionManager.Instance.Settings.Value.MaxPlayers;
            
            // Update player count
            if (_playerCountText != null)
            {
                _playerCountText.text = $"Players ({players.Count}/{maxPlayers})";
            }

            // Clear existing items
            foreach (var item in _playerListItems)
            {
                if (item != null) Destroy(item);
            }
            _playerListItems.Clear();

            // Create new items
            foreach (var player in players)
            {
                CreatePlayerListItem(player);
            }

            // Update button states
            UpdateButtonStates();
        }

        private void CreatePlayerListItem(PlayerSessionData player)
        {
            GameObject item = new GameObject($"Player_{player.ClientId}");
            item.transform.SetParent(_playerListContent, false);
            
            // RectTransform with fixed height
            RectTransform itemRect = item.AddComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0, 35);
            
            // Layout element for VerticalLayoutGroup
            LayoutElement layout = item.AddComponent<LayoutElement>();
            layout.preferredHeight = 35;
            layout.flexibleWidth = 1;

            // Background
            Image bg = item.AddComponent<Image>();
            bg.sprite = _roundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = player.JoinedDuringMatch ? waitingColor : new Color(0.25f, 0.25f, 0.3f);

            // Left side: Name + Host badge
            string nameText = player.PlayerName.ToString();
            if (player.IsHost) nameText += " <color=#FFD700>[HOST]</color>";
            
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(item.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.02f, 0);
            nameRect.anchorMax = new Vector2(0.65f, 1);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            
            Text nameTxt = nameObj.AddComponent<Text>();
            nameTxt.text = nameText;
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize = 14;
            nameTxt.color = Color.white;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.supportRichText = true;

            // Right side: Status (right-aligned)
            string statusColor = player.IsReady ? "#33CC33" : "#CC3333";
            string statusText = player.JoinedDuringMatch ? "WAITING" : (player.IsReady ? "READY" : "NOT READY");
            
            GameObject statusObj = new GameObject("Status");
            statusObj.transform.SetParent(item.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.65f, 0);
            statusRect.anchorMax = new Vector2(0.98f, 1);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            
            Text statusTxt = statusObj.AddComponent<Text>();
            statusTxt.text = $"<color={statusColor}><b>{statusText}</b></color>";
            statusTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusTxt.fontSize = 13;
            statusTxt.color = Color.white;
            statusTxt.alignment = TextAnchor.MiddleRight;
            statusTxt.supportRichText = true;

            _playerListItems.Add(item);
        }

        private void RefreshSettings()
        {
            if (GameSessionManager.Instance == null) return;

            var settings = GameSessionManager.Instance.Settings.Value;

            // Update sliders without triggering callbacks
            SetSliderWithoutNotify(_maxPlayersSlider, settings.MaxPlayers);
            SetSliderWithoutNotify(_kavkaziCountSlider, settings.KavkaziCount);
            SetSliderWithoutNotify(_votingTimeSlider, settings.VotingTime);
            SetSliderWithoutNotify(_moveSpeedSlider, settings.MoveSpeed);
            SetSliderWithoutNotify(_killCooldownSlider, settings.KillCooldown);
            SetSliderWithoutNotify(_missionsSlider, settings.MissionsPerInnocent);

            // Update value texts
            _maxPlayersValue.text = settings.MaxPlayers.ToString();
            _kavkaziCountValue.text = settings.KavkaziCount.ToString();
            _votingTimeValue.text = settings.VotingTime.ToString("F0");
            _moveSpeedValue.text = settings.MoveSpeed.ToString("F1");
            _killCooldownValue.text = settings.KillCooldown.ToString("F0");
            _missionsValue.text = settings.MissionsPerInnocent.ToString();

            // Update slider interactability
            UpdateSliderInteractability();
        }

        private void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(value);
            }
        }

        private void OnPhaseChanged(MatchPhase phase)
        {
            Debug.Log($"[LobbyUI] Phase changed to: {phase}");

            switch (phase)
            {
                case MatchPhase.LobbyOpen:
                    _lobbyPanel.SetActive(true);
                    _waitingPanel.SetActive(false);
                    if (_phaseText != null) _phaseText.text = "Lobby Open";
                    break;

                case MatchPhase.MatchInProgress:
                    // Check if we're a late joiner
                    bool isWaiting = GameSessionManager.Instance?.IsPlayerWaiting(NetworkManager.Singleton.LocalClientId) ?? false;
                    
                    if (isWaiting)
                    {
                        // Late joiner - show waiting panel
                        _lobbyPanel.SetActive(false);
                        _waitingPanel.SetActive(true);
                        _canvasObj.SetActive(true);
                        if (_phaseText != null) _phaseText.text = "Match In Progress";
                    }
                    else
                    {
                        // Original player - hide lobby UI, gameplay will show
                        _lobbyPanel.SetActive(false);
                        _waitingPanel.SetActive(false);
                        _canvasObj.SetActive(false);
                    }
                    break;

                case MatchPhase.PostMatch:
                    _lobbyPanel.SetActive(true);
                    _waitingPanel.SetActive(false);
                    _canvasObj.SetActive(true);
                    if (_phaseText != null) _phaseText.text = "Returning to Lobby...";
                    break;
            }

            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            if (NetworkManager.Singleton == null || GameSessionManager.Instance == null) return;

            _isHost = NetworkManager.Singleton.IsServer;
            bool isLobbyOpen = GameSessionManager.Instance.CurrentPhase.Value == MatchPhase.LobbyOpen;

            // Start Game: Host only, lobby phase only
            _startGameButton.gameObject.SetActive(_isHost);
            _startGameButton.interactable = isLobbyOpen && CanStartGame();

            // Ready: Clients only (host is always ready)
            _readyButton.gameObject.SetActive(!_isHost);
            _readyButton.interactable = isLobbyOpen;
            
            // Update ready button text
            if (_readyButtonText != null)
            {
                _readyButtonText.text = _isReady ? "UNREADY" : "READY";
            }

            // Update slider interactability
            UpdateSliderInteractability();
        }

        private void UpdateSliderInteractability()
        {
            bool canEdit = _isHost && GameSessionManager.Instance?.CurrentPhase.Value == MatchPhase.LobbyOpen;
            
            if (_maxPlayersSlider != null) _maxPlayersSlider.interactable = canEdit;
            if (_kavkaziCountSlider != null) _kavkaziCountSlider.interactable = canEdit;
            if (_votingTimeSlider != null) _votingTimeSlider.interactable = canEdit;
            if (_moveSpeedSlider != null) _moveSpeedSlider.interactable = canEdit;
            if (_killCooldownSlider != null) _killCooldownSlider.interactable = canEdit;
            if (_missionsSlider != null) _missionsSlider.interactable = canEdit;
        }

        private bool CanStartGame()
        {
            if (GameSessionManager.Instance == null) return false;

            int eligibleCount = GameSessionManager.Instance.GetEligiblePlayerCount();
            if (eligibleCount < 2) return false;

            // Check all eligible players are ready
            foreach (var player in GameSessionManager.Instance.Players)
            {
                if (!player.JoinedDuringMatch && !player.IsReady)
                {
                    return false;
                }
            }

            return true;
        }

        // ========== BUTTON CALLBACKS ==========

        private void OnSettingChanged()
        {
            if (!_isHost || GameSessionManager.Instance == null) return;
            if (GameSessionManager.Instance.CurrentPhase.Value != MatchPhase.LobbyOpen) return;

            var settings = new LobbySettings
            {
                MaxPlayers = Mathf.RoundToInt(_maxPlayersSlider.value),
                KavkaziCount = Mathf.RoundToInt(_kavkaziCountSlider.value),
                VotingTime = _votingTimeSlider.value,
                MoveSpeed = _moveSpeedSlider.value,
                KillCooldown = _killCooldownSlider.value,
                MissionsPerInnocent = Mathf.RoundToInt(_missionsSlider.value)
            };

            GameSessionManager.Instance.UpdateSettingsServerRpc(settings);
        }

        private void OnStartGameClicked()
        {
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.StartGameServerRpc();
            }
        }

        private void OnReadyClicked()
        {
            _isReady = !_isReady;
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.SetReadyServerRpc(_isReady);
            }
            UpdateButtonStates();
        }

        private void OnLeaveClicked()
        {
            Debug.Log("[LobbyUI] Leave clicked");
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
            SceneManager.LoadScene("MainMenu");
        }

        // ========== UI HELPERS ==========

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            Image img = panel.AddComponent<Image>();
            img.sprite = _roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return panel;
        }

        private GameObject CreateText(Transform parent, string name, string text, int fontSize, FontStyle style,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            Text txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            
            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(200, 40);
            
            return textObj;
        }

        private Button CreateButton(Transform parent, string name, string text, Color color, 
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            
            Image img = btnObj.AddComponent<Image>();
            img.sprite = _roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            
            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = colors;
            
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            Text txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return btn;
        }
        private Sprite CreateRoundedRectSprite(int size = 64, int radius = 16)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            
            // Init clear
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;

            float rOuter = radius;
            float rOuterSq = rOuter * rOuter;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Check corners
                    float dx = 0, dy = 0;

                    if (x < radius && y < radius) // Bottom-Left
                    {
                        dx = radius - x - 0.5f;
                        dy = radius - y - 0.5f;
                    }
                    else if (x < radius && y >= size - radius) // Top-Left
                    {
                        dx = radius - x - 0.5f;
                        dy = y - (size - radius) + 0.5f;
                    }
                    else if (x >= size - radius && y < radius) // Bottom-Right
                    {
                        dx = x - (size - radius) + 0.5f;
                        dy = radius - y - 0.5f;
                    }
                    else if (x >= size - radius && y >= size - radius) // Top-Right
                    {
                        dx = x - (size - radius) + 0.5f;
                        dy = y - (size - radius) + 0.5f;
                    }
                    else
                    {
                        // Not a corner
                        colors[y * size + x] = Color.white;
                        continue;
                    }

                    if (dx * dx + dy * dy <= rOuterSq)
                    {
                        // Antialiasing could go here, but binary alpha is fine for now
                        colors[y * size + x] = Color.white; 
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            
            // Return 9-sliced sprite
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
