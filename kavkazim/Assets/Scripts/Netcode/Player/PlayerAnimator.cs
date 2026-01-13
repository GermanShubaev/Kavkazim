// Assets/Scripts/Netcode/Player/PlayerAnimator.cs

using Unity.Netcode;
using UnityEngine;

namespace Netcode.Player
{
    /// <summary>
    /// Handles player movement animations based on input direction.
    /// Runs on all clients for smooth visual feedback.
    /// Uses NetworkVariable to sync movement direction across network.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAnimator : NetworkBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        // Animator parameter hashes for performance
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        
        // Networked movement direction for syncing animations across clients
        private NetworkVariable<Vector2> _networkMoveDirection = new (
            Vector2.zero
        );
        
        // Last non-zero direction for idle facing
        private Vector2 _lastDirection = Vector2.down;

        private void Awake()
        {
            if (!animator) animator = GetComponent<Animator>();
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public override void OnNetworkSpawn()
        {
            // Subscribe to movement changes for animation updates on all clients
            _networkMoveDirection.OnValueChanged += OnMoveDirectionChanged;
            
            // Apply initial state
            UpdateAnimation(_networkMoveDirection.Value);
        }

        public override void OnNetworkDespawn()
        {
            _networkMoveDirection.OnValueChanged -= OnMoveDirectionChanged;
        }

        /// <summary>
        /// Called by PlayerMotorServer when input is applied.
        /// Server-only: updates the networked move direction which syncs to all clients.
        /// </summary>
        /// <param name="direction">The normalized movement direction from input</param>
        public void SetMoveDirection(Vector2 direction)
        {
            if (!IsServer) return;
            _networkMoveDirection.Value = direction;
        }

        private void OnMoveDirectionChanged(Vector2 oldValue, Vector2 newValue)
        {
            UpdateAnimation(newValue);
        }

        private void UpdateAnimation(Vector2 direction)
        {
            bool isMoving = direction.sqrMagnitude > 0.01f;
            
            // Store last movement direction for idle facing
            if (isMoving)
            {
                _lastDirection = direction.normalized;
            }
            
            // Update animator parameters
            if (animator != null)
            {
                // When moving, use actual direction; when idle, use last direction
                animator.SetFloat(MoveXHash, isMoving ? direction.x : _lastDirection.x);
                animator.SetFloat(MoveYHash, isMoving ? direction.y : _lastDirection.y);
                animator.SetBool(IsMovingHash, isMoving);
            }
        }
    }
}
