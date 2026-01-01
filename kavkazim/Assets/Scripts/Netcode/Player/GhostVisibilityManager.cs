using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace Netcode.Player
{
    /// <summary>
    /// Manages ghost visibility across all players.
    /// - Alive players cannot see ghosts
    /// - Ghosts can see everyone (alive + other ghosts)
    /// 
    /// Attach this to the Player Prefab alongside PlayerState.
    /// </summary>
    [RequireComponent(typeof(PlayerState))]
    public class GhostVisibilityManager : NetworkBehaviour
    {
        [Header("Renderers to Hide")]
        [Tooltip("Renderers to hide when this player should be invisible")]
        [SerializeField] private Renderer[] renderersToHide;
        
        [Header("Other Objects to Hide")]
        [Tooltip("Additional GameObjects to hide (name label, etc.)")]
        [SerializeField] private GameObject[] objectsToHide;

        private PlayerState _playerState;
        private static GhostVisibilityManager _localPlayerVisibility;
        private static List<GhostVisibilityManager> _allPlayers = new List<GhostVisibilityManager>();
        private bool _initComplete;

        private void Awake()
        {
            _playerState = GetComponent<PlayerState>();
            
            // Auto-find renderers if not assigned
            if (renderersToHide == null || renderersToHide.Length == 0)
            {
                renderersToHide = GetComponentsInChildren<Renderer>();
            }
        }

        /// <summary>
        /// Late initialization to find dynamically created objects like NameLabel.
        /// </summary>
        private void LateInit()
        {
            if (_initComplete) return;
            
            // Find NameLabel if not in objectsToHide
            Transform nameLabel = transform.Find("NameLabel");
            if (nameLabel != null)
            {
                // Add to objects to hide if not already there
                var list = new List<GameObject>(objectsToHide ?? new GameObject[0]);
                if (!list.Contains(nameLabel.gameObject))
                {
                    list.Add(nameLabel.gameObject);
                    objectsToHide = list.ToArray();
                }
            }
            
            // Refresh renderers list
            renderersToHide = GetComponentsInChildren<Renderer>();
            
            _initComplete = true;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // Register this player
            _allPlayers.Add(this);
            
            // Track local player
            if (IsOwner)
            {
                _localPlayerVisibility = this;
                Debug.Log($"[GhostVisibility] Local player registered: {OwnerClientId}");
            }
            
            // Subscribe to this player's state changes
            _playerState.IsAlive.OnValueChanged += OnThisPlayerStateChanged;
            
            // Apply initial visibility based on current states
            UpdateVisibility();
            
            Debug.Log($"[GhostVisibility] Player {OwnerClientId} spawned. Total players: {_allPlayers.Count}");
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe
            _playerState.IsAlive.OnValueChanged -= OnThisPlayerStateChanged;
            
            // Unregister
            _allPlayers.Remove(this);
            
            if (IsOwner)
            {
                _localPlayerVisibility = null;
            }
            
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Called when THIS player's alive state changes.
        /// </summary>
        private void OnThisPlayerStateChanged(bool previousValue, bool newValue)
        {
            Debug.Log($"[GhostVisibility] Player {OwnerClientId} state changed: {previousValue} -> {newValue}");
            
            // If THIS is the local player, we need to re-evaluate visibility of ALL other players
            if (IsOwner)
            {
                Debug.Log($"[GhostVisibility] Local player died/revived. Updating all visibility.");
                UpdateAllPlayersVisibility();
            }
            
            // Update this player's visibility for everyone
            UpdateVisibility();
        }

        /// <summary>
        /// Updates visibility of this player based on local player's state.
        /// </summary>
        public void UpdateVisibility()
        {
            // Ensure we've found dynamically created objects
            LateInit();
            
            // Always visible to self
            if (IsOwner)
            {
                SetVisible(true);
                return;
            }
            
            // If no local player tracked yet, default to visible
            if (_localPlayerVisibility == null)
            {
                SetVisible(true);
                return;
            }
            
            bool localPlayerIsAlive = _localPlayerVisibility._playerState.IsAlive.Value;
            bool thisPlayerIsAlive = _playerState.IsAlive.Value;
            
            // Visibility rules:
            // - If this player is alive: always visible to everyone
            // - If this player is a ghost:
            //   - Visible to ghosts (local player is dead)
            //   - Invisible to alive players (local player is alive)
            
            if (thisPlayerIsAlive)
            {
                // Alive players are always visible
                SetVisible(true);
            }
            else
            {
                // Ghost visibility depends on local player's state
                if (localPlayerIsAlive)
                {
                    // Local player is alive, hide ghosts
                    SetVisible(false);
                    Debug.Log($"[GhostVisibility] Hiding ghost {OwnerClientId} from alive local player");
                }
                else
                {
                    // Local player is also a ghost, show other ghosts
                    SetVisible(true);
                    Debug.Log($"[GhostVisibility] Showing ghost {OwnerClientId} to ghost local player");
                }
            }
        }

        /// <summary>
        /// Updates visibility of all players (called when local player's state changes).
        /// </summary>
        private static void UpdateAllPlayersVisibility()
        {
            foreach (var player in _allPlayers)
            {
                if (player != null)
                {
                    player.UpdateVisibility();
                }
            }
        }

        /// <summary>
        /// Sets whether this player's visuals are visible or hidden.
        /// </summary>
        private void SetVisible(bool visible)
        {
            // Hide/show renderers
            foreach (var renderer in renderersToHide)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
            
            // Hide/show additional objects
            foreach (var obj in objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(visible);
                }
            }
        }

        /// <summary>
        /// Check if this player is currently visible.
        /// </summary>
        private bool IsVisible
        {
            get
            {
                if (renderersToHide != null && renderersToHide.Length > 0 && renderersToHide[0] != null)
                {
                    return renderersToHide[0].enabled;
                }
                return true;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Log Visibility State")]
        private void DebugLogState()
        {
            Debug.Log($"[GhostVisibility] Player {OwnerClientId}:");
            Debug.Log($"  - IsOwner: {IsOwner}");
            Debug.Log($"  - IsAlive: {_playerState?.IsAlive.Value}");
            Debug.Log($"  - IsVisible: {IsVisible}");
            Debug.Log($"  - Local player alive: {_localPlayerVisibility?._playerState?.IsAlive.Value}");
        }
#endif
    }
}
