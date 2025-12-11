using Netcode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class GameplayUI : MonoBehaviour
    {
        private GameObject _panel;
        private bool _isPanelOpen = false;

        private GameObject _canvasObj;

        private void Start()
        {
            CreateUI();
            SceneManager.activeSceneChanged += OnSceneChanged;
            UpdateVisibility(SceneManager.GetActiveScene());
            
            // Add disconnect handler for clients
            if (!gameObject.GetComponent<DisconnectHandler>())
            {
                gameObject.AddComponent<DisconnectHandler>();
            }
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            UpdateVisibility(newScene);
        }

        private void UpdateVisibility(Scene scene)
        {
            if (_canvasObj)
            {
                // Hide in MainMenu, show elsewhere (Gameplay)
                bool isMainMenu = scene.name == "MainMenu"; 
                _canvasObj.SetActive(!isMainMenu);

                // Ensure EventSystem exists if we are active
                if (!isMainMenu && FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
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
    }
}
