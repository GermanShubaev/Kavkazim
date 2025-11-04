using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Handles presentation-only details that can run on all clients (e.g., animator).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerAvatar : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public override void OnNetworkSpawn()
        {
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            // Example: tint owner differently
            if (IsOwner && spriteRenderer) spriteRenderer.color = new Color(0.8f, 1f, 0.8f);
        }
    }
}