using System.Collections.Generic;
using Kavkazim.Netcode;
using Kavkazim.Utils;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Kavkazim.Netcode.Validation;

namespace UI
{
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private Color notReadyColor = UIUtils.ColorNotReady;
        [SerializeField] private Color waitingColor = UIUtils.ColorWaiting;

        private GameObject _canvasObj;
        private GameObject _lobbyPanel;
        private GameObject _waitingPanel;
        private Transform _playerListContent;
        private Text _roomCodeText;

        private Text _playerCountText;
        private Text _phaseText;
        
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
        
        private Toggle _testModeToggle;
        private GameObject _testModeContainer;
        private Text _testModeStatusText;
        
        private Text _validationErrorText;
        private LobbyValidator _validator;

        private Button _startGameButton;
        private Button _readyButton;
        private Button _leaveButton;
        private Text _readyButtonText;
        
        private List<GameObject> _playerListItems = new List<GameObject>();
        
        private Sprite _roundedSprite;

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
                
                RefreshPlayerList();
                RefreshSettings();
                OnPhaseChanged(GameSessionManager.Instance.CurrentPhase.Value);
            }
            else
            {
                Debug.LogWarning("[LobbyUI] GameSessionManager.Instance is null");
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
            if (GameSessionManager.Instance != null)
            {
                OnPhaseChanged(GameSessionManager.Instance.CurrentPhase.Value);
            }
        }

        private void CreateUI()
        {
            _roundedSprite = CreateRoundedRectSprite(64, 8);
            _validator = new LobbyValidator();

            UIUtils.EnsureEventSystem();

            _canvasObj = new GameObject("LobbyCanvas");
            _canvasObj.transform.SetParent(transform, false);
            Canvas canvas = _canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasObj.AddComponent<GraphicRaycaster>();

            CreateLobbyPanel();
            
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

            var titleObj = CreateText(_lobbyPanel.transform, "Title", "LOBBY", 40, FontStyle.Bold, 
                Vector2.zero, Vector2.zero, Vector2.zero);
            titleObj.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.92f);
            titleObj.GetComponent<RectTransform>().anchorMax = new Vector2(0.2f, 0.98f);
            titleObj.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            titleObj.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            titleObj.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

            _roomCodeText = CreateText(_lobbyPanel.transform, "RoomCode", "Code: ----", 30, FontStyle.Bold, 
                 Vector2.zero, Vector2.zero, Vector2.zero).GetComponent<Text>();
            RectTransform codeRect = _roomCodeText.GetComponent<RectTransform>();
            codeRect.anchorMin = new Vector2(0.75f, 0.92f);
            codeRect.anchorMax = new Vector2(0.96f, 0.98f);
            codeRect.offsetMin = Vector2.zero;
            codeRect.offsetMax = Vector2.zero;
            _roomCodeText.color = Color.yellow;
            _roomCodeText.alignment = TextAnchor.MiddleRight;

            CreatePlayerListPanel();
            CreateSettingsPanel();
            CreateButtonPanel();
            UpdateRoomCode();

            _validationErrorText = CreateText(_lobbyPanel.transform, "ValidationError", "", 18, FontStyle.Bold, 
                 Vector2.zero, Vector2.zero, Vector2.zero).GetComponent<Text>();
            
            RectTransform errorRect = _validationErrorText.GetComponent<RectTransform>();
            errorRect.anchorMin = new Vector2(0.02f, 0.87f);
            errorRect.anchorMax = new Vector2(0.5f, 0.92f);
            errorRect.offsetMin = Vector2.zero;
            errorRect.offsetMax = Vector2.zero;
            
            _validationErrorText.color = notReadyColor;
            _validationErrorText.alignment = TextAnchor.MiddleLeft;
        }

        private void CreatePlayerListPanel()
        {
            GameObject listPanel = CreatePanel(_lobbyPanel.transform, "PlayerListPanel", new Color(0.15f, 0.15f, 0.2f, 1f));
            RectTransform listRect = listPanel.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.02f, 0.10f);
            listRect.anchorMax = new Vector2(0.48f, 0.85f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;
            
            VerticalLayoutGroup mainLayout = listPanel.AddComponent<VerticalLayoutGroup>();
            mainLayout.padding = new RectOffset(20, 20, 20, 20);
            mainLayout.spacing = 15;
            mainLayout.childAlignment = TextAnchor.UpperCenter;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = true;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childForceExpandHeight = false;

            GameObject headerObj = CreateText(listPanel.transform, "Header", "Players (0/10)", 32, FontStyle.Bold, Vector2.zero, Vector2.zero, Vector2.zero);
            _playerCountText = headerObj.GetComponent<Text>();
            LayoutElement headerLayout = headerObj.AddComponent<LayoutElement>();
            headerLayout.minHeight = 40;
            headerLayout.preferredHeight = 40;
            headerLayout.flexibleHeight = 0;
            _playerCountText.alignment = TextAnchor.MiddleCenter;

            GameObject content = new GameObject("PlayerListContent");
            content.transform.SetParent(listPanel.transform, false);
            
            LayoutElement contentLayoutElement = content.AddComponent<LayoutElement>();
            contentLayoutElement.flexibleHeight = 1;
            contentLayoutElement.flexibleWidth = 1;

            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 4;
            contentLayout.padding = new RectOffset(2, 2, 2, 2);
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            
            _playerListContent = content.transform;
        }

        private void CreateSettingsPanel()
        {
            GameObject settingsPanel = CreatePanel(_lobbyPanel.transform, "SettingsPanel", new Color(0.15f, 0.15f, 0.2f, 1f));
            RectTransform settingsRect = settingsPanel.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(0.52f, 0.10f);
            settingsRect.anchorMax = new Vector2(0.98f, 0.85f);
            settingsRect.offsetMin = Vector2.zero;
            settingsRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = settingsPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 15;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject headerObj = CreateText(settingsPanel.transform, "Header", "Settings", 32, FontStyle.Bold, Vector2.zero, Vector2.zero, Vector2.zero);
            LayoutElement headerLayout = headerObj.AddComponent<LayoutElement>();
            headerLayout.minHeight = 40;
            headerLayout.preferredHeight = 40;
            headerLayout.flexibleHeight = 0;

            (_maxPlayersSlider, _maxPlayersValue) = CreateSettingSlider(settingsPanel.transform, "Max Players", 4, 10, 10);
            _maxPlayersSlider.wholeNumbers = true;
            _maxPlayersSlider.onValueChanged.AddListener(v => OnSettingChanged());

            (_kavkaziCountSlider, _kavkaziCountValue) = CreateSettingSlider(settingsPanel.transform, "Kavkazi Count", 1, 3, 2);
            _kavkaziCountSlider.wholeNumbers = true;
            _kavkaziCountSlider.onValueChanged.AddListener(v => OnSettingChanged());

            (_votingTimeSlider, _votingTimeValue) = CreateSettingSlider(settingsPanel.transform, "Voting Time (s)", 30, 180, 60);
            _votingTimeSlider.wholeNumbers = true;
            _votingTimeSlider.onValueChanged.AddListener(v => OnSettingChanged());

            (_moveSpeedSlider, _moveSpeedValue) = CreateSettingSlider(settingsPanel.transform, "Move Speed", 0.5f, 5f, 3.5f);
            _moveSpeedSlider.onValueChanged.AddListener(v => OnSettingChanged());

            (_killCooldownSlider, _killCooldownValue) = CreateSettingSlider(settingsPanel.transform, "Kill Cooldown (s)", 5, 60, 15);
            _killCooldownSlider.wholeNumbers = true;
            _killCooldownSlider.onValueChanged.AddListener(v => OnSettingChanged());

            (_missionsSlider, _missionsValue) = CreateSettingSlider(settingsPanel.transform, "Missions Count", 1, 10, 3);
            _missionsSlider.wholeNumbers = true;
            _missionsSlider.onValueChanged.AddListener(v => OnSettingChanged());
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CreateTestModeToggle(settingsPanel.transform);
#endif
        }

        private void CreateTestModeToggle(Transform parent)
        {
            _testModeContainer = new GameObject("TestModeContainer");
            _testModeContainer.transform.SetParent(parent, false);
            
            LayoutElement layout = _testModeContainer.AddComponent<LayoutElement>();
            layout.minHeight = 40;
            layout.preferredHeight = 40;
            layout.flexibleHeight = 0;
            layout.flexibleWidth = 1;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(_testModeContainer.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0.4f, 1);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = "TEST MODE";
            labelText.font = UIUtils.GetDefaultFont();
            labelText.fontSize = 16;
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = new Color(1f, 0.6f, 0.2f);
            labelText.alignment = TextAnchor.MiddleLeft;

            GameObject toggleBtn = new GameObject("TestModeToggleBtn");
            toggleBtn.transform.SetParent(_testModeContainer.transform, false);
            RectTransform toggleBtnRect = toggleBtn.AddComponent<RectTransform>();
            toggleBtnRect.anchorMin = new Vector2(0.42f, 0.2f);
            toggleBtnRect.anchorMax = new Vector2(0.58f, 0.8f);
            toggleBtnRect.offsetMin = Vector2.zero;
            toggleBtnRect.offsetMax = Vector2.zero;
            
            Image btnBgImg = toggleBtn.AddComponent<Image>();
            btnBgImg.sprite = _roundedSprite;
            btnBgImg.type = Image.Type.Sliced;
            btnBgImg.color = new Color(0.25f, 0.25f, 0.3f);

            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(toggleBtn.transform, false);
            RectTransform checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.1f, 0.1f);
            checkRect.anchorMax = new Vector2(0.9f, 0.9f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            
            Image checkImg = checkmark.AddComponent<Image>();
            checkImg.sprite = _roundedSprite;
            checkImg.type = Image.Type.Sliced;
            checkImg.color = new Color(1f, 0.6f, 0.2f);

            _testModeToggle = toggleBtn.AddComponent<Toggle>();
            _testModeToggle.targetGraphic = btnBgImg;
            _testModeToggle.graphic = checkImg;
            _testModeToggle.toggleTransition = Toggle.ToggleTransition.Fade;
            _testModeToggle.isOn = false;
            _testModeToggle.onValueChanged.AddListener(OnTestModeToggled);
            
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(_testModeContainer.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.6f, 0);
            statusRect.anchorMax = new Vector2(1f, 1);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            
            _testModeStatusText = statusObj.AddComponent<Text>();
            _testModeStatusText.text = "OFF";
            _testModeStatusText.font = UIUtils.GetDefaultFont();
            _testModeStatusText.fontSize = 14;
            _testModeStatusText.fontStyle = FontStyle.Bold;
            _testModeStatusText.color = new Color(0.6f, 0.6f, 0.6f);
            _testModeStatusText.alignment = TextAnchor.MiddleLeft;
        }
        
        private void OnTestModeToggled(bool isOn)
        {
            if (_testModeStatusText != null)
            {
                _testModeStatusText.text = isOn ? "ON" : "OFF";
                _testModeStatusText.color = isOn ? new Color(1f, 0.6f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
            }
            
            if (!_isHost || GameSessionManager.Instance == null) return;
            if (GameSessionManager.Instance.CurrentPhase.Value != MatchPhase.LobbyOpen) return;
            
            Debug.Log($"[LobbyUI] Test Mode toggled: {isOn}");
            
            OnSettingChanged();
        }

        private (Slider, Text) CreateSettingSlider(Transform parent, string label, float min, float max, float defaultVal)
        {
            GameObject container = new GameObject(label + "Container");
            container.transform.SetParent(parent, false);
            
            LayoutElement layout = container.AddComponent<LayoutElement>();
            layout.minHeight = 40;
            layout.preferredHeight = 40;
            layout.flexibleHeight = 0;
            layout.flexibleWidth = 1;

            CreateText(container.transform, "Label", label, 20, FontStyle.Normal,
                new Vector2(0, 0), new Vector2(0.4f, 1), Vector2.zero);

            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(container.transform, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.42f, 0.2f);
            sliderRect.anchorMax = new Vector2(0.84f, 0.8f);
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

            Text valueText = CreateText(container.transform, "Value", defaultVal.ToString("F1"), 20, FontStyle.Bold,
                new Vector2(0.86f, 0), new Vector2(0.98f, 1), Vector2.zero).GetComponent<Text>();
            valueText.alignment = TextAnchor.MiddleCenter;

            slider.onValueChanged.AddListener(v => {
                valueText.text = slider.wholeNumbers ? v.ToString("F0") : v.ToString("F1");
            });

            return (slider, valueText);
        }

        private void CreateButtonPanel()
        {
            _startGameButton = CreateButton(_lobbyPanel.transform, "StartGame", "START GAME", 
                UIUtils.ColorReady, new Vector2(0.15f, 0.02f), new Vector2(0.35f, 0.1f));
            _startGameButton.onClick.AddListener(OnStartGameClicked);

            _readyButton = CreateButton(_lobbyPanel.transform, "Ready", "READY", 
                new Color(0.3f, 0.5f, 0.8f), new Vector2(0.4f, 0.02f), new Vector2(0.6f, 0.1f));
            _readyButton.onClick.AddListener(OnReadyClicked);
            _readyButtonText = _readyButton.GetComponentInChildren<Text>();

            _leaveButton = CreateButton(_lobbyPanel.transform, "Leave", "LEAVE", 
                UIUtils.ColorNotReady, new Vector2(0.65f, 0.02f), new Vector2(0.85f, 0.1f));
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

        private void RefreshPlayerList()
        {
            if (GameSessionManager.Instance == null) return;

            var players = GameSessionManager.Instance.Players;
            int maxPlayers = GameSessionManager.Instance.Settings.Value.MaxPlayers;
            
            if (_playerCountText != null)
            {
                _playerCountText.text = $"Players ({players.Count}/{maxPlayers})";
            }

            foreach (var item in _playerListItems)
            {
                if (item != null) Destroy(item);
            }
            _playerListItems.Clear();

            foreach (var player in players)
            {
                CreatePlayerListItem(player);
            }

            UpdateButtonStates();
        }

        private void CreatePlayerListItem(PlayerSessionData player)
        {
            GameObject item = new GameObject($"Player_{player.ClientId}");
            item.transform.SetParent(_playerListContent, false);
            
            LayoutElement layout = item.AddComponent<LayoutElement>();
            layout.minHeight = 65;
            layout.preferredHeight = 65;
            layout.flexibleHeight = 0;
            layout.flexibleWidth = 1;

            Image bg = item.AddComponent<Image>();
            bg.sprite = _roundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = player.JoinedDuringMatch ? waitingColor : new Color(0.25f, 0.25f, 0.3f);

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
            nameTxt.font = UIUtils.GetDefaultFont();
            nameTxt.fontSize = 24;
            nameTxt.color = Color.white;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.supportRichText = true;

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
            statusTxt.font = UIUtils.GetDefaultFont();
            statusTxt.fontSize = 22;
            statusTxt.color = Color.white;
            statusTxt.alignment = TextAnchor.MiddleRight;
            statusTxt.supportRichText = true;

            _playerListItems.Add(item);
        }

        private void RefreshSettings()
        {
            if (GameSessionManager.Instance == null) return;

            var settings = GameSessionManager.Instance.Settings.Value;

            SetSliderWithoutNotify(_maxPlayersSlider, settings.MaxPlayers);
            SetSliderWithoutNotify(_kavkaziCountSlider, settings.KavkaziCount);
            SetSliderWithoutNotify(_votingTimeSlider, settings.VotingTime);
            SetSliderWithoutNotify(_moveSpeedSlider, settings.MoveSpeed);
            SetSliderWithoutNotify(_killCooldownSlider, settings.KillCooldown);
            SetSliderWithoutNotify(_missionsSlider, settings.MissionsPerInnocent);

            _maxPlayersValue.text = settings.MaxPlayers.ToString();
            _kavkaziCountValue.text = settings.KavkaziCount.ToString();
            _votingTimeValue.text = settings.VotingTime.ToString("F0");
            _moveSpeedValue.text = settings.MoveSpeed.ToString("F1");
            _killCooldownValue.text = settings.KillCooldown.ToString("F0");
            _missionsValue.text = settings.MissionsPerInnocent.ToString();
            
            RefreshPlayerList();
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
                    bool isWaiting = GameSessionManager.Instance?.IsPlayerWaiting(NetworkManager.Singleton.LocalClientId) ?? false;
                    
                    if (isWaiting)
                    {
                        _lobbyPanel.SetActive(false);
                        _waitingPanel.SetActive(true);
                        _canvasObj.SetActive(true);
                        if (_phaseText != null) _phaseText.text = "Match In Progress";
                    }
                    else
                    {
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

            _startGameButton.gameObject.SetActive(_isHost);
            _startGameButton.interactable = isLobbyOpen && CanStartGame();

            _readyButton.gameObject.SetActive(!_isHost);
            _readyButton.interactable = isLobbyOpen;
            
            if (_readyButtonText != null)
            {
                _readyButtonText.text = _isReady ? "UNREADY" : "READY";
            }

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
            
            CheckValidation();
        }
        
        private void CheckValidation()
        {
            if (GameSessionManager.Instance == null) return;
            
            var settings = GameSessionManager.Instance.Settings.Value;
            
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = GameSessionManager.Instance.GetEligiblePlayerCount(), IsTestMode = settings.TestMode };
            var result = _validator.Validate(settings, ctx);
            
            if (!result.IsValid)
            {
                if (_validationErrorText != null)
                {
                     _validationErrorText.text = result.Errors[0].Message;
                }
                if (_startGameButton != null && _isHost)
                {
                    _startGameButton.interactable = false;
                }
            }
            else
            {
                if (_validationErrorText != null) _validationErrorText.text = "";
                if (_startGameButton != null && _isHost)
                {
                     _startGameButton.interactable = CanStartGame();
                }
            }
        }

        private bool CanStartGame()
        {
            if (GameSessionManager.Instance == null) return false;

            int eligibleCount = GameSessionManager.Instance.GetEligiblePlayerCount();
            bool isTestMode = GameSessionManager.Instance.Settings.Value.TestMode;
            
            int minPlayers = isTestMode ? 1 : 2;
            if (eligibleCount < minPlayers) return false;

            foreach (var player in GameSessionManager.Instance.Players)
            {
                if (!player.JoinedDuringMatch && !player.IsReady)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnSettingChanged()
        {
            if (!_isHost || GameSessionManager.Instance == null) return;
            if (GameSessionManager.Instance.CurrentPhase.Value != MatchPhase.LobbyOpen) return;

            bool testModeEnabled = _testModeToggle != null && _testModeToggle.isOn;
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            testModeEnabled = false;
#endif

            var settings = new LobbySettings
            {
                MaxPlayers = Mathf.RoundToInt(_maxPlayersSlider.value),
                KavkaziCount = Mathf.RoundToInt(_kavkaziCountSlider.value),
                VotingTime = _votingTimeSlider.value,
                MoveSpeed = _moveSpeedSlider.value,
                KillCooldown = _killCooldownSlider.value,
                MissionsPerInnocent = Mathf.RoundToInt(_missionsSlider.value),
                TestMode = testModeEnabled
            };
            
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = GameSessionManager.Instance.GetEligiblePlayerCount(), IsTestMode = testModeEnabled };
            var sanitized = _validator.Sanitize(settings, ctx);
            
            if (!sanitized.Equals(settings))
            {
                settings = sanitized;
                
                SetSliderWithoutNotify(_maxPlayersSlider, settings.MaxPlayers);
                SetSliderWithoutNotify(_kavkaziCountSlider, settings.KavkaziCount);
                SetSliderWithoutNotify(_votingTimeSlider, settings.VotingTime);
                SetSliderWithoutNotify(_moveSpeedSlider, settings.MoveSpeed);
                SetSliderWithoutNotify(_killCooldownSlider, settings.KillCooldown);
                SetSliderWithoutNotify(_missionsSlider, settings.MissionsPerInnocent);
                RefreshSettings(); 
            }

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
            txt.font = UIUtils.GetDefaultFont();
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
            txt.font = UIUtils.GetDefaultFont();
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
            
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;

            float rOuter = radius;
            float rOuterSq = rOuter * rOuter;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0, dy = 0;

                    if (x < radius && y < radius)
                    {
                        dx = radius - x - 0.5f;
                        dy = radius - y - 0.5f;
                    }
                    else if (x < radius && y >= size - radius)
                    {
                        dx = radius - x - 0.5f;
                        dy = y - (size - radius) + 0.5f;
                    }
                    else if (x >= size - radius && y < radius)
                    {
                        dx = x - (size - radius) + 0.5f;
                        dy = radius - y - 0.5f;
                    }
                    else if (x >= size - radius && y >= size - radius)
                    {
                        dx = x - (size - radius) + 0.5f;
                        dy = y - (size - radius) + 0.5f;
                    }
                    else
                    {
                        colors[y * size + x] = Color.white;
                        continue;
                    }

                    if (dx * dx + dy * dy <= rOuterSq)
                    {
                        colors[y * size + x] = Color.white; 
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
