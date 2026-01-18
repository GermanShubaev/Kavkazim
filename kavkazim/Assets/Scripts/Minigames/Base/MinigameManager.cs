using System.Collections.Generic;
using System.Linq;
using Minigames.Base;
using UnityEngine;

namespace Minigames
{
    public class MinigameManager : MonoBehaviour
    {
        private static MinigameManager _instance;
        public static MinigameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MinigameManager>();
                    if (_instance == null)
                    {
                        GameObject managerObj = new GameObject("MinigameManager");
                        _instance = managerObj.AddComponent<MinigameManager>();
                        DontDestroyOnLoad(managerObj);
                    }
                }
                return _instance;
            }
        }

        private List<MinigameTriggerPoint> _triggerPoints = new List<MinigameTriggerPoint>();

        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateDefaultTrigger = true;
        [SerializeField] private Vector2 defaultTriggerPosition = new Vector2(16, 26);
        [SerializeField] private float defaultTriggerRadius = 4f;
        [SerializeField] private MinigameType defaultTriggerGameType = MinigameType.DefaultType;

        [Header("Additional Auto Triggers")]
        [SerializeField] private bool createAdditionalTriggers = true;
        [SerializeField] private AdditionalTriggerData[] additionalTriggers = new AdditionalTriggerData[]
        {
            new AdditionalTriggerData { position = new Vector2(3, 10), radius = 2f, gameType = MinigameType.PraySort },
            new AdditionalTriggerData { position = new Vector2(-25, 13), radius = 2f, gameType = MinigameType.LezginkaSort },
            new AdditionalTriggerData { position = new Vector2(37, 18), radius = 2f, gameType = MinigameType.PapakhaClick },
            new AdditionalTriggerData { position = new Vector2(53, 5), radius = 2f, gameType = MinigameType.DishClick },
            new AdditionalTriggerData { position = new Vector2(51, 12), radius = 2f, gameType = MinigameType.Wolf },
            new AdditionalTriggerData { position = new Vector2(-10, -2), radius = 8f, gameType = MinigameType.Takedown },
            new AdditionalTriggerData { position = new Vector2(7, 45), radius = 2f, gameType = MinigameType.ShashlikSort }
        };

        [System.Serializable]
        public class AdditionalTriggerData
        {
            public Vector2 position;
            public float radius;
            public MinigameType gameType;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            RefreshTriggerPoints();

            if (autoCreateDefaultTrigger && _triggerPoints.Count == 0)
            {
                CreateDefaultTriggerPoint();
            }

            if (createAdditionalTriggers && additionalTriggers != null)
            {
                foreach (var triggerData in additionalTriggers)
                {
                    CreateTriggerPoint(triggerData.position, triggerData.radius, triggerData.gameType);
                }
            }
        }

        private void CreateDefaultTriggerPoint()
        {
            CreateTriggerPoint(defaultTriggerPosition, defaultTriggerRadius, defaultTriggerGameType);
        }

        private void CreateTriggerPoint(Vector2 position, float radius, MinigameType gameType)
        {
            GameObject triggerObj = new GameObject($"MinigameTriggerPoint_{position.x}_{position.y}");
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
            RegisterTriggerPoint(trigger);

            Debug.Log($"[MinigameManager] Created trigger point at ({position.x}, {position.y}) with radius {radius} for {gameType}");
        }

        public void RefreshTriggerPoints()
        {
            _triggerPoints.Clear();
            _triggerPoints.AddRange(FindObjectsByType<MinigameTriggerPoint>(FindObjectsSortMode.None));
        }

        public void RegisterTriggerPoint(MinigameTriggerPoint triggerPoint)
        {
            if (triggerPoint != null && !_triggerPoints.Contains(triggerPoint))
            {
                _triggerPoints.Add(triggerPoint);
            }
        }

        public void UnregisterTriggerPoint(MinigameTriggerPoint triggerPoint)
        {
            _triggerPoints.Remove(triggerPoint);
        }

        public bool GetNearestTriggerPoint(Vector2 playerPosition, out MinigameTriggerPoint trigger, out float distance)
        {
            trigger = null;
            distance = float.MaxValue;

            var validTriggers = _triggerPoints
                .Where(tp => tp != null && tp.IsWithinRange(playerPosition))
                .Select(tp => new { Trigger = tp, Distance = tp.GetDistance(playerPosition) })
                .OrderBy(x => x.Distance)
                .ToList();

            if (validTriggers.Count == 0)
            {
                return false;
            }

            var nearest = validTriggers[0];
            trigger = nearest.Trigger;
            distance = nearest.Distance;
            return true;
        }

        public List<MinigameTriggerPoint> GetTriggerPointsInRange(Vector2 playerPosition)
        {
            return _triggerPoints
                .Where(tp => tp != null && tp.IsWithinRange(playerPosition))
                .OrderBy(tp => tp.GetDistance(playerPosition))
                .ToList();
        }
    }
}

