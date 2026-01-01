using Kavkazim.Netcode;
using Netcode.Player;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode.Reporting
{
    /// <summary>
    /// Server-only service that spawns dead bodies when players are killed.
    /// Subscribes to PlayerState.OnPlayerKilled event.
    /// This is a regular MonoBehaviour (not NetworkBehaviour) since it only runs on server.
    /// </summary>
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
            // Subscribe to player death events (server only)
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
            
            // Make sure to unsubscribe
            if (_isSubscribed)
            {
                PlayerState.OnPlayerKilled -= OnPlayerKilled;
                _isSubscribed = false;
            }
        }

        /// <summary>
        /// Called when any player is killed. Server only.
        /// </summary>
        private void OnPlayerKilled(PlayerState victim)
        {
            // Double-check we're on server
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

        /// <summary>
        /// Spawns a networked dead body at the victim's position.
        /// </summary>
        private void SpawnDeadBody(PlayerState victim)
        {
            if (deadBodyPrefab == null)
            {
                Debug.LogError("[DeadBodySpawner] Dead body prefab is not assigned!");
                return;
            }
            
            // Get victim info
            Vector3 spawnPosition = victim.transform.position + spawnOffset;
            ulong victimPlayerId = victim.OwnerClientId; // Use OwnerClientId (Player ID) consistently
            
            // Get victim name from PlayerAvatar if available
            string victimName = $"Player {victim.OwnerClientId}";
            PlayerAvatar avatar = victim.GetComponent<PlayerAvatar>();
            if (avatar != null && !string.IsNullOrEmpty(avatar.PlayerName.Value.ToString()))
            {
                victimName = avatar.PlayerName.Value.ToString();
            }
            
            // Spawn the prefab
            GameObject bodyObj = Instantiate(deadBodyPrefab, spawnPosition, Quaternion.identity);
            NetworkObject netObj = bodyObj.GetComponent<NetworkObject>();
            
            if (netObj == null)
            {
                Debug.LogError("[DeadBodySpawner] Dead body prefab missing NetworkObject component!");
                Destroy(bodyObj);
                return;
            }
            
            // Spawn on network (server-owned)
            netObj.Spawn();
            
            // Initialize the dead body component
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

        /// <summary>
        /// Sets the dead body prefab at runtime.
        /// </summary>
        public void SetDeadBodyPrefab(GameObject prefab)
        {
            deadBodyPrefab = prefab;
        }
    }
}
