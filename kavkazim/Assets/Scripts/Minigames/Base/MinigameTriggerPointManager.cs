using System.Collections.Generic;
using Minigames.Base;
using UnityEngine;

namespace Minigames
{
    public class MinigameTriggerPointManager : MonoBehaviour
    {
        private static MinigameTriggerPointManager _instance;
        public static MinigameTriggerPointManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MinigameTriggerPointManager>();
                    if (_instance == null)
                    {
                        GameObject managerObj = new GameObject("MinigameTriggerPointManager");
                        _instance = managerObj.AddComponent<MinigameTriggerPointManager>();
                        DontDestroyOnLoad(managerObj);
                    }
                }
                return _instance;
            }
        }

        [Header("Spawn Settings")]
        [SerializeField] private bool spawnOnAwake = true;

        private List<MinigameTriggerPoint> _spawnedTriggerPoints = new List<MinigameTriggerPoint>();

        [System.Serializable]
        public class TriggerPointData
        {
            public Vector2 position;
            public float radius;
            public MinigameType gameType;
        }

        /// <summary>
        /// List of all trigger points to spawn.
        /// </summary>
        private readonly TriggerPointData[] _triggerPoints = new TriggerPointData[]
        {
            new TriggerPointData { position = new Vector2(28.39f, -0.72f), radius = 1.2f, gameType = MinigameType.PapakhaClick },
            new TriggerPointData { position = new Vector2(16.17f, -2.32f), radius = 1.2f, gameType = MinigameType.Remote },
            new TriggerPointData { position = new Vector2(40.62f, 14.82f), radius = 1.8f, gameType = MinigameType.LaundrySort },
            new TriggerPointData { position = new Vector2(44.56f, 20.26f), radius = 1.2f, gameType = MinigameType.Tapachki },
            new TriggerPointData { position = new Vector2(-6.58f, 31.78f), radius = 3f, gameType = MinigameType.ShashlikSort },
            new TriggerPointData { position = new Vector2(-27.27f, 2.76f), radius = 1.2f, gameType = MinigameType.Tapachki },
            new TriggerPointData { position = new Vector2(-36.37f, 13.26f), radius = 1.2f, gameType = MinigameType.Remote },
            new TriggerPointData { position = new Vector2(-49.52f, -4.98f), radius = 3f, gameType = MinigameType.LezginkaSort },
            new TriggerPointData { position = new Vector2(-49.42f, -10.08f), radius = 1.2f, gameType = MinigameType.Tapachki },
            new TriggerPointData { position = new Vector2(-30.21f, -25.30f), radius = 3f, gameType = MinigameType.Takedown },
            new TriggerPointData { position = new Vector2(-12.79f, -10.33f), radius = 2.2f, gameType = MinigameType.PraySort },
            new TriggerPointData { position = new Vector2(50.92f, -13.91f), radius = 3f, gameType = MinigameType.DishClick },
            new TriggerPointData { position = new Vector2(47.83f, -6.41f), radius = 3f, gameType = MinigameType.Wolf },
            new TriggerPointData { position = new Vector2(40.94f, 1.87f), radius = 1.2f, gameType = MinigameType.Remote }
        };

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Debug.LogWarning("[MinigameTriggerPointManager] Duplicate instance detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            if (spawnOnAwake)
            {
                SpawnAllTriggerPoints();
            }
        }

        public void SpawnAllTriggerPoints()
        {
            foreach (var triggerData in _triggerPoints)
            {
                SpawnTriggerPoint(triggerData.position, triggerData.radius, triggerData.gameType);
            }

            Debug.Log($"[MinigameTriggerPointManager] Spawned {_spawnedTriggerPoints.Count} trigger points");
        }

        public MinigameTriggerPoint SpawnTriggerPoint(Vector2 position, float radius, MinigameType gameType)
        {
            GameObject triggerObj = new GameObject($"MinigameTriggerPoint_{gameType}_{position.x}_{position.y}");
            MinigameTriggerPoint trigger = triggerObj.AddComponent<MinigameTriggerPoint>();
            
            var positionField = typeof(MinigameTriggerPoint).GetField("position", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var radiusField = typeof(MinigameTriggerPoint).GetField("radius", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gameTypeField = typeof(MinigameTriggerPoint).GetField("gameType", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (positionField != null)
                positionField.SetValue(trigger, position);
            if (radiusField != null)
                radiusField.SetValue(trigger, radius);
            if (gameTypeField != null)
                gameTypeField.SetValue(trigger, gameType);

            triggerObj.transform.position = new Vector3(position.x, position.y, 0);

            MinigameManager manager = FindFirstObjectByType<MinigameManager>();
            if (manager != null)
            {
                manager.RegisterTriggerPoint(trigger);
            }

            _spawnedTriggerPoints.Add(trigger);

            Debug.Log($"[MinigameTriggerPointManager] Spawned trigger point at ({position.x}, {position.y}) with radius {radius} for {gameType}");
            
            return trigger;
        }

        public void ClearAllTriggerPoints()
        {
            foreach (var trigger in _spawnedTriggerPoints)
            {
                if (trigger != null)
                {
                    MinigameManager manager = FindFirstObjectByType<MinigameManager>();
                    if (manager != null)
                    {
                        manager.UnregisterTriggerPoint(trigger);
                    }
                    Destroy(trigger.gameObject);
                }
            }
            _spawnedTriggerPoints.Clear();
            Debug.Log("[MinigameTriggerPointManager] Cleared all trigger points");
        }

        public List<MinigameTriggerPoint> GetSpawnedTriggerPoints()
        {
            return new List<MinigameTriggerPoint>(_spawnedTriggerPoints);
        }

        private void OnDestroy()
        {
            ClearAllTriggerPoints();
        }
    }
}
