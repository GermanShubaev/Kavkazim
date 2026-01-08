using Kavkazim.Netcode;
using Kavkazim.Netcode.Reporting;
using Netcode;
using Netcode.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Kavkazim.UI;
using Minigames.Progress;
using System.Collections.Generic;

namespace UI
{
    public class GameplayUI : MonoBehaviour
    {
        public static GameplayUI Instance { get; private set; }
        
        private GameObject _panel;
        private bool _isPanelOpen = false;

        private GameObject _canvasObj;
        
        // Kill cooldown UI
        private GameObject _cooldownContainer;
        private Image _cooldownFill;
        private Text _cooldownText;
        private KillerAbility _localKillerAbility;
        private PlayerAvatar _localAvatar;
        
        // Report system
        private ReportUIController _reportUIController;
        private IReportInput _reportInput;
        private PlayerState _localPlayerState;
        
        // Task list UI
        private GameObject _taskListContainer;
        private GameObject _taskListContentContainer;
        private Dictionary<ulong, List<Task>> _taskAssignments;
        private HashSet<Task> _completedTasks = new HashSet<Task>();
        private List<GameObject> _taskTextObjects = new List<GameObject>();
        
        // Task progress bar UI
        private GameObject _progressBarContainer;
        private GameObject _progressBarBackground;
        private List<Image> _progressBarStripes = new List<Image>();
        private int _totalTasks = 0;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameplayUI] Duplicate detected! Destroying self. Existing Instance: {Instance.GetInstanceID()}, This: {GetInstanceID()}");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Only create UI if it doesn't exist yet
            if (_canvasObj == null)
            {
                CreateUI();
            }
            
            SceneManager.activeSceneChanged += OnSceneChanged;
            UpdateVisibility(SceneManager.GetActiveScene());
            
            // Add disconnect handler for clients
            if (!gameObject.GetComponent<DisconnectHandler>())
            {
                gameObject.AddComponent<DisconnectHandler>();
            }
            
