using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Services.Authentication;
using System.Collections;
using System.Collections.Generic;
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

        // Networked Role variable - OWNER ONLY read permission for security
        // Only the server and the owning client can read the true role
        public NetworkVariable<PlayerRoleType> Role = 
            new NetworkVariable<PlayerRoleType>(
                PlayerRoleType.Innocent,
                NetworkVariableReadPermission.Owner,
                NetworkVariableWritePermission.Server
            );

        // Local cache of perceived roles for each player (what THIS client sees)
        // Key: NetworkObjectId, Value: Perceived role
        private Dictionary<ulong, PlayerRoleType> _perceivedRoles = new Dictionary<ulong, PlayerRoleType>();
        
        // The role this client perceives for THIS player (set via RPC)
        public PlayerRoleType PerceivedRole { get; private set; } = PlayerRoleType.Innocent;

        private TextMeshPro _nameLabel;
        public PlayerRole CurrentRole { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
            // Setup name label
            SetupNameLabel();

            // Initialize visuals with default Innocent appearance
            // Actual perceived role will be set via RPC from server
            UpdateVisuals(PerceivedRole);

            // If we are the owner, set up local player specifics
            if (IsOwner)
            {
                // PlayerAvatar is only spawned when match starts, so spawn GameplayUI
                if (GameObject.FindFirstObjectByType<GameplayUI>() == null)
                {
                    GameObject uiGo = new GameObject("GameplayUIManager");
                    uiGo.transform.SetParent(transform); // Parent to player to persist
                    uiGo.AddComponent<GameplayUI>();
                }

                // Role assignment is handled by PlayerSpawnHandler on the server
                
                // Only set name if server hasn't already set it
                // (Server sets name from GameSessionManager PlayerSessionData)
                if (string.IsNullOrEmpty(PlayerName.Value.ToString()))
                {
                    // Get name from PlayerPrefs (set during MainMenu connect)
                    string pName = PlayerPrefs.GetString("PlayerName", "");
                    
                    // Fallback to Auth service if PlayerPrefs is empty
                    if (string.IsNullOrEmpty(pName))
                    {
                        try 
                        {
                            if (AuthenticationService.Instance.IsSignedIn)
                            {
                                pName = AuthenticationService.Instance.PlayerName;
                                if (!string.IsNullOrEmpty(pName))
                                {
                                    var parts = pName.Split('#');
                                    if (parts.Length > 0) pName = parts[0];
                                }
                            }
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(pName)) pName = $"Player {OwnerClientId}";
                    
                    // Request the server to set our name on the avatar
                    SetPlayerNameServerRpc(pName);
                }

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

        /// <summary>
        /// Updates visuals based on PERCEIVED role (not true role).
        /// Called when we receive role perception update from server.
        /// </summary>
        private void UpdateVisuals(PlayerRoleType perceivedRole)
        {
            switch (perceivedRole)
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
            Debug.Log($"[PlayerAvatar] Visuals set to perceived role: {perceivedRole}");
        }

        /// <summary>
        /// SERVER ONLY: Get the true role of this player.
        /// Use this on the server for game logic (killing, voting, etc.)
        /// </summary>
        public PlayerRoleType GetTrueRole()
        {
            return Role.Value;
        }

        /// <summary>
        /// Targeted ClientRpc to receive perceived role for a specific player.
        /// Called by server to tell THIS client what role they should see for a player.
        /// </summary>
        /// <param name="targetNetworkObjectId">The NetworkObjectId of the player being described</param>
        /// <param name="perceivedRole">The role this client should perceive for that player</param>
        [Rpc(SendTo.SpecifiedInParams)]
        public void ReceivePerceivedRoleClientRpc(ulong targetNetworkObjectId, PlayerRoleType perceivedRole, RpcParams rpcParams = default)
        {
            // Store in our local perception cache
            _perceivedRoles[targetNetworkObjectId] = perceivedRole;
            
            // If this is about ourselves, update our perceived role
            if (targetNetworkObjectId == NetworkObjectId)
            {
                PerceivedRole = perceivedRole;
                UpdateVisuals(perceivedRole);
                Debug.Log($"[PlayerAvatar] Received my perceived role: {perceivedRole}");
            }
            else
            {
                // Find the target player and update their visuals
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    targetNetworkObjectId, out NetworkObject targetNetObj))
                {
                    PlayerAvatar targetAvatar = targetNetObj.GetComponent<PlayerAvatar>();
                    if (targetAvatar != null)
                    {
                        targetAvatar.ApplyPerceivedRole(perceivedRole);
                    }
                }
                Debug.Log($"[PlayerAvatar] Received perceived role for player {targetNetworkObjectId}: {perceivedRole}");
            }
        }

        /// <summary>
        /// Apply a perceived role to this avatar (called by other avatars receiving RPC).
        /// </summary>
        public void ApplyPerceivedRole(PlayerRoleType perceivedRole)
        {
            PerceivedRole = perceivedRole;
            UpdateVisuals(perceivedRole);
        }

        /// <summary>
        /// Get the perceived role for a player from our local cache.
        /// </summary>
        public PlayerRoleType GetPerceivedRoleFor(ulong networkObjectId)
        {
            return _perceivedRoles.TryGetValue(networkObjectId, out var role) ? role : PlayerRoleType.Innocent;
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