using Unity.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Makes the main camera follow the local player.
    /// </summary>
    public class CameraFollow : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);
        [SerializeField] private float smoothSpeed = 5f;

        private Camera _cam;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Debug.Log($"[CameraFollow] Spawned for {name}. Owner: {OwnerClientId}");
                TryFindCamera();
            }
        }

        private void TryFindCamera()
        {
            if (_cam) return;
            _cam = Camera.main;
            if (_cam)
            {
                Debug.Log($"[CameraFollow] Camera found and attached to {name}");
            }
            else
            {
                Debug.LogWarning($"[CameraFollow] Main Camera not found yet for {name}...");
            }
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;

            if (!_cam)
            {
                TryFindCamera();
                if (!_cam) return;
            }

            Vector3 desiredPos = transform.position + offset;
            Vector3 smoothedPos = Vector3.Lerp(_cam.transform.position, desiredPos, smoothSpeed * Time.deltaTime);
            _cam.transform.position = smoothedPos;
        }
    }
}
