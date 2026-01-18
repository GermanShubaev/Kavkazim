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
    [RequireComponent(typeof(NetworkObject))]
    public class GameSessionManager : NetworkBehaviour
    {
        public static GameSessionManager Instance { get; private set; }
        
        public static WinResultData CachedWinResult { get; set; }
        
        public static Dictionary<ulong, string> CachedPlayerNames { get; private set; } = new Dictionary<ulong, string>();

        [Header("Configuration")]
        [SerializeField] private float transitionDuration = 1.0f;

        public NetworkVariable<MatchPhase> CurrentPhase = new();

        public NetworkList<PlayerSessionData> Players = new();

        public NetworkVariable<LobbySettings> Settings = new(
            LobbySettings.Default
        );

        public NetworkVariable<WinResultData> WinResult = new(
            WinResultData.Empty
        );

        public NetworkVariable<int> TasksLeft = new(0);

        public event Action OnPlayersChanged;
        
        public event Action OnSettingsChanged;
        
        public event Action<MatchPhase> OnPhaseChanged;
        
        public event Action<WinResultData> OnGameEnded;
        
        private WinConditionEvaluator _winEvaluator;
        private LobbyValidator _lobbyValidator;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameSessionManager] Duplicate instance detected, destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            _winEvaluator = WinConditionEvaluator.CreateDefault();
            _lobbyValidator = new LobbyValidator();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[GameSessionManager] OnNetworkSpawn. IsServer={IsServer}");
            
            DontDestroyOnLoad(gameObject);
            
            if (_winEvaluator == null) _winEvaluator = WinConditionEvaluator.CreateDefault();
            if (_lobbyValidator == null) _lobbyValidator = new LobbyValidator();
            
            if (Players != null) Players.OnListChanged += HandlePlayersListChanged;
            if (Settings != null) Settings.OnValueChanged += HandleSettingsChanged;
            if (CurrentPhase != null) CurrentPhase.OnValueChanged += HandlePhaseChanged;
            if (WinResult != null) WinResult.OnValueChanged += HandleWinResultChanged;
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
            
            if (IsServer)
            {
                PlayerState.OnPlayerKilled += OnPlayerKilledForWinCheck;
            }
            
            if (IsServer)
            {
                if (Settings.Value.MaxPlayers <= 0)
                {
                    Settings.Value = LobbySettings.Default;
                }
                if (CachedWinResult.HasEnded || CurrentPhase.Value == MatchPhase.PostMatch)
                {
                    Players.Clear();
                    
                    foreach (var kvp in CachedPlayerNames)
                    {
                        AddPlayer(kvp.Key, kvp.Value);
                    }
                    
                    WinResult.Value = WinResultData.Empty;
                    CachedWinResult = WinResultData.Empty;
                    CachedPlayerNames.Clear();
                    CurrentPhase.Value = MatchPhase.LobbyOpen;
                }
                else
                {
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
            
            OnPlayersChanged?.Invoke();
            OnSettingsChanged?.Invoke();
            OnPhaseChanged?.Invoke(CurrentPhase.Value);
            
            if (IsClient && !IsServer)
            {
                bool isReturning = CachedWinResult.HasEnded || CurrentPhase.Value == MatchPhase.PostMatch;
                
                if (isReturning)
                {
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
            
            if (IsServer)
            {
                PlayerState.OnPlayerKilled -= OnPlayerKilledForWinCheck;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

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

        private void OnPlayerKilledForWinCheck(PlayerState killedPlayer)
        {
            if (!IsServer) return;
            if (CurrentPhase.Value != MatchPhase.MatchInProgress) return;
            
            CheckWinConditions();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            
            for (int i = Players.Count - 1; i >= 0; i--)
            {
                if (Players[i].ClientId == clientId)
                {
                    Players.RemoveAt(i);
                    if (IsServer && CurrentPhase.Value == MatchPhase.LobbyOpen)
                    {
                        ValidateAndClampSettings();
                    }
                    break;
                }
            }
            
            if (CurrentPhase.Value == MatchPhase.MatchInProgress)
            {
                DespawnPlayerAvatar(clientId);
                CheckWinConditions();
            }
        }

        [Rpc(SendTo.Server)]
        public void SubmitPlayerNameServerRpc(string playerName, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            
            if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 20)
            {
                playerName = $"Player {clientId}";
            }
            playerName = playerName.Trim();
            
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
            
            AddPlayer(clientId, playerName);
        }

        [Rpc(SendTo.Server)]
        public void SetReadyServerRpc(bool ready, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            
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

        [Rpc(SendTo.Server)]
        public void UpdateSettingsServerRpc(LobbySettings newSettings, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            
            if (senderId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"[GameSessionManager] Non-host tried to change settings (Client {senderId})");
                return;
            }
            
            if (CurrentPhase.Value != MatchPhase.LobbyOpen)
            {
                Debug.LogWarning("[GameSessionManager] Cannot change settings during match");
                return;
            }
            
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = Players.Count };
            newSettings = _lobbyValidator.Sanitize(newSettings, ctx);
            Settings.Value = newSettings;
        }

        [Rpc(SendTo.Server)]
        public void StartGameServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            
            if (senderId != NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"[GameSessionManager] Non-host tried to start game (Client {senderId})");
                return;
            }
            
            if (CurrentPhase.Value != MatchPhase.LobbyOpen)
            {
                Debug.LogWarning("[GameSessionManager] Cannot start game - not in lobby phase");
                return;
            }
            
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
            
            if (!isTestMode && eligibleCount < 2)
            {
                Debug.LogWarning($"[GameSessionManager] Need at least 2 players to start (have {eligibleCount})");
                return;
            }
            
            if (isTestMode && eligibleCount < 1)
            {
                Debug.LogWarning($"[GameSessionManager] Need at least 1 player to start in test mode");
                return;
            }
            
            if (readyCount < eligibleCount)
            {
                Debug.LogWarning($"[GameSessionManager] Not all players ready: {readyCount}/{eligibleCount}");
                return;
            }
            
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = eligibleCount, IsTestMode = isTestMode };
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
            
            TriggerFadeOutClientRpc(transitionDuration);
            
            StartCoroutine(DelayedStartGame(transitionDuration, eligibleCount, kavkaziCount));
        }

        public void AddPlayer(ulong clientId, string playerName)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[GameSessionManager] AddPlayer called on client");
                return;
            }
            
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
                IsReady = isHost,
                IsHost = isHost,
                JoinedDuringMatch = joinedDuringMatch
            };
            
            Players.Add(newPlayer);
            
            if (CurrentPhase.Value == MatchPhase.LobbyOpen)
            {
                 ValidateAndClampSettings();
            }
        }

        public void EndMatch()
        {
            EndMatch(null);
        }

        public void EndMatch(WinConditions.WinResult winResult)
        {
            if (!IsServer) return;
            
            if (CurrentPhase.Value != MatchPhase.MatchInProgress)
            {
                Debug.LogWarning("[GameSessionManager] EndMatch called but not in match");
                return;
            }
            
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
                
                TriggerFadeOutClientRpc(transitionDuration);
                
                StartCoroutine(DelayedWinScreenLoad(transitionDuration));
                return;
            }
            else
            {
                WinResult.Value = new WinResultData
                {
                    WinningTeam = 0,
                    WinnerNames = "",
                    ReasonKey = "match_aborted",
                    HasEnded = true
                };
            }
            
            CurrentPhase.Value = MatchPhase.PostMatch;
            
            CachedWinResult = WinResult.Value;
            
            CachedPlayerNames.Clear();
            foreach (var player in Players)
            {
                CachedPlayerNames[player.ClientId] = player.PlayerName.ToString();
            }
            
            CacheWinResultClientRpc(WinResult.Value);
            
            var cachedNames = new List<KeyValuePair<ulong, string>>(CachedPlayerNames);
            foreach (var kvp in cachedNames)
            {
                CachePlayerNameClientRpc(kvp.Key, kvp.Value);
            }
            
            DespawnAllAvatars();
            LoadWinScreenScene();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CacheWinResultClientRpc(WinResultData winResult)
        {
            CachedWinResult = winResult;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CachePlayerNameClientRpc(ulong clientId, string playerName)
        {
            CachedPlayerNames[clientId] = playerName;
        }

        private void LoadWinScreenScene()
        {
            if (!IsServer) return;
            
            if (NetworkManager.SceneManager != null)
            {
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

        private void OnSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            // Scene load callback - for Debug
        }

        [Rpc(SendTo.Server)]
        public void ReturnToLobbyServerRpc(RpcParams rpcParams = default)
        {
            if (CurrentPhase.Value == MatchPhase.LobbyOpen)
            {
                Debug.LogWarning("[GameSessionManager] ReturnToLobby called but already in LobbyOpen");
                return;
            }
            
            TriggerFadeOutClientRpc(transitionDuration);
            StartCoroutine(DelayedReturnToLobby(transitionDuration));
        }

        private IEnumerator DelayedReturnToLobby(float delay)
        {
            yield return new WaitForSeconds(delay);
            PerformReturnToLobby();
        }

        private void PerformReturnToLobby()
        {
            if (!IsServer) return;
            
            for (int i = 0; i < Players.Count; i++)
            {

                var player = Players[i];
                
                player.JoinedDuringMatch = false;
                player.IsReady = player.IsHost;
                Players[i] = player;
            }
            
            WinResult.Value = WinResultData.Empty;
            
            CachedMeetingData = default;
            _cachedPlayerStates.Clear();
            
            DespawnAllDeadBodies();
            DespawnAllPlayerAvatars();
            
            CurrentPhase.Value = MatchPhase.LobbyOpen;
            
            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.LoadScene("GameSession", LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[GameSessionManager] NetworkManager.SceneManager is null!");
            }
        }
        
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

        public void CheckWinConditions()
        {
            if (!IsServer) return;
            if (CurrentPhase.Value != MatchPhase.MatchInProgress) return;
            
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

        public GameSnapshot BuildGameSnapshot()
        {
            var playerSnapshots = new List<PlayerSnapshot>();
            
            if (NetworkManager.SpawnManager != null)
            {
                foreach (var netObj in NetworkManager.SpawnManager.SpawnedObjects.Values)
                {
                    var avatar = netObj.GetComponent<PlayerAvatar>();
                    if (avatar == null) continue;
                    
                    var playerState = netObj.GetComponent<PlayerState>();
                    if (playerState == null) continue;
                    
                    string playerName = $"Player {avatar.OwnerClientId}";
                    if (TryGetPlayer(avatar.OwnerClientId, out var sessionData))
                    {
                        playerName = sessionData.PlayerName.ToString();
                    }
                    
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

        private void ValidateAndClampSettings()
        {
            if (!IsServer) return;
            
            if (_lobbyValidator == null) _lobbyValidator = new LobbyValidator();

            var currentSettings = Settings.Value;
            var ctx = new LobbyRuntimeContext { CurrentPlayerCount = Players.Count, IsTestMode = currentSettings.TestMode };
            var sanitized = _lobbyValidator.Sanitize(currentSettings, ctx);
            
            if (!sanitized.Equals(currentSettings))
            {
                Settings.Value = sanitized;
            }
        }

        public int GetEligiblePlayerCount()
        {
            int count = 0;
            foreach (var player in Players)
            {
                if (!player.JoinedDuringMatch) count++;
            }
            return count;
        }

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

            CachePlayerStatesBeforeMeeting();
            TriggerFadeOutClientRpc(transitionDuration);
            StartCoroutine(DelayedMeetingLoad(transitionDuration, meetingData));
        }

        public static Kavkazim.Netcode.Meeting.MeetingStartData CachedMeetingData { get; set; }

        public static ulong CachedEliminatedPlayerId { get; set; } = ulong.MaxValue;

        private static Dictionary<ulong, (PlayerRoleType Role, bool IsAlive)> _cachedPlayerStates = new Dictionary<ulong, (PlayerRoleType, bool)>();

        public static Dictionary<ulong, (PlayerRoleType Role, bool IsAlive)> GetCachedPlayerStates()
        {
            return _cachedPlayerStates;
        }

        public void CachePlayerStatesBeforeMeeting()
        {
            if (!IsServer) return;

            _cachedPlayerStates.Clear();
            
            CachedEliminatedPlayerId = ulong.MaxValue;

            if (NetworkManager.SpawnManager == null)

            {
                Debug.LogWarning("[GameSessionManager] SpawnManager is null, cannot cache player states!");
                return;
            }

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

        private void DespawnAllDeadBodies()
        {
            if (!IsServer) return;

            var deadBodies = FindObjectsByType<Kavkazim.Netcode.Reporting.DeadBody>(FindObjectsSortMode.None);
            
            foreach (var body in deadBodies)
            {
                if (body != null && body.NetworkObject != null)
                {
                    body.NetworkObject.Despawn();
                }
            }
        }

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

        private void OnGameSessionSceneLoadedAfterMeeting(ulong clientId, string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadMode)
        {
            if (sceneName != "GameSession") return;

            NetworkManager.SceneManager.OnLoadComplete -= OnGameSessionSceneLoadedAfterMeeting;
            StartCoroutine(RespawnPlayersAfterMeetingCoroutine());
        }

        private System.Collections.IEnumerator RespawnPlayersAfterMeetingCoroutine()
        {
            yield return new WaitForSeconds(0.5f);

            if (!IsServer) yield break;

            if (PlayerSpawnHandler.Instance == null)
            {
                Debug.LogError("[GameSessionManager] PlayerSpawnHandler.Instance is null!");
                yield break;
            }

            List<PlayerSessionData> playersToSpawn = new List<PlayerSessionData>();
            foreach (var player in Players)
            {
                if (_cachedPlayerStates.ContainsKey(player.ClientId))
                {
                    playersToSpawn.Add(player);
                }
            }

            PlayerSpawnHandler.Instance.SpawnGameplayAvatars(playersToSpawn, Settings.Value, skipRoleAssignment: true);

            yield return new WaitForSeconds(1f);

            RestorePlayerStatesAfterMeeting();
        }

        private void RestorePlayerStatesAfterMeeting()
        {
            if (NetworkManager.SpawnManager == null)
            {
                Debug.LogWarning("[GameSessionManager] SpawnManager is null, cannot restore player states!");
                return;
            }

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

                            playerState.ForceSetAliveState(cachedState.IsAlive);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            if (CachedEliminatedPlayerId != ulong.MaxValue)
            {
                bool foundEliminated = false;

                foreach (var netObj in spawnedObjects)
                {
                    if (netObj == null) continue;
                    if (netObj.OwnerClientId == CachedEliminatedPlayerId)
                    {
                        var playerState = netObj.GetComponent<PlayerState>();
                        if (playerState != null)
                        {
                            playerState.Kill(false);
                            foundEliminated = true;
                            break;
                        }
                    }
                }
                
                if (!foundEliminated)
                {
                    Debug.LogError($"[GameSessionManager] FATAL: Could not find NetworkObject for eliminated player {CachedEliminatedPlayerId} in spawned objects list!");
                }

                CachedEliminatedPlayerId = ulong.MaxValue;
            }

            if (PlayerSpawnHandler.Instance != null)
            {
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

                StartCoroutine(DistributeRolesAfterRestore(spawnedPlayers));
            }

            _cachedPlayerStates.Clear();
        }

        private System.Collections.IEnumerator DistributeRolesAfterRestore(List<PlayerAvatar> players)
        {
            yield return null;

            foreach (var observer in players)
            {
                if (observer == null) continue;

                PlayerRoleType observerTrueRole = observer.Role.Value;

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

            yield return null;

            CheckWinConditions();
            TriggerFadeInClientRpc(transitionDuration);
        }

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
            
            CachedEliminatedPlayerId = ulong.MaxValue;
            CachedMeetingData = default;
            _cachedPlayerStates.Clear();
            
            if (Kavkazim.Netcode.Meeting.MeetingManager.Instance != null)
            {
                Kavkazim.Netcode.Meeting.MeetingManager.Instance.ResetForNewGame();
            }
            
            Kavkazim.Netcode.Reporting.ReportService.ResetEmergencyTracking();
            
            if (PlayerSpawnHandler.Instance != null)
            {
                PlayerSpawnHandler.Instance.SpawnGameplayAvatars(GetEligiblePlayers(), Settings.Value);
            }
            else
            {
                Debug.LogError("[GameSessionManager] PlayerSpawnHandler.Instance is null!");
            }
            
            TriggerFadeInClientRpc(transitionDuration);
        }

        private IEnumerator DelayedMeetingLoad(float delay, Kavkazim.Netcode.Meeting.MeetingStartData meetingData)
        {
            yield return new WaitForSeconds(delay);
            
            DespawnAllDeadBodies();

            CachedMeetingData = meetingData;

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

            SuppressFadeInClientRpc();

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
            
            CurrentPhase.Value = MatchPhase.PostMatch;
            CachedWinResult = WinResult.Value;
            CachedPlayerNames.Clear();
            foreach (var player in Players)
            {
                CachedPlayerNames[player.ClientId] = player.PlayerName.ToString();
            }
            
            CacheWinResultClientRpc(WinResult.Value);
            
            var cachedNames = new List<KeyValuePair<ulong, string>>(CachedPlayerNames);
            foreach (var kvp in cachedNames)
            {
                CachePlayerNameClientRpc(kvp.Key, kvp.Value);
            }
            
            DespawnAllAvatars();
            LoadWinScreenScene();
        }

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
                // ParrelSync..
            }
#endif
            return "";
        }
    }
}
