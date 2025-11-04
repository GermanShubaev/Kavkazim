using System.Threading.Tasks;
using Kavkazim.Netcode;
using Kavkazim.Services;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Kavkazim.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private Button leaveLobbyButton;

        private INetworkBootstrap _bootstrap;
        private IUnityAuthService _auth;

        private void Awake()
        {
            // Manual DI for now
            _auth = new UnityAuthService();
            var relay = new UnityRelayService();
            var lobby = new UnityLobbyService();
            _bootstrap = new NetworkBootstrap(_auth, relay, lobby);

            hostButton.onClick.AddListener(() => _ = OnHostClicked());
            quickJoinButton.onClick.AddListener(() => _ = OnQuickJoinClicked());
            leaveLobbyButton.onClick.AddListener(() => _ = OnLeaveClicked());
            leaveLobbyButton.interactable = false;
        }

        private async Task OnHostClicked()
        {
            await _auth.InitializeAsync();
            await _auth.SignInAnonymouslyAsync(nameInput.text);

            bool ok = await _bootstrap.HostWithRelayAsync("Kavkazim Lobby", 10);
            if (ok)
            {
                leaveLobbyButton.interactable = true;
                UnityEngine.SceneManagement.SceneManager.LoadScene("_Project/Scenes/Gameplay_SkeldLike");
            }
        }

        private async Task OnQuickJoinClicked()
        {
            await _auth.InitializeAsync();
            await _auth.SignInAnonymouslyAsync(nameInput.text);

            bool ok = await _bootstrap.QuickJoinAsync();
            if (ok)
            {
                leaveLobbyButton.interactable = true;
                UnityEngine.SceneManagement.SceneManager.LoadScene("_Project/Scenes/Gameplay_SkeldLike");
            }
        }

        private async Task OnLeaveClicked()
        {
            await _bootstrap.LeaveLobbyAsync();
            if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost)
            {
                if (NetworkManager.Singleton.IsHost) NetworkManager.Singleton.Shutdown();
                else NetworkManager.Singleton.Shutdown();
            }
            leaveLobbyButton.interactable = false;
        }
    }
}
