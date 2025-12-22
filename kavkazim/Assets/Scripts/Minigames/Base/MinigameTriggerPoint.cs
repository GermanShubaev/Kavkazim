using UnityEngine;

namespace Minigames
{
    /// <summary>
    /// Defines a trigger point for minigames. When a player is within the specified radius
    /// of this position and presses 't', the associated minigame will start.
    /// </summary>
    public class MinigameTriggerPoint : MonoBehaviour
    {
        [Header("Trigger Settings")]
        [SerializeField] private Vector2 position = Vector2.zero;
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private MinigameType gameType = MinigameType.LezginkaSort;

        [Header("Debug")]
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor = Color.yellow;

        public Vector2 Position => position;
        public float Radius => radius;
        public MinigameType GameType => gameType;

        private void Awake()
        {
            // Register with MinigameManager if it exists
            MinigameManager manager = FindFirstObjectByType<MinigameManager>();
            if (manager != null)
            {
                manager.RegisterTriggerPoint(this);
            }
        }

        private void OnDestroy()
        {
            // Unregister from MinigameManager
            MinigameManager manager = FindFirstObjectByType<MinigameManager>();
            if (manager != null)
            {
                manager.UnregisterTriggerPoint(this);
            }
        }

        /// <summary>
        /// Checks if the given position is within the trigger radius.
        /// </summary>
        public bool IsWithinRange(Vector2 playerPosition)
        {
            float distance = Vector2.Distance(playerPosition, position);
            return distance <= radius;
        }

        /// <summary>
        /// Gets the distance from the given position to this trigger point.
        /// </summary>
        public float GetDistance(Vector2 playerPosition)
        {
            return Vector2.Distance(playerPosition, position);
        }

        private void OnDrawGizmos()
        {
            if (!showGizmo) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(new Vector3(position.x, position.y, 0), radius);
            
            // Draw a small cross at the center
            Gizmos.color = Color.red;
            float crossSize = 0.1f;
            Gizmos.DrawLine(
                new Vector3(position.x - crossSize, position.y, 0),
                new Vector3(position.x + crossSize, position.y, 0)
            );
            Gizmos.DrawLine(
                new Vector3(position.x, position.y - crossSize, 0),
                new Vector3(position.x, position.y + crossSize, 0)
            );
        }
    }
}

