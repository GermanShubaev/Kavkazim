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
            _lobby = new UnityLobbyService(); // if you move to UMS later, swap here
            _bootstrap = new NetworkBootstrap(_auth, _relay, _lobby);

            // Wire buttons
            hostButton.onClick.AddListener(() => _ = OnHostClicked());
            quickJoinButton.onClick.AddListener(() => _ = OnQuickJoinClicked());
            leaveLobbyButton.onClick.AddListener(() => _ = OnLeaveClicked());

            leaveLobbyButton.interactable = false;
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
                    // IMPORTANT: host tells all clients to load "Gameplay"
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

        private async Task OnQuickJoinClicked()
        {
            SetUIInteractable(false);
            try
            {
                await _auth.InitializeAsync();
                await _auth.SignInAnonymouslyAsync(nameInput.text);

                bool ok = await _bootstrap.QuickJoinAsync();
                if (ok)
                {
                    // Do not call LoadScene here.
                    // Client will automatically follow the host's networked scene load.
                    leaveLobbyButton.interactable = true;
                }
                else
                {
                    Debug.LogWarning("QuickJoin failed (no lobby found?)");
                }
            }
            finally { SetUIInteractable(true); }
        }

        private async Task OnLeaveClicked()
        {
            SetUIInteractable(false);
            try
            {
                await _bootstrap.LeaveLobbyAsync();
                if (NetworkManager.Singleton && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
                {
                    NetworkManager.Singleton.Shutdown();
                }
                leaveLobbyButton.interactable = false;
            }
            finally { SetUIInteractable(true); }
        }

        private void SetUIInteractable(bool state)
        {
            nameInput.interactable = state;
            hostButton.interactable = state;
            quickJoinButton.interactable = state;
            // leaveLobbyButton stays as set by flow.
        }
    }
}
