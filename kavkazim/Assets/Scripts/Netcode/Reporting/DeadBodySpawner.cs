using Kavkazim.Netcode;
using Netcode.Player;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode.Reporting
{
    public class DeadBodySpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject deadBodyPrefab;
        
        [Header("Spawn Settings")]
        [Tooltip("Offset from player position for body spawn (e.g., slightly down)")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0, -0.3f, 0);

        private static DeadBodySpawner _instance;
        public static DeadBodySpawner Instance => _instance;

        private bool _isSubscribed = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[DeadBodySpawner] Duplicate instance detected, destroying.");
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && !_isSubscribed)
            {
                PlayerState.OnPlayerKilled += OnPlayerKilled;
                _isSubscribed = true;
                Debug.Log("[DeadBodySpawner] SERVER: Subscribed to OnPlayerKilled event.");
            }
        }

        private void OnDisable()
        {
            if (_isSubscribed)
            {
                PlayerState.OnPlayerKilled -= OnPlayerKilled;
                _isSubscribed = false;
                Debug.Log("[DeadBodySpawner] SERVER: Unsubscribed from OnPlayerKilled event.");
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            
            if (_isSubscribed)
            {
                PlayerState.OnPlayerKilled -= OnPlayerKilled;
                _isSubscribed = false;
            }
        }

        private void OnPlayerKilled(PlayerState victim)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }
            
            if (victim == null)
            {
                Debug.LogError("[DeadBodySpawner] OnPlayerKilled called with null victim!");
                return;
            }
            
            SpawnDeadBody(victim);
        }

        private void SpawnDeadBody(PlayerState victim)
        {
            if (deadBodyPrefab == null)
            {
                Debug.LogError("[DeadBodySpawner] Dead body prefab is not assigned!");
                return;
            }
            
            Vector3 spawnPosition = victim.transform.position + spawnOffset;
            ulong victimPlayerId = victim.OwnerClientId;
            
            string victimName = $"Player {victim.OwnerClientId}";
            PlayerAvatar avatar = victim.GetComponent<PlayerAvatar>();
            if (avatar != null && !string.IsNullOrEmpty(avatar.PlayerName.Value.ToString()))
            {
                victimName = avatar.PlayerName.Value.ToString();
            }
            
            GameObject bodyObj = Instantiate(deadBodyPrefab, spawnPosition, Quaternion.identity);
            NetworkObject netObj = bodyObj.GetComponent<NetworkObject>();
            
            if (netObj == null)
            {
                Debug.LogError("[DeadBodySpawner] Dead body prefab missing NetworkObject component!");
                Destroy(bodyObj);
                return;
            }
            
            netObj.Spawn();
            
            DeadBody deadBody = bodyObj.GetComponent<DeadBody>();
            if (deadBody != null)
            {
                deadBody.Initialize(victimPlayerId, victimName, spawnPosition);
            }
            else
            {
                Debug.LogError("[DeadBodySpawner] Dead body prefab missing DeadBody component!");
            }
            
            Debug.Log($"[DeadBodySpawner] SERVER: Spawned dead body for {victimName} at {spawnPosition}");
        }

        public void SetDeadBodyPrefab(GameObject prefab)
        {
            deadBodyPrefab = prefab;
        }
    }
}
