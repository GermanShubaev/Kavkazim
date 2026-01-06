using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        //[SerializeField] private float postMatchDuration = 5f;
        [SerializeField] private float transitionDuration = 1.0f;

        // ========== NETWORKED STATE ==========
        
        /// <summary>Current match phase - determines what players can do.</summary>
        public NetworkVariable<MatchPhase> CurrentPhase = new();

        /// <summary>All connected players. Single source of truth.</summary>
        public NetworkList<PlayerSessionData> Players = new();

        /// <summary>Lobby settings configured by host.</summary>
        public NetworkVariable<LobbySettings> Settings = new(
            LobbySettings.Default
        );

        /// <summary>Win result data synced to all clients when game ends.</summary>
        public NetworkVariable<WinResultData> WinResult = new(
            WinResultData.Empty
        );

        /// <summary>Total number of tasks remaining across all innocent players.</summary>
        public NetworkVariable<int> TasksLeft = new(0);

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
            
            // Initialize win condition evaluator with default conditions
            _winEvaluator = WinConditionEvaluator.CreateDefault();
            _lobbyValidator = new LobbyValidator();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[GameSessionManager] OnNetworkSpawn. IsServer={IsServer}");
            
            // CRITICAL: Make this persist across scene loads for ALL clients
            // This must happen early to prevent destruction during scene transitions
            DontDestroyOnLoad(gameObject);
            
            // Ensure validators are initialized (defensive)
            if (_winEvaluator == null) _winEvaluator = WinConditionEvaluator.CreateDefault();
            if (_lobbyValidator == null) _lobbyValidator = new LobbyValidator();
            
            // Subscribe to NetworkVariable/List changes
            if (Players != null) Players.OnListChanged += HandlePlayersListChanged;
            if (Settings != null) Settings.OnValueChanged += HandleSettingsChanged;
            if (CurrentPhase != null) CurrentPhase.OnValueChanged += HandlePhaseChanged;
            if (WinResult != null) WinResult.OnValueChanged += HandleWinResultChanged;
            
            // Subscribe to network events
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
            
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
            if (Players != null) Players.OnListChanged -= HandlePlayersListChanged;
            if (Settings != null) Settings.OnValueChanged -= HandleSettingsChanged;
            if (CurrentPhase != null) CurrentPhase.OnValueChanged -= HandlePhaseChanged;
            if (WinResult != null) WinResult.OnValueChanged -= HandleWinResultChanged;
            
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

        private void OnDestroy()
        {
            // Clear singleton reference when destroyed
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ========== EVENT HANDLERS ==========

        private void HandlePlayersListChanged(NetworkListEvent<PlayerSessionData> changeEvent)
        {
            OnPlayersChanged?.Invoke();
        }

        private void HandleSettingsChanged(LobbySettings previousValue, LobbySettings newValue)
        {
            OnSettingsChanged?.Invoke();
        }

        private void HandlePhaseChanged(MatchPhase previousValue, MatchPhase newValue)
        {
            OnPhaseChanged?.Invoke(newValue);
        }

        private void HandleWinResultChanged(WinResultData previousValue, WinResultData newValue)
        {
            if (newValue.HasEnded)
            {
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
            
            CheckWinConditions();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            
            // Remove from Players list
            for (int i = Players.Count - 1; i >= 0; i--)
            {
                if (Players[i].ClientId == clientId)
                {
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
                    return;
                }
            }
            
            // Player not found - this can happen due to timing (RPC arrives before OnClientConnected)
            // Add them now with the correct name
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
            
            bool isTestMode = Settings.Value.TestMode;
            
            // Require at least 2 eligible players (skip in test mode)
            if (!isTestMode && eligibleCount < 2)
            {
                Debug.LogWarning($"[GameSessionManager] Need at least 2 players to start (have {eligibleCount})");
                return;
            }
            
            // In test mode, require at least 1 player
            if (isTestMode && eligibleCount < 1)
            {
                Debug.LogWarning($"[GameSessionManager] Need at least 1 player to start in test mode");
                return;
            }
            
            // Require all eligible players to be ready
            if (readyCount < eligibleCount)
            {
                Debug.LogWarning($"[GameSessionManager] Not all players ready: {readyCount}/{eligibleCount}");
                return;
            }
            
            // Use Validator for comprehensive checks
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = eligibleCount, IsTestMode = isTestMode }; // Use eligible count for start check
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
            
            // Trigger fade out
            TriggerFadeOutClientRpc(transitionDuration);
            
            StartCoroutine(DelayedStartGame(transitionDuration, eligibleCount, kavkaziCount));
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
            
            // Auto-clamp settings if needed
            if (CurrentPhase.Value == MatchPhase.LobbyOpen)
            {
                 ValidateAndClampSettings();
            }
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
                
                // Trigger fade out for win sequence
                TriggerFadeOutClientRpc(transitionDuration);
                
                // Delay scene load to allow fade
                StartCoroutine(DelayedWinScreenLoad(transitionDuration));
                return; // Exit here, coroutine handles the rest
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
            // Scene load callback - can be used for debugging if needed
        }

        /// <summary>
        /// SERVER RPC: Request return to lobby. Called from win screen button.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ReturnToLobbyServerRpc(RpcParams rpcParams = default)
        {
            // Allow return from any phase except lobby (in case of manual return or error recovery)
            if (CurrentPhase.Value == MatchPhase.LobbyOpen)
            {
                Debug.LogWarning("[GameSessionManager] ReturnToLobby called but already in LobbyOpen");
                return;
            }
            
            // Trigger fade out before returning
            TriggerFadeOutClientRpc(transitionDuration);
            
            StartCoroutine(DelayedReturnToLobby(transitionDuration));
        }

        private IEnumerator DelayedReturnToLobby(float delay)
        {
            yield return new WaitForSeconds(delay);
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
            
            // Clear cached meeting data and player states
            CachedMeetingData = default;
            _cachedPlayerStates.Clear();
            
            // Clean up all gameplay objects before returning to lobby
            DespawnAllDeadBodies();
            DespawnAllPlayerAvatars();
            
            // Return to lobby phase BEFORE loading scene
            CurrentPhase.Value = MatchPhase.LobbyOpen;
            
            // Load GameSession scene (lobby) for all clients
            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.LoadScene("GameSession", LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[GameSessionManager] NetworkManager.SceneManager is null!");
            }
        }
        
        /// <summary>
        /// SERVER ONLY: Despawn all player avatars when returning to lobby.
        /// </summary>
        private void DespawnAllPlayerAvatars()
        {
            if (!IsServer || NetworkManager.SpawnManager == null) return;
            
            var avatars = FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
            
            foreach (var avatar in avatars)
            {
                if (avatar != null && avatar.NetworkObject != null && avatar.NetworkObject.IsSpawned)
                {
                    avatar.NetworkObject.Despawn();
                }
            }
        }

        /// <summary>
        /// Check all win conditions and end match if one is met.
        /// Server only. Disabled in test mode.
        /// </summary>
        public void CheckWinConditions()
        {
            if (!IsServer) return;
            if (CurrentPhase.Value != MatchPhase.MatchInProgress) return;
            
            // Skip win condition checking in test mode
            if (Settings.Value.TestMode)
            {
                return;
            }
            
            var snapshot = BuildGameSnapshot();
            
            if (_winEvaluator.TryEvaluate(snapshot, out var result))
            {
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
            
            // Defensive init
            if (_lobbyValidator == null) _lobbyValidator = new LobbyValidator();

            var currentSettings = Settings.Value;
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = Players.Count, IsTestMode = currentSettings.TestMode };
            var sanitized = _lobbyValidator.Sanitize(currentSettings, ctx);
            
            if (!sanitized.Equals(currentSettings))
            {
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
        }
        

        // ========== MEETING SYSTEM INTEGRATION ==========

        /// <summary>
        /// SERVER ONLY: Load the Meeting scene and start a meeting.
        /// Called by ReportService when a body is reported or emergency meeting called.
        /// </summary>
        public void LoadMeetingScene(Kavkazim.Netcode.Meeting.MeetingStartData meetingData)
        {
            if (!IsServer)
            {
                Debug.LogError("[GameSessionManager] LoadMeetingScene called on client!");
                return;
            }

            if (CurrentPhase.Value != MatchPhase.MatchInProgress)
            {
                Debug.LogWarning($"[GameSessionManager] Cannot start meeting - not in MatchInProgress phase (current: {CurrentPhase.Value})");
                return;
            }

            // CRITICAL: Cache player states NOW (before scene changes)
            // Players are still spawned in GameSession at this point
            CachePlayerStatesBeforeMeeting();
            
            // Trigger Fade Out
            TriggerFadeOutClientRpc(transitionDuration);

            // Clean up dead bodies - they're evidence that's already been discussed
            // Do this in coroutine? No, can do it now or later. But load scene must wait.
            // Move DespawnAllDeadBodies to DelayedMeetingLoad?
            // "Bodies are evidence that was already discussed in the meeting"
            // If we delay, they stay visible during fade out. That is fine.
            
            StartCoroutine(DelayedMeetingLoad(transitionDuration, meetingData));
        }

        /// <summary>
        /// Cached meeting data to pass to MeetingManager after scene loads.
        /// </summary>
        public static Kavkazim.Netcode.Meeting.MeetingStartData CachedMeetingData { get; set; }

        /// <summary>
        /// Cached ID of player eliminated during meeting vote.
        /// ulong.MaxValue means no one was eliminated.
        /// </summary>
        public static ulong CachedEliminatedPlayerId { get; set; } = ulong.MaxValue;

        /// <summary>
        /// Cached player states for respawning after meeting.
        /// Key = ClientId, Value = (Role, IsAlive)
        /// </summary>
        private static Dictionary<ulong, (PlayerRoleType Role, bool IsAlive)> _cachedPlayerStates = new Dictionary<ulong, (PlayerRoleType, bool)>();

        /// <summary>
        /// Get cached player states (for use by MeetingManager to count alive players).
        /// </summary>
        public static Dictionary<ulong, (PlayerRoleType Role, bool IsAlive)> GetCachedPlayerStates()
        {
            return _cachedPlayerStates;
        }

        /// <summary>
        /// SERVER ONLY: Cache all player states before transitioning to meeting.
        /// </summary>
        public void CachePlayerStatesBeforeMeeting()
        {
            if (!IsServer) return;

            _cachedPlayerStates.Clear();
            
            // Force reset elimination ID on cache start
            CachedEliminatedPlayerId = ulong.MaxValue;

            if (NetworkManager.SpawnManager == null)

            {
                Debug.LogWarning("[GameSessionManager] SpawnManager is null, cannot cache player states!");
                return;
            }

            // Find all PlayerState and PlayerAvatar components
            foreach (var netObj in NetworkManager.SpawnManager.SpawnedObjects.Values)
            {
                var playerState = netObj.GetComponent<PlayerState>();
                var playerAvatar = netObj.GetComponent<PlayerAvatar>();

                if (playerState != null && playerAvatar != null)
                {
                    ulong clientId = netObj.OwnerClientId;
                    PlayerRoleType role = playerAvatar.Role.Value;
                    bool isAlive = playerState.IsAlive.Value;

                    _cachedPlayerStates[clientId] = (role, isAlive);
                }
            }
        }

        /// <summary>
        /// SERVER ONLY: Despawn all dead bodies before meeting.
        /// Bodies are evidence that was already discussed in the meeting.
        /// </summary>
        private void DespawnAllDeadBodies()
        {
            if (!IsServer) return;

            // Find all DeadBody NetworkObjects and despawn them
            var deadBodies = FindObjectsByType<Kavkazim.Netcode.Reporting.DeadBody>(FindObjectsSortMode.None);
            
            foreach (var body in deadBodies)
            {
                if (body != null && body.NetworkObject != null)
                {
                    body.NetworkObject.Despawn();
                }
            }
        }

        /// <summary>
        /// SERVER ONLY: Return to gameplay scene and respawn players with preserved state.
        /// </summary>
        public void ReturnToGameplayFromMeeting()
        {
            if (!IsServer)
            {
                Debug.LogError("[GameSessionManager] ReturnToGameplayFromMeeting called on client!");
                return;
            }

            TriggerFadeOutClientRpc(transitionDuration);
            
            StartCoroutine(DelayedReturnToGameplay(transitionDuration));
        }

        /// <summary>
        /// Called when GameSession scene loads after meeting.
        /// </summary>
        private void OnGameSessionSceneLoadedAfterMeeting(ulong clientId, string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadMode)
        {
            if (sceneName != "GameSession") return;

            // Unsubscribe
            NetworkManager.SceneManager.OnLoadComplete -= OnGameSessionSceneLoadedAfterMeeting;

            // Delay slightly to ensure scene is fully loaded
            StartCoroutine(RespawnPlayersAfterMeetingCoroutine());
        }

        /// <summary>
        /// Respawn players with their preserved state after meeting.
        /// </summary>
        private System.Collections.IEnumerator RespawnPlayersAfterMeetingCoroutine()
        {
            yield return new WaitForSeconds(0.5f);

            if (!IsServer) yield break;

            if (PlayerSpawnHandler.Instance == null)
            {
                Debug.LogError("[GameSessionManager] PlayerSpawnHandler.Instance is null!");
                yield break;
            }

            // Create player data list
            List<PlayerSessionData> playersToSpawn = new List<PlayerSessionData>();
            foreach (var player in Players)
            {
                if (_cachedPlayerStates.ContainsKey(player.ClientId))
                {
                    playersToSpawn.Add(player);
                }
            }

            // Spawn all players WITHOUT reassigning roles (we'll restore cached roles after)
            PlayerSpawnHandler.Instance.SpawnGameplayAvatars(playersToSpawn, Settings.Value, skipRoleAssignment: true);

            // Wait for spawns to complete
            yield return new WaitForSeconds(1f);

            // Restore player states
            RestorePlayerStatesAfterMeeting();
        }

        /// <summary>
        /// Restore player states (role, alive/dead) after respawning.
        /// </summary>
        private void RestorePlayerStatesAfterMeeting()
        {
            if (NetworkManager.SpawnManager == null)
            {
                Debug.LogWarning("[GameSessionManager] SpawnManager is null, cannot restore player states!");
                return;
            }

            // Create a copy to avoid "Collection was modified" exception
            var spawnedObjects = NetworkManager.SpawnManager.SpawnedObjects.Values.ToList();

            try
            {
                foreach (var netObj in spawnedObjects)
                {
                    if (netObj == null) continue;

                    var playerState = netObj.GetComponent<PlayerState>();
                    if (playerState != null)
                    {
                        ulong clientId = netObj.OwnerClientId;

                        if (_cachedPlayerStates.TryGetValue(clientId, out var cachedState))
                        {
                            var playerAvatar = netObj.GetComponent<PlayerAvatar>();
                            if (playerAvatar != null)
                            {
                                playerAvatar.Role.Value = cachedState.Role;
                            }

                            // Restore alive/dead state
                            playerState.ForceSetAliveState(cachedState.IsAlive);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            // Apply cached elimination from meeting vote (if any)
            if (CachedEliminatedPlayerId != ulong.MaxValue)
            {
                bool foundEliminated = false;

                // Find the eliminated player's PlayerState
                foreach (var netObj in spawnedObjects)
                {
                    if (netObj == null) continue;
                    if (netObj.OwnerClientId == CachedEliminatedPlayerId)
                    {
                        var playerState = netObj.GetComponent<PlayerState>();
                        if (playerState != null)
                        {
                            playerState.Kill(false); // false = don't spawn body for meeting eliminations
                            foundEliminated = true;
                            break; // Stop calling kill found the player
                        }
                        // If null, it's just another object owned by this client (e.g. manager), keep searching.
                    }
                }
                
                if (!foundEliminated)
                {
                    Debug.LogError($"[GameSessionManager] FATAL: Could not find NetworkObject for eliminated player {CachedEliminatedPlayerId} in spawned objects list!");
                }

                // Clear the cached elimination
                CachedEliminatedPlayerId = ulong.MaxValue;
            }

            // After restoring roles, distribute perceived roles to all clients
            if (PlayerSpawnHandler.Instance != null)
            {
                // Build the spawned players list for role distribution
                var spawnedPlayers = new List<PlayerAvatar>();
                foreach (var netObj in spawnedObjects)
                {
                    if (netObj != null)
                    {
                        var avatar = netObj.GetComponent<PlayerAvatar>();
                        if (avatar != null)
                        {
                            spawnedPlayers.Add(avatar);
                        }
                    }
                }

                // Distribute perceived roles
                StartCoroutine(DistributeRolesAfterRestore(spawnedPlayers));
            }

            // Clear the cache
            _cachedPlayerStates.Clear();
        }

        /// <summary>
        /// Distribute perceived roles to clients after restoring from cache.
        /// </summary>
        private System.Collections.IEnumerator DistributeRolesAfterRestore(List<PlayerAvatar> players)
        {
            yield return null; // Wait a frame for role sync

            foreach (var observer in players)
            {
                if (observer == null) continue;

                PlayerRoleType observerTrueRole = observer.Role.Value;

                // Send perceived role for each player to this observer
                foreach (var target in players)
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

            // NOW check win conditions after all roles are restored and distributed
            yield return null; // One more frame to ensure everything is synced

            CheckWinConditions();
            
            // NOW fade in - respawn is complete
            TriggerFadeInClientRpc(transitionDuration);
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

        // ========== TRANSITION HELPERS ==========

        [Rpc(SendTo.ClientsAndHost)]
        private void TriggerFadeOutClientRpc(float duration)
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.FadeOut(duration);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void TriggerFadeInClientRpc(float duration)
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.FadeIn(duration);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SuppressFadeInClientRpc()
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.SuppressNextAutoFadeIn = true;
            }
        }
        
        private IEnumerator DelayedStartGame(float delay, int eligibleCount, int kavkaziCount)
        {
            yield return new WaitForSeconds(delay);
            
            CurrentPhase.Value = MatchPhase.MatchInProgress;
            
            // Reset meeting state for new game
            CachedEliminatedPlayerId = ulong.MaxValue;
            CachedMeetingData = default;
            _cachedPlayerStates.Clear();
            
            if (Kavkazim.Netcode.Meeting.MeetingManager.Instance != null)
            {
                Kavkazim.Netcode.Meeting.MeetingManager.Instance.ResetForNewGame();
            }
            
            // RESET EMERGENCY TRACKING
            Kavkazim.Netcode.Reporting.ReportService.ResetEmergencyTracking();
            
            // Directly call spawn handler to spawn gameplay avatars
            if (PlayerSpawnHandler.Instance != null)
            {
                PlayerSpawnHandler.Instance.SpawnGameplayAvatars(GetEligiblePlayers(), Settings.Value);
            }
            else
            {
                Debug.LogError("[GameSessionManager] PlayerSpawnHandler.Instance is null!");
            }
            
            // No scene change for start game, so we must trigger fade in manually
            TriggerFadeInClientRpc(transitionDuration);
        }

        private IEnumerator DelayedMeetingLoad(float delay, Kavkazim.Netcode.Meeting.MeetingStartData meetingData)
        {
            yield return new WaitForSeconds(delay);
            
            // Clean up dead bodies - they're evidence that's already been discussed
            DespawnAllDeadBodies();

            // Cache meeting data (MeetingManager will read this after spawning)
            CachedMeetingData = meetingData;

            // Load Meeting scene (MeetingManager will be spawned with the scene)
            // Note: GameSessionManager persists via DontDestroyOnLoad set in OnNetworkSpawn
            if (NetworkManager.SceneManager != null)
            {
                var status = NetworkManager.SceneManager.LoadScene("MeetingScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
                
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError($"[GameSessionManager] Failed to load MeetingScene! Status: {status}");
                    Debug.LogError("[GameSessionManager] Make sure MeetingScene is added to Build Settings!");
                }
                else
                {
                    Debug.Log("[GameSessionManager] MeetingScene load started.");
                }
            }
            else
            {
                Debug.LogError("[GameSessionManager] NetworkManager.SceneManager is null!");
            }
        }
        
        private IEnumerator DelayedReturnToGameplay(float delay)
        {
            yield return new WaitForSeconds(delay);

            CurrentPhase.Value = MatchPhase.MatchInProgress;

            // Suppress auto-fade-in - we'll trigger it manually after respawn
            SuppressFadeInClientRpc();

            // Load GameSession scene
            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnLoadComplete += OnGameSessionSceneLoadedAfterMeeting;
                NetworkManager.SceneManager.LoadScene("GameSession", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[GameSessionManager] NetworkManager.SceneManager is null!");
            }
        }

        private IEnumerator DelayedWinScreenLoad(float delay)
        {
            yield return new WaitForSeconds(delay);
            
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
