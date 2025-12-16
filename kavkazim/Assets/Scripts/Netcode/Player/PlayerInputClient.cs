// Assets/Scripts/Netcode/Player/PlayerInputClient.cs

using Kavkazim.Netcode;
using Minigames;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Netcode.Player
{
    [RequireComponent(typeof(PlayerMotorServer))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputClient : NetworkBehaviour
    {
        private InputAction _move;
        private PlayerAvatar _avatar;
        private IMinigame _currentMinigame;


        private void Start()
        {
            Debug.Log($"[Input] IsOwner={IsOwner}, MoveFound={_move!=null}");

            var pi = GetComponent<PlayerInput>();
            _move = pi && pi.actions ? pi.actions["Move"] : null;
            if (_move != null && !_move.enabled) _move.Enable();

            _avatar = GetComponent<PlayerAvatar>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Handle Move
            if (_move != null)
            {
                Vector2 v = _move.ReadValue<Vector2>();
                SubmitInputToServerRpc(v); 
            }

            // Handle Kill (K key)
            // Note: Using direct Keyboard access for simplicity as per plan. 
            // Ideally this should be an Input Action.
            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            {
                if (_avatar && _avatar.CurrentRole is KavkaziRole kavkazi)
                {
                    kavkazi.TryKill();
                }
            }

            // Handle Minigame (T key)
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                HandleMinigameTrigger();
            }

        }

        private void HandleMinigameTrigger()
        {
            // If a minigame is already active, close it
            if (_currentMinigame != null && _currentMinigame.IsActive)
            {
                _currentMinigame.CloseGame();
                _currentMinigame = null;
                return;
            }

            // Get player's current position
            Vector2 playerPosition = transform.position;

            // Check for nearest trigger point
            MinigameManager manager = MinigameManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[PlayerInputClient] MinigameManager not found!");
                return;
            }

            if (manager.GetNearestTriggerPoint(playerPosition, out MinigameTriggerPoint trigger, out float distance))
            {
                Debug.Log($"[PlayerInputClient] Trigger found! Game: {trigger.GameType}, Distance: {distance:F2}");
                
                // Create and start the minigame
                _currentMinigame = MinigameFactory.CreateMinigame(trigger.GameType);
                if (_currentMinigame != null)
                {
                    _currentMinigame.StartGame();
                }
                else
                {
                    Debug.LogError($"[PlayerInputClient] Failed to create minigame of type {trigger.GameType}");
                }
            }
            else
            {
                Debug.Log("[PlayerInputClient] No minigame trigger point within range.");
            }
        }

        // MUST end with 'Rpc'
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputToServerRpc(Vector2 move)
        {
            var motor = GetComponent<PlayerMotorServer>();
            motor?.ApplyInput(move);
        }

        private void OnDestroy()
        {
            // Clean up minigame if still active
            if (_currentMinigame != null && _currentMinigame.IsActive)
            {
                _currentMinigame.CloseGame();
            }
        }
    }
}