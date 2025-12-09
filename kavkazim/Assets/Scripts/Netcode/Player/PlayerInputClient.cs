// Assets/Scripts/Netcode/Player/PlayerInputClient.cs
// Assets/Scripts/Netcode/Player/PlayerInputClient.cs

using Kavkazim.Netcode;
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
        }

        // MUST end with 'Rpc'
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputToServerRpc(Vector2 move)
        {
            var motor = GetComponent<PlayerMotorServer>();
            motor?.ApplyInput(move);
        }
    }
}