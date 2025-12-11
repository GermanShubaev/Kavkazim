using Unity.Netcode;
using UnityEngine;

namespace Netcode.Player
{
    /// <summary>
    /// Handles custom player spawning in a circular pattern around the hexagon center.
    /// Attach to the NetworkManager GameObject and enable Connection Approval.
    /// </summary>
    public class PlayerSpawnHandler : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [Tooltip("Center of the spawn area (hexagon center)")]
        [SerializeField] private Vector3 spawnCenter = new Vector3(12.4f, 30.3f, 0f);
        
        [Tooltip("Radius of the spawn circle")]
        [SerializeField] private float spawnRadius = 1.5f;
        
        [Tooltip("Maximum players to distribute around the circle")]
        [SerializeField] private int maxPlayersOnCircle = 10;

        private int _spawnedPlayerCount = 0;

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback += OnConnectionApproval;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApproval;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        /// <summary>
        /// Called on the server when a client requests to connect.
        /// Sets the spawn position in a circular pattern around the hexagon.
        /// </summary>
        private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // Approve the connection
            response.Approved = true;
            response.CreatePlayerObject = true;
            
            // Calculate spawn position on circle
            response.Position = GetNextSpawnPosition();
            response.Rotation = Quaternion.identity;
            
            Debug.Log($"[PlayerSpawnHandler] Player spawning at position: {response.Position}");
        }

        /// <summary>
        /// Calculate the next spawn position in a circular pattern.
        /// </summary>
        private Vector3 GetNextSpawnPosition()
        {
            // Calculate angle for this player (evenly distributed around circle)
            float angleStep = 360f / maxPlayersOnCircle;
            float angle = _spawnedPlayerCount * angleStep * Mathf.Deg2Rad;
            
            // Calculate position on circle
            float x = spawnCenter.x + Mathf.Cos(angle) * spawnRadius;
            float y = spawnCenter.y + Mathf.Sin(angle) * spawnRadius;
            
            _spawnedPlayerCount++;
            
            return new Vector3(x, y, spawnCenter.z);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            // Optionally decrease count when players leave (for respawn scenarios)
            // Note: This simple implementation doesn't reclaim positions
            Debug.Log($"[PlayerSpawnHandler] Client {clientId} disconnected");
        }

        /// <summary>
        /// Reset spawn counter (useful when returning to lobby)
        /// </summary>
        public void ResetSpawnCounter()
        {
            _spawnedPlayerCount = 0;
        }
    }
}