            // Subscribe to TasksLeft changes
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.TasksLeft.OnValueChanged += OnTasksLeftChanged;
            }
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            
            // Unsubscribe from TasksLeft changes
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.TasksLeft.OnValueChanged -= OnTasksLeftChanged;
            }
            
            // Clear singleton reference if this is the instance
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            UpdateCooldownUI();
            UpdateReportUI();
            HandleReportInput(); // L key handles both body reports AND emergency meetings
            UpdateTaskListUI();
            
            // Initialize progress bar if TasksLeft is available but we haven't set it up yet
            if (_progressBarContainer != null && _totalTasks == 0 && GameSessionManager.Instance != null)
            {
                int tasksLeft = GameSessionManager.Instance.TasksLeft.Value;
                if (tasksLeft > 0)
                {
                    // Try to get total from task assignments if available
                    if (_taskAssignments != null && _taskAssignments.Count > 0)
                    {
                        int totalTasks = 0;
                        foreach (var assignment in _taskAssignments)
                        {
                            totalTasks += assignment.Value.Count;
                        }
                        if (totalTasks > 0)
                        {
                            _totalTasks = totalTasks;
                            UpdateTaskProgressBar();
                        }
                    }
                }
            }
        }

        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            // Clear cached player references - they're destroyed on scene change
            _localAvatar = null;
            _localKillerAbility = null;
            _localPlayerState = null;
            
            UpdateVisibility(newScene);
        }

        private void UpdateVisibility(Scene scene)
        {
            if (_canvasObj)
            {
                // Only show in GameSession scene during gameplay (not lobby, meeting, etc.)
                // Hide in MainMenu, MeetingScene, WinScreen
                bool shouldHide = scene.name == "MainMenu" || 
                                  scene.name == "MeetingScene" || 
                                  scene.name == "WinScreen";
                _canvasObj.SetActive(!shouldHide);

                // Ensure EventSystem exists if we are active
                if (!shouldHide && FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    CreateEventSystem();
                }
            }
        }

        private void CreateEventSystem()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private void CreateUI()
        {
            // 0. Ensure EventSystem
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                CreateEventSystem();
            }

            // 1. Create Canvas
            _canvasObj = new GameObject("GameplayCanvas");
            _canvasObj.transform.SetParent(transform, false); // Keep with this object
            Canvas canvas = _canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasObj.AddComponent<CanvasScaler>();
            _canvasObj.AddComponent<GraphicRaycaster>();

            // 2. Create Settings Button (Top Right)
            GameObject settingsBtnObj = CreateButton(_canvasObj.transform, "SettingsButton", "Settings", new Vector2(160, 60), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, -40));
            settingsBtnObj.GetComponent<Button>().onClick.AddListener(TogglePanel);

            // 3. Create Panel (Center)
            _panel = new GameObject("SettingsPanel");
            _panel.transform.SetParent(_canvasObj.transform, false);
            Image panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);
            RectTransform panelRect = _panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(300, 250); // Increased height
            panelRect.anchoredPosition = Vector2.zero;
            _panel.SetActive(false);

            // 3.1 Room Code Text
            GameObject codeTextObj = new GameObject("RoomCodeText");
            codeTextObj.transform.SetParent(_panel.transform, false);
            Text codeText = codeTextObj.AddComponent<Text>();
            codeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            codeText.alignment = TextAnchor.MiddleCenter;
            codeText.color = Color.yellow;
            codeText.fontSize = 20;
            
            // Get code from Bootstrap
            string code = "Unknown";
            if (NetworkBootstrap.Instance != null)
            {
                code = NetworkBootstrap.Instance.LobbyCode ?? "None";
            }
            codeText.text = $"Room Code: {code}";

            RectTransform codeRect = codeTextObj.GetComponent<RectTransform>();
            codeRect.sizeDelta = new Vector2(280, 40);
            codeRect.anchoredPosition = new Vector2(0, 60); // Top of panel

            // 4. Create Leave Button inside Panel
            GameObject leaveBtnObj = CreateButton(_panel.transform, "LeaveButton", "Leave Game", new Vector2(200, 50), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20));
            leaveBtnObj.GetComponent<Button>().onClick.AddListener(OnLeaveClicked);
            
            // 5. Create Kill Cooldown UI (Bottom Left)
            CreateCooldownUI();
            
            // 6. Create Report UI (positioned above Kill icon)
            CreateReportUI();
            
            // 7. Create Task List UI (Upper Left)
            CreateTaskListUI();
            
            // 8. Create Task Progress Bar (Upper Center)
            CreateTaskProgressBar();
        }

        private void CreateCooldownUI()
        {
            // Container for cooldown UI (bottom right)
            _cooldownContainer = new GameObject("KillCooldownUI");
            _cooldownContainer.transform.SetParent(_canvasObj.transform, false);
            RectTransform containerRect = _cooldownContainer.AddComponent<RectTransform>();
            // Anchor bottom-right
            containerRect.anchorMin = new Vector2(1, 0);
            containerRect.anchorMax = new Vector2(1, 0);
            containerRect.pivot = new Vector2(1, 0);
            containerRect.sizeDelta = new Vector2(120, 120);
            containerRect.anchoredPosition = new Vector2(-60, 60); // increased padding from edges
            
            // Generate a circle sprite
            Sprite circleSprite = CreateCircleSprite();

            // Background circle
            GameObject bgCircle = new GameObject("Background");
            bgCircle.transform.SetParent(_cooldownContainer.transform, false);
            Image bgImage = bgCircle.AddComponent<Image>();
            bgImage.sprite = circleSprite;
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            RectTransform bgRect = bgCircle.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            // Fill circle (radial fill for cooldown)
            GameObject fillCircle = new GameObject("Fill");
            fillCircle.transform.SetParent(_cooldownContainer.transform, false);
            _cooldownFill = fillCircle.AddComponent<Image>();
            _cooldownFill.sprite = circleSprite;
            _cooldownFill.color = new Color(1f, 0.2f, 0.2f, 0.9f); // Red
            _cooldownFill.type = Image.Type.Filled;
            _cooldownFill.fillMethod = Image.FillMethod.Radial360;
            _cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            _cooldownFill.fillClockwise = true;
            _cooldownFill.fillAmount = 1f;
            RectTransform fillRect = fillCircle.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.1f, 0.1f);
            fillRect.anchorMax = new Vector2(0.9f, 0.9f);
            fillRect.sizeDelta = Vector2.zero;
            
            // Icon (Image instead of Text)
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(_cooldownContainer.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = CreateKnifeSprite();
            iconImg.color = Color.white;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(40, 40); // Size of the icon
            iconRect.anchoredPosition = new Vector2(0, 15); // Slightly raised
            
            // Cooldown text (shows seconds remaining or KILL)
            GameObject textObj = new GameObject("CooldownText");
            textObj.transform.SetParent(_cooldownContainer.transform, false);
            _cooldownText = textObj.AddComponent<Text>();
            _cooldownText.text = "KILL";
            _cooldownText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _cooldownText.alignment = TextAnchor.MiddleCenter;
            _cooldownText.color = Color.white;
            _cooldownText.fontSize = 16;
            _cooldownText.fontStyle = FontStyle.Bold;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            // Full circle area for centering
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = new Vector2(0, -10); // Slightly lowered below icon
            
            // Initially hidden (only show for Kavkazi players)
            _cooldownContainer.SetActive(false);
        }

        private Sprite CreateCircleSprite()
        {
            int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float center = size / 2f;
            float radius = size / 2f;
            float radiusSq = radius * radius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distSq = dx * dx + dy * dy;
                    
                    // Simple anti-aliasing
                    float distance = Mathf.Sqrt(distSq);
                    float alpha = Mathf.Clamp01(radius - distance); // 1 inside, 0 outside, gradient at edge

                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            texture.SetPixels(colors);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateKnifeSprite()
        {
            int width = 32;
            int height = 32;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            // Clear to transparent
            for (int i = 0; i < width * height; i++) texture.SetPixel(i % width, i / width, Color.clear);
            
            // Draw a simple knife/sword shape
            // Handle (brown/dark)
            for (int y = 4; y < 12; y++)
            {
                for (int x = 14; x < 18; x++)
                {
                     texture.SetPixel(x, y, new Color(0.4f, 0.2f, 0.1f, 1f));
                }
            }
            // Guard (grey)
            for (int x = 10; x < 22; x++)
            {
                texture.SetPixel(x, 12, Color.gray);
                texture.SetPixel(x, 13, Color.gray);
            }
            // Blade (silver)
            for (int y = 14; y < 28; y++)
            {
                for (int x = 14; x < 18; x++)
                {
                     texture.SetPixel(x, y, new Color(0.8f, 0.8f, 0.9f, 1f));
                }
            }
            // Tip (pointed)
            for (int y = 28; y < 31; y++)
            {
                 int offset = y - 28;
                 for (int x = 14 + offset; x < 18 - offset; x++)
                 {
                     texture.SetPixel(x, y, new Color(0.8f, 0.8f, 0.9f, 1f));
                 }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        private void UpdateCooldownUI()
        {
            // Try to find local player's KillerAbility if not cached
            if (_localKillerAbility == null || _localAvatar == null)
            {
                TryFindLocalPlayer();
            }
            
            // No local player found yet
            if (_localAvatar == null)
            {
                if (_cooldownContainer != null)
                    _cooldownContainer.SetActive(false);
                return;
            }
            
            // Only show for Kavkazi players (use PerceivedRole since Role is OwnerOnly)
            bool isKavkazi = _localAvatar.PerceivedRole == PlayerRoleType.Kavkazi;
            if (_cooldownContainer != null)
            {
                _cooldownContainer.SetActive(isKavkazi && _localKillerAbility != null);
            }
            
            // Update cooldown display
            if (isKavkazi && _localKillerAbility != null && _cooldownFill != null && _cooldownText != null)
            {
                float remaining = _localKillerAbility.RemainingCooldown;
                bool isReady = _localKillerAbility.IsKillReady;
                
                if (isReady)
                {
                    _cooldownFill.fillAmount = 1f;
                    _cooldownFill.color = new Color(0.2f, 0.8f, 0.2f, 0.9f); // Green when ready
                    _cooldownText.text = "KILL";
                    _cooldownText.color = Color.white;
                }
                else
                {
                    // Calculate fill based on cooldown progress
                    float totalCooldown = 15f; // Default, could get from config
                    if (_localKillerAbility.CooldownEndTime.Value > 0)
                    {
                        // Estimate total cooldown from current state
                        float elapsed = totalCooldown - remaining;
                        float progress = Mathf.Clamp01(elapsed / totalCooldown);
                        _cooldownFill.fillAmount = progress;
                    }
                    else
                    {
                        _cooldownFill.fillAmount = 0f;
                    }
                    
                    _cooldownFill.color = new Color(1f, 0.2f, 0.2f, 0.9f); // Red when on cooldown
                    _cooldownText.text = $"{remaining:F1}s";
                    _cooldownText.color = Color.white;
                }
            }
        }

        private void TryFindLocalPlayer()
        {
            // Find all PlayerAvatars and get the local one
            PlayerAvatar[] avatars = FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
            foreach (var avatar in avatars)
            {
                if (avatar.IsOwner)
                {
                    _localAvatar = avatar;
                    _localKillerAbility = avatar.GetComponent<KillerAbility>();
                    break;
                }
            }
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            
            Image img = btnObj.AddComponent<Image>();
            img.color = Color.white;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f);
            colors.highlightedColor = new Color(1f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            btn.colors = colors;

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            Text txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            txt.fontSize = 24;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return btnObj;
        }

        /// <summary>
        /// Creates the Report UI component.
        /// </summary>
        private void CreateReportUI()
        {
            _reportUIController = new ReportUIController(_canvasObj.transform);
            _reportUIController.CreateUI();
            
            // Initialize keyboard input (can be swapped for mobile button later)
            _reportInput = new KeyboardReportInput(KeyCode.L);
        }

        /// <summary>
        /// Updates the Report UI state each frame.
        /// </summary>
        private void UpdateReportUI()
        {
            // Try to find local player if not cached
            if (_localPlayerState == null)
            {
                TryFindLocalPlayerState();
            }
            
            // Update report UI visibility and state
            if (_reportUIController != null)
            {
                _reportUIController.UpdateUI(_localPlayerState);
            }
        }

        /// <summary>
        /// Handles report input from keyboard.
        /// Also handles Emergency Button interaction via ReportService.
        /// </summary>
        private void HandleReportInput()
        {
            if (_reportInput == null || _localPlayerState == null)
                return;
            
            // Only process input if player is alive
            if (!_localPlayerState.IsAlive.Value)
                return;
            
            if (_reportInput.WantsToReport())
            {
                // Attempt to report via static ReportService
                ReportService.TryReport(_localPlayerState);
            }
        }

        /// <summary>
        /// Tries to find the local player's PlayerState.
        /// </summary>
        private void TryFindLocalPlayerState()
        {
            PlayerState[] players = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player.IsOwner)
                {
                    _localPlayerState = player;
                    break;
                }
            }
        }

        private void TogglePanel()
        {
            _isPanelOpen = !_isPanelOpen;
            _panel.SetActive(_isPanelOpen);
        }

        private void OnLeaveClicked()
        {
            if (NetworkManager.Singleton)
            {
                NetworkManager.Singleton.Shutdown();
            }
            // Attempt to load MainMenu. 
            // Note: If we were host, Shutdown destroys the NetworkManager (unless DontDestroyOnLoad is set differently).
            // We assume "MainMenu" is the name of the scene.
            SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// Creates the task list UI in the upper left corner.
        /// </summary>
        private void CreateTaskListUI()
        {
            // Container for task list (upper left)
            _taskListContainer = new GameObject("TaskListUI");
            _taskListContainer.transform.SetParent(_canvasObj.transform, false);
            RectTransform containerRect = _taskListContainer.AddComponent<RectTransform>();
            // Anchor top-left
            containerRect.anchorMin = new Vector2(0, 1);
            containerRect.anchorMax = new Vector2(0, 1);
            containerRect.pivot = new Vector2(0, 1);
            containerRect.sizeDelta = new Vector2(300, 100); // Initial size, will be adjusted by ContentSizeFitter
            containerRect.anchoredPosition = new Vector2(20, -20); // Padding from top-left corner
            
            // Background panel
            Image bgImage = _taskListContainer.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            
            // Add VerticalLayoutGroup to main container to stack title and content
            VerticalLayoutGroup mainLayoutGroup = _taskListContainer.AddComponent<VerticalLayoutGroup>();
            mainLayoutGroup.childAlignment = TextAnchor.UpperLeft;
            mainLayoutGroup.childControlWidth = true;
            mainLayoutGroup.childControlHeight = false;
            mainLayoutGroup.childForceExpandWidth = true;
            mainLayoutGroup.childForceExpandHeight = false;
            mainLayoutGroup.spacing = 5f;
            mainLayoutGroup.padding = new RectOffset(10, 10, 10, 10);
            
            // Add ContentSizeFitter to main container so it adjusts to content
            ContentSizeFitter containerFitter = _taskListContainer.AddComponent<ContentSizeFitter>();
            containerFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            containerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Title text
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_taskListContainer.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Tasks:";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.alignment = TextAnchor.UpperLeft;
            titleText.color = Color.white;
            titleText.fontSize = 18;
            titleText.fontStyle = FontStyle.Bold;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0, 1);
            titleRect.sizeDelta = new Vector2(0, 25);
            
            // Add LayoutElement to title
            LayoutElement titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 25;
            titleLayout.flexibleHeight = 0;
            
            // Task list content container (holds individual task text elements)
            _taskListContentContainer = new GameObject("TaskListContent");
            _taskListContentContainer.transform.SetParent(_taskListContainer.transform, false);
            RectTransform contentRect = _taskListContentContainer.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 0);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;
            
            // Add VerticalLayoutGroup to stack tasks vertically
            VerticalLayoutGroup layoutGroup = _taskListContentContainer.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.spacing = 2f; // Small spacing between tasks
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            
            // Add ContentSizeFitter to content container so it expands based on children
            ContentSizeFitter contentFitter = _taskListContentContainer.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Add LayoutElement to content container
            LayoutElement contentLayout = _taskListContentContainer.AddComponent<LayoutElement>();
            contentLayout.flexibleHeight = 1;
            
            // Initially hidden until tasks are assigned
            _taskListContainer.SetActive(false);
            _taskAssignments = new Dictionary<ulong, List<Task>>();
        }

        /// <summary>
        /// Updates the task list UI with the current player's tasks.
        /// </summary>
        private void UpdateTaskListUI()
        {
            // Try to find local player if not cached
            if (_localAvatar == null)
            {
                TryFindLocalPlayer();
            }
            
            // No local player found yet
            if (_localAvatar == null || _taskListContainer == null || _taskListContentContainer == null)
            {
                return;
            }
            
            // Only show for innocent players
            bool isInnocent = _localAvatar.PerceivedRole == PlayerRoleType.Innocent;
            
            // Check if we need to distribute tasks (server only, once)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && 
                (_taskAssignments == null || _taskAssignments.Count == 0))
            {
                // Distribute tasks when game starts
                if (GameSessionManager.Instance != null && 
                    GameSessionManager.Instance.CurrentPhase.Value == MatchPhase.MatchInProgress)
                {
                    _taskAssignments = TaskDistributor.DistributeTasksToInnocentPlayers();
                    Debug.Log($"[GameplayUI] Distributed tasks to {_taskAssignments.Count} players on server.");
                    
                    // Initialize tasksLeft with total task count
                    if (GameSessionManager.Instance != null)
                    {
                        int totalTasks = 0;
                        foreach (var assignment in _taskAssignments)
                        {
                            totalTasks += assignment.Value.Count;
                        }
                        GameSessionManager.Instance.TasksLeft.Value = totalTasks;
                        _totalTasks = totalTasks; // Store total for progress bar
                        Debug.Log($"[GameplayUI] Initialized TasksLeft to {totalTasks}");
                        
                        // Update progress bar with initial state
                        UpdateTaskProgressBar();
                    }
                }
            }
            
            // Get tasks for local player
            ulong localClientId = _localAvatar.OwnerClientId;
            List<Task> playerTasks = new List<Task>();
            
            if (_taskAssignments != null && _taskAssignments.ContainsKey(localClientId))
            {
                playerTasks = _taskAssignments[localClientId];
            }
            else if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            {
                // Client: For now, clients can't see tasks until network sync is added
                // TODO: Add RPC to sync tasks from server to clients
                // For testing, we can distribute locally, but this won't match server
                if (isInnocent && GameSessionManager.Instance != null && 
                    GameSessionManager.Instance.CurrentPhase.Value == MatchPhase.MatchInProgress)
                {
                    // Temporary: distribute locally for client testing (not ideal, but works for now)
                    if (_taskAssignments == null || _taskAssignments.Count == 0)
                    {
                        _taskAssignments = TaskDistributor.DistributeTasksToInnocentPlayers();
                        Debug.LogWarning("[GameplayUI] Client distributed tasks locally - this may not match server!");
                    }
                    if (_taskAssignments != null && _taskAssignments.ContainsKey(localClientId))
                    {
                        playerTasks = _taskAssignments[localClientId];
                    }
                }
            }
            
            // Update UI visibility and content
            if (_taskListContainer != null)
            {
                _taskListContainer.SetActive(isInnocent);
            }
            
            if (isInnocent && _taskListContentContainer != null)
            {
                // Clear existing task text objects
                foreach (var taskObj in _taskTextObjects)
                {
                    if (taskObj != null)
                    {
                        Destroy(taskObj);
                    }
                }
                _taskTextObjects.Clear();
                
                if (playerTasks.Count > 0)
                {
                    // Filter out completed tasks - they should no longer be visible
                    List<Task> incompleteTasks = new List<Task>();
                    for (int i = 0; i < playerTasks.Count; i++)
                    {
                        var task = playerTasks[i];
                        if (!IsTaskCompleted(task))
                        {
                            incompleteTasks.Add(task);
                        }
                    }
                    
                    // Create individual text elements for each incomplete task
                    for (int i = 0; i < incompleteTasks.Count; i++)
                    {
                        var task = incompleteTasks[i];
                        string taskText = $"{i + 1}. {task.Description}";
                        
                        // Create text object for this task
                        GameObject taskTextObj = new GameObject($"Task_{i}");
                        taskTextObj.transform.SetParent(_taskListContentContainer.transform, false);
                        Text text = taskTextObj.AddComponent<Text>();
                        text.text = taskText;
                        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        text.alignment = TextAnchor.MiddleLeft;
                        text.fontSize = 44; // Twice the size (22 * 2)
                        text.resizeTextForBestFit = false;
                        
                        // Set color: white for all visible tasks (completed tasks are hidden)
                        text.color = Color.white;
                        
                        RectTransform rect = taskTextObj.GetComponent<RectTransform>();
                        // Set anchors to stretch horizontally, fixed height per row
                        rect.anchorMin = new Vector2(0, 1);
                        rect.anchorMax = new Vector2(1, 1);
                        rect.pivot = new Vector2(0, 1);
                        rect.sizeDelta = new Vector2(0, 50); // Increased height for larger font
                        
                        // Add LayoutElement to control sizing
                        LayoutElement layoutElement = taskTextObj.AddComponent<LayoutElement>();
                        layoutElement.preferredHeight = 50; // Increased height for larger font
                        layoutElement.flexibleHeight = 0;
                        
                        _taskTextObjects.Add(taskTextObj);
                    }
                }
                else
                {
                    // Show "No tasks" message
                    GameObject noTasksObj = new GameObject("NoTasksText");
                    noTasksObj.transform.SetParent(_taskListContentContainer.transform, false);
                    Text noTasksText = noTasksObj.AddComponent<Text>();
                    noTasksText.text = "No tasks assigned yet.";
                    noTasksText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    noTasksText.alignment = TextAnchor.MiddleLeft;
                    noTasksText.color = Color.white;
                    noTasksText.fontSize = 14;
                    
                    RectTransform rect = noTasksObj.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(0, 1);
                    rect.sizeDelta = new Vector2(0, 30);
                    
                    // Add LayoutElement
                    LayoutElement layoutElement = noTasksObj.AddComponent<LayoutElement>();
                    layoutElement.preferredHeight = 30;
                    layoutElement.flexibleHeight = 0;
                    
                    _taskTextObjects.Add(noTasksObj);
                }
                
                // Force layout rebuild to update container size
                if (_taskListContentContainer != null)
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_taskListContentContainer.GetComponent<RectTransform>());
                }
                
                // Force layout rebuild on main container to adjust to content
                if (_taskListContainer != null)
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_taskListContainer.GetComponent<RectTransform>());
                }
            }
        }

        /// <summary>
        /// Sets the task assignments (called from server or when synced).
        /// </summary>
        public void SetTaskAssignments(Dictionary<ulong, List<Task>> assignments)
        {
            _taskAssignments = assignments;
        }

        /// <summary>
        /// Gets the current task assignments dictionary.
        /// </summary>
        public Dictionary<ulong, List<Task>> GetTaskAssignments()
        {
            return _taskAssignments;
        }

        /// <summary>
        /// Marks a task as completed. Called when a minigame is finished.
        /// </summary>
        public void MarkTaskAsCompleted(Task task)
        {
            if (task == null) return;
            
            // Check if already completed to avoid duplicates
            if (!IsTaskCompleted(task))
            {
                // Add to completed tasks set
                _completedTasks.Add(task);
                
                Debug.Log($"[GameplayUI] Task marked as completed: {task.Description}");
                
                // Force UI update to show completed status
                UpdateTaskListUI();
            }
        }

        /// <summary>
        /// Checks if a task is completed.
        /// </summary>
        public bool IsTaskCompleted(Task task)
        {
            if (task == null) return false;
            
            // Compare tasks by position and type (not reference)
            float positionTolerance = 0.1f;
            foreach (var completedTask in _completedTasks)
            {
                if (completedTask != null &&
                    Vector2.Distance(completedTask.Location, task.Location) < positionTolerance &&
                    completedTask.MinigameType == task.MinigameType)
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Creates the task progress bar in the upper center of the screen.
        /// Shows a dark grey rectangle with light grey stripes that turn green as tasks are completed.
        /// </summary>
        private void CreateTaskProgressBar()
        {
            // Container for progress bar (upper center)
            _progressBarContainer = new GameObject("TaskProgressBar");
            _progressBarContainer.transform.SetParent(_canvasObj.transform, false);
            RectTransform containerRect = _progressBarContainer.AddComponent<RectTransform>();
            
            // Anchor top-center
            containerRect.anchorMin = new Vector2(0.5f, 1f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.sizeDelta = new Vector2(600, 40);
            containerRect.anchoredPosition = new Vector2(0, -20); // Padding from top
            
            // Background (dark grey rectangle)
            _progressBarBackground = new GameObject("ProgressBarBackground");
            _progressBarBackground.transform.SetParent(_progressBarContainer.transform, false);
            Image bgImage = _progressBarBackground.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // Dark grey
            RectTransform bgRect = _progressBarBackground.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Updates the progress bar with the current number of tasks.
        /// Creates stripes based on total tasks and colors them green as tasks are completed.
        /// </summary>
        private void UpdateTaskProgressBar()
        {
            if (_progressBarContainer == null || _progressBarBackground == null) return;
            if (GameSessionManager.Instance == null) return;

            int tasksLeft = GameSessionManager.Instance.TasksLeft.Value;
            
            // Initialize total tasks on first update if not set
            if (_totalTasks == 0 && tasksLeft > 0)
            {
                _totalTasks = tasksLeft;
            }
            
            // If we still don't have a total, try to calculate from task assignments
            if (_totalTasks == 0 && _taskAssignments != null && _taskAssignments.Count > 0)
            {
                int totalTasks = 0;
                foreach (var assignment in _taskAssignments)
                {
                    totalTasks += assignment.Value.Count;
                }
                if (totalTasks > 0)
                {
                    _totalTasks = totalTasks;
                }
            }
            
            // Don't update if we don't have a total yet
            if (_totalTasks == 0) return;

            // Clear existing stripes
            foreach (var stripe in _progressBarStripes)
            {
                if (stripe != null && stripe.gameObject != null)
                {
                    Destroy(stripe.gameObject);
                }
            }
            _progressBarStripes.Clear();

            // Create stripes
            int completedTasks = _totalTasks - tasksLeft;
            float stripeWidth = 1.0f / _totalTasks; // Each stripe takes up 1/totalTasks of the width

            for (int i = 0; i < _totalTasks; i++)
            {
                GameObject stripeObj = new GameObject($"Stripe_{i}");
                stripeObj.transform.SetParent(_progressBarBackground.transform, false);
                Image stripeImage = stripeObj.AddComponent<Image>();
                
                // Color: green if completed, light grey if not
                if (i < completedTasks)
                {
                    stripeImage.color = new Color(0.2f, 0.8f, 0.2f, 1f); // Green
                }
                else
                {
                    stripeImage.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Light grey
                }
                
                RectTransform stripeRect = stripeObj.GetComponent<RectTransform>();
                stripeRect.anchorMin = new Vector2(i * stripeWidth, 0);
                stripeRect.anchorMax = new Vector2((i + 1) * stripeWidth, 1);
                stripeRect.sizeDelta = Vector2.zero;
                stripeRect.anchoredPosition = Vector2.zero;
                
                _progressBarStripes.Add(stripeImage);
            }
        }

        /// <summary>
        /// Called when TasksLeft NetworkVariable changes.
        /// </summary>
        private void OnTasksLeftChanged(int previousValue, int newValue)
        {
            UpdateTaskProgressBar();
        }
    }
}

