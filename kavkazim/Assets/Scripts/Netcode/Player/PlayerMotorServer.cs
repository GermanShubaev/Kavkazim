using Kavkazim.Config;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Player
{
    /// <summary>
    /// Server-only motor: applies validated velocity to Rigidbody2D.
    /// Collision is now handled by Unity's physics engine (CapsuleCollider2D + Rigidbody2D).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerMotorServer : NetworkBehaviour
    {
        [SerializeField] private NetworkGameplayConfig config;

        // Old manual collision fields removed:
        // skinWidth, collisionMask

        private Rigidbody2D _rb;
        private CapsuleCollider2D _collider;
        private Vector2 _serverVelocity;
        private PlayerState _playerState;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CapsuleCollider2D>();
            _playerState = GetComponent<PlayerState>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                //_rb.bodyType = RigidbodyType2D.Dynamic; // Ensure it's dynamic for collisions
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Better for wall sliding
                _rb.freezeRotation = true;
                _rb.gravityScale = 0f;
            }
            else
            {
                enabled = false; // server authority only
            }
        }

        public void ApplyInput(Vector2 moveInput)
        {
            // Sanitize input length
            var clamped = Vector2.ClampMagnitude(moveInput, 1f);
            
            // Get move speed from lobby settings, fallback to config, then default
            float moveSpeed = 3.5f;
            if (Kavkazim.Netcode.GameSessionManager.Instance != null)
                moveSpeed = Kavkazim.Netcode.GameSessionManager.Instance.Settings.Value.MoveSpeed;
            
            _serverVelocity = clamped * moveSpeed;
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            // Apply velocity directly to the Rigidbody.
            // Unity's physics engine handles wall sliding and collisions automatically.
            // - Alive players (Collider not trigger): Will collide and slide against walls.
            // - Ghosts (Collider is trigger): Will pass through walls (as set by PlayerState).
            _rb.linearVelocity = _serverVelocity;
        }
    }
}
