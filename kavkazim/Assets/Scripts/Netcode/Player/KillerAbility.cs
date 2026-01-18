using Kavkazim.Config;
using Netcode.Player;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode
{
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
        
        public NetworkVariable<double> CooldownEndTime = new ();

        private PlayerState _playerState;
        
        private float KillRange => config ? config.killRange : defaultKillRange;
        
        private float KillCooldown
        {
            get
            {
                if (GameSessionManager.Instance != null)
                    return GameSessionManager.Instance.Settings.Value.KillCooldown;
                
                return defaultKillCooldown;
            }
        }

        private void Awake()
        {
            _playerState = GetComponent<PlayerState>();
            
            if (!avatar)
                avatar = GetComponent<PlayerAvatar>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            if (IsServer)
            {
                CooldownEndTime.Value = 0f;
            }
        }

        private double ServerTime => NetworkManager.Singleton?.ServerTime.Time ?? 0;

        public bool IsKillReady => ServerTime >= CooldownEndTime.Value;

        public float RemainingCooldown => Mathf.Max(0f, (float)(CooldownEndTime.Value - ServerTime));

        public void TryKill()
        {
            if (!IsOwner)
            {
                Debug.LogWarning("[KillerAbility] TryKill called on non-owner.");
                return;
            }
            
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
            
            PlayerState target = FindClosestTarget();
            if (target != null)
            {
                Debug.Log($"[KillerAbility] Requesting kill on player {target.OwnerClientId}");
                
                if (avatar)
                    avatar.PerformSlashAnimation();
                
                RequestKillServerRpc(target.NetworkObjectId);
            }
            else
            {
                Debug.Log("[KillerAbility] No valid target in range.");
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestKillServerRpc(ulong targetNetworkObjectId)
        {
            Debug.Log($"[KillerAbility] SERVER: Received kill request from {OwnerClientId} targeting object {targetNetworkObjectId}");
            
            if (!_playerState || !_playerState.IsAlive.Value)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - killer {OwnerClientId} is dead.");
                return;
            }
            
            if (Time.time < CooldownEndTime.Value)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - cooldown not ready.");
                return;
            }
            
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId, out NetworkObject targetNetObj))
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - target object {targetNetworkObjectId} not found.");
                return;
            }
            
            PlayerState targetState = targetNetObj.GetComponent<PlayerState>();
            if (targetState == null)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - target has no PlayerState.");
                return;
            }
            
            if (!targetState.IsAlive.Value)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - target {targetState.OwnerClientId} is already dead.");
                return;
            }
            
            if (targetState.OwnerClientId == OwnerClientId)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - cannot kill self.");
                return;
            }
            
            float distance = Vector3.Distance(transform.position, targetNetObj.transform.position);
            if (distance > KillRange)
            {
                Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - target out of range ({distance:F2} > {KillRange}).");
                return;
            }
            
            PlayerAvatar killerAvatar = GetComponent<PlayerAvatar>();
            PlayerAvatar targetAvatar = targetNetObj.GetComponent<PlayerAvatar>();
            if (killerAvatar != null && targetAvatar != null)
            {
                if (killerAvatar.GetTrueRole() == PlayerRoleType.Kavkazi && 
                    targetAvatar.GetTrueRole() == PlayerRoleType.Kavkazi)
                {
                    Debug.LogWarning($"[KillerAbility] SERVER: Kill rejected - cannot kill Kavkazi teammate.");
                    return;
                }
            }
            
            CooldownEndTime.Value = ServerTime + KillCooldown;
            
            targetState.Kill();
            
            Debug.Log($"[KillerAbility] SERVER: Player {OwnerClientId} successfully killed player {targetState.OwnerClientId}");
            
            PlayKillEffectClientRpc(targetNetworkObjectId);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayKillEffectClientRpc(ulong victimNetworkObjectId)
        {
            Debug.Log($"[KillerAbility] CLIENT: Playing kill effect for victim {victimNetworkObjectId}");
            
            if (!IsOwner && avatar)
            {
                avatar.PerformSlashAnimation();
            }
        }

        private PlayerState FindClosestTarget()
        {
            var allPlayers = PlayerState.ActivePlayers;
            
            PlayerState closest = null;
            float minDistance = KillRange;

            foreach (var player in allPlayers)
            {
                if (player.NetworkObjectId == NetworkObjectId)
                    continue;
                
                if (!player.IsAlive.Value)
                    continue;
                
                PlayerAvatar targetAvatar = player.GetComponent<PlayerAvatar>();
                if (targetAvatar != null && targetAvatar.PerceivedRole == PlayerRoleType.Kavkazi)
                {
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, KillRange);
        }
#endif
    }
}
