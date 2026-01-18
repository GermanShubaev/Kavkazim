
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Player
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAnimator : NetworkBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        
        private NetworkVariable<Vector2> _networkMoveDirection = new (
            Vector2.zero
        );
        
        private Vector2 _lastDirection = Vector2.down;

        private void Awake()
        {
            if (!animator) animator = GetComponent<Animator>();
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public override void OnNetworkSpawn()
        {
            _networkMoveDirection.OnValueChanged += OnMoveDirectionChanged;
            UpdateAnimation(_networkMoveDirection.Value);
        }

        public override void OnNetworkDespawn()
        {
            _networkMoveDirection.OnValueChanged -= OnMoveDirectionChanged;
        }

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
            
            if (isMoving)
            {
                _lastDirection = direction.normalized;
            }
            
            if (animator != null)
            {
                animator.SetFloat(MoveXHash, isMoving ? direction.x : _lastDirection.x);
                animator.SetFloat(MoveYHash, isMoving ? direction.y : _lastDirection.y);
                animator.SetBool(IsMovingHash, isMoving);
            }
        }
    }
}
