using Kavkazim.Config;
using Netcode.Player;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Handles kill ability for Kavkazi (impostor) players.
    /// Server-authoritative with validated kill requests.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerState))]
    public class KillerAbility : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private NetworkGameplayConfig config;
        
        [Header("Settings (used if config is null)")]
        [SerializeField] private float defaultKillRange = 2.0f;
        [SerializeField] private float defaultKillCooldown = 15f;

        [Header("References")]
        [SerializeField] private PlayerAvatar avatar;
        
        /// <summary>
        /// Network-synced cooldown end time. Clients can read this for UI display.
        /// Value is the Time.time when cooldown ends.
        /// </summary>
        public NetworkVariable<float> CooldownEndTime = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private PlayerState _playerState;
        
        // Cached config values
        private float KillRange => config ? config.killRange : defaultKillRange;
        private float KillCooldown => config ? config.killCooldown : defaultKillCooldown;

        private void Awake()
        {
            _playerState = GetComponent<PlayerState>();
            
            if (!avatar)
                avatar = GetComponent<PlayerAvatar>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // Set initial cooldown to 0 (ready immediately on spawn)
            // The server will set this properly
            if (IsServer)
            {
                CooldownEndTime.Value = 0f;
            }
        }

        /// <summary>
        /// Check if kill is off cooldown.
        /// </summary>
        public bool IsKillReady => Time.time >= CooldownEndTime.Value;

        /// <summary>
        /// Get remaining cooldown time in seconds.
        /// </summary>
        public float RemainingCooldown => Mathf.Max(0f, CooldownEndTime.Value - Time.time);

        /// <summary>
        /// Attempts to kill the nearest valid target.
        /// Call this from the owner client (e.g., on button press).
        /// </summary>
        public void TryKill()
        {
            if (!IsOwner)
            {
                Debug.LogWarning("[KillerAbility] TryKill called on non-owner.");
                return;
            }
            
            // Client-side pre-validation (avoid unnecessary RPCs)
            if (!IsKillReady)
            {
                Debug.Log($"[KillerAbility] Kill on cooldown. {RemainingCooldown:F1}s remaining.");
                return;
            }
            
            if (_playerState && !_playerState.IsAlive.Value)
            {
                Debug.Log("[KillerAbility] Cannot kill while dead.");
                return;
            }
            
            // Find closest valid target
            PlayerState target = FindClosestTarget();
            if (target != null)
            {
                Debug.Log($"[KillerAbility] Requesting kill on player {target.OwnerClientId}");
                
                // Play local animation immediately for responsiveness
                if (avatar)
                    avatar.PerformSlashAnimation();
                
                // Send request to server
                RequestKillServerRpc(target.NetworkObjectId);
            }
            else
            {
                Debug.Log("[KillerAbility] No valid target in range.");
            }
        }

        /// <summary>
        /// Server RPC to request a kill. Server validates and executes if valid.
        /// </summary>
        /// <param name="targetNetworkObjectId">NetworkObjectId of the target player</param>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestKillServerRpc(ulong targetNetworkObjectId)
        {
            Debug.Log($"[KillerAbility] SERVER: Received kill request from {OwnerClientId} targeting object {targetNetworkObjectId}");
            
            // === VALIDATION ===
            
            // 1. Check if killer is alive
            if (!_playerState || !_playerState.IsAlive.Value)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - killer {OwnerClientId} is dead.");
                return;
            }
            
            // 2. Check cooldown (server-side validation)
            if (Time.time < CooldownEndTime.Value)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - cooldown not ready.");
                return;
            }
            
            // 3. Find target NetworkObject
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId, out NetworkObject targetNetObj))
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - target object {targetNetworkObjectId} not found.");
                return;
            }
            
            // 4. Get target's PlayerState
            PlayerState targetState = targetNetObj.GetComponent<PlayerState>();
            if (targetState == null)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - target has no PlayerState.");
                return;
            }
            
            // 5. Check if target is alive
            if (!targetState.IsAlive.Value)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - target {targetState.OwnerClientId} is already dead.");
                return;
            }
            
            // 6. Check if trying to kill self
            if (targetState.OwnerClientId == OwnerClientId)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - cannot kill self.");
                return;
            }
            
            // 7. Check distance
            float distance = Vector3.Distance(transform.position, targetNetObj.transform.position);
            if (distance > KillRange)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - target out of range ({distance:F2} > {KillRange}).");
                return;
            }
            
            // 8. Check if target is a Kavkazi teammate (SERVER-SIDE validation using true role)
            PlayerAvatar killerAvatar = GetComponent<PlayerAvatar>();
            PlayerAvatar targetAvatar = targetNetObj.GetComponent<PlayerAvatar>();
            if (killerAvatar != null && targetAvatar != null)
            {
                // On server, GetTrueRole() works correctly
                if (killerAvatar.GetTrueRole() == PlayerRoleType.Kavkazi && 
                    targetAvatar.GetTrueRole() == PlayerRoleType.Kavkazi)
                {
                    Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - cannot kill Kavkazi teammate.");
                    return;
                }
            }
            
            // === EXECUTE KILL ===
            
            // Start cooldown
            CooldownEndTime.Value = Time.time + KillCooldown;
            
            // Kill the target
            targetState.Kill();
            
            Debug.Log($"[KillerAbility] SERVER: Player {OwnerClientId} successfully killed player {targetState.OwnerClientId}");
            
            // Trigger visual effects on all clients
            PlayKillEffectClientRpc(targetNetworkObjectId);
        }

        /// <summary>
        /// Client RPC to play kill visual/audio effects.
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void PlayKillEffectClientRpc(ulong victimNetworkObjectId)
        {
            Debug.Log($"[KillerAbility] CLIENT: Playing kill effect for victim {victimNetworkObjectId}");
            
            // Play killer animation (if not already played locally)
            if (!IsOwner && avatar)
            {
                avatar.PerformSlashAnimation();
            }
            
            // Get victim for additional effects
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                victimNetworkObjectId, out NetworkObject victimNetObj))
            {
                // You can add death particle effects, sounds, etc. here
                // Example: Instantiate a death effect at victim's position
                // ParticleSystem deathFx = Instantiate(deathEffectPrefab, victimNetObj.transform.position, Quaternion.identity);
                // Destroy(deathFx.gameObject, 2f);
                
                // Play death sound
                // AudioSource.PlayClipAtPoint(deathSound, victimNetObj.transform.position);
            }
        }

        /// <summary>
        /// Find the closest alive player within kill range.
        /// </summary>
        private PlayerState FindClosestTarget()
        {
            PlayerState[] allPlayers = Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            PlayerState closest = null;
            float minDistance = KillRange;

            foreach (var player in allPlayers)
            {
                // Skip self
                if (player.NetworkObjectId == NetworkObjectId)
                    continue;
                
                // Skip dead players
                if (!player.IsAlive.Value)
                    continue;
                
                // Check if it's a Kavkazi teammate (use PerceivedRole since Role is OwnerOnly)
                // On the client, we use what we PERCEIVE the target to be
                // If we're Kavkazi, we'll see other Kavkazis as Kavkazi and skip them
                PlayerAvatar targetAvatar = player.GetComponent<PlayerAvatar>();
                if (targetAvatar != null && targetAvatar.PerceivedRole == PlayerRoleType.Kavkazi)
                {
                    // Skip fellow Kavkazi (as we perceive them)
                    continue;
                }

                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = player;
                }
            }

            return closest;
        }

        /// <summary>
        /// Get a formatted string showing remaining cooldown for UI.
        /// Returns empty string if ready.
        /// </summary>
        public string GetCooldownDisplayText()
        {
            if (IsKillReady)
                return "";
            
            return $"{RemainingCooldown:F1}s";
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor visualization of kill range.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, KillRange);
        }
#endif
    }
}
