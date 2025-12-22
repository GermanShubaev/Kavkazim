using Unity.Netcode;
using UnityEngine;
using Kavkazim.Netcode;
using System.Collections.Generic;
using System.Collections;

namespace Netcode.Player
{
    /// <summary>
    /// Handles custom player spawning in a circular pattern around the hexagon center.
    /// Also manages server-side role assignment and secure role distribution.
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

        [Header("Role Configuration")]
        [Tooltip("Chance for a player to be Kavkazi (0-1)")]
        [SerializeField] private float kavkaziChance = 0.3f;

        private int _spawnedPlayerCount = 0;
        private bool _isRegistered = false;
        
        // Track spawned players for role distribution
        private List<PlayerAvatar> _spawnedPlayers = new List<PlayerAvatar>();

        private void OnEnable()
        {
            // Only register if we're actually on the NetworkManager object
            // and haven't already registered
            if (_isRegistered) return;
            
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.gameObject == gameObject)
            {
                // Use direct assignment (Unity Netcode only allows one callback)
                nm.ConnectionApprovalCallback = OnConnectionApproval;
                nm.OnClientDisconnectCallback += OnClientDisconnected;
                nm.OnClientConnectedCallback += OnClientConnected;
                _isRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (!_isRegistered) return;
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback = null;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
            _isRegistered = false;
            _spawnedPlayers.Clear();
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
            // Remove disconnected player from our tracking list
            _spawnedPlayers.RemoveAll(p => p == null || p.OwnerClientId == clientId);
            Debug.Log($"[PlayerSpawnHandler] Client {clientId} disconnected. Tracked players: {_spawnedPlayers.Count}");
        }

        /// <summary>
        /// Called when a client fully connects (after spawn).
        /// Used to assign role and distribute role visibility.
        /// </summary>
        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            // Use coroutine to wait for player object to be spawned
            StartCoroutine(AssignAndDistributeRolesCoroutine(clientId));
        }

        /// <summary>
        /// Coroutine to wait for player spawn and then assign/distribute roles.
        /// </summary>
        private IEnumerator AssignAndDistributeRolesCoroutine(ulong clientId)
        {
            // Wait a frame for the player object to be fully spawned
            yield return null;
            yield return null; // Extra frame for safety
            
            // Find the player's avatar
            PlayerAvatar newPlayerAvatar = null;
            foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj.OwnerClientId == clientId)
                {
                    var avatar = netObj.GetComponent<PlayerAvatar>();
                    if (avatar != null)
                    {
                        newPlayerAvatar = avatar;
                        break;
                    }
                }
            }
            
            if (newPlayerAvatar == null)
            {
                Debug.LogWarning($"[PlayerSpawnHandler] Could not find PlayerAvatar for client {clientId}");
                yield break;
            }
            
            // Assign role to new player
            AssignRoleToPlayer(newPlayerAvatar);
            
            // Add to tracked players
            if (!_spawnedPlayers.Contains(newPlayerAvatar))
            {
                _spawnedPlayers.Add(newPlayerAvatar);
            }
            
            // Distribute roles: tell new client about all existing players
            DistributeRolesToClient(clientId, newPlayerAvatar.Role.Value);
            
            // Broadcast new player's role to all existing clients
            BroadcastNewPlayerRole(newPlayerAvatar);
            
            Debug.Log($"[PlayerSpawnHandler] Role assignment complete for client {clientId}. Role: {newPlayerAvatar.Role.Value}");
        }

        /// <summary>
        /// SERVER ONLY: Assign a random role to a player.
        /// </summary>
        private void AssignRoleToPlayer(PlayerAvatar avatar)
        {
            // Random role assignment
            PlayerRoleType role = Random.value < kavkaziChance ? PlayerRoleType.Kavkazi : PlayerRoleType.Innocent;
            avatar.Role.Value = role;
            Debug.Log($"[PlayerSpawnHandler] Assigned role {role} to player {avatar.OwnerClientId}");
        }

        /// <summary>
        /// SERVER ONLY: Send perceived roles for ALL players to a specific client.
        /// Uses the new client's true role to determine what they should see.
        /// </summary>
        private void DistributeRolesToClient(ulong clientId, PlayerRoleType observerTrueRole)
        {
            // Find the observer's avatar to send RPCs through
            PlayerAvatar observerAvatar = null;
            foreach (var player in _spawnedPlayers)
            {
                if (player != null && player.OwnerClientId == clientId)
                {
                    observerAvatar = player;
                    break;
                }
            }
            
            if (observerAvatar == null)
            {
                Debug.LogWarning($"[PlayerSpawnHandler] Could not find observer avatar for client {clientId}");
                return;
            }
            
            // Send perceived role for each player (including self)
            foreach (var targetPlayer in _spawnedPlayers)
            {
                if (targetPlayer == null) continue;
                
                PlayerRoleType targetTrueRole = targetPlayer.Role.Value;
                PlayerRoleType perceivedRole = RoleVisibilityService.GetPerceivedRole(observerTrueRole, targetTrueRole);
                
                // Send targeted RPC to this specific client
                // Access RpcTarget through the NetworkBehaviour instance
                observerAvatar.ReceivePerceivedRoleClientRpc(
                    targetPlayer.NetworkObjectId,
                    perceivedRole,
                    observerAvatar.RpcTarget.Single(clientId, RpcTargetUse.Temp)
                );
                
                Debug.Log($"[PlayerSpawnHandler] Sent to client {clientId}: Player {targetPlayer.OwnerClientId} perceived as {perceivedRole}");
            }
        }

        /// <summary>
        /// SERVER ONLY: Broadcast a new player's role to all existing clients.
        /// Each client receives the perceived role based on their own true role.
        /// </summary>
        private void BroadcastNewPlayerRole(PlayerAvatar newPlayer)
        {
            PlayerRoleType newPlayerTrueRole = newPlayer.Role.Value;
            
            foreach (var existingPlayer in _spawnedPlayers)
            {
                if (existingPlayer == null) continue;
                
                // Get what this existing player should see for the new player
                PlayerRoleType existingPlayerTrueRole = existingPlayer.Role.Value;
                PlayerRoleType perceivedRole = RoleVisibilityService.GetPerceivedRole(existingPlayerTrueRole, newPlayerTrueRole);
                
                // Send targeted RPC
                // Access RpcTarget through the NetworkBehaviour instance
                existingPlayer.ReceivePerceivedRoleClientRpc(
                    newPlayer.NetworkObjectId,
                    perceivedRole,
                    existingPlayer.RpcTarget.Single(existingPlayer.OwnerClientId, RpcTargetUse.Temp)
                );
                
                Debug.Log($"[PlayerSpawnHandler] Broadcast to client {existingPlayer.OwnerClientId}: New player {newPlayer.OwnerClientId} perceived as {perceivedRole}");
            }
        }

        /// <summary>
        /// Reset spawn counter and player tracking (useful when returning to lobby)
        /// </summary>
        public void ResetSpawnCounter()
        {
            _spawnedPlayerCount = 0;
            _spawnedPlayers.Clear();
        }
    }
}