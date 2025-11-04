// Assets/Scripts/Netcode/Player/PlayerInputClient.cs
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace Kavkazim.Netcode
{
    [RequireComponent(typeof(PlayerMotorServer))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputClient : NetworkBehaviour
    {
        private InputAction _move;

        private void Start()
        {
            Debug.Log($"[Input] IsOwner={IsOwner}, MoveFound={_move!=null}");

            var pi = GetComponent<PlayerInput>();
            _move = pi && pi.actions ? pi.actions["Move"] : null;
            if (_move != null && !_move.enabled) _move.Enable();
        }

        private void Update()
        {
            if (!IsOwner || _move == null) return;
            Vector2 v = _move.ReadValue<Vector2>();
            SubmitInputToServerRpc(v); // <-- call the RPC with 'Rpc' suffix
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