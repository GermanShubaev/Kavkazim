using System.Collections.Generic;
using Kavkazim.Netcode;
using Kavkazim.Netcode.Meeting;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.UI.Meeting
{
    public class MeetingVoteUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MeetingSkipView skipView;
        [SerializeField] private List<MeetingPlayerSlotView> slotViews;
        

        private MeetingManager _meetingManager;
        private ulong _localClientId;
        private bool _hasVotedLocally = false;
        

        private void Start()
        {
            _meetingManager = MeetingManager.Instance;
            if (_meetingManager == null)
            {
                Debug.LogError("[MeetingVoteUIController] MeetingManager not found!");
                return;
            }

            if (NetworkManager.Singleton != null)
            {
                _localClientId = NetworkManager.Singleton.LocalClientId;
            }

            MeetingManager.OnVoteSubmitted += OnVoteConfirmed;
            MeetingManager.OnVoteCountsReceived += OnVoteCountsReceived;
            _meetingManager.PlayersInMeeting.OnListChanged += OnPlayersListChanged;

            if (_meetingManager.PlayersInMeeting.Count > 0)
            {
                InitializeUI();
            }
        }

        private void OnDestroy()
        {
            MeetingManager.OnVoteSubmitted -= OnVoteConfirmed;
            MeetingManager.OnVoteCountsReceived -= OnVoteCountsReceived;
            if (_meetingManager != null && _meetingManager.PlayersInMeeting != null)
            {
                _meetingManager.PlayersInMeeting.OnListChanged -= OnPlayersListChanged;
            }
        }

        private void OnPlayersListChanged(NetworkListEvent<ulong> changeEvent)
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            var players = _meetingManager.PlayersInMeeting;
            bool isLocalAlive = IsPlayerAlive(_localClientId);

            if (skipView != null)
            {
                skipView.SetInteractive(isLocalAlive && !_hasVotedLocally, OnSkipClicked);
                skipView.SetSelected(false);
            }

            for (int i = 0; i < slotViews.Count; i++)
            {
                if (i < players.Count)
                {
                    ulong playerId = players[i];
                    bool isDead = !IsPlayerAlive(playerId);
                    string name = GetPlayerName(playerId);
                    
                    slotViews[i].gameObject.SetActive(true);
                    slotViews[i].Setup(playerId, name, playerId == _localClientId, isDead);

                    bool canVoteFor = isLocalAlive && 
                                      !_hasVotedLocally && 
                                      playerId != _localClientId && 
                                      !isDead;

                    slotViews[i].SetInteractive(canVoteFor, OnSlotClicked);
                }
                else
                {
                    slotViews[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnSlotClicked(ulong targetId)
        {
            if (_hasVotedLocally) return;

            ClearSelection();
            
            _meetingManager.SubmitVoteServerRpc(targetId, false);
            
            SetSelectionVisuals(targetId, false);
        }

        private void OnSkipClicked()
        {
            if (_hasVotedLocally) return;

            ClearSelection();
            SetSelectionVisuals(0, true);
            
            _meetingManager.SubmitVoteServerRpc(ulong.MaxValue, true);
        }

        private void SetSelectionVisuals(ulong targetId, bool isSkip)
        {
            if (isSkip)
            {
                if (skipView != null) skipView.SetSelected(true);
            }
            else
            {
                var players = _meetingManager.PlayersInMeeting;
                 for (int i = 0; i < slotViews.Count; i++)
                {
                    if (i < players.Count && players[i] == targetId)
                    {
                        slotViews[i].SetSelected(true);
                        break;
                    }
                }
            }
        }

        private void ClearSelection()
        {
            if (skipView != null) skipView.SetSelected(false);
            foreach (var slot in slotViews)
            {
                if (slot.gameObject.activeSelf) slot.SetSelected(false);
            }
        }

        private void OnVoteConfirmed()
        {
            _hasVotedLocally = true;

            if (skipView != null) skipView.SetInteractive(false);
            foreach (var slot in slotViews)
            {
                if (slot.gameObject.activeSelf) slot.SetInteractive(false);
            }
        }

        private bool IsPlayerAlive(ulong clientId)
        {
            var aliveList = _meetingManager.AlivePlayersInMeeting;
            if (aliveList != null)
            {
                return aliveList.Contains(clientId);
            }
            return true;
        }

        private string GetPlayerName(ulong clientId)
        {
            if (GameSessionManager.Instance != null)
            {
                foreach (var p in GameSessionManager.Instance.Players)
                {
                    if (p.ClientId == clientId) return p.PlayerName.ToString();
                }
            }
            return $"Player {clientId}";
        }

        private void OnVoteCountsReceived(ulong[] playerIds, int[] voteCounts, int skipCount)
        {
            Debug.Log($"[MeetingVoteUIController] Received vote counts: {playerIds.Length} players, skip={skipCount}");

            if (skipView != null)
            {
                skipView.SetSkipCount(skipCount);
            }

            for (int i = 0; i < playerIds.Length; i++)
            {
                ulong playerId = playerIds[i];
                int voteCount = voteCounts[i];

                foreach (var slot in slotViews)
                {
                    if (slot.gameObject.activeSelf && slot.ClientId == playerId)
                    {
                        slot.SetVoteCount(voteCount);
                        break;
                    }
                }
            }
        }
    }
}
