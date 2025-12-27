using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Minigames
{
    /// <summary>
    /// Manages all minigame trigger points in the scene and handles distance checking.
    /// </summary>
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
        [SerializeField] private MinigameType defaultTriggerGameType = MinigameType.EmptyPopup;

        [Header("Additional Auto Triggers")]
        [SerializeField] private bool createAdditionalTriggers = true;
        [SerializeField] private AdditionalTriggerData[] additionalTriggers = new AdditionalTriggerData[]
        {
            new AdditionalTriggerData { position = new Vector2(3, 10), radius = 2f, gameType = MinigameType.PraySortGame },
            new AdditionalTriggerData { position = new Vector2(-25, 13), radius = 2f, gameType = MinigameType.LezginkaSort },
            new AdditionalTriggerData { position = new Vector2(37, 18), radius = 2f, gameType = MinigameType.PapakhaClick },
            new AdditionalTriggerData { position = new Vector2(53, 5), radius = 2f, gameType = MinigameType.DishClick }
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

            // Find all existing trigger points in the scene
            RefreshTriggerPoints();

            // Auto-create default trigger point if enabled and none exist
            if (autoCreateDefaultTrigger && _triggerPoints.Count == 0)
            {
                CreateDefaultTriggerPoint();
            }

            // Create additional trigger points
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

        /// <summary>
        /// Creates a trigger point at the specified position with the given radius and game type.
        /// </summary>
        private void CreateTriggerPoint(Vector2 position, float radius, MinigameType gameType)
        {
            GameObject triggerObj = new GameObject($"MinigameTriggerPoint_{position.x}_{position.y}");
            MinigameTriggerPoint trigger = triggerObj.AddComponent<MinigameTriggerPoint>();
            
            // Use reflection to set private serialized fields
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

            // Set transform position for visual reference
            triggerObj.transform.position = new Vector3(position.x, position.y, 0);

            // Register the trigger point
            RegisterTriggerPoint(trigger);

            Debug.Log($"[MinigameManager] Created trigger point at ({position.x}, {position.y}) with radius {radius} for {gameType}");
        }

        /// <summary>
        /// Refreshes the list of trigger points by finding all in the scene.
        /// </summary>
        public void RefreshTriggerPoints()
        {
            _triggerPoints.Clear();
            _triggerPoints.AddRange(FindObjectsByType<MinigameTriggerPoint>(FindObjectsSortMode.None));
        }

        /// <summary>
        /// Registers a trigger point with the manager.
        /// </summary>
        public void RegisterTriggerPoint(MinigameTriggerPoint triggerPoint)
        {
            if (triggerPoint != null && !_triggerPoints.Contains(triggerPoint))
            {
                _triggerPoints.Add(triggerPoint);
            }
        }

        /// <summary>
        /// Unregisters a trigger point from the manager.
        /// </summary>
        public void UnregisterTriggerPoint(MinigameTriggerPoint triggerPoint)
        {
            _triggerPoints.Remove(triggerPoint);
        }

        /// <summary>
        /// Gets the nearest valid trigger point to the given player position.
        /// A trigger point is valid if the player is within its radius.
        /// </summary>
        /// <param name="playerPosition">The player's current position</param>
        /// <param name="trigger">The nearest valid trigger point, or null if none found</param>
        /// <param name="distance">The distance to the trigger point</param>
        /// <returns>True if a valid trigger point was found, false otherwise</returns>
        public bool GetNearestTriggerPoint(Vector2 playerPosition, out MinigameTriggerPoint trigger, out float distance)
        {
            trigger = null;
            distance = float.MaxValue;

            // Filter to only trigger points within range
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

        /// <summary>
        /// Gets all trigger points within range of the player position.
        /// </summary>
        public List<MinigameTriggerPoint> GetTriggerPointsInRange(Vector2 playerPosition)
        {
            return _triggerPoints
                .Where(tp => tp != null && tp.IsWithinRange(playerPosition))
                .OrderBy(tp => tp.GetDistance(playerPosition))
                .ToList();
        }
    }
}

