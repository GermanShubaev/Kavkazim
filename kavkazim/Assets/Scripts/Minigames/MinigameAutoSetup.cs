using UnityEngine;

namespace Minigames
{
    /// <summary>
    /// Automatically creates the default minigame trigger point at (16, 26) with radius 4.
    /// Add this script to any GameObject in the Gameplay scene, or it will auto-create on scene load.
    /// </summary>
    public class MinigameAutoSetup : MonoBehaviour
    {
        [Header("Default Trigger Point")]
        [SerializeField] private bool createTriggerPoint = true;
        [SerializeField] private Vector2 triggerPosition = new Vector2(16, 26);
        [SerializeField] private float triggerRadius = 4f;
        [SerializeField] private MinigameType triggerGameType = MinigameType.EmptyPopup;

        private void Awake()
        {
            if (createTriggerPoint)
            {
                CreateTriggerPoint(triggerPosition, triggerRadius, triggerGameType);
            }
        }

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

            Debug.Log($"[MinigameAutoSetup] Created trigger point at ({position.x}, {position.y}) with radius {radius} for {gameType}");
        }
    }
}

