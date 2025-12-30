using Unity.Netcode;
using UnityEngine;
using Netcode.Player;
using Kavkazim.Netcode;

namespace Kavkazim.Netcode.Reporting
{
    /// <summary>
    /// Emergency Meeting button that players can interact with.
    /// Place this component on the button object in the scene (the red circle).
    /// </summary>
    public class EmergencyButton : NetworkBehaviour
    {
        [Header("Settings")]
        [Tooltip("Maximum distance to interact with the button")]
        [SerializeField] private float interactionRange = 2.0f;
        
        [Tooltip("Cooldown between emergency meetings in seconds")]
        [SerializeField] private float cooldownDuration = 30f;

        // Networked cooldown state
        private NetworkVariable<float> _lastUsedTime = new NetworkVariable<float>(-999f);
        private NetworkVariable<bool> _isOnCooldown = new NetworkVariable<bool>(false);

        private static EmergencyButton _instance;
        public static EmergencyButton Instance => _instance;

        /// <summary>
        /// Event fired when an emergency meeting is called.
        /// Parameters: (callerName)
        /// </summary>
        public static event System.Action<string> OnEmergencyMeetingCalled;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[EmergencyButton] Multiple instances detected!");
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Check if a player is in range to use the button.
        /// </summary>
        public bool IsPlayerInRange(Vector3 playerPosition)
        {
            float distance = Vector3.Distance(playerPosition, transform.position);
            return distance <= interactionRange;
        }

        /// <summary>
        /// Check if the button is currently on cooldown.
        /// </summary>
        public bool IsOnCooldown => _isOnCooldown.Value;

        /// <summary>
        /// Get remaining cooldown time.
        /// </summary>
        public float RemainingCooldown
        {
            get
            {
                if (!_isOnCooldown.Value) return 0f;
                float elapsed = Time.time - _lastUsedTime.Value;
                return Mathf.Max(0f, cooldownDuration - elapsed);
            }
        }

        /// <summary>
        /// Client calls this to attempt to use the emergency button.
        /// </summary>
        public void TryCallEmergencyMeeting(PlayerState caller)
        {
            if (caller == null)
            {
                Debug.LogWarning("[EmergencyButton] TryCallEmergencyMeeting called with null caller.");
                return;
            }

            // Client-side checks
            if (!caller.IsAlive.Value)
            {
                Debug.Log("[EmergencyButton] Dead players cannot call emergency meetings.");
                return;
            }

            if (!IsPlayerInRange(caller.transform.position))
            {
                Debug.Log("[EmergencyButton] Player not in range of emergency button.");
                return;
            }

            if (_isOnCooldown.Value)
            {
                Debug.Log($"[EmergencyButton] Button on cooldown. {RemainingCooldown:F1}s remaining.");
                return;
            }

            // Get caller name
            string callerName = $"Player {caller.OwnerClientId}";
            PlayerAvatar avatar = caller.GetComponent<PlayerAvatar>();
            if (avatar != null && !string.IsNullOrEmpty(avatar.PlayerName.Value.ToString()))
            {
                callerName = avatar.PlayerName.Value.ToString();
            }

            // Send RPC to server
            RequestEmergencyMeetingServerRpc(callerName, caller.OwnerClientId);
        }

        /// <summary>
        /// Server RPC to request an emergency meeting.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestEmergencyMeetingServerRpc(string callerName, ulong callerClientId)
        {
            Debug.Log($"[EmergencyButton] SERVER: Received emergency meeting request from {callerName}");

            // Check if player has already reported this game
            if (ReportService.HasPlayerReported(callerClientId))
            {
                Debug.LogWarning("[EmergencyButton] SERVER: Request rejected - player already used their report this game.");
                return;
            }

            // Find caller to validate
            PlayerState caller = FindPlayerByClientId(callerClientId);
            if (caller == null)
            {
                Debug.LogWarning("[EmergencyButton] SERVER: Request rejected - caller not found.");
                return;
            }

            // Validate caller is alive
            if (!caller.IsAlive.Value)
            {
                Debug.LogWarning("[EmergencyButton] SERVER: Request rejected - caller is dead.");
                return;
            }

            // Validate distance
            float distance = Vector3.Distance(caller.transform.position, transform.position);
            if (distance > interactionRange)
            {
                Debug.LogWarning($"[EmergencyButton] SERVER: Request rejected - out of range ({distance:F2} > {interactionRange}).");
                return;
            }

            // Check cooldown
            if (_isOnCooldown.Value)
            {
                Debug.LogWarning("[EmergencyButton] SERVER: Request rejected - button on cooldown.");
                return;
            }

            // Mark player as having reported (one report per game)
            ReportService.MarkPlayerAsReported(callerClientId);

            // Start cooldown
            _lastUsedTime.Value = Time.time;
            _isOnCooldown.Value = true;

            // Announce to all clients
            AnnounceEmergencyMeetingClientRpc(callerName);

            Debug.Log($"[EmergencyButton] SERVER: Emergency meeting validated successfully.");
        }

        /// <summary>
        /// Client RPC to announce the emergency meeting.
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void AnnounceEmergencyMeetingClientRpc(string callerName)
        {
            // Use ReportService for consistent logging
            ReportService.NotifyEmergencyMeeting(callerName);
            OnEmergencyMeetingCalled?.Invoke(callerName);
        }

        private void Update()
        {
            // Server updates cooldown state
            if (IsServer && _isOnCooldown.Value)
            {
                float elapsed = Time.time - _lastUsedTime.Value;
                if (elapsed >= cooldownDuration)
                {
                    _isOnCooldown.Value = false;
                    Debug.Log("[EmergencyButton] SERVER: Cooldown ended, button available.");
                }
            }
        }

        /// <summary>
        /// Find a player by their client ID.
        /// </summary>
        private PlayerState FindPlayerByClientId(ulong clientId)
        {
            PlayerState[] allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.OwnerClientId == clientId)
                    return player;
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
#endif
    }
}
