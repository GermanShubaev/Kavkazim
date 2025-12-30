using Kavkazim.Netcode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

namespace UI
{
    /// <summary>
    /// Controller for the WinScreen scene.
    /// Displays winning team, winner names, and handles return to lobby.
    /// This script should be attached to a GameObject in the WinScreen scene.
    /// </summary>
    public class WinScreenSceneController : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float autoReturnDelay = 10f; // Auto-return after 10 seconds

        // UI References (created dynamically or assigned in inspector)
        private GameObject _canvas;
        private CanvasGroup _canvasGroup;
        private Text _titleText;
        private Text _winnersText;
        private Text _reasonText;
        private Button _returnButton;
        private Text _countdownText;

        private bool _isReturning = false;
        private float _returnCountdown;
        private WinResultData _cachedWinResult;

        private void Start()
        {
            // Get win result from static cache (set before scene load)
            // GameSessionManager.Instance won't exist as it was destroyed with previous scene
            _cachedWinResult = GameSessionManager.CachedWinResult;
            
            // Validate we have valid data
            if (!_cachedWinResult.HasEnded)
            {
                Debug.LogWarning("[WinScreenSceneController] No cached win result found, using default");
                _cachedWinResult = new WinResultData
                {
                    WinningTeam = 0,
                    WinnerNames = "Unknown",
                    ReasonKey = "unknown",
                    HasEnded = true
                };
            }
            else
            {
                Debug.Log($"[WinScreenSceneController] Loaded win result: {_cachedWinResult.GetWinningTeamDisplay()}");
            }

            CreateUI();
            DisplayWinResult(_cachedWinResult);
            StartCoroutine(FadeIn());
            
            // Start countdown for auto-return
            _returnCountdown = autoReturnDelay;
        }

        private void Update()
        {
            // Update countdown
            if (!_isReturning && _returnCountdown > 0)
            {
                _returnCountdown -= Time.deltaTime;
                if (_countdownText != null)
                {
                    _countdownText.text = $"Returning to lobby in {Mathf.CeilToInt(_returnCountdown)}s";
                }
                
                if (_returnCountdown <= 0)
                {
                    OnReturnClicked();
                }
            }
        }

        private void CreateUI()
        {
            // Create Canvas
            _canvas = new GameObject("WinScreenCanvas");
            Canvas canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _canvas.AddComponent<CanvasScaler>();
            _canvas.AddComponent<GraphicRaycaster>();

            // Add CanvasGroup for fading
            _canvasGroup = _canvas.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;

            // Ensure EventSystem exists
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(_canvas.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 1f); // Dark blue-gray
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Content container
            GameObject content = new GameObject("Content");
            content.transform.SetParent(_canvas.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(600, 500);

            // Victory banner background
            GameObject bannerBg = new GameObject("BannerBg");
            bannerBg.transform.SetParent(content.transform, false);
            Image bannerImage = bannerBg.AddComponent<Image>();
            bannerImage.color = new Color(0, 0, 0, 0.6f);
            RectTransform bannerRect = bannerBg.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0, 0.7f);
            bannerRect.anchorMax = new Vector2(1, 1);
            bannerRect.sizeDelta = Vector2.zero;

            // Title text - "KAVKAZIS WIN!" or "INNOCENTS WIN!"
            _titleText = CreateText(content.transform, "TitleText", "VICTORY!", 56,
                new Vector2(0, 170), Color.white, FontStyle.Bold);

            // Reason text
            _reasonText = CreateText(content.transform, "ReasonText", "", 22,
                new Vector2(0, 100), new Color(0.8f, 0.8f, 0.8f), FontStyle.Italic);

            // Winners header
            CreateText(content.transform, "WinnersHeader", "— WINNERS —", 28,
                new Vector2(0, 40), Color.yellow, FontStyle.Bold);

            // Winners list panel
            GameObject winnersPanel = new GameObject("WinnersPanel");
            winnersPanel.transform.SetParent(content.transform, false);
            Image panelImage = winnersPanel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.4f);
            RectTransform panelRect = winnersPanel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(400, 150);
            panelRect.anchoredPosition = new Vector2(0, -60);

            // Winners list
            _winnersText = CreateText(winnersPanel.transform, "WinnersText", "", 24,
                Vector2.zero, Color.white, FontStyle.Normal);
            RectTransform winnersRect = _winnersText.GetComponent<RectTransform>();
            winnersRect.anchorMin = Vector2.zero;
            winnersRect.anchorMax = Vector2.one;
            winnersRect.sizeDelta = new Vector2(-20, -20);
            winnersRect.anchoredPosition = Vector2.zero;

            // Return button
            GameObject btnObj = CreateButton(content.transform, "ReturnButton", "RETURN TO LOBBY",
                new Vector2(280, 60), new Vector2(0, -180));
            _returnButton = btnObj.GetComponent<Button>();
            _returnButton.onClick.AddListener(OnReturnClicked);

            // Countdown text
            _countdownText = CreateText(content.transform, "CountdownText", "", 16,
                new Vector2(0, -230), new Color(0.6f, 0.6f, 0.6f), FontStyle.Normal);
        }

        private Text CreateText(Transform parent, string name, string text, int fontSize,
            Vector2 position, Color color, FontStyle style)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            Text txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.fontSize = fontSize;
            txt.fontStyle = style;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(550, fontSize + 30);
            rect.anchoredPosition = position;

            return txt;
        }

        private GameObject CreateButton(Transform parent, string name, string text,
            Vector2 size, Vector2 position)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.2f);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.5f, 0.2f);
            colors.highlightedColor = new Color(0.3f, 0.6f, 0.3f);
            colors.pressedColor = new Color(0.15f, 0.4f, 0.15f);
            btn.colors = colors;

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            Text txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return btnObj;
        }

        private void DisplayWinResult(WinResultData result)
        {
            // Set title with team color
            string teamName = result.GetWinningTeamDisplay();
            Color teamColor = result.WinningTeam == 2 
                ? new Color(1f, 0.3f, 0.3f) // Red for Kavkazi
                : new Color(0.3f, 0.8f, 1f); // Cyan for Innocent

            _titleText.text = $"{teamName.ToUpper()} WIN!";
            _titleText.color = teamColor;

            // Set reason
            _reasonText.text = result.GetReasonDisplay();

            // Set winner names
            string[] winners = result.GetWinnerNamesList();
            if (winners.Length > 0)
            {
                _winnersText.text = string.Join("\n", winners);
            }
            else
            {
                _winnersText.text = "(No winners)";
            }
        }

        private void OnReturnClicked()
        {
            if (_isReturning) return;
            _isReturning = true;

            _returnButton.interactable = false;
            _countdownText.text = "Returning...";

            // Only the server/host can trigger the return
            // Clients will automatically sync when server loads the new scene
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                Debug.Log("[WinScreenSceneController] Server requesting return to lobby...");
                NetworkManager.Singleton.SceneManager.LoadScene("GameSession", LoadSceneMode.Single);
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                Debug.Log("[WinScreenSceneController] Client waiting for server to load lobby scene...");
                // Client just waits - server will trigger scene sync
                _countdownText.text = "Waiting for host...";
            }
            else
            {
                // Fallback: not connected, just load scene directly (for testing)
                Debug.Log("[WinScreenSceneController] Not connected, loading scene directly...");
                SceneManager.LoadScene("GameSession");
            }
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1;
        }
    }
}
