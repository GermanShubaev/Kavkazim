using Kavkazim.Netcode;
using Netcode;
using Netcode.Player;
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
        
        // Kill cooldown UI
        private GameObject _cooldownContainer;
        private Image _cooldownFill;
        private Text _cooldownText;
        private KillerAbility _localKillerAbility;
        private PlayerAvatar _localAvatar;

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

        private void Update()
        {
            UpdateCooldownUI();
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
            
            // 5. Create Kill Cooldown UI (Bottom Left)
            CreateCooldownUI();
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
            
            // Only show for Kavkazi players
            bool isKavkazi = _localAvatar.Role.Value == PlayerRoleType.Kavkazi;
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

