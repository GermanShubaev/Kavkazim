using UnityEngine;

namespace Minigames
{
    /// <summary>
    /// Setup script that creates minigame trigger points in the scene.
    /// Add this component to a GameObject in the scene to automatically create trigger points.
    /// </summary>
    public class MinigameTriggerPointSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField] private bool createDefaultTriggerPoint = true;
        [SerializeField] private Vector2 defaultPosition = new Vector2(16, 26);
        [SerializeField] private float defaultRadius = 4f;
        [SerializeField] private MinigameType defaultGameType = MinigameType.EmptyPopup;

        [Header("Additional Trigger Points")]
        [SerializeField] private bool createAdditionalTriggers = false;
        [SerializeField] private TriggerPointData[] additionalTriggers = new TriggerPointData[0];

        [System.Serializable]
        public class TriggerPointData
        {
            public Vector2 position;
            public float radius;
            public MinigameType gameType;
        }

        private void Awake()
        {
            if (createDefaultTriggerPoint)
            {
                CreateTriggerPoint(defaultPosition, defaultRadius, defaultGameType);
            }

            if (createAdditionalTriggers && additionalTriggers != null)
            {
                foreach (var triggerData in additionalTriggers)
                {
                    CreateTriggerPoint(triggerData.position, triggerData.radius, triggerData.gameType);
                }
            }
        }

        /// <summary>
        /// Creates a trigger point at the specified position with the given radius and game type.
        /// </summary>
        public void CreateTriggerPoint(Vector2 position, float radius, MinigameType gameType)
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

            // Set transform position for visual reference (though the actual position is in the field)
            triggerObj.transform.position = new Vector3(position.x, position.y, 0);

            Debug.Log($"Created MinigameTriggerPoint at ({position.x}, {position.y}) with radius {radius} for {gameType}");
        }
    }
}

