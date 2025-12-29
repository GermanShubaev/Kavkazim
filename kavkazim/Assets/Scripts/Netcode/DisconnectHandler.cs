using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Netcode
{
    /// <summary>
    /// Handles client disconnection - returns clients to main menu if host disconnects.
    /// Attach this to a persistent GameObject or NetworkManager.
    /// </summary>
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
            // If we are a client (not host/server) and OUR local client disconnected,
            // it means we lost connection to the server
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
            // Transport failure means network error - also return to menu
            Debug.LogWarning("[DisconnectHandler] Transport failure detected. Returning to main menu...");
            HandleDisconnection();
        }

        private void HandleDisconnection()
        {
            // Shutdown the network connection
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // Return to main menu (avoid reloading if already there)
            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                SceneManager.LoadScene("MainMenu");
            }
        }
    }
}
