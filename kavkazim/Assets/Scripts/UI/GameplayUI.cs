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
using Kavkazim.Utils;

namespace UI
{
    public class GameplayUI : MonoBehaviour
    {
        public static GameplayUI Instance { get; private set; }
        
        private GameObject _panel;
        private bool _isPanelOpen = false;

        private GameObject _canvasObj;
        
        private GameObject _cooldownContainer;
        private Image _cooldownFill;
        private Text _cooldownText;
        private KillerAbility _localKillerAbility;
        private PlayerAvatar _localAvatar;
        
        private ReportUIController _reportUIController;
        private IReportInput _reportInput;
        private PlayerState _localPlayerState;
        
        private GameObject _taskListContainer;
        private GameObject _taskListContentContainer;
        private Dictionary<ulong, List<Task>> _taskAssignments;
        private HashSet<Task> _completedTasks = new HashSet<Task>();
        private List<GameObject> _taskTextObjects = new List<GameObject>();
        
        private GameObject _progressBarContainer;
        private GameObject _progressBarBackground;
        private List<Image> _progressBarStripes = new List<Image>();
        private int _totalTasks = 0;
        private string _lastTaskUiSignature = "";

        private void Awake()
        {
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
            if (_canvasObj == null)
            {
                CreateUI();
            }
            
            SceneManager.activeSceneChanged += OnSceneChanged;
            UpdateVisibility(SceneManager.GetActiveScene());
            
            if (!gameObject.GetComponent<DisconnectHandler>())
            {
                gameObject.AddComponent<DisconnectHandler>();
            }
            
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.TasksLeft.OnValueChanged += OnTasksLeftChanged;
                GameSessionManager.Instance.OnPhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.TasksLeft.OnValueChanged -= OnTasksLeftChanged;
                GameSessionManager.Instance.OnPhaseChanged -= OnPhaseChanged;
            }
            
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            UpdateCooldownUI();
            UpdateReportUI();
            HandleReportInput();
            UpdateTaskListUI();
            
            if (_progressBarContainer != null && _totalTasks == 0 && GameSessionManager.Instance != null)
            {
                int tasksLeft = GameSessionManager.Instance.TasksLeft.Value;
                if (tasksLeft > 0)
                {
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
            _localAvatar = null;
            _localKillerAbility = null;
            _localPlayerState = null;
            
            UpdateVisibility(newScene);
        }

        private void UpdateVisibility(Scene scene)
        {
            if (_canvasObj)
            {
                bool shouldHide = scene.name == "MainMenu" || 
                                  scene.name == "MeetingScene" || 
                                  scene.name == "WinScreen";
                _canvasObj.SetActive(!shouldHide);

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
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                CreateEventSystem();
            }

            _canvasObj = new GameObject("GameplayCanvas");
            _canvasObj.transform.SetParent(transform, false);
            Canvas canvas = _canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasObj.AddComponent<GraphicRaycaster>();

            GameObject settingsBtnObj = CreateButton(_canvasObj.transform, "SettingsButton", "Settings", new Vector2(160, 60), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, -40));
            settingsBtnObj.GetComponent<Button>().onClick.AddListener(TogglePanel);

            _panel = new GameObject("SettingsPanel");
            _panel.transform.SetParent(_canvasObj.transform, false);
            Image panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);
            RectTransform panelRect = _panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(300, 250);
            panelRect.anchoredPosition = Vector2.zero;
            _panel.SetActive(false);

            GameObject codeTextObj = new GameObject("RoomCodeText");
            codeTextObj.transform.SetParent(_panel.transform, false);
            Text codeText = codeTextObj.AddComponent<Text>();
            codeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            codeText.alignment = TextAnchor.MiddleCenter;
            codeText.color = Color.yellow;
            codeText.fontSize = 20;
            
            string code = "Unknown";
            if (NetworkBootstrap.Instance != null)
            {
                code = NetworkBootstrap.Instance.LobbyCode ?? "None";
            }
            codeText.text = $"Room Code: {code}";

