using Kavkazim.Config;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode.Reporting
{
    /// <summary>
    /// Setup component for the Reporting system.
    /// Attach to a scene object in Gameplay scene to initialize DeadBodySpawner and ReportService.
    /// This ensures the reporting systems are available when the game starts.
    /// </summary>
    public class ReportingSetup : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("The DeadBody prefab to spawn when players die")]
        [SerializeField] private GameObject deadBodyPrefab;
        
        [Header("Configuration")]
        [SerializeField] private NetworkGameplayConfig config;

        private GameObject _deadBodySpawnerObj;
        private bool _isInitialized = false;

        private void Start()
        {
            // Try to initialize immediately if already connected
            TryInitialize();
        }

        private void Update()
        {
            // Keep trying to initialize until successful
            if (!_isInitialized)
            {
                TryInitialize();
            }
        }

        private void TryInitialize()
        {
            // Only initialize on server/host when network is ready
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
            {
                // Not server - no need to initialize spawner
                _isInitialized = true;
                return;
            }

            if (!NetworkManager.Singleton.IsListening)
            {
                return;
            }

            InitializeReportingSystems();
            _isInitialized = true;
            Debug.Log("[ReportingSetup] Initialization complete.");
        }

        /// <summary>
        /// Initialize the reporting system components.
        /// </summary>
        private void InitializeReportingSystems()
        {
            // Create DeadBodySpawner (simple MonoBehaviour, no networking needed)
            if (DeadBodySpawner.Instance == null)
            {
                _deadBodySpawnerObj = new GameObject("DeadBodySpawner");
                _deadBodySpawnerObj.transform.SetParent(transform);
                
                // Add DeadBodySpawner (regular MonoBehaviour)
                DeadBodySpawner spawner = _deadBodySpawnerObj.AddComponent<DeadBodySpawner>();
                spawner.SetDeadBodyPrefab(deadBodyPrefab);
                
                Debug.Log("[ReportingSetup] Created DeadBodySpawner.");
            }

            // Apply configuration
            if (config != null)
            {
                ReportService.SetReportRange(config.reportRange);
                DeadBody.SetReportRange(config.reportRange);
            }

            Debug.Log("[ReportingSetup] Reporting systems initialized successfully.");
        }

        private void OnDestroy()
        {
            // Cleanup spawned objects
            if (_deadBodySpawnerObj != null)
            {
                Destroy(_deadBodySpawnerObj);
            }
        }
    }
}
