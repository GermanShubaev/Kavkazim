using Kavkazim.Config;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Player
{
    /// <summary>
    /// Server-only motor: applies validated velocity to Rigidbody2D.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerMotorServer : NetworkBehaviour
    {
        [SerializeField] private NetworkGameplayConfig config;
        [SerializeField] private float skinWidth = 0.02f;
        [SerializeField] private LayerMask collisionMask = ~0; // All layers by default
        
        private Rigidbody2D _rb;
        private BoxCollider2D _collider;
        private Vector2 _serverVelocity;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<BoxCollider2D>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            else enabled = false; // server authority only
        }

        public void ApplyInput(Vector2 moveInput)
        {
            // Sanitize input length and compute velocity
            var clamped = Vector2.ClampMagnitude(moveInput, 1f);
            _serverVelocity = clamped * (config ? config.moveSpeed : 3.5f);
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            
            Vector2 moveDirection = _serverVelocity.normalized;
            float moveDistance = _serverVelocity.magnitude * Time.fixedDeltaTime;
            
            if (moveDistance < 0.0001f) return; // No significant movement
            
            // Use BoxCastAll to allow filtering out other players
            Vector2 castOrigin = _rb.position + _collider.offset;
            RaycastHit2D[] hits = Physics2D.BoxCastAll(
                castOrigin,
                _collider.size * 0.9f,
                0f,
                moveDirection,
                moveDistance + skinWidth,
                collisionMask
            );
            
            // Sort by distance to ensure we handle the closest valid obstacle first
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            
            Vector2 targetPosition;
            
            // Find the first valid collision that is NOT another player
            RaycastHit2D? validHit = null;
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject) continue; // Skip self
                if (hit.collider.isTrigger) continue; // Skip triggers
                if (hit.distance < 0.001f) continue; // Skip initial overlaps
                
                // Skip collision with other players
                if (hit.collider.GetComponent<PlayerMotorServer>() != null) continue;

                validHit = hit;
                break; // Found the closest valid obstacle
            }
            
            if (validHit.HasValue)
            {
                float safeDistance = Mathf.Max(0f, validHit.Value.distance - skinWidth);
                targetPosition = _rb.position + moveDirection * safeDistance;
            }
            else
            {
                targetPosition = _rb.position + _serverVelocity * Time.fixedDeltaTime;
            }
            
            _rb.MovePosition(targetPosition);
        }
    }
}