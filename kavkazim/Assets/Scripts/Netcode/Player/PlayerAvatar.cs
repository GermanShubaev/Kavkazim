using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Services.Authentication;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Handles presentation-only details that can run on all clients (e.g., animator).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerAvatar : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        // Networked name variable
        public NetworkVariable<Unity.Collections.FixedString32Bytes> PlayerName = 
            new NetworkVariable<Unity.Collections.FixedString32Bytes>();

        private TextMeshPro _nameLabel;

        public override void OnNetworkSpawn()
        {
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
            // Setup name label
            SetupNameLabel();

            // If we are the owner, we set the name from Auth service
            if (IsOwner)
            {
                // Spawn Gameplay UI
                if (GameObject.FindFirstObjectByType<Kavkazim.UI.GameplayUI>() == null)
                {
                    GameObject uiGo = new GameObject("GameplayUIManager");
                    uiGo.transform.SetParent(transform); // Parent to player to persist across scenes
                    uiGo.AddComponent<Kavkazim.UI.GameplayUI>();
                }

                // Tint owner green
                if (spriteRenderer) spriteRenderer.color = new Color(0.8f, 1f, 0.8f);
                
                // Set name
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
                
                PlayerName.Value = pName;
            }

            // Update label initially
            UpdateNameLabel(PlayerName.Value);

            // Listen for changes
            PlayerName.OnValueChanged += (oldVal, newVal) => UpdateNameLabel(newVal);
        }

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
    }
}