using Kavkazim.Config;
using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode.Reporting
{
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
            TryInitialize();
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                TryInitialize();
            }
        }

        private void TryInitialize()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
            {
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

        private void InitializeReportingSystems()
        {
            if (DeadBodySpawner.Instance == null)
            {
                _deadBodySpawnerObj = new GameObject("DeadBodySpawner");
                _deadBodySpawnerObj.transform.SetParent(transform);
                
                DeadBodySpawner spawner = _deadBodySpawnerObj.AddComponent<DeadBodySpawner>();
                spawner.SetDeadBodyPrefab(deadBodyPrefab);
                
                Debug.Log("[ReportingSetup] Created DeadBodySpawner.");
            }

            if (config != null)
            {
                ReportService.SetReportRange(config.reportRange);
                DeadBody.SetReportRange(config.reportRange);
            }

            Debug.Log("[ReportingSetup] Reporting systems initialized successfully.");
        }

        private void OnDestroy()
        {
            if (_deadBodySpawnerObj != null)
            {
                Destroy(_deadBodySpawnerObj);
            }
        }
    }
}