            RectTransform codeRect = codeTextObj.GetComponent<RectTransform>();
            codeRect.sizeDelta = new Vector2(280, 40);
            codeRect.anchoredPosition = new Vector2(0, 60);

            GameObject leaveBtnObj = CreateButton(_panel.transform, "LeaveButton", "Leave Game", new Vector2(200, 50), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20));
            leaveBtnObj.GetComponent<Button>().onClick.AddListener(OnLeaveClicked);
            
            CreateCooldownUI();
            CreateReportUI();
            CreateTaskListUI();
            CreateTaskProgressBar();
        }

        private void CreateCooldownUI()
        {
            _cooldownContainer = new GameObject("KillCooldownUI");
            _cooldownContainer.transform.SetParent(_canvasObj.transform, false);
            RectTransform containerRect = _cooldownContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1, 0);
            containerRect.anchorMax = new Vector2(1, 0);
            containerRect.pivot = new Vector2(1, 0);
            containerRect.sizeDelta = new Vector2(120, 120);
            containerRect.anchoredPosition = new Vector2(-60, 60);
            
            Sprite circleSprite = CreateCircleSprite();

            GameObject bgCircle = new GameObject("Background");
            bgCircle.transform.SetParent(_cooldownContainer.transform, false);
            Image bgImage = bgCircle.AddComponent<Image>();
            bgImage.sprite = circleSprite;
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            RectTransform bgRect = bgCircle.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            GameObject fillCircle = new GameObject("Fill");
            fillCircle.transform.SetParent(_cooldownContainer.transform, false);
            _cooldownFill = fillCircle.AddComponent<Image>();
            _cooldownFill.sprite = circleSprite;
            _cooldownFill.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            _cooldownFill.type = Image.Type.Filled;
            _cooldownFill.fillMethod = Image.FillMethod.Radial360;
            _cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            _cooldownFill.fillClockwise = true;
            _cooldownFill.fillAmount = 1f;
            RectTransform fillRect = fillCircle.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.1f, 0.1f);
            fillRect.anchorMax = new Vector2(0.9f, 0.9f);
            fillRect.sizeDelta = Vector2.zero;
            
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(_cooldownContainer.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = CreateKnifeSprite();
            iconImg.color = Color.white;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(40, 40);
            iconRect.anchoredPosition = new Vector2(0, 15);
            
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

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = new Vector2(0, -10);
            
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
                    
                    float distance = Mathf.Sqrt(distSq);
                    float alpha = Mathf.Clamp01(radius - distance);

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
            for (int i = 0; i < width * height; i++) texture.SetPixel(i % width, i / width, Color.clear);
            
            for (int y = 4; y < 12; y++)
            {
                for (int x = 14; x < 18; x++)
                {
                     texture.SetPixel(x, y, new Color(0.4f, 0.2f, 0.1f, 1f));
                }
            }
            for (int x = 10; x < 22; x++)
            {
                texture.SetPixel(x, 12, Color.gray);
                texture.SetPixel(x, 13, Color.gray);
            }
            for (int y = 14; y < 28; y++)
            {
                for (int x = 14; x < 18; x++)
                {
                     texture.SetPixel(x, y, new Color(0.8f, 0.8f, 0.9f, 1f));
                }
            }
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
            if (_localKillerAbility == null || _localAvatar == null)
            {
                TryFindLocalPlayer();
            }
            
            if (_localAvatar == null)
            {
                if (_cooldownContainer != null)
                    _cooldownContainer.SetActive(false);
                return;
            }
            
            bool isKavkazi = _localAvatar.PerceivedRole == PlayerRoleType.Kavkazi;
            if (_cooldownContainer != null)
            {
                _cooldownContainer.SetActive(isKavkazi && _localKillerAbility != null);
            }
            
            if (isKavkazi && _localKillerAbility != null && _cooldownFill != null && _cooldownText != null)
            {
                float remaining = _localKillerAbility.RemainingCooldown;
                bool isReady = _localKillerAbility.IsKillReady;
                
                if (isReady)
                {
                    _cooldownFill.fillAmount = 1f;
                    _cooldownFill.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
                    _cooldownText.text = "KILL";
                    _cooldownText.color = Color.white;
                }
                else
                {
                    float totalCooldown = 15f;
                    if (_localKillerAbility.CooldownEndTime.Value > 0)
                    {
                        float elapsed = totalCooldown - remaining;
                        float progress = Mathf.Clamp01(elapsed / totalCooldown);
                        _cooldownFill.fillAmount = progress;
                    }
                    else
                    {
                        _cooldownFill.fillAmount = 0f;
                    }
                    
                    _cooldownFill.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                    _cooldownText.text = $"{remaining:F1}s";
                    _cooldownText.color = Color.white;
                }
            }
        }

        private void TryFindLocalPlayer()
        {
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

        private void CreateReportUI()
        {
            _reportUIController = new ReportUIController(_canvasObj.transform);
            _reportUIController.CreateUI();
            
            _reportInput = new KeyboardReportInput(KeyCode.L);
        }

        private void UpdateReportUI()
        {
            if (_localPlayerState == null)
            {
                TryFindLocalPlayerState();
            }
            
            if (_reportUIController != null)
            {
                _reportUIController.UpdateUI(_localPlayerState);
            }
        }

        private void HandleReportInput()
        {
            if (_reportInput == null || _localPlayerState == null)
                return;
            
            if (!_localPlayerState.IsAlive.Value)
                return;
            
            if (_reportInput.WantsToReport())
            {
                ReportService.TryReport(_localPlayerState);
            }
        }

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
            SceneManager.LoadScene("MainMenu");
        }

        private void CreateTaskListUI()
        {
            _taskListContainer = new GameObject("TaskListUI");
            _taskListContainer.transform.SetParent(_canvasObj.transform, false);
            RectTransform containerRect = _taskListContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 1);
            containerRect.anchorMax = new Vector2(0, 1);
            containerRect.pivot = new Vector2(0, 1);
            containerRect.sizeDelta = new Vector2(300, 100);
            containerRect.anchoredPosition = new Vector2(20, -20);
            
            Image bgImage = _taskListContainer.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            
            VerticalLayoutGroup mainLayoutGroup = _taskListContainer.AddComponent<VerticalLayoutGroup>();
            mainLayoutGroup.childAlignment = TextAnchor.UpperLeft;
            mainLayoutGroup.childControlWidth = true;
            mainLayoutGroup.childControlHeight = false;
            mainLayoutGroup.childForceExpandWidth = true;
            mainLayoutGroup.childForceExpandHeight = false;
            mainLayoutGroup.spacing = 5f;
            mainLayoutGroup.padding = new RectOffset(10, 10, 10, 10);
            
            ContentSizeFitter containerFitter = _taskListContainer.AddComponent<ContentSizeFitter>();
            containerFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            containerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
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
            
            LayoutElement titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 25;
            titleLayout.flexibleHeight = 0;
            
            _taskListContentContainer = new GameObject("TaskListContent");
            _taskListContentContainer.transform.SetParent(_taskListContainer.transform, false);
            RectTransform contentRect = _taskListContentContainer.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 0);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;
            
            VerticalLayoutGroup layoutGroup = _taskListContentContainer.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.spacing = 2f;
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            
            ContentSizeFitter contentFitter = _taskListContentContainer.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            LayoutElement contentLayout = _taskListContentContainer.AddComponent<LayoutElement>();
            contentLayout.flexibleHeight = 1;
            
            _taskListContainer.SetActive(false);
            _taskAssignments = new Dictionary<ulong, List<Task>>();
        }

        private void UpdateTaskListUI()
        {
            if (_localAvatar == null)
            {
                TryFindLocalPlayer();
            }
            
            if (_localAvatar == null || _taskListContainer == null || _taskListContentContainer == null)
            {
                return;
            }
            
            bool isInnocent = _localAvatar.PerceivedRole == PlayerRoleType.Innocent;
            if (_taskListContainer.activeSelf != isInnocent)
            {
                _taskListContainer.SetActive(isInnocent);
            }
            
            if (!isInnocent) return;
            
            // Task assignments are now synchronized from server via GameSessionManager.DistributeAndSyncTasks()
            
            ulong localClientId = _localAvatar.OwnerClientId;
            List<Task> playerTasks = new List<Task>();
            
            if (_taskAssignments != null && _taskAssignments.ContainsKey(localClientId))
            {
                playerTasks = _taskAssignments[localClientId];
            }
            
            // Check if we need to rebuild the UI
            // Build a string signature of the current tasks state to detect changes
            System.Text.StringBuilder currentSignature = new System.Text.StringBuilder();
            if (playerTasks.Count > 0)
            {
                foreach(var t in playerTasks)
                {
                    currentSignature.Append($"{t.MinigameType}_{t.Location}_{IsTaskCompleted(t)}|");
                }
            }
            else
            {
                currentSignature.Append("NoTasks");
            }
            
            string newSignature = currentSignature.ToString();
            if (_lastTaskUiSignature == newSignature)
            {
                return; // UI is up to date
            }
            _lastTaskUiSignature = newSignature;
            
            // Clear existing task text objects before rebuilding
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
                List<Task> incompleteTasks = new List<Task>();
                for (int i = 0; i < playerTasks.Count; i++)
                {
                    var task = playerTasks[i];
                    if (!IsTaskCompleted(task))
                    {
                        incompleteTasks.Add(task);
                    }
                }
                
                for (int i = 0; i < incompleteTasks.Count; i++)
                {
                    var task = incompleteTasks[i];
                    string taskText = $"{i + 1}. {task.Description}";
                        
                    GameObject taskTextObj = new GameObject($"Task_{i}");
                    taskTextObj.transform.SetParent(_taskListContentContainer.transform, false);
                    Text text = taskTextObj.AddComponent<Text>();
                    text.text = taskText;
                    text.font = UIUtils.GetDefaultFont();
                    text.alignment = TextAnchor.MiddleLeft;
                    text.fontSize = 24;
                    text.resizeTextForBestFit = false;
                        
                    text.color = Color.white;
                        
                    RectTransform rect = taskTextObj.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(0, 1);
                    rect.sizeDelta = new Vector2(0, 30);
                        
                    LayoutElement layoutElement = taskTextObj.AddComponent<LayoutElement>();
                    layoutElement.preferredHeight = 30;
                    layoutElement.flexibleHeight = 0;
                        
                    _taskTextObjects.Add(taskTextObj);
                }
            }
            else
            {
                GameObject noTasksObj = new GameObject("NoTasksText");
                noTasksObj.transform.SetParent(_taskListContentContainer.transform, false);
                Text noTasksText = noTasksObj.AddComponent<Text>();
                noTasksText.text = "No tasks assigned yet.";
                noTasksText.font = UIUtils.GetDefaultFont();
                noTasksText.alignment = TextAnchor.MiddleLeft;
                noTasksText.color = Color.white;
                noTasksText.fontSize = 14;
                
                RectTransform rect = noTasksObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(0, 30);
                
                LayoutElement layoutElement = noTasksObj.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = 30;
                layoutElement.flexibleHeight = 0;
                
                _taskTextObjects.Add(noTasksObj);
            }
            
            if (_taskListContentContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_taskListContentContainer.GetComponent<RectTransform>());
            }
            
            if (_taskListContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_taskListContainer.GetComponent<RectTransform>());
            }
        }

        public void SetTaskAssignments(Dictionary<ulong, List<Task>> assignments)
        {
            _taskAssignments = assignments;
            int totalTasks = 0;
            if (_taskAssignments != null)
            {
                foreach (var assignment in _taskAssignments)
                {
                    totalTasks += assignment.Value.Count;
                }
            }
            _totalTasks = totalTasks;
            
            // Update the progress bar
            UpdateTaskProgressBar();
            
            // Update the task list UI
            UpdateTaskListUI();
        }

        public Dictionary<ulong, List<Task>> GetTaskAssignments()
        {
            return _taskAssignments;
        }

        public void MarkTaskAsCompleted(Task task)
        {
            if (task == null) return;
            
            if (!IsTaskCompleted(task))
            {
                _completedTasks.Add(task);
                
                Debug.Log($"[GameplayUI] Task marked as completed: {task.Description}");
                
                UpdateTaskListUI();
            }
        }

        public bool IsTaskCompleted(Task task)
        {
            if (task == null) return false;
            
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

        private void CreateTaskProgressBar()
        {
            _progressBarContainer = new GameObject("TaskProgressBar");
            _progressBarContainer.transform.SetParent(_canvasObj.transform, false);
            RectTransform containerRect = _progressBarContainer.AddComponent<RectTransform>();
            
            containerRect.anchorMin = new Vector2(0.5f, 1f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.sizeDelta = new Vector2(600, 40);
            containerRect.anchoredPosition = new Vector2(0, -20);
            
            _progressBarBackground = new GameObject("ProgressBarBackground");
            _progressBarBackground.transform.SetParent(_progressBarContainer.transform, false);
            Image bgImage = _progressBarBackground.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            RectTransform bgRect = _progressBarBackground.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
        }

        private void UpdateTaskProgressBar()
        {
            if (_progressBarContainer == null || _progressBarBackground == null) return;
            if (GameSessionManager.Instance == null) return;

            int tasksLeft = GameSessionManager.Instance.TasksLeft.Value;
            
            if (_totalTasks == 0 && tasksLeft > 0)
            {
                _totalTasks = tasksLeft;
            }
            
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
            
            if (_totalTasks == 0) return;

            if (!_progressBarContainer.activeSelf)
            {
                _progressBarContainer.SetActive(true);
            }

            foreach (var stripe in _progressBarStripes)
            {
                if (stripe != null && stripe.gameObject != null)
                {
                    Destroy(stripe.gameObject);
                }
            }
            _progressBarStripes.Clear();

            int completedTasks = _totalTasks - tasksLeft;
            float stripeWidth = 1.0f / _totalTasks;

            for (int i = 0; i < _totalTasks; i++)
            {
                GameObject stripeObj = new GameObject($"Stripe_{i}");
                stripeObj.transform.SetParent(_progressBarBackground.transform, false);
                Image stripeImage = stripeObj.AddComponent<Image>();
                
                if (i < completedTasks)
                {
                    stripeImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);
                }
                else
                {
                    stripeImage.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }
                
                RectTransform stripeRect = stripeObj.GetComponent<RectTransform>();
                stripeRect.anchorMin = new Vector2(i * stripeWidth, 0);
                stripeRect.anchorMax = new Vector2((i + 1) * stripeWidth, 1);
                stripeRect.sizeDelta = Vector2.zero;
                stripeRect.anchoredPosition = Vector2.zero;
                
                _progressBarStripes.Add(stripeImage);
            }
        }

        private void OnTasksLeftChanged(int previousValue, int newValue)
        {
            UpdateTaskProgressBar();
        }

        private void OnPhaseChanged(MatchPhase phase)
        {
            if (phase == MatchPhase.LobbyOpen)
            {
                ResetGameplayUI();
            }
        }

        public void ResetGameplayUI()
        {
            if (_taskListContainer) _taskListContainer.SetActive(false);
            if (_progressBarContainer) _progressBarContainer.SetActive(false);
            if (_cooldownContainer) _cooldownContainer.SetActive(false);
            
            _taskAssignments = null;
            _completedTasks.Clear();
            _totalTasks = 0;
            _lastTaskUiSignature = "";
            
            foreach (var taskObj in _taskTextObjects)
            {
                if (taskObj != null) Destroy(taskObj);
            }
            _taskTextObjects.Clear();

            foreach (var stripe in _progressBarStripes)
            {
                if (stripe != null) Destroy(stripe.gameObject);
            }
            _progressBarStripes.Clear();
        }
    }
}

