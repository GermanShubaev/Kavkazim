using Unity.Netcode;
using UnityEngine;
using Netcode.Player;
using Kavkazim.Netcode;

namespace Kavkazim.Netcode.Reporting
{
    public class EmergencyButton : NetworkBehaviour
    {
        [Header("Settings")]
        [Tooltip("Maximum distance to interact with the button")]
        [SerializeField] private float interactionRange = 2.0f;
        
        [Tooltip("Cooldown between emergency meetings in seconds")]
        [SerializeField] private float cooldownDuration = 30f;

        private NetworkVariable<float> _lastUsedTime = new NetworkVariable<float>(-999f);
        private NetworkVariable<bool> _isOnCooldown = new NetworkVariable<bool>(false);

        private static EmergencyButton _instance;
        public static EmergencyButton Instance => _instance;

        public static event System.Action<string> OnEmergencyMeetingCalled;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[EmergencyButton] Multiple instances detected!");
            }
            _instance = this;
        }

        public override void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            base.OnDestroy();
        }

        public bool IsPlayerInRange(Vector3 playerPosition)
        {
            float distance = Vector3.Distance(playerPosition, transform.position);
            return distance <= interactionRange;
        }

        public bool IsOnCooldown => _isOnCooldown.Value;

        public float RemainingCooldown
        {
            get
            {
                if (!_isOnCooldown.Value) return 0f;
                float elapsed = Time.time - _lastUsedTime.Value;
                return Mathf.Max(0f, cooldownDuration - elapsed);
            }
        }

        public void TryCallEmergencyMeeting(PlayerState caller)
        {
            if (caller == null)
            {
                Debug.LogWarning("[EmergencyButton] TryCallEmergencyMeeting called with null caller.");
                return;
            }

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

            RequestEmergencyMeetingServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestEmergencyMeetingServerRpc(RpcParams rpcParams = default)
        {
            ulong callerClientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[EmergencyButton] SERVER: Received emergency meeting request from ClientID: {callerClientId}");

            if (ReportService.HasCalledEmergency(callerClientId))
            {
                Debug.LogWarning("[EmergencyButton] SERVER: Request rejected - player already used their emergency meeting this game.");
                return;
            }

            PlayerState caller = FindPlayerByClientId(callerClientId);
            if (caller == null)
            {
                Debug.LogWarning("[EmergencyButton] SERVER: Request rejected - caller not found.");
                return;
            }

            if (!caller.IsAlive.Value)
            {
                Debug.LogWarning("[EmergencyButton] SERVER: Request rejected - caller is dead.");
                return;
            }

            float distance = Vector3.Distance(caller.transform.position, transform.position);
            if (distance > interactionRange)
            {
                Debug.LogWarning($"[EmergencyButton] SERVER: Request rejected - out of range ({distance:F2} > {interactionRange}).");
                return;
            }

            if (_isOnCooldown.Value)
            {
                Debug.LogWarning("[EmergencyButton] SERVER: Request rejected - button on cooldown.");
                return;
            }

            string callerName = $"Player {callerClientId}";
            var avatar = caller.GetComponent<PlayerAvatar>();
            if (avatar != null && !string.IsNullOrEmpty(avatar.PlayerName.Value.ToString()))
            {
                callerName = avatar.PlayerName.Value.ToString();
            }

            ReportService.MarkEmergencyCalled(callerClientId);

            _lastUsedTime.Value = Time.time;
            _isOnCooldown.Value = true;

            AnnounceEmergencyMeetingClientRpc(callerName, callerClientId);

            Debug.Log($"[EmergencyButton] SERVER: Emergency meeting validated successfully.");
        }

        [ClientRpc]
        private void AnnounceEmergencyMeetingClientRpc(string callerName, ulong callerClientId)
        {
            ReportService.NotifyEmergencyMeeting(callerName, callerClientId);
            OnEmergencyMeetingCalled?.Invoke(callerName);
            
            ReportService.MarkEmergencyCalled(callerClientId);
        }

        private void Update()
        {
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
