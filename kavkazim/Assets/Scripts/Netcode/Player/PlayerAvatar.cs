using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Services.Authentication;
using System.Collections;
using UI;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Handles presentation-only details that can run on all clients (e.g., animator).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerAvatar : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        [Header("Camera Follow")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -10);
        [SerializeField] private float cameraSmoothSpeed = 5f;
        private Camera _mainCamera;
        
        // Networked name variable
        public NetworkVariable<Unity.Collections.FixedString32Bytes> PlayerName = 
            new NetworkVariable<Unity.Collections.FixedString32Bytes>();

        // Networked Role variable
        public NetworkVariable<PlayerRoleType> Role = 
            new NetworkVariable<PlayerRoleType>(PlayerRoleType.Innocent);

        private TextMeshPro _nameLabel;
        public PlayerRole CurrentRole { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
            // Setup name label
            SetupNameLabel();

            // Initialize Role
            UpdateRole(Role.Value);
            Role.OnValueChanged += (oldVal, newVal) => UpdateRole(newVal);

            // If we are the owner, we set the name from Auth service
            if (IsOwner)
            {
                // Spawn Gameplay UI
                if (GameObject.FindFirstObjectByType<GameplayUI>() == null)
                {
                    GameObject uiGo = new GameObject("GameplayUIManager");
                    uiGo.transform.SetParent(transform); // Parent to player to persist across scenes
                    uiGo.AddComponent<GameplayUI>();
                }

                // Assign random role if we are the server (Host)
                if (IsServer)
                {
                    // Simple random role assignment for testing
                    // 50% chance to be Kavkazi
                    Role.Value = Random.value > 0.5f ? PlayerRoleType.Kavkazi : PlayerRoleType.Innocent;
                }
                
                // Set name - use RPC to avoid write permission error
                string pName = "";
                try 
                {
                    if (AuthenticationService.Instance.IsSignedIn)
                    {
                        pName = AuthenticationService.Instance.PlayerName;
                        // Remove #1234 suffix if present
                        if (!string.IsNullOrEmpty(pName))
                        {
                            var parts = pName.Split('#');
                            if (parts.Length > 0) pName = parts[0];
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(pName)) pName = $"Player {OwnerClientId}";
                
                // Request the server to set our name
                SetPlayerNameServerRpc(pName);

                // Initialize Camera
                TryFindCamera();
            }

            // Update label initially
            UpdateNameLabel(PlayerName.Value);

            // Listen for changes
            PlayerName.OnValueChanged += (oldVal, newVal) => UpdateNameLabel(newVal);
        }

        [Rpc(SendTo.Server)]
        private void SetPlayerNameServerRpc(string name)
        {
            PlayerName.Value = name;
        }

        private void UpdateRole(PlayerRoleType roleType)
        {
            switch (roleType)
            {
                case PlayerRoleType.Kavkazi:
                    CurrentRole = new KavkaziRole(this);
                    break;
                case PlayerRoleType.Innocent:
                default:
                    CurrentRole = new InnocentRole(this);
                    break;
            }
            
            CurrentRole.SetupVisuals();
            Debug.Log($"[PlayerAvatar] Role set to {roleType}");
        }

        public void SetBodyColor(Color c)
        {
            if (spriteRenderer) spriteRenderer.color = c;
        }

        public void SetNameColor(Color c)
        {
            if (_nameLabel) _nameLabel.color = c;
        }

        public void PerformSlashAnimation()
        {
            StartCoroutine(SlashRoutine());
        }

        private System.Collections.IEnumerator SlashRoutine()
        {
            // Simple slash: rotate back and forth
            float duration = 0.2f;
            float elapsed = 0;
            Quaternion startRot = transform.rotation;
            Quaternion targetRot = startRot * Quaternion.Euler(0, 0, -45);

            while (elapsed < duration)
            {
                transform.rotation = Quaternion.Lerp(startRot, targetRot, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.rotation = startRot;
        }

        // NOTE: Kill functionality has been moved to KillerAbility and PlayerState components.
        // - KillerAbility.RequestKillServerRpc() handles the kill request
        // - PlayerState manages the alive/ghost state
        // PerformSlashAnimation() is kept here for visual feedback

        private void SetupNameLabel()
        {
            // Create a child object for the text
            GameObject textObj = new GameObject("NameLabel");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = new Vector3(0, 1.2f, 0); // Above player
            
            _nameLabel = textObj.AddComponent<TextMeshPro>();
            _nameLabel.alignment = TextAlignmentOptions.Center;
            _nameLabel.fontSize = 4;
            _nameLabel.color = Color.white;
            _nameLabel.sortingOrder = 10; // Ensure it's above sprites
        }

        private void UpdateNameLabel(Unity.Collections.FixedString32Bytes newName)
        {
            if (_nameLabel) _nameLabel.text = newName.ToString();
        }

        private void TryFindCamera()
        {
            if (_mainCamera) return;
            _mainCamera = Camera.main;
            if (_mainCamera)
            {
                Debug.Log($"[PlayerAvatar] Camera found and attached to {name}");
            }
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;

            if (!_mainCamera)
            {
                TryFindCamera();
                if (!_mainCamera) return;
            }

            Vector3 desiredPos = transform.position + cameraOffset;
            Vector3 smoothedPos = Vector3.Lerp(_mainCamera.transform.position, desiredPos, cameraSmoothSpeed * Time.deltaTime);
            _mainCamera.transform.position = smoothedPos;
        }
    }
}