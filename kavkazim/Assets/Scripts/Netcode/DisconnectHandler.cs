using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Netcode
{
    public class DisconnectHandler : MonoBehaviour
    {
        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
                NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
                NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (NetworkManager.Singleton != null && 
                !NetworkManager.Singleton.IsHost && 
                !NetworkManager.Singleton.IsServer)
            {
                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    Debug.Log("[DisconnectHandler] Lost connection to server. Returning to main menu...");
                    HandleDisconnection();
                }
            }
        }

        private void OnTransportFailure()
        {
            Debug.LogWarning("[DisconnectHandler] Transport failure detected. Returning to main menu...");
            HandleDisconnection();
        }

        private void HandleDisconnection()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }

            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                SceneManager.LoadScene("MainMenu");
            }
        }
    }
}
