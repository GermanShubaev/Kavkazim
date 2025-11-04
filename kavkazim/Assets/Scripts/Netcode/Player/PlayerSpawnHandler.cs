using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Spawns the player prefab when a client connects; set as the PlayerPrefab in NetworkManager.
    /// </summary>
    public class PlayerSpawnHandler : MonoBehaviour
    {
        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientConnected(ulong clientId) { /* reserved for later (spawn points, cosmetics) */ }
        private void OnClientDisconnected(ulong clientId) { /* cleanup if needed */ }
    }
}