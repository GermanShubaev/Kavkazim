using System;
using System.Collections.Generic;
using System.Linq;
using Kavkazim.Config;
using Kavkazim.Netcode;
using Netcode.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode.Meeting
{
    /// <summary>
    /// Server-authoritative meeting manager.
    /// Handles voting, timer, and meeting flow.
    /// NetworkBehaviour spawned with MeetingScene.
    /// </summary>
    public class MeetingManager : NetworkBehaviour
    {
        public static MeetingManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private float meetingDuration = 60f;

        // ========== NETWORKED STATE ==========

        /// <summary>Data about how the meeting was started.</summary>
        public NetworkVariable<MeetingStartData> MeetingData = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>Time remaining in the voting period.</summary>
        public NetworkVariable<float> TimeRemaining = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>Number of votes submitted (not who voted for whom).</summary>
        public NetworkVariable<int> VotesSubmitted = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>Number of skip votes (for UI display).</summary>
        public NetworkVariable<int> SkipVoteCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>List of all players in the meeting (alive + dead).</summary>
        public NetworkList<ulong> PlayersInMeeting;
        
        /// <summary>List of ALIVE player IDs in the meeting (for client-side alive check).</summary>
        public NetworkList<ulong> AlivePlayersInMeeting;

        /// <summary>Has the meeting ended?</summary>
        public NetworkVariable<bool> HasEnded = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // ========== SERVER-ONLY STATE ==========

        /// <summary>Server-only: Map of clientId -> VoteTarget (anonymous).</summary>
        private Dictionary<ulong, VoteTarget> _votes = new Dictionary<ulong, VoteTarget>();

        /// <summary>Server-only: Track who has voted (for double-vote prevention).</summary>
        private HashSet<ulong> _hasVoted = new HashSet<ulong>();

        /// <summary>Server-only: Cached config reference.</summary>
        private NetworkGameplayConfig _config;

        // ========== EVENTS ==========

        /// <summary>Fired when meeting ends and results are calculated.</summary>
        public static event Action<MeetingResult> OnMeetingEnded;

        /// <summary>Fired when meeting starts (for audio/UI hooks).</summary>
        public static event Action<MeetingStartData> OnMeetingStarted;

        /// <summary>Fired when time is low (< 10s).</summary>
        public static event Action OnTimeLow;

        /// <summary>Fired on each client when they successfully submit a vote.</summary>
        public static event Action OnVoteSubmitted;

        private bool _timeLowFired = false;

        // ========== LIFECYCLE ==========

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MeetingManager] Duplicate instance detected, destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initialize NetworkLists
            PlayersInMeeting = new NetworkList<ulong>();
            AlivePlayersInMeeting = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[MeetingManager] OnNetworkSpawn. IsServer={IsServer}");

            // Server initializes meeting
            if (IsServer)
            {
                // Load meeting duration from GameSessionManager settings (lobby setting)
                if (GameSessionManager.Instance != null)
                {
                    meetingDuration = GameSessionManager.Instance.Settings.Value.VotingTime;
                    Debug.Log($"[MeetingManager] Using lobby VotingTime: {meetingDuration}s");
                }
                else
                {
                    // Fallback to config if GameSessionManager not available
                    _config = Resources.Load<NetworkGameplayConfig>("NetworkGameplayConfig");
                    if (_config != null)
                    {
                        meetingDuration = _config.meetingDuration;
                        Debug.Log($"[MeetingManager] Fallback to config meeting duration: {meetingDuration}s");
                    }
                }

                // Read cached meeting data and start meeting
                // Fix: Check !CallerName.IsEmpty because CallerId can be 0 (Host)
                if (!GameSessionManager.CachedMeetingData.CallerName.IsEmpty)
                {
                    Debug.Log("[MeetingManager] Found cached meeting data, starting meeting...");
                    StartMeeting(GameSessionManager.CachedMeetingData);
                    
                    // Clear cache
                    GameSessionManager.CachedMeetingData = default;
                }
                else
                {
                    Debug.LogWarning("[MeetingManager] No cached meeting data found!");
                }
            }

            // Subscribe to disconnect events (server + client for cleanup)
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Reset meeting state for a new game.
        /// Called by GameSessionManager when starting a new game.
        /// </summary>
        public void ResetForNewGame()
        {
            if (!IsServer) return;
            
            Debug.Log("[MeetingManager] Resetting for new game...");
            
            HasEnded.Value = false;
            TimeRemaining.Value = 0;
            VotesSubmitted.Value = 0;
            SkipVoteCount.Value = 0;
            MeetingData.Value = default;
            
            PlayersInMeeting.Clear();
            AlivePlayersInMeeting.Clear();
            
            _votes.Clear();
            _hasVoted.Clear();
            _timeLowFired = false;
        }

        private void Update()
        {
            if (!IsServer || HasEnded.Value) return;

            // Tick timer
            if (TimeRemaining.Value > 0)
            {
                TimeRemaining.Value -= Time.deltaTime;

                // Fire time low event once
                if (!_timeLowFired && TimeRemaining.Value <= 10f)
                {
                    _timeLowFired = true;
                    FireTimeLowClientRpc();
                }

                // Timer expired → end meeting
                if (TimeRemaining.Value <= 0)
                {
                    Debug.Log("[MeetingManager] Timer expired, ending meeting.");
                    EndMeeting();
                }
            }
        }

        // ========== PUBLIC API (Server) ==========

        /// <summary>
        /// SERVER ONLY: Start a meeting with the given data.
        /// Called by ReportService or GameSessionManager.
        /// </summary>
        public void StartMeeting(MeetingStartData data)
        {
            if (!IsServer)
            {
                Debug.LogError("[MeetingManager] StartMeeting called on client!");
                return;
            }

            if (HasEnded.Value)
            {
                Debug.LogWarning("[MeetingManager] Meeting already ended, cannot start another.");
                return;
            }

            Debug.Log($"[MeetingManager] Starting meeting: {data}");

            // Set meeting data
            MeetingData.Value = data;
            TimeRemaining.Value = meetingDuration;
            VotesSubmitted.Value = 0;
            SkipVoteCount.Value = 0;
            HasEnded.Value = false;
            _timeLowFired = false;

            // Populate players in meeting from GameSessionManager
            PlayersInMeeting.Clear();
            AlivePlayersInMeeting.Clear();
            
            if (GameSessionManager.Instance != null)
            {
                var cachedStates = GameSessionManager.GetCachedPlayerStates();
                
                foreach (var playerData in GameSessionManager.Instance.Players)
                {
                    PlayersInMeeting.Add(playerData.ClientId);
                    
                    // Add to alive list only if player is alive (from cached states)
                    if (cachedStates.TryGetValue(playerData.ClientId, out var state) && state.IsAlive)
                    {
                        AlivePlayersInMeeting.Add(playerData.ClientId);
                    }
                }
                Debug.Log($"[MeetingManager] Added {PlayersInMeeting.Count} players to meeting ({AlivePlayersInMeeting.Count} alive).");
            }
            else
            {
                Debug.LogWarning("[MeetingManager] GameSessionManager not found, cannot populate players!");
            }

            // Clear vote tracking
            _votes.Clear();
            _hasVoted.Clear();

            // Transition to Meeting phase
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.CurrentPhase.Value = MatchPhase.Meeting;
                Debug.Log("[MeetingManager] Transitioned to Meeting phase.");
            }

            // Fire started event on all clients
            FireMeetingStartedClientRpc(data);

            Debug.Log($"[MeetingManager] Meeting started. Duration: {meetingDuration}s");
        }

        /// <summary>
        /// SERVER RPC: Client submits a vote.
        /// Single-click voting: immediately submits vote (no confirmation).
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitVoteServerRpc(ulong targetClientId, bool isSkip, RpcParams rpcParams = default)
        {
            ulong voterId = rpcParams.Receive.SenderClientId;

            // Validation 1: Has meeting ended?
            if (HasEnded.Value)
            {
                Debug.LogWarning($"[MeetingManager] Vote rejected: meeting has ended");
                return;
            }

            // Validation 2: Has player already voted?
            if (_hasVoted.Contains(voterId))
            {
                Debug.LogWarning($"[MeetingManager] Vote rejected: player {voterId} already voted");
                return;
            }

            // Validation 3: Is voter alive? Use cached states since PlayerState doesn't exist in MeetingScene
            var cachedStates = GameSessionManager.GetCachedPlayerStates();
            if (cachedStates != null && cachedStates.TryGetValue(voterId, out var voterCached))
            {
                if (!voterCached.IsAlive)
                {
                    Debug.LogWarning($"[MeetingManager] Vote rejected: player {voterId} is dead");
                    return;
                }
            }

            // Validation 4: Is target valid (if not skip)?
            if (!isSkip)
            {
                bool targetExists = PlayersInMeeting.Contains(targetClientId);
                if (!targetExists)
                {
                    Debug.LogWarning($"[MeetingManager] Vote rejected: invalid target {targetClientId}");
                    return;
                }
            }

            // Record vote
            VoteTarget vote = isSkip ? VoteTarget.CreateSkip() : VoteTarget.CreatePlayerVote(targetClientId);
            _votes[voterId] = vote;
            _hasVoted.Add(voterId);

            VotesSubmitted.Value++;
            if (isSkip)
            {
                SkipVoteCount.Value++;
            }

            // Notify voter client that their vote was accepted
            ConfirmVoteClientRpc(RpcTarget.Single(voterId, RpcTargetUse.Temp));

            // Check if all alive players have voted
            int aliveCount = CountAlivePlayers();
            Debug.Log($"[MeetingManager] Vote recorded: {VotesSubmitted.Value}/{aliveCount} votes");
            
            if (VotesSubmitted.Value >= aliveCount)
            {
                Debug.Log($"[MeetingManager] All players voted, ending meeting...");
                EndMeeting();
            }
        }

        // ========== MEETING END & RESULTS ==========

        /// <summary>
        /// SERVER ONLY: End the meeting, compute results, eliminate player, check win conditions.
        /// </summary>
        private void EndMeeting()
        {
            if (!IsServer || HasEnded.Value) return;

            HasEnded.Value = true;

            // Compute results and print summary
            MeetingResult result = ComputeResults();

            // Broadcast results to all clients
            BroadcastResultsClientRpc(result);

            // Fire server-side event
            OnMeetingEnded?.Invoke(result);

            // Cache elimination to apply after respawn (can't apply here - no PlayerState in MeetingScene)
            if (result.EliminatedId != ulong.MaxValue)
            {
                Debug.Log($"[MEETING RESULT] >>> {result.EliminatedName} WILL BE ELIMINATED (ClientId {result.EliminatedId}) <<<");
                GameSessionManager.CachedEliminatedPlayerId = result.EliminatedId;
            }
            else
            {
                if (result.IsTie)
                    Debug.Log("[MEETING RESULT] >>> TIE - NO ELIMINATION <<<");
                else if (result.SkipWon)
                    Debug.Log("[MEETING RESULT] >>> SKIP WON - NO ELIMINATION <<<");
                else
                    Debug.Log("[MEETING RESULT] >>> NO VOTES CAST - NO ELIMINATION <<<");
                    
                GameSessionManager.CachedEliminatedPlayerId = ulong.MaxValue;
            }

            // Wait a few seconds before returning to gameplay
            StartCoroutine(ReturnToGameplayAfterDelay(3f, result));
        }

        /// <summary>
        /// Compute vote results.
        /// Returns MeetingResult with eliminated player or tie/skip info.
        /// </summary>
        private MeetingResult ComputeResults()
        {
            // Tally votes
            Dictionary<ulong, int> voteCounts = new Dictionary<ulong, int>();
            int skipCount = 0;

            foreach (var vote in _votes.Values)
            {
                if (vote.IsSkip)
                {
                    skipCount++;
                }
                else
                {
                    if (!voteCounts.ContainsKey(vote.TargetClientId))
                    {
                        voteCounts[vote.TargetClientId] = 0;
                    }
                    voteCounts[vote.TargetClientId]++;
                }
            }

            int totalVotes = _votes.Count;

            // === VOTING SUMMARY LOG ===
            Debug.Log($"[VOTING SUMMARY] ============================================");
            Debug.Log($"[VOTING SUMMARY] Total votes: {totalVotes} | Skip votes: {skipCount}");
            
            // Show ALL players with their vote counts (including 0)
            foreach (ulong clientId in PlayersInMeeting)
            {
                string playerName = GetPlayerName(clientId);
                int voteCount = voteCounts.ContainsKey(clientId) ? voteCounts[clientId] : 0;
                Debug.Log($"[VOTING SUMMARY] {playerName}: {voteCount} votes");
            }
            Debug.Log($"[VOTING SUMMARY] ============================================");

            // Find player(s) with most votes
            if (voteCounts.Count == 0)
            {
                return MeetingResult.CreateNoElimination(false, skipCount, totalVotes);
            }

            int maxVotes = voteCounts.Values.Max();

            // Check if skip has more votes
            if (skipCount > maxVotes)
            {
                return MeetingResult.CreateNoElimination(false, skipCount, totalVotes);
            }

            // Check for tie
            var topVoted = voteCounts.Where(kvp => kvp.Value == maxVotes).ToList();
            if (topVoted.Count > 1)
            {
                return MeetingResult.CreateNoElimination(true, skipCount, totalVotes);
            }

            // Single player with most votes → eliminate
            ulong eliminatedId = topVoted[0].Key;
            string eliminatedName = GetPlayerName(eliminatedId);
            return MeetingResult.CreateElimination(eliminatedId, eliminatedName, maxVotes, skipCount, totalVotes);
        }

        /// <summary>
        /// Apply elimination: kill the player.
        /// </summary>
        private void ApplyElimination(ulong clientId)
        {
            PlayerState playerState = FindPlayerByClientId(clientId);
            if (playerState == null)
            {
                Debug.LogWarning($"[MeetingManager] Cannot eliminate: PlayerState for {clientId} not found.");
                return;
            }

            if (!playerState.IsAlive.Value)
            {
                Debug.LogWarning($"[MeetingManager] Player {clientId} is already dead.");
                return;
            }

            Debug.Log($"[MeetingManager] Eliminating player {clientId}...");
            playerState.Kill();
        }

        /// <summary>
        /// Wait a few seconds, then return to gameplay.
        /// Win conditions will be checked after players respawn with restored roles.
        /// </summary>
        private System.Collections.IEnumerator ReturnToGameplayAfterDelay(float delay, MeetingResult result)
        {
            yield return new WaitForSeconds(delay);

            Debug.Log("[MeetingManager] Returning to gameplay...");

            if (GameSessionManager.Instance != null)
            {
                // Load GameSession scene - GameSessionManager will handle respawn and win check
                // Player states were already cached before meeting started
                GameSessionManager.Instance.ReturnToGameplayFromMeeting();
            }
            else
            {
                Debug.LogError("[MeetingManager] GameSessionManager not found after meeting!");
            }
        }

        // ========== CLIENT RPCs ==========

        [Rpc(SendTo.ClientsAndHost)]
        private void FireMeetingStartedClientRpc(MeetingStartData data)
        {
            Debug.Log($"[MeetingManager] Client: Meeting started - {data}");
            OnMeetingStarted?.Invoke(data);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void FireTimeLowClientRpc()
        {
            Debug.Log("[MeetingManager] Client: Time low warning!");
            OnTimeLow?.Invoke();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastResultsClientRpc(MeetingResult result)
        {
            Debug.Log($"[MeetingManager] Client: Meeting results - {result}");
            // UI will display results
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ConfirmVoteClientRpc(RpcParams rpcParams)
        {
            Debug.Log("[MeetingManager] Client: Vote confirmed!");
            OnVoteSubmitted?.Invoke();
        }

        // ========== DISCONNECT HANDLING ==========

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            Debug.Log($"[MeetingManager] Client {clientId} disconnected during meeting.");

            // Remove from players in meeting
            for (int i = PlayersInMeeting.Count - 1; i >= 0; i--)
            {
                if (PlayersInMeeting[i] == clientId)
                {
                    PlayersInMeeting.RemoveAt(i);
                    break;
                }
            }

            // Keep their vote if they already voted (anonymous voting)
            // But recalculate if all remaining alive players have voted
            int aliveCount = CountAlivePlayers();
            if (VotesSubmitted.Value >= aliveCount && !HasEnded.Value)
            {
                Debug.Log($"[MeetingManager] After disconnect, all alive players voted. Ending meeting.");
                EndMeeting();
            }
        }

        // ========== HELPER METHODS ==========

        private PlayerState FindPlayerByClientId(ulong clientId)
        {
            if (NetworkManager.SpawnManager == null) return null;

            foreach (var netObj in NetworkManager.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj.OwnerClientId == clientId)
                {
                    var playerState = netObj.GetComponent<PlayerState>();
                    if (playerState != null)
                        return playerState;
                }
            }
            return null;
        }

        private string GetPlayerName(ulong clientId)
        {
            string name = $"Player {clientId}";
            if (GameSessionManager.Instance != null)
            {
                foreach (var playerData in GameSessionManager.Instance.Players)
                {
                    if (playerData.ClientId == clientId)
                    {
                        name = playerData.PlayerName.ToString();
                        break;
                    }
                }
            }
            return name;
        }

        private int CountAlivePlayers()
        {
            // IMPORTANT: In MeetingScene, PlayerState objects don't exist!
            // Use cached player states from GameSessionManager instead
            int count = 0;
            
            // First try to use cached player states (from before meeting)
            var cachedStates = GameSessionManager.GetCachedPlayerStates();
            if (cachedStates != null && cachedStates.Count > 0)
            {
                foreach (var kvp in cachedStates)
                {
                    if (kvp.Value.IsAlive)
                    {
                        count++;
                    }
                }
                Debug.Log($"[MeetingManager] CountAlivePlayers (from cache): {count}");
                return count;
            }
            
            // Fallback: Count from PlayersInMeeting (assuming all are alive)
            // This is less accurate but ensures meeting can conclude
            count = PlayersInMeeting.Count;
            Debug.Log($"[MeetingManager] CountAlivePlayers (fallback - all players): {count}");
            return count;
        }

        // ========== DEBUG COMMANDS ==========

#if UNITY_EDITOR
        [ContextMenu("Force End Meeting")]
        private void DebugForceEndMeeting()
        {
            if (!IsServer) return;
            Debug.Log("[MeetingManager] DEBUG: Force ending meeting...");
            EndMeeting();
        }

        [ContextMenu("Print Vote Tally")]
        private void DebugPrintVoteTally()
        {
            if (!IsServer) return;
            Debug.Log($"[MeetingManager] === VOTE TALLY ===");
            Debug.Log($"Total votes: {_votes.Count}");
            foreach (var kvp in _votes)
            {
                Debug.Log($"  {GetPlayerName(kvp.Key)} voted for: {kvp.Value}");
            }
        }

        [ContextMenu("Simulate Timeout (1s)")]
        private void DebugSimulateTimeout()
        {
            if (!IsServer) return;
            Debug.Log("[MeetingManager] DEBUG: Setting timer to 1 second...");
            TimeRemaining.Value = 1f;
        }
#endif
    }
}
