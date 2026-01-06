using System.Collections.Generic;
using Kavkazim.Netcode;
using Kavkazim.Netcode.Meeting;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.UI.Meeting
{
    /// <summary>
    /// Controller for the voting logic and UI states.
    /// Manages PlayerSlots and Skip button, handles selection, and submits votes.
    /// </summary>
    public class MeetingVoteUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MeetingSkipView skipView;
        [SerializeField] private List<MeetingPlayerSlotView> slotViews;
        
        [Header("Container (Optional)")]
        [Tooltip("If slots are not assigned, will search in this container")]
        [SerializeField] private Transform slotsContainer;

        private MeetingManager _meetingManager;
        private ulong _localClientId;
        private bool _hasVotedLocally = false;

        private void Awake()
        {
            // Auto-find slots if empty
            if (slotViews == null || slotViews.Count == 0)
            {
                if (slotsContainer != null)
                {
                    slotViews = new List<MeetingPlayerSlotView>(slotsContainer.GetComponentsInChildren<MeetingPlayerSlotView>(true));
                }
                else
                {
                    // Fallback to finding all in children of this object
                    slotViews = new List<MeetingPlayerSlotView>(GetComponentsInChildren<MeetingPlayerSlotView>(true));
                }
            }

            // Auto-find skip view
            if (skipView == null)
            {
                skipView = GetComponentInChildren<MeetingSkipView>();
            }
        }

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

            // Subscribe
            MeetingManager.OnVoteSubmitted += OnVoteConfirmed;
            _meetingManager.PlayersInMeeting.OnListChanged += OnPlayersListChanged;

            // Initial setup if data exists
            if (_meetingManager.PlayersInMeeting.Count > 0)
            {
                InitializeUI();
            }
        }

        private void OnDestroy()
        {
            MeetingManager.OnVoteSubmitted -= OnVoteConfirmed;
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

            // 1. Setup Skip Button
            if (skipView != null)
            {
                skipView.SetInteractive(isLocalAlive && !_hasVotedLocally, OnSkipClicked);
                skipView.SetSelected(false);
            }

            // 2. Setup Player Slots
            for (int i = 0; i < slotViews.Count; i++)
            {
                if (i < players.Count)
                {
                    ulong playerId = players[i];
                    bool isDead = !IsPlayerAlive(playerId);
                    string name = GetPlayerName(playerId);
                    
                    slotViews[i].gameObject.SetActive(true);
                    slotViews[i].Setup(playerId, name, playerId == _localClientId, isDead);

                    // Interaction rules:
                    // - Local player must be alive
                    // - Cannot vote for self
                    // - Cannot vote for dead players (usually, depending on game rules - standard is no)
                    // - Cannot vote if already voted
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

            // Visual feedback: clear others, select this one
            ClearSelection();
            
            // NOTE: Since "OnClick should call my existing vote method", we do that immediately.
            _meetingManager.SubmitVoteServerRpc(targetId, false);
            
            // We'll wait for "OnVoteConfirmed" to lock UI, but we can optimistically select here.
            SetSelectionVisuals(targetId, false);
        }

        private void OnSkipClicked()
        {
            if (_hasVotedLocally) return;

            ClearSelection();
            SetSelectionVisuals(0, true); // ID irrelevant if isSkip is true
            
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
                // Find and select the slot
                // We need to match targetId with the slot index.
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
            // Server accepted our vote (or someone else's? Event is static/global?)
            // MeetingManager.OnVoteSubmitted seems to be fired on "ConfirmVoteClientRpc" which is targeted to Single(voter).
            // So this event fires only on the client who voted. 
            // Let's verify MeetingManager.cs...
            // Yes: ConfirmVoteClientRpc -> OnVoteSubmitted?.Invoke().
            
            _hasVotedLocally = true;


            // Lock all interactions
            if (skipView != null) skipView.SetInteractive(false);
            foreach (var slot in slotViews)
            {
                if (slot.gameObject.activeSelf) slot.SetInteractive(false);
            }
            
            // Show "Voted" checkmark on self? Or simply "You have voted".
            // The requirements didn't specify showing "Voted" on self specifically, 
            // but usually you want to confirm the action. 
            // The selection ring/border should persist as confirmation.
        }

        // --- Helpers ---

        private bool IsPlayerAlive(ulong clientId)
        {
            var aliveList = _meetingManager.AlivePlayersInMeeting;
            if (aliveList != null)
            {
                return aliveList.Contains(clientId);
            }
            return true; // Fallback
        }

        private string GetPlayerName(ulong clientId)
        {
            // Use GameSessionManager if available
            if (GameSessionManager.Instance != null)
            {
                foreach (var p in GameSessionManager.Instance.Players)
                {
                    if (p.ClientId == clientId) return p.PlayerName.ToString();
                }
            }
            return $"Player {clientId}";
        }
    }
}
