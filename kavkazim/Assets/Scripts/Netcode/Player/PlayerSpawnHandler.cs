using Unity.Netcode;
using UnityEngine;
using Kavkazim.Netcode;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Netcode.Player
{
    public class PlayerSpawnHandler : MonoBehaviour
    {
        public static PlayerSpawnHandler Instance { get; private set; }

        [Header("Gameplay Spawn Configuration")]
        [Tooltip("Center of the gameplay spawn area (hexagon center)")]
        [SerializeField] private Vector3 gameplaySpawnCenter = new Vector3(12.4f, 30.3f, 0f);
        
        [Tooltip("Radius of the gameplay spawn circle")]
        [SerializeField] private float gameplaySpawnRadius = 1.5f;

        [Header("References")]
        [Tooltip("Player prefab to spawn (must have PlayerAvatar component)")]
        [SerializeField] private GameObject playerPrefab;

        private int _spawnedPlayerCount = 0;
        private bool _isRegistered = false;
        
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
            if (_isRegistered) return;
            
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.gameObject == gameObject)
            {
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

        private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            int currentCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
            int maxPlayers = GameSessionManager.Instance?.Settings.Value.MaxPlayers ?? 10;
            
            if (currentCount + 1 > maxPlayers)
            {
                response.Approved = false;
                response.Reason = "Server full";
                return;
            }
            
            string playerName = null;
            if (request.Payload != null && request.Payload.Length > 0)
            {
                try
                {
                    playerName = System.Text.Encoding.UTF8.GetString(request.Payload);
                }
                catch { }
            }
            
            if (!string.IsNullOrEmpty(playerName) && GameSessionManager.Instance != null)
            {
                var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
                
                foreach (var player in GameSessionManager.Instance.Players)
                {
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
            
            response.Approved = true;
            response.CreatePlayerObject = false;
            
            Debug.Log($"[PlayerSpawnHandler] Connection approved for '{playerName ?? "unknown"}'. Total will be: {currentCount + 1}/{maxPlayers}");
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            
            Debug.Log($"[PlayerSpawnHandler] Client {clientId} connected");
            
            if (GameSessionManager.Instance != null)
            {
                string pendingName = $"Player {clientId}";
                GameSessionManager.Instance.AddPlayer(clientId, pendingName);
            }
            else
            {
                bool isHost = clientId == NetworkManager.ServerClientId;
                if (!isHost)
                {
                    Debug.LogWarning($"[PlayerSpawnHandler] GameSessionManager.Instance is null for client {clientId}");
                }
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            _spawnedPlayers.RemoveAll(p => p == null || p.OwnerClientId == clientId);
            Debug.Log($"[PlayerSpawnHandler] Client {clientId} disconnected. Tracked avatars: {_spawnedPlayers.Count}");
        }

        public void SpawnGameplayAvatars(List<PlayerSessionData> eligiblePlayers, LobbySettings settings, bool skipRoleAssignment = false)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("[PlayerSpawnHandler] SpawnGameplayAvatars called on client!");
                return;
            }

            Debug.Log($"[PlayerSpawnHandler] Spawning {eligiblePlayers.Count} gameplay avatars (skipRoleAssignment={skipRoleAssignment})");
            
            _spawnedPlayerCount = 0;
            _spawnedPlayers.Clear();
            
            if (playerPrefab == null)
            {
                playerPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            }
            
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawnHandler] No player prefab configured!");
                return;
            }
            
            foreach (var playerData in eligiblePlayers)
            {
                SpawnPlayerAvatar(playerData.ClientId, playerData.PlayerName.ToString());
            }
            
            if (!skipRoleAssignment)
            {
                StartCoroutine(AssignRolesCoroutine(settings.KavkaziCount));
            }
            else
            {
                Debug.Log("[PlayerSpawnHandler] Skipping role assignment - roles will be restored from cache");
            }
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
            
            PlayerAvatar avatar = playerObj.GetComponent<PlayerAvatar>();
            if (avatar != null)
            {
                avatar.PlayerName.Value = playerName;
                _spawnedPlayers.Add(avatar);
            }
            
            netObj.SpawnAsPlayerObject(clientId, true);
            
            Debug.Log($"[PlayerSpawnHandler] Spawned avatar for {playerName} (Client {clientId}) at {spawnPos}");
        }

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

        private IEnumerator AssignRolesCoroutine(int kavkaziCount)
        {
            yield return null;
            yield return null;
            
            _spawnedPlayers.RemoveAll(p => p == null);
            
            if (_spawnedPlayers.Count == 0)
            {
                Debug.LogWarning("[PlayerSpawnHandler] No players to assign roles to!");
                yield break;
            }
            
            List<PlayerAvatar> shuffled = _spawnedPlayers.OrderBy(_ => Random.value).ToList();
            
            kavkaziCount = Mathf.Clamp(kavkaziCount, 1, shuffled.Count - 1);
            
            Debug.Log($"[PlayerSpawnHandler] Assigning roles: {kavkaziCount} Kavkazi, {shuffled.Count - kavkaziCount} Innocent");
            
            for (int i = 0; i < shuffled.Count; i++)
            {
                PlayerAvatar avatar = shuffled[i];
                PlayerRoleType role = i < kavkaziCount ? PlayerRoleType.Kavkazi : PlayerRoleType.Innocent;
                avatar.Role.Value = role;
                Debug.Log($"[PlayerSpawnHandler] Assigned {role} to {avatar.PlayerName.Value} (Client {avatar.OwnerClientId})");
            }
            
            yield return null;
            
            DistributeAllRoles();
        }

        private void DistributeAllRoles()
        {
            foreach (var observer in _spawnedPlayers)
            {
                if (observer == null) continue;
                
                PlayerRoleType observerTrueRole = observer.Role.Value;
                
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
            
            // Distribute and sync task assignments after roles are assigned
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.DistributeAndSyncTasks();
            }
        }
    }
}