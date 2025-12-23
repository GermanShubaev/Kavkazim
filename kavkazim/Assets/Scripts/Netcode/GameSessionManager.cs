using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Netcode.Player;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Server-authoritative game session manager.
    /// Manages lobby state, player list, match settings, and phase transitions.
    /// This is the single source of truth for all lobby data.
    /// 
    /// Attach to a GameObject in the GameSession scene (not as a prefab spawn).
    /// The NetworkObject should be set to spawn with scene.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class GameSessionManager : NetworkBehaviour
    {
        /// <summary>Singleton instance for easy access.</summary>
        public static GameSessionManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private float postMatchDuration = 5f;

        // ========== NETWORKED STATE ==========
        
        /// <summary>Current match phase - determines what players can do.</summary>
        public NetworkVariable<MatchPhase> CurrentPhase = new(
            MatchPhase.LobbyOpen,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>All connected players. Single source of truth.</summary>
        public NetworkList<PlayerSessionData> Players;

        /// <summary>Lobby settings configured by host.</summary>
        public NetworkVariable<LobbySettings> Settings = new(
            LobbySettings.Default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // ========== EVENTS FOR UI ==========
        
        /// <summary>Fired when player list changes (join, leave, ready, name).</summary>
        public event Action OnPlayersChanged;
        
        /// <summary>Fired when settings are updated.</summary>
        public event Action OnSettingsChanged;
        
        /// <summary>Fired when match phase changes.</summary>
        public event Action<MatchPhase> OnPhaseChanged;

        // ========== LIFECYCLE ==========

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameSessionManager] Duplicate instance detected, destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Initialize NetworkList (must be done in Awake before OnNetworkSpawn)
            Players = new NetworkList<PlayerSessionData>();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[GameSessionManager] OnNetworkSpawn. IsServer={IsServer}, IsClient={IsClient}");
            
            // Subscribe to NetworkVariable/List changes
            Players.OnListChanged += HandlePlayersListChanged;
            Settings.OnValueChanged += HandleSettingsChanged;
            CurrentPhase.OnValueChanged += HandlePhaseChanged;
            
            // Subscribe to network events
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            // Server: Initialize settings and add host player
            if (IsServer)
            {
                if (Settings.Value.MaxPlayers <= 0)
                {
                    Settings.Value = LobbySettings.Default;
                }
                
                // Add the host player if not already in list
                // (OnClientConnected fires before GameSessionManager exists for the host)
                ulong hostId = NetworkManager.ServerClientId;
                bool hostExists = false;
                foreach (var p in Players)
                {
                    if (p.ClientId == hostId)
                    {
                        hostExists = true;
                        break;
                    }
                }
                
                if (!hostExists)
                {
                    string hostName = PlayerPrefs.GetString("PlayerName", $"Player {hostId}");
                    AddPlayer(hostId, hostName);
                    Debug.Log($"[GameSessionManager] Added host player: {hostName}");
                }
            }
            
            // Fire initial events for UI setup
            OnPlayersChanged?.Invoke();
            OnSettingsChanged?.Invoke();
            OnPhaseChanged?.Invoke(CurrentPhase.Value);
            
            // Client (non-host): Submit name to server
            if (IsClient && !IsServer)
            {
                string playerName = PlayerPrefs.GetString("PlayerName", $"Player {NetworkManager.LocalClientId}");
                SubmitPlayerNameServerRpc(playerName);
            }
        }

        public override void OnNetworkDespawn()
        {
            Players.OnListChanged -= HandlePlayersListChanged;
            Settings.OnValueChanged -= HandleSettingsChanged;
            CurrentPhase.OnValueChanged -= HandlePhaseChanged;
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private new void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ========== EVENT HANDLERS ==========

        private void HandlePlayersListChanged(NetworkListEvent<PlayerSessionData> changeEvent)
        {
            Debug.Log($"[GameSessionManager] Players list changed: {changeEvent.Type}");
            OnPlayersChanged?.Invoke();
        }

        private void HandleSettingsChanged(LobbySettings previousValue, LobbySettings newValue)
        {
            Debug.Log($"[GameSessionManager] Settings changed: {newValue}");
            OnSettingsChanged?.Invoke();
        }

        private void HandlePhaseChanged(MatchPhase previousValue, MatchPhase newValue)
        {
            Debug.Log($"[GameSessionManager] Phase changed: {previousValue} -> {newValue}");
            OnPhaseChanged?.Invoke(newValue);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            
            Debug.Log($"[GameSessionManager] Client {clientId} disconnected");
            
            // Remove from Players list
            for (int i = Players.Count - 1; i >= 0; i--)
            {
                if (Players[i].ClientId == clientId)
                {
                    Debug.Log($"[GameSessionManager] Removing player: {Players[i].PlayerName}");
                    Players.RemoveAt(i);
                    break;
                }
            }
            
            // If match is in progress, also despawn their PlayerAvatar
            if (CurrentPhase.Value == MatchPhase.MatchInProgress)
            {
                DespawnPlayerAvatar(clientId);
            }
        }

        // ========== SERVER RPCs ==========

        /// <summary>
        /// Called by clients immediately after connecting to submit their display name.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitPlayerNameServerRpc(string playerName, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            
            // Validate and sanitize name
            if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 20)
            {
                playerName = $"Player {clientId}";
            }
            playerName = playerName.Trim();
            
            // Find existing entry and update name
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == clientId)
                {
                    var data = Players[i];
                    data.PlayerName = playerName;
                    Players[i] = data;
                    Debug.Log($"[GameSessionManager] Updated player name: {playerName} (Client {clientId})");
                    return;
                }
            }
            
            // Player not found - this can happen due to timing (RPC arrives before OnClientConnected)
            // Add them now with the correct name
            Debug.Log($"[GameSessionManager] SubmitPlayerNameServerRpc: Adding player {clientId} with name: {playerName}");
            AddPlayer(clientId, playerName);
        }

        /// <summary>
        /// Toggle ready state for a player. Only works in LobbyOpen phase.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SetReadyServerRpc(bool ready, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            
            // Only allow in lobby phase
            if (CurrentPhase.Value != MatchPhase.LobbyOpen)
            {
                Debug.LogWarning($"[GameSessionManager] SetReady rejected - not in lobby phase");
                return;
            }
            
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId == clientId)
                {
                    var data = Players[i];
                    data.IsReady = ready;
                    Players[i] = data;
                    Debug.Log($"[GameSessionManager] Player {data.PlayerName} ready: {ready}");
                    return;
                }
            }
        }

        /// <summary>
        /// Update lobby settings. Host only, LobbyOpen phase only.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void UpdateSettingsServerRpc(LobbySettings newSettings, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            
            // Validate: only host can change settings
            if (senderId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"[GameSessionManager] Non-host tried to change settings (Client {senderId})");
                return;
            }
            
            // Only allow in lobby phase
            if (CurrentPhase.Value != MatchPhase.LobbyOpen)
            {
                Debug.LogWarning("[GameSessionManager] Cannot change settings during match");
                return;
            }
            
            // Validate and clamp settings
            newSettings = ValidateSettings(newSettings);
            Settings.Value = newSettings;
            
            Debug.Log($"[GameSessionManager] Settings updated: {newSettings}");
        }

        /// <summary>
        /// Start the game. Host only, validates all conditions.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void StartGameServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            
            // Validate: only host can start
            if (senderId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"[GameSessionManager] Non-host tried to start game (Client {senderId})");
                return;
            }
            
            // Must be in lobby phase
            if (CurrentPhase.Value != MatchPhase.LobbyOpen)
            {
                Debug.LogWarning("[GameSessionManager] Cannot start game - not in lobby phase");
                return;
            }
            
            // Count eligible players (not late joiners)
            int eligibleCount = 0;
            int readyCount = 0;
            
            foreach (var player in Players)
            {
                if (!player.JoinedDuringMatch)
                {
                    eligibleCount++;
                    if (player.IsReady) readyCount++;
                }
            }
            
            // Require at least 2 eligible players
            if (eligibleCount < 2)
            {
                Debug.LogWarning($"[GameSessionManager] Need at least 2 players to start (have {eligibleCount})");
                return;
            }
            
            // Require all eligible players to be ready
            if (readyCount < eligibleCount)
            {
                Debug.LogWarning($"[GameSessionManager] Not all players ready: {readyCount}/{eligibleCount}");
                return;
            }
            
            // Validate Kavkazi count
            int kavkaziCount = Settings.Value.KavkaziCount;
            if (kavkaziCount >= eligibleCount)
            {
                Debug.LogWarning($"[GameSessionManager] Too many Kavkazi ({kavkaziCount}) for {eligibleCount} players");
                return;
            }
            
            // All checks passed - start the game!
            Debug.Log($"[GameSessionManager] Starting game with {eligibleCount} players, {kavkaziCount} Kavkazi");
            
            CurrentPhase.Value = MatchPhase.MatchInProgress;
            
            // Directly call spawn handler to spawn gameplay avatars
            if (PlayerSpawnHandler.Instance != null)
            {
                PlayerSpawnHandler.Instance.SpawnGameplayAvatars(GetEligiblePlayers(), Settings.Value);
            }
            else
            {
                Debug.LogError("[GameSessionManager] PlayerSpawnHandler.Instance is null!");
            }
        }

        // ========== PUBLIC METHODS (Server Only) ==========

        /// <summary>
        /// Add a player to the lobby. Called by PlayerSpawnHandler on client connect.
        /// </summary>
        public void AddPlayer(ulong clientId, string playerName)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[GameSessionManager] AddPlayer called on client");
                return;
            }
            
            // Check if already exists
            foreach (var player in Players)
            {
                if (player.ClientId == clientId)
                {
                    Debug.LogWarning($"[GameSessionManager] Player {clientId} already in list");
                    return;
                }
            }
            
            bool isHost = clientId == NetworkManager.ServerClientId;
            bool joinedDuringMatch = CurrentPhase.Value == MatchPhase.MatchInProgress;
            
            var newPlayer = new PlayerSessionData
            {
                ClientId = clientId,
                PlayerName = string.IsNullOrEmpty(playerName) ? $"Player {clientId}" : playerName,
                IsReady = isHost, // Host is auto-ready
                IsHost = isHost,
                JoinedDuringMatch = joinedDuringMatch
            };
            
            Players.Add(newPlayer);
            
            Debug.Log($"[GameSessionManager] Added player: {newPlayer}");
        }

        /// <summary>
        /// End the current match and return to lobby.
        /// Call this when a win condition is met.
        /// </summary>
        public void EndMatch()
        {
            if (!IsServer) return;
            
            if (CurrentPhase.Value != MatchPhase.MatchInProgress)
            {
                Debug.LogWarning("[GameSessionManager] EndMatch called but not in match");
                return;
            }
            
            Debug.Log("[GameSessionManager] Ending match...");
            
            // Destroy all PlayerAvatars
            DespawnAllAvatars();
            
            // Reset all players for next round
            for (int i = 0; i < Players.Count; i++)
            {
                var player = Players[i];
                
                // Waiting players become eligible for next round
                player.JoinedDuringMatch = false;
                
                // Reset ready state (host stays ready)
                player.IsReady = player.IsHost;
                
                Players[i] = player;
            }
            
            // Show post-match results briefly
            CurrentPhase.Value = MatchPhase.PostMatch;
            
            // Return to lobby after delay
            StartCoroutine(ReturnToLobbyCoroutine());
        }

        // ========== HELPER METHODS ==========

        private LobbySettings ValidateSettings(LobbySettings s)
        {
            // Clamp to valid ranges
            s.MaxPlayers = Mathf.Clamp(s.MaxPlayers, 4, 15);
            s.KavkaziCount = Mathf.Clamp(s.KavkaziCount, 1, 3);
            s.VotingTime = Mathf.Clamp(s.VotingTime, 30f, 180f);
            s.MoveSpeed = Mathf.Clamp(s.MoveSpeed, 0.5f, 5f);
            s.KillCooldown = Mathf.Clamp(s.KillCooldown, 5f, 60f);
            s.MissionsPerInnocent = Mathf.Clamp(s.MissionsPerInnocent, 1, 10);
            
            // Kavkazi count must be less than current player count (if any)
            int currentPlayerCount = GetEligiblePlayerCount();
            if (currentPlayerCount > 0 && s.KavkaziCount >= currentPlayerCount)
            {
                s.KavkaziCount = Mathf.Max(1, currentPlayerCount - 1);
            }
            
            // MaxPlayers cannot go below current connected count
            if (s.MaxPlayers < Players.Count)
            {
                s.MaxPlayers = Players.Count;
            }
            
            return s;
        }

        /// <summary>
        /// Get count of players eligible to play (not late joiners).
        /// </summary>
        public int GetEligiblePlayerCount()
        {
            int count = 0;
            foreach (var player in Players)
            {
                if (!player.JoinedDuringMatch) count++;
            }
            return count;
        }

        /// <summary>
        /// Get list of players eligible to play (not late joiners).
        /// </summary>
        public List<PlayerSessionData> GetEligiblePlayers()
        {
            var eligible = new List<PlayerSessionData>();
            foreach (var player in Players)
            {
                if (!player.JoinedDuringMatch)
                {
                    eligible.Add(player);
                }
            }
            return eligible;
        }

        /// <summary>
        /// Check if a client is in the waiting state (joined during match).
        /// </summary>
        public bool IsPlayerWaiting(ulong clientId)
        {
            foreach (var player in Players)
            {
                if (player.ClientId == clientId)
                {
                    return player.JoinedDuringMatch;
                }
            }
            return false;
        }

        /// <summary>
        /// Get player data by client ID.
        /// </summary>
        public bool TryGetPlayer(ulong clientId, out PlayerSessionData playerData)
        {
            foreach (var player in Players)
            {
                if (player.ClientId == clientId)
                {
                    playerData = player;
                    return true;
                }
            }
            playerData = default;
            return false;
        }

        private void DespawnPlayerAvatar(ulong clientId)
        {
            if (NetworkManager.SpawnManager == null) return;
            
            foreach (var netObj in NetworkManager.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj.OwnerClientId == clientId && netObj.GetComponent<PlayerAvatar>() != null)
                {
                    netObj.Despawn(true);
                    Debug.Log($"[GameSessionManager] Despawned avatar for client {clientId}");
                    return;
                }
            }
        }

        private void DespawnAllAvatars()
        {
            if (NetworkManager.SpawnManager == null) return;
            
            var toDespawn = new List<NetworkObject>();
            foreach (var netObj in NetworkManager.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj.GetComponent<PlayerAvatar>() != null)
                {
                    toDespawn.Add(netObj);
                }
            }
            
            foreach (var netObj in toDespawn)
            {
                netObj.Despawn(true);
            }
            
            Debug.Log($"[GameSessionManager] Despawned {toDespawn.Count} avatars");
        }

        private IEnumerator ReturnToLobbyCoroutine()
        {
            yield return new WaitForSeconds(postMatchDuration);
            
            CurrentPhase.Value = MatchPhase.LobbyOpen;
            Debug.Log("[GameSessionManager] Returned to lobby");
        }

        // ========== DEBUG ==========

#if UNITY_EDITOR
        [ContextMenu("Debug: Print Players")]
        private void DebugPrintPlayers()
        {
            Debug.Log($"=== Players ({Players.Count}) ===");
            foreach (var player in Players)
            {
                Debug.Log($"  {player}");
            }
        }

        [ContextMenu("Debug: End Match")]
        private void DebugEndMatch()
        {
            if (Application.isPlaying && IsServer)
            {
                EndMatch();
            }
        }
#endif
    }
}
