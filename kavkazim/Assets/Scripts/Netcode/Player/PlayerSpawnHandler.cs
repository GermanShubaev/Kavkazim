using Unity.Netcode;
using UnityEngine;
using Kavkazim.Netcode;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Netcode.Player
{
    /// <summary>
    /// Handles connection approval and player avatar spawning.
    /// 
    /// In lobby mode:
    /// - Connection approval enforces MaxPlayers limit
    /// - Player objects are NOT auto-spawned (CreatePlayerObject = false)
    /// - Players are added to GameSessionManager.Players list
    /// 
    /// When match starts:
    /// - SpawnGameplayAvatars() creates PlayerAvatar for eligible players
    /// - Roles are assigned based on lobby settings
    /// 
    /// Attach to the NetworkManager GameObject and enable Connection Approval.
    /// </summary>
    public class PlayerSpawnHandler : MonoBehaviour
    {
        /// <summary>Singleton instance for GameSessionManager to call SpawnGameplayAvatars.</summary>
        public static PlayerSpawnHandler Instance { get; private set; }

        [Header("Gameplay Spawn Configuration")]
        [Tooltip("Center of the gameplay spawn area (hexagon center)")]
        [SerializeField] private Vector3 gameplaySpawnCenter = new Vector3(12.4f, 30.3f, 0f);
        
        [Tooltip("Radius of the gameplay spawn circle")]
        [SerializeField] private float gameplaySpawnRadius = 1.5f;

        [Header("Lobby Area Configuration")]
        [Tooltip("Center of the lobby waiting area")]
        [SerializeField] private Vector3 lobbySpawnCenter = new Vector3(0f, -50f, 0f);

        [Header("References")]
        [Tooltip("Player prefab to spawn (must have PlayerAvatar component)")]
        [SerializeField] private GameObject playerPrefab;

        private int _spawnedPlayerCount = 0;
        private bool _isRegistered = false;
        
        // Track spawned player avatars for role distribution
        private List<PlayerAvatar> _spawnedPlayers = new List<PlayerAvatar>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerSpawnHandler] Duplicate instance detected");
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

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
                Debug.Log("[PlayerSpawnHandler] Registered with NetworkManager");
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
        /// Enforces MaxPlayers limit, checks for duplicate names, and does NOT auto-spawn player objects.
        /// </summary>
        private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // Get current player count (not including this incoming connection)
            int currentCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
            
            // Get max players from lobby settings (fallback to 10 if not available)
            int maxPlayers = GameSessionManager.Instance?.Settings.Value.MaxPlayers ?? 10;
            
            // Timing-safe check: currentCount + 1 (this connection) > maxPlayers
            if (currentCount + 1 > maxPlayers)
            {
                response.Approved = false;
                response.Reason = "Server full";
                return;
            }
            
            // Get player name from connection payload (if provided)
            string playerName = null;
            if (request.Payload != null && request.Payload.Length > 0)
            {
                try
                {
                    playerName = System.Text.Encoding.UTF8.GetString(request.Payload);
                }
                catch { }
            }
            
            // Check for duplicate names (only if GameSessionManager exists and name was provided)
            if (!string.IsNullOrEmpty(playerName) && GameSessionManager.Instance != null)
            {
                var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
                
                foreach (var player in GameSessionManager.Instance.Players)
                {
                    // Skip if this player is no longer connected (stale entry)
                    if (!connectedClients.Contains(player.ClientId))
                        continue;
                    
                    if (player.PlayerName.ToString().Equals(playerName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        response.Approved = false;
                        response.Reason = $"Name '{playerName}' is already taken";
                        return;
                    }
                }
            }
            
            // Approve the connection but DO NOT spawn player object
            // Player objects are spawned only when match starts
            response.Approved = true;
            response.CreatePlayerObject = false;
            
            Debug.Log($"[PlayerSpawnHandler] Connection approved for '{playerName ?? "unknown"}'. Total will be: {currentCount + 1}/{maxPlayers}");
        }

        /// <summary>
        /// Called when a client fully connects.
        /// Adds them to the GameSessionManager player list.
        /// </summary>
        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            Debug.Log($"[PlayerSpawnHandler] Client {clientId} connected");
            
            // Add player to lobby list
            // Name will be updated when client calls SubmitPlayerNameServerRpc
            if (GameSessionManager.Instance != null)
            {
                string pendingName = $"Player {clientId}";
                GameSessionManager.Instance.AddPlayer(clientId, pendingName);
            }
            else
            {
                // This is expected for the host - they connect before GameSession scene loads
                // The host will be added when GameSessionManager.OnNetworkSpawn runs
                bool isHost = clientId == NetworkManager.ServerClientId;
                if (!isHost)
                {
                    Debug.LogWarning($"[PlayerSpawnHandler] GameSessionManager.Instance is null for client {clientId}");
                }
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            // Remove disconnected player from our tracking list
            _spawnedPlayers.RemoveAll(p => p == null || p.OwnerClientId == clientId);
            Debug.Log($"[PlayerSpawnHandler] Client {clientId} disconnected. Tracked avatars: {_spawnedPlayers.Count}");
        }

        /// <summary>
        /// Spawn gameplay avatars for all eligible players.
        /// Called by GameSessionManager when match starts.
        /// </summary>
        /// <param name="eligiblePlayers">Players who were in lobby (not late joiners)</param>
        /// <param name="settings">Lobby settings for role assignment</param>
        public void SpawnGameplayAvatars(List<PlayerSessionData> eligiblePlayers, LobbySettings settings)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("[PlayerSpawnHandler] SpawnGameplayAvatars called on client!");
                return;
            }

            Debug.Log($"[PlayerSpawnHandler] Spawning {eligiblePlayers.Count} gameplay avatars");
            
            // Reset spawn counter
            _spawnedPlayerCount = 0;
            _spawnedPlayers.Clear();
            
            // Get player prefab from NetworkManager if not set
            if (playerPrefab == null)
            {
                playerPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            }
            
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawnHandler] No player prefab configured!");
                return;
            }
            
            // Spawn avatar for each eligible player
            foreach (var playerData in eligiblePlayers)
            {
                SpawnPlayerAvatar(playerData.ClientId, playerData.PlayerName.ToString());
            }
            
            // Assign roles after all avatars are spawned
            StartCoroutine(AssignRolesCoroutine(settings.KavkaziCount));
        }

        private void SpawnPlayerAvatar(ulong clientId, string playerName)
        {
            Vector3 spawnPos = GetNextGameplaySpawnPosition();
            
            GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
            
            if (netObj == null)
            {
                Debug.LogError("[PlayerSpawnHandler] Player prefab missing NetworkObject!");
                Destroy(playerObj);
                return;
            }
            
            // Set the player name BEFORE spawning so it's synced with initial spawn
            PlayerAvatar avatar = playerObj.GetComponent<PlayerAvatar>();
            if (avatar != null)
            {
                avatar.PlayerName.Value = playerName;
                _spawnedPlayers.Add(avatar);
            }
            
            // NOW spawn as player object owned by the client
            // The PlayerName value will be included in the initial sync
            netObj.SpawnAsPlayerObject(clientId, true);
            
            Debug.Log($"[PlayerSpawnHandler] Spawned avatar for {playerName} (Client {clientId}) at {spawnPos}");
        }

        /// <summary>
        /// Calculate the next spawn position in a circular pattern around gameplay area.
        /// </summary>
        private Vector3 GetNextGameplaySpawnPosition()
        {
            int maxPlayers = GameSessionManager.Instance?.Settings.Value.MaxPlayers ?? 10;
            float angleStep = 360f / maxPlayers;
            float angle = _spawnedPlayerCount * angleStep * Mathf.Deg2Rad;
            
            float x = gameplaySpawnCenter.x + Mathf.Cos(angle) * gameplaySpawnRadius;
            float y = gameplaySpawnCenter.y + Mathf.Sin(angle) * gameplaySpawnRadius;
            
            _spawnedPlayerCount++;
            
            return new Vector3(x, y, gameplaySpawnCenter.z);
        }

        /// <summary>
        /// Assign roles after avatars are spawned.
        /// Uses lobby settings for Kavkazi count instead of random chance.
        /// </summary>
        private IEnumerator AssignRolesCoroutine(int kavkaziCount)
        {
            // Wait for all avatars to be fully spawned
            yield return null;
            yield return null;
            
            // Clean up null references
            _spawnedPlayers.RemoveAll(p => p == null);
            
            if (_spawnedPlayers.Count == 0)
            {
                Debug.LogWarning("[PlayerSpawnHandler] No players to assign roles to!");
                yield break;
            }
            
            // Shuffle players for random role assignment
            List<PlayerAvatar> shuffled = _spawnedPlayers.OrderBy(_ => Random.value).ToList();
            
            // Clamp Kavkazi count to valid range
            kavkaziCount = Mathf.Clamp(kavkaziCount, 1, shuffled.Count - 1);
            
            Debug.Log($"[PlayerSpawnHandler] Assigning roles: {kavkaziCount} Kavkazi, {shuffled.Count - kavkaziCount} Innocent");
            
            // Assign Kavkazi roles
            for (int i = 0; i < shuffled.Count; i++)
            {
                PlayerAvatar avatar = shuffled[i];
                PlayerRoleType role = i < kavkaziCount ? PlayerRoleType.Kavkazi : PlayerRoleType.Innocent;
                avatar.Role.Value = role;
                Debug.Log($"[PlayerSpawnHandler] Assigned {role} to {avatar.PlayerName.Value} (Client {avatar.OwnerClientId})");
            }
            
            // Distribute perceived roles to all clients
            yield return null; // Wait a frame for roles to sync
            
            DistributeAllRoles();
        }

        /// <summary>
        /// Distribute perceived roles to all clients after role assignment.
        /// </summary>
        private void DistributeAllRoles()
        {
            foreach (var observer in _spawnedPlayers)
            {
                if (observer == null) continue;
                
                PlayerRoleType observerTrueRole = observer.Role.Value;
                
                // Send perceived role for each player to this observer
                foreach (var target in _spawnedPlayers)
                {
                    if (target == null) continue;
                    
                    PlayerRoleType targetTrueRole = target.Role.Value;
                    PlayerRoleType perceivedRole = RoleVisibilityService.GetPerceivedRole(observerTrueRole, targetTrueRole);
                    
                    observer.ReceivePerceivedRoleClientRpc(
                        target.NetworkObjectId,
                        perceivedRole,
                        observer.RpcTarget.Single(observer.OwnerClientId, RpcTargetUse.Temp)
                    );
                }
            }
            
            Debug.Log($"[PlayerSpawnHandler] Distributed roles to {_spawnedPlayers.Count} players");
        }

        /// <summary>
        /// Reset spawn counter and player tracking (useful when returning to lobby)
        /// </summary>
        public void ResetSpawnCounter()
        {
            _spawnedPlayerCount = 0;
            _spawnedPlayers.Clear();
        }

        /// <summary>
        /// Get the lobby spawn position (for UI/camera positioning)
        /// </summary>
        public Vector3 GetLobbySpawnCenter() => lobbySpawnCenter;

        /// <summary>
        /// Get the gameplay spawn position (for UI/camera positioning)
        /// </summary>
        public Vector3 GetGameplaySpawnCenter() => gameplaySpawnCenter;
    }
}