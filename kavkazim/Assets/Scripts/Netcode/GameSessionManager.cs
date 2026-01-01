using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Netcode.Player;
using Kavkazim.Netcode.WinConditions;
using Kavkazim.Netcode.Validation;

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
        
        /// <summary>
        /// Cached win result that persists across scene loads.
        /// Set before loading WinScreen scene, read by WinScreenSceneController.
        /// </summary>
        public static WinResultData CachedWinResult { get; set; }
        
        /// <summary>
        /// Cached player names that persist across scene loads.
        /// Key = ClientId, Value = PlayerName
        /// Set before loading WinScreen, restored when returning to lobby.
        /// </summary>
        public static Dictionary<ulong, string> CachedPlayerNames { get; private set; } = new Dictionary<ulong, string>();

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

        /// <summary>Win result data synced to all clients when game ends.</summary>
        public NetworkVariable<WinResultData> WinResult = new(
            WinResultData.Empty,
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
        
        /// <summary>Fired when game ends with a win result. Used by UI.</summary>
        public event Action<WinResultData> OnGameEnded;
        
        // ========== WIN CONDITION SYSTEM & VALIDATION ==========
        
        private WinConditionEvaluator _winEvaluator;
        private LobbyValidator _lobbyValidator;

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
            
            // Initialize win condition evaluator with default conditions
            _winEvaluator = WinConditionEvaluator.CreateDefault();
            _lobbyValidator = new LobbyValidator();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[GameSessionManager] OnNetworkSpawn. IsServer={IsServer}, IsClient={IsClient}");
            
            // Subscribe to NetworkVariable/List changes
            Players.OnListChanged += HandlePlayersListChanged;
            Settings.OnValueChanged += HandleSettingsChanged;
            CurrentPhase.OnValueChanged += HandlePhaseChanged;
            WinResult.OnValueChanged += HandleWinResultChanged;
            
            // Subscribe to network events
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            // Server: Subscribe to player death events for win condition checking
            if (IsServer)
            {
                PlayerState.OnPlayerKilled += OnPlayerKilledForWinCheck;
            }
            
            // Server: Initialize settings and add host player
            if (IsServer)
            {
                if (Settings.Value.MaxPlayers <= 0)
                {
                    Settings.Value = LobbySettings.Default;
                }
                
                // Check if returning from WinScreen - restore players from cache
                // CachedWinResult.HasEnded is set when transitioning to WinScreen
                if (CachedWinResult.HasEnded || CurrentPhase.Value == MatchPhase.PostMatch)
                {
                    Players.Clear();
                    
                    // Restore all players from cached names
                    foreach (var kvp in CachedPlayerNames)
                    {
                        AddPlayer(kvp.Key, kvp.Value);
                    }
                    
                    // Clear cached data
                    WinResult.Value = WinResultData.Empty;
                    CachedWinResult = WinResultData.Empty;
                    CachedPlayerNames.Clear();
                    CurrentPhase.Value = MatchPhase.LobbyOpen;
                }
                else
                {
                    // Normal startup - add host if not already in list
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
                        string prefsKey = "PlayerName" + GetParrelSyncSuffix();
                        string hostName = PlayerPrefs.GetString(prefsKey, $"Player {hostId}");
                        AddPlayer(hostId, hostName);
                        Debug.Log($"[GameSessionManager] Added host player: {hostName}");
                    }
                }
            }
            
            // Fire initial events for UI setup
            OnPlayersChanged?.Invoke();
            OnSettingsChanged?.Invoke();
            OnPhaseChanged?.Invoke(CurrentPhase.Value);
            
            // Client (non-host): Submit name to server
            if (IsClient && !IsServer)
            {
                // If returning from WinScreen, do NOT submit name from PlayerPrefs
                // The server has already restored our correct name from the cache
                bool isReturning = CachedWinResult.HasEnded || CurrentPhase.Value == MatchPhase.PostMatch;
                
                if (isReturning)
                {
                     // Clear our local cache flag so future normal joins work
                     CachedWinResult = WinResultData.Empty;
                }
                else
                {
                    string prefsKey = "PlayerName" + GetParrelSyncSuffix();
                    string playerName = PlayerPrefs.GetString(prefsKey, $"Player {NetworkManager.LocalClientId}");
                    SubmitPlayerNameServerRpc(playerName);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            Players.OnListChanged -= HandlePlayersListChanged;
            Settings.OnValueChanged -= HandleSettingsChanged;
            CurrentPhase.OnValueChanged -= HandlePhaseChanged;
            WinResult.OnValueChanged -= HandleWinResultChanged;
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            
            // Unsubscribe from player death events
            if (IsServer)
            {
                PlayerState.OnPlayerKilled -= OnPlayerKilledForWinCheck;
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

        private void HandleWinResultChanged(WinResultData previousValue, WinResultData newValue)
        {
            if (newValue.HasEnded)
            {
                Debug.Log($"[GameSessionManager] Win result received: {newValue.GetWinningTeamDisplay()}");
                OnGameEnded?.Invoke(newValue);
            }
        }

        /// <summary>
        /// Called when any player is killed. Server-only.
        /// Triggers win condition evaluation.
        /// </summary>
        private void OnPlayerKilledForWinCheck(PlayerState killedPlayer)
        {
            if (!IsServer) return;
            if (CurrentPhase.Value != MatchPhase.MatchInProgress) return;
            
            Debug.Log($"[GameSessionManager] Player killed, checking win conditions...");
            CheckWinConditions();
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
                    // Players list change will trigger HandlePlayersListChanged
                    // But we want to run auto-clamp logic on Server immediately after modifying the list?
                    // HandlePlayersListChanged runs on everyone.
                    // We should do auto-clamp here manually or in OnListChanged if IsServer.
                    // Doing it here is safer/clearer for "logic consequent to action".
                    if (IsServer && CurrentPhase.Value == MatchPhase.LobbyOpen)
                    {
                        ValidateAndClampSettings();
                    }
                    break;
                }
            }
            
            // If match is in progress, also despawn their PlayerAvatar
            if (CurrentPhase.Value == MatchPhase.MatchInProgress)
            {
                DespawnPlayerAvatar(clientId);
                
                // CRITICAL: Check win conditions after a player disconnects
                // Disconnecting might change the balance (e.g. Kavkazi majority)
                Debug.Log($"[GameSessionManager] Player {clientId} disconnected during match, checking win conditions...");
                CheckWinConditions();
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
            
            // Validate and clamp settings via Validator
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = Players.Count };
            newSettings = _lobbyValidator.Sanitize(newSettings, ctx);
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
            
            // Use Validator for comprehensive checks
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = eligibleCount }; // Use eligible count for start check
            var validationResult = _lobbyValidator.Validate(Settings.Value, ctx);
            
            if (!validationResult.IsValid)
            {
                foreach(var error in validationResult.Errors)
                {
                    Debug.LogWarning($"[GameSessionManager] Start blocked: {error.Message}");
                }
                return;
            }
            
            int kavkaziCount = Settings.Value.KavkaziCount;
            
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
            
            // Auto-clamp settings if needed (e.g. increase player cap if host forced join?)
            // Usually we don't change settings on join unless necessary?
            // "if host lowers max below current players -> either block or auto-clamp back up"
            // If new player joins, current count increases. If it exceeds MaxPlayers, we might clamp MaxPlayers?
            // Although usually networking layer prevents join if full.
            // But let's run sanitize just in case.
            if (CurrentPhase.Value == MatchPhase.LobbyOpen)
            {
                 ValidateAndClampSettings();
            }

            Debug.Log($"[GameSessionManager] Added player: {newPlayer}");
        }

        /// <summary>
        /// End the current match and return to lobby.
        /// Call this when a win condition is met.
        /// </summary>
        public void EndMatch()
        {
            EndMatch(null);
        }

        /// <summary>
        /// End the current match with a specific win result.
        /// </summary>
        /// <param name="winResult">The win result, or null for no winner.</param>
        public void EndMatch(WinConditions.WinResult winResult)
        {
            if (!IsServer) return;
            
            if (CurrentPhase.Value != MatchPhase.MatchInProgress)
            {
                Debug.LogWarning("[GameSessionManager] EndMatch called but not in match");
                return;
            }
            
            Debug.Log("[GameSessionManager] Ending match...");
            
            // Set win result NetworkVariable for UI sync
            if (winResult != null)
            {
                string winnerNames = string.Join(",", winResult.WinnerNames);
                WinResult.Value = new WinResultData
                {
                    WinningTeam = (byte)winResult.WinningTeamEnum,
                    WinnerNames = winnerNames,
                    ReasonKey = winResult.ReasonKey,
                    HasEnded = true
                };
                Debug.Log($"[GameSessionManager] Win: {winResult.WinningTeamEnum} - {winResult.ReasonKey}");
            }
            else
            {
                // No winner (e.g., match aborted)
                WinResult.Value = new WinResultData
                {
                    WinningTeam = 0,
                    WinnerNames = "",
                    ReasonKey = "match_aborted",
                    HasEnded = true
                };
            }
            
            // Set phase to PostMatch
            CurrentPhase.Value = MatchPhase.PostMatch;
            
            // Cache the win result for the WinScreen scene to read
            // (GameSessionManager gets destroyed on scene load)
            CachedWinResult = WinResult.Value;
            
            // Cache all player names before scene transition
            // This ensures correct names are restored when returning to lobby
            CachedPlayerNames.Clear();
            foreach (var player in Players)
            {
                CachedPlayerNames[player.ClientId] = player.PlayerName.ToString();
            }
            
            // Sync win result to all clients BEFORE scene loads
            CacheWinResultClientRpc(WinResult.Value);
            
            // Sync player names to all clients BEFORE scene loads
            // Send each player name individually since Dictionary can't be sent via RPC
            // Create a copy to avoid collection modification during enumeration
            var cachedNames = new List<KeyValuePair<ulong, string>>(CachedPlayerNames);
            foreach (var kvp in cachedNames)
            {
                CachePlayerNameClientRpc(kvp.Key, kvp.Value);
            }
            
            // Despawn all player avatars before scene transition
            DespawnAllAvatars();
            
            // Load WinScreen scene for all clients
            LoadWinScreenScene();
        }

        /// <summary>
        /// ClientRpc to cache win result on all clients before scene transition.
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void CacheWinResultClientRpc(WinResultData winResult)
        {
            CachedWinResult = winResult;
            Debug.Log($"[GameSessionManager] Client received win result: {winResult.GetWinningTeamDisplay()}");
        }

        /// <summary>
        /// ClientRpc to cache a player name on all clients before scene transition.
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void CachePlayerNameClientRpc(ulong clientId, string playerName)
        {
            CachedPlayerNames[clientId] = playerName;
        }

        /// <summary>
        /// Loads the WinScreen scene for all connected clients.
        /// </summary>
        private void LoadWinScreenScene()
        {
            if (!IsServer) return;
            
            // Use Netcode's scene management for synchronized loading
            if (NetworkManager.SceneManager != null)
            {
                Debug.Log("[GameSessionManager] Loading WinScreen scene for all clients...");
                
                // Subscribe to scene load events for debugging
                NetworkManager.SceneManager.OnLoadComplete -= OnSceneLoadComplete;
                NetworkManager.SceneManager.OnLoadComplete += OnSceneLoadComplete;
                
                var status = NetworkManager.SceneManager.LoadScene("WinScreen", LoadSceneMode.Single);
                
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError($"[GameSessionManager] Failed to start loading WinScreen scene! Status: {status}");
                    Debug.LogError("[GameSessionManager] Make sure WinScreen scene is added to Build Settings!");
                }
            }
            else
            {
                Debug.LogError("[GameSessionManager] NetworkManager.SceneManager is null!");
            }
        }

        /// <summary>
        /// Called when a scene load completes for a client.
        /// </summary>
        private void OnSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            Debug.Log($"[GameSessionManager] Scene '{sceneName}' loaded for client {clientId}");
        }

        /// <summary>
        /// SERVER RPC: Request return to lobby. Called from win screen button.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ReturnToLobbyServerRpc(RpcParams rpcParams = default)
        {
            if (CurrentPhase.Value != MatchPhase.PostMatch)
            {
                Debug.LogWarning("[GameSessionManager] ReturnToLobby called but not in PostMatch");
                return;
            }
            
            Debug.Log("[GameSessionManager] Returning to lobby (requested by client)...");
            PerformReturnToLobby();
        }

        /// <summary>
        /// Performs the actual return to lobby logic.
        /// </summary>
        private void PerformReturnToLobby()
        {
            if (!IsServer) return;
            
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
            
            // Reset win result
            WinResult.Value = WinResultData.Empty;
            
            // Return to lobby phase
            CurrentPhase.Value = MatchPhase.LobbyOpen;
            
            // Load GameSession scene (lobby) for all clients
            if (NetworkManager.SceneManager != null)
            {
                Debug.Log("[GameSessionManager] Loading GameSession scene (lobby)...");
                NetworkManager.SceneManager.LoadScene("GameSession", LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[GameSessionManager] NetworkManager.SceneManager is null!");
            }
        }

        /// <summary>
        /// Check all win conditions and end match if one is met.
        /// Server only.
        /// </summary>
        public void CheckWinConditions()
        {
            if (!IsServer) return;
            if (CurrentPhase.Value != MatchPhase.MatchInProgress) return;
            
            var snapshot = BuildGameSnapshot();
            
            if (_winEvaluator.TryEvaluate(snapshot, out var result))
            {
                Debug.Log($"[GameSessionManager] Win condition met: {result.WinningTeamEnum}");
                EndMatch(result);
            }
        }

        /// <summary>
        /// Builds a GameSnapshot from current player state.
        /// </summary>
        public GameSnapshot BuildGameSnapshot()
        {
            var playerSnapshots = new List<PlayerSnapshot>();
            
            // Find all PlayerAvatars to get their alive state and roles
            if (NetworkManager.SpawnManager != null)
            {
                foreach (var netObj in NetworkManager.SpawnManager.SpawnedObjects.Values)
                {
                    var avatar = netObj.GetComponent<PlayerAvatar>();
                    if (avatar == null) continue;
                    
                    var playerState = netObj.GetComponent<PlayerState>();
                    if (playerState == null) continue;
                    
                    // Get player name from session data
                    string playerName = $"Player {avatar.OwnerClientId}";
                    if (TryGetPlayer(avatar.OwnerClientId, out var sessionData))
                    {
                        playerName = sessionData.PlayerName.ToString();
                    }
                    
                    // Convert PlayerRoleType to Team
                    var team = avatar.GetTrueRole() == PlayerRoleType.Kavkazi 
                        ? TeamEnum.Kavkazi 
                        : TeamEnum.Innocent;
                    
                    playerSnapshots.Add(new PlayerSnapshot(
                        avatar.OwnerClientId,
                        playerName,
                        team,
                        playerState.IsAlive.Value
                    ));
                }
            }
            
            return new GameSnapshot(playerSnapshots);
        }

        // ========== HELPER METHODS ==========

        private void ValidateAndClampSettings()
        {
            if (!IsServer) return;
            
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = Players.Count };
            var currentSettings = Settings.Value;
            var sanitized = _lobbyValidator.Sanitize(currentSettings, ctx);
            
            if (!sanitized.Equals(currentSettings))
            {
                Debug.Log("[GameSessionManager] Auto-clamping settings due to player count change...");
                Settings.Value = sanitized;
            }
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

        [ContextMenu("Debug: Force Kavkazi Win")]
        private void DebugForceKavkaziWin()
        {
            if (Application.isPlaying && IsServer && CurrentPhase.Value == MatchPhase.MatchInProgress)
            {
                var snapshot = BuildGameSnapshot();
                var result = WinConditions.WinResult.FromSnapshot(snapshot, TeamEnum.Kavkazi, "debug_forced");
                Debug.Log("[GameSessionManager] DEBUG: Forcing Kavkazi win");
                EndMatch(result);
            }
        }

        [ContextMenu("Debug: Force Innocent Win")]
        private void DebugForceInnocentWin()
        {
            if (Application.isPlaying && IsServer && CurrentPhase.Value == MatchPhase.MatchInProgress)
            {
                var snapshot = BuildGameSnapshot();
                var result = WinConditions.WinResult.FromSnapshot(snapshot, TeamEnum.Innocent, "debug_forced");
                Debug.Log("[GameSessionManager] DEBUG: Forcing Innocent win");
                EndMatch(result);
            }
        }

        [ContextMenu("Debug: Check Win Conditions")]
        private void DebugCheckWinConditions()
        {
            if (Application.isPlaying && IsServer)
            {
                var snapshot = BuildGameSnapshot();
                Debug.Log($"=== Game Snapshot ===");
                Debug.Log($"  Alive Kavkazi: {snapshot.AliveKavkaziCount}");
                Debug.Log($"  Alive Innocent: {snapshot.AliveInnocentCount}");
                Debug.Log($"  Total Alive: {snapshot.TotalAliveCount}");
                
                if (_winEvaluator.TryEvaluate(snapshot, out var result))
                {
                    Debug.Log($"  WIN DETECTED: {result.WinningTeamEnum} - {result.ReasonKey}");
                }
                else
                {
                    Debug.Log($"  No win condition met");
                }
            }
        }
#endif

        /// <summary>
        /// Gets a unique suffix for ParrelSync clones to prevent PlayerPrefs sharing.
        /// </summary>
        private static string GetParrelSyncSuffix()
        {
#if UNITY_EDITOR
            try
            {
                var clonesManagerType = System.Type.GetType("ParrelSync.ClonesManager, ParrelSync");
                if (clonesManagerType != null)
                {
                    var isCloneMethod = clonesManagerType.GetMethod("IsClone", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (isCloneMethod != null && (bool)isCloneMethod.Invoke(null, null))
                    {
                        var getArgMethod = clonesManagerType.GetMethod("GetArgument", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        string arg = getArgMethod?.Invoke(null, null) as string ?? "";
                        return string.IsNullOrEmpty(arg) ? "_clone" : $"_clone{arg}";
                    }
                }
            }
            catch
            {
                // ParrelSync not available
            }
#endif
            return "";
        }
    }
}
