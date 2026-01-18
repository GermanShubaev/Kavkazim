using Kavkazim.Config;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerMotorServer : NetworkBehaviour
    {
        [SerializeField] private NetworkGameplayConfig config;

        private Rigidbody2D _rb;
        private Vector2 _serverVelocity;
        private PlayerAnimator _animator;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<PlayerAnimator>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.freezeRotation = true;
                _rb.gravityScale = 0f;
            }
            else
            {
                enabled = false;
            }
        }

        public void ApplyInput(Vector2 moveInput)
        {
            var clamped = Vector2.ClampMagnitude(moveInput, 1f);
            
            float moveSpeed = 3.5f;
            if (Kavkazim.Netcode.GameSessionManager.Instance != null)
                moveSpeed = Kavkazim.Netcode.GameSessionManager.Instance.Settings.Value.MoveSpeed;
            
            _serverVelocity = clamped * moveSpeed;
            
            if (_animator != null)
            {
                _animator.SetMoveDirection(clamped);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            _rb.linearVelocity = _serverVelocity;
        }
    }
}
