using System.Threading.Tasks;
using Kavkazim.Netcode;
using Kavkazim.Services;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Kavkazim.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private Button leaveLobbyButton;

        // Services (simple manual DI)
        private IUnityAuthService _auth;
        private IUnityRelayService _relay;
        private IUnityLobbyService _lobby;
        private INetworkBootstrap _bootstrap;

        // Popup elements
        private GameObject _codePopup;
        private TMP_InputField _codeInput;

        private void Awake()
        {
            // Validate refs
            if (!networkManager) networkManager = FindFirstObjectByType<NetworkManager>();

            if (!nameInput || !hostButton || !quickJoinButton || !leaveLobbyButton)
            {
                Debug.LogError("MainMenuUI: Assign all UI references in Inspector.");
                enabled = false; return;
            }

            // Instantiate services
            _auth = new UnityAuthService();
            _relay = new UnityRelayService();
            _lobby = new UnityLobbyService();
            _bootstrap = new NetworkBootstrap(_auth, _relay, _lobby);

            // Wire buttons
            hostButton.onClick.AddListener(() => _ = OnHostClicked());
            quickJoinButton.onClick.AddListener(ShowRoomCodePopup);
            leaveLobbyButton.onClick.AddListener(OnLeaveClicked);
            leaveLobbyButton.interactable = true; // Force enable in case it's disabled in Inspector

            CreateRoomCodePopup();
        }

        private void CreateRoomCodePopup()
        {
            // Create a popup panel (will be shown/hidden as needed)
            _codePopup = new GameObject("RoomCodePopup");
            _codePopup.transform.SetParent(transform, false);

            // Need a Canvas for this popup
            Canvas canvas = _codePopup.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // On top
            _codePopup.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Semi-transparent background
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(_codePopup.transform, false);
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Panel
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(_codePopup.transform, false);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(400, 200);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);

            // Title Text
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "Enter Room Code";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 24;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(380, 40);
            titleRect.anchoredPosition = new Vector2(0, 60);

            // Input Field
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(panel.transform, false);
            Image inputBg = inputObj.AddComponent<Image>();
            inputBg.color = Color.white;
            _codeInput = inputObj.AddComponent<TMP_InputField>();
            _codeInput.textComponent = CreateTextComponent(inputObj.transform, "Text", 18, Color.black);
            _codeInput.placeholder = CreateTextComponent(inputObj.transform, "Placeholder", 18, new Color(0.5f, 0.5f, 0.5f));
            ((TMP_Text)_codeInput.placeholder).text = "Enter code or leave empty...";
            RectTransform inputRect = inputObj.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(360, 40);
            inputRect.anchoredPosition = new Vector2(0, 10);

            // Join Button
            GameObject joinBtn = CreateButton(panel.transform, "JoinButton", "Join", new Vector2(150, 40), new Vector2(-90, -50));
            joinBtn.GetComponent<Button>().onClick.AddListener(() => _ = OnJoinWithCode());

            // Cancel Button
            GameObject cancelBtn = CreateButton(panel.transform, "CancelButton", "Cancel", new Vector2(150, 40), new Vector2(90, -50));
            cancelBtn.GetComponent<Button>().onClick.AddListener(HideRoomCodePopup);

            _codePopup.SetActive(false);
        }

        private TMP_Text CreateTextComponent(Transform parent, string name, int fontSize, Color color)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            TMP_Text text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);
            return text;
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector2 size, Vector2 pos)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.6f, 0.9f);
            Button btn = btnObj.AddComponent<Button>();
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            Text txt = txtObj.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 18;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            return btnObj;
        }

        private void ShowRoomCodePopup()
        {
            _codeInput.text = "";
            _codePopup.SetActive(true);
        }

        private void HideRoomCodePopup()
        {
            _codePopup.SetActive(false);
        }

        private async Task OnHostClicked()
        {
            SetUIInteractable(false);
            try
            {
                await _auth.InitializeAsync();
                await _auth.SignInAnonymouslyAsync(nameInput.text);

                bool ok = await _bootstrap.HostWithRelayAsync("Kavkazim Lobby", 10);
                if (ok)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene(
                        "Gameplay",
                        LoadSceneMode.Single
                    );
                    leaveLobbyButton.interactable = true;
                }
                else
                {
                    Debug.LogError("StartHost failed");
                }
            }
            finally { SetUIInteractable(true); }
        }

        private async Task OnJoinWithCode()
        {
            HideRoomCodePopup();
            SetUIInteractable(false);
            try
            {
                await _auth.InitializeAsync();
                await _auth.SignInAnonymouslyAsync(nameInput.text);

                bool ok = false;
                string code = _codeInput.text.Trim();

                if (!string.IsNullOrEmpty(code))
                {
                    Debug.Log($"Joining by code: {code}");
                    try 
                    {
                        ok = await _bootstrap.JoinByCodeAsync(code);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Join by code failed: {e.Message}");
                    }
                }
                else
                {
                    Debug.Log("Quick Joining...");
                    try
                    {
                        ok = await _bootstrap.QuickJoinAsync();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Quick Join failed: {e.Message}");
                    }
                }

                if (ok)
                {
                    leaveLobbyButton.interactable = true;
                }
                else
                {
                    Debug.LogWarning("Join failed");
                }
            }
            finally { SetUIInteractable(true); }
        }

        private void OnLeaveClicked()
        {
            Debug.Log("Leave/Quit button clicked. Exiting game...");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void SetUIInteractable(bool state)
        {
            nameInput.interactable = state;
            hostButton.interactable = state;
            quickJoinButton.interactable = state;
        }
    }
}
