using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode.Meeting
{
    /// <summary>
    /// Server-authoritative meeting manager.
    /// Handles voting, timer, and meeting flow.
    /// </summary>
    public class MeetingManager : NetworkBehaviour
    {
        public static MeetingManager Instance { get; private set; }

        [SerializeField] private float meetingDuration = 60f;

        // Networked State
        public NetworkVariable<MeetingStartData> MeetingData = new();
        public NetworkVariable<float> TimeRemaining = new();
        public NetworkVariable<int> VotesSubmitted = new();
        public NetworkVariable<int> SkipVoteCount = new();
        public NetworkVariable<bool> HasEnded = new();
        public NetworkList<ulong> PlayersInMeeting;
        public NetworkList<ulong> AlivePlayersInMeeting;

        // Server-only state
        private readonly Dictionary<ulong, VoteTarget> _votes = new();
        private readonly HashSet<ulong> _hasVoted = new();
        private bool _timeLowFired;

        // Events
        public static event Action<MeetingResult> OnMeetingEnded;
        public static event Action<MeetingStartData> OnMeetingStarted;
        public static event Action OnTimeLow;
        public static event Action OnVoteSubmitted;
        public static event Action<ulong[], int[], int> OnVoteCountsReceived;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            PlayersInMeeting = new NetworkList<ulong>();
            AlivePlayersInMeeting = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (GameSessionManager.Instance != null)
                {
                    meetingDuration = GameSessionManager.Instance.Settings.Value.VotingTime;
                }

                if (!GameSessionManager.CachedMeetingData.CallerName.IsEmpty)
                {
                    StartMeeting(GameSessionManager.CachedMeetingData);
                    GameSessionManager.CachedMeetingData = default;
                }
            }

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ResetForNewGame()
        {
            if (!IsServer) return;
            
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
            if (!IsServer || HasEnded.Value || TimeRemaining.Value <= 0) return;

            TimeRemaining.Value -= Time.deltaTime;

            if (!_timeLowFired && TimeRemaining.Value <= 10f)
            {
                _timeLowFired = true;
                FireTimeLowClientRpc();
            }

            if (TimeRemaining.Value <= 0)
            {
                EndMeeting();
            }
        }

        // ========== PUBLIC API (Server) ==========

        /// <summary>
        /// SERVER ONLY: Start a meeting with the given data.
        /// Called by ReportService or GameSessionManager
        /// </summary>
        public void StartMeeting(MeetingStartData data)
        {
            if (!IsServer) return;
            if (HasEnded.Value) return;

            MeetingData.Value = data;
            TimeRemaining.Value = meetingDuration;
            VotesSubmitted.Value = 0;
            SkipVoteCount.Value = 0;
            HasEnded.Value = false;
            _timeLowFired = false;
            _votes.Clear();
            _hasVoted.Clear();

            PlayersInMeeting.Clear();
            AlivePlayersInMeeting.Clear();

            if (GameSessionManager.Instance != null)
            {
                var cachedStates = GameSessionManager.GetCachedPlayerStates();
                foreach (var playerData in GameSessionManager.Instance.Players)
                {
                    PlayersInMeeting.Add(playerData.ClientId);
                    if (cachedStates.TryGetValue(playerData.ClientId, out var state) && state.IsAlive)
                    {
                        AlivePlayersInMeeting.Add(playerData.ClientId);
                    }
                }
                GameSessionManager.Instance.CurrentPhase.Value = MatchPhase.Meeting;
            }

            FireMeetingStartedClientRpc(data);
        }

        [Rpc(SendTo.Server)]
        public void SubmitVoteServerRpc(ulong targetClientId, bool isSkip, RpcParams rpcParams = default)
        {
            ulong voterId = rpcParams.Receive.SenderClientId;

            if (HasEnded.Value || _hasVoted.Contains(voterId)) return;

            var cachedStates = GameSessionManager.GetCachedPlayerStates();
            if (cachedStates.TryGetValue(voterId, out var voterState) && !voterState.IsAlive) return;
            if (!isSkip && !PlayersInMeeting.Contains(targetClientId)) return;

            VoteTarget vote = isSkip ? VoteTarget.CreateSkip() : VoteTarget.CreatePlayerVote(targetClientId);
            _votes[voterId] = vote;
            _hasVoted.Add(voterId);
            VotesSubmitted.Value++;
            if (isSkip) SkipVoteCount.Value++;

            ConfirmVoteClientRpc(RpcTarget.Single(voterId, RpcTargetUse.Temp));

            if (VotesSubmitted.Value >= CountAlivePlayers())
            {
                EndMeeting();
            }
        }

        private void EndMeeting()
        {
            if (!IsServer || HasEnded.Value) return;
            HasEnded.Value = true;

            MeetingResult result = ComputeResults(out ulong[] playerIds, out int[] voteCounts, out int skipCount);

            BroadcastResultsClientRpc(result);
            BroadcastVoteCountsClientRpc(playerIds, voteCounts, skipCount);
            OnMeetingEnded?.Invoke(result);

            GameSessionManager.CachedEliminatedPlayerId = result.EliminatedId != ulong.MaxValue 
                ? result.EliminatedId 
                : ulong.MaxValue;

            StartCoroutine(ReturnToGameplayAfterDelay(3f));
        }

        private MeetingResult ComputeResults(out ulong[] outPlayerIds, out int[] outVoteCounts, out int outSkipCount)
        {
            var voteCounts = new Dictionary<ulong, int>();
            int skipCount = 0;

            foreach (var vote in _votes.Values)
            {
                if (vote.IsSkip)
                    skipCount++;
                else
                    voteCounts[vote.TargetClientId] = voteCounts.GetValueOrDefault(vote.TargetClientId) + 1;
            }

            int playerCount = PlayersInMeeting.Count;
            outPlayerIds = new ulong[playerCount];
            outVoteCounts = new int[playerCount];
            outSkipCount = skipCount;

            for (int i = 0; i < playerCount; i++)
            {
                ulong clientId = PlayersInMeeting[i];
                outPlayerIds[i] = clientId;
                outVoteCounts[i] = voteCounts.GetValueOrDefault(clientId);
            }

            if (voteCounts.Count == 0)
                return MeetingResult.CreateNoElimination(false, skipCount, _votes.Count);

            int maxVotes = voteCounts.Values.Max();
            if (skipCount > maxVotes)
                return MeetingResult.CreateNoElimination(false, skipCount, _votes.Count);

            var topVoted = voteCounts.Where(kvp => kvp.Value == maxVotes).ToList();
            if (topVoted.Count > 1)
                return MeetingResult.CreateNoElimination(true, skipCount, _votes.Count);

            ulong eliminatedId = topVoted[0].Key;
            return MeetingResult.CreateElimination(eliminatedId, GetPlayerName(eliminatedId), maxVotes, skipCount, _votes.Count);
        }

        private IEnumerator ReturnToGameplayAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            GameSessionManager.Instance?.ReturnToGameplayFromMeeting();
        }

        // Client RPCs
        [Rpc(SendTo.ClientsAndHost)]
        private void FireMeetingStartedClientRpc(MeetingStartData data) => OnMeetingStarted?.Invoke(data);

        [Rpc(SendTo.ClientsAndHost)]
        private void FireTimeLowClientRpc() => OnTimeLow?.Invoke();

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastResultsClientRpc(MeetingResult result) { }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastVoteCountsClientRpc(ulong[] playerIds, int[] voteCounts, int skipCount) 
            => OnVoteCountsReceived?.Invoke(playerIds, voteCounts, skipCount);

        [Rpc(SendTo.SpecifiedInParams)]
        private void ConfirmVoteClientRpc(RpcParams rpcParams) => OnVoteSubmitted?.Invoke();

        // Disconnect handling
        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            for (int i = PlayersInMeeting.Count - 1; i >= 0; i--)
            {
                if (PlayersInMeeting[i] == clientId)
                {
                    PlayersInMeeting.RemoveAt(i);
                    break;
                }
            }

            if (VotesSubmitted.Value >= CountAlivePlayers() && !HasEnded.Value)
            {
                EndMeeting();
            }
        }

        // Helpers
        private string GetPlayerName(ulong clientId)
        {
            if (GameSessionManager.Instance == null) return $"Player {clientId}";
            
            foreach (var p in GameSessionManager.Instance.Players)
            {
                if (p.ClientId == clientId) return p.PlayerName.ToString();
            }
            return $"Player {clientId}";
        }

        private int CountAlivePlayers()
        {
            var cachedStates = GameSessionManager.GetCachedPlayerStates();
            if (cachedStates != null && cachedStates.Count > 0)
            {
                return cachedStates.Values.Count(s => s.IsAlive);
            }
            return PlayersInMeeting.Count;
        }
    }
}
