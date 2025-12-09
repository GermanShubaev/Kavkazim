using Kavkazim.Config;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Player
{
    /// <summary>
    /// Server-only motor: applies validated velocity to Rigidbody2D.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMotorServer : NetworkBehaviour
    {
        [SerializeField] private NetworkGameplayConfig config;
        private Rigidbody2D _rb;
        private Vector2 _serverVelocity;

        private void Awake() => _rb = GetComponent<Rigidbody2D>();

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
            _rb.MovePosition(_rb.position + _serverVelocity * Time.fixedDeltaTime);
        }
    }
}