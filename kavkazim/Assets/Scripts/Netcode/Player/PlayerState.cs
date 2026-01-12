using System;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Player
{
    /// <summary>
    /// Manages player alive/ghost state with full network synchronization.
    /// Handles layer changes, collision, and visual state for ghosts.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerState : NetworkBehaviour
    {
        /// <summary>
        /// Global list of all active PlayerState components.
        /// Replaces slow FindObjectsByType calls.
        /// </summary>
        public static readonly System.Collections.Generic.List<PlayerState> ActivePlayers = new System.Collections.Generic.List<PlayerState>();

        /// <summary>
        /// Event fired on the server when any player is killed.
        /// Used by GameSessionManager to check win conditions.
        /// </summary>
        public static event Action<PlayerState> OnPlayerKilled;

        [Header("Layer Configuration")]
        [Tooltip("Layer name for alive players (must exist in Tags & Layers)")]
        [SerializeField] private string aliveLayerName = "Alive";
        
        [Tooltip("Layer name for ghost players (must exist in Tags & Layers)")]
        [SerializeField] private string ghostLayerName = "Ghost";

        [Header("Ghost Visual Settings")]
        [Tooltip("Sprite alpha when in ghost mode (0-1)")]
        [SerializeField] private float ghostAlpha = 0.5f;

        [Header("References")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Collider2D playerCollider;

        /// <summary>
        /// Networked alive state. True = alive, False = ghost.
        /// Only the server can modify this value.
        /// </summary>
        public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private int _aliveLayer;
        private int _ghostLayer;
        private Color _originalColor;

        private void Awake()
        {
            CacheComponents();
            CacheLayers();
        }

        private void CacheComponents()
        {
            if (!spriteRenderer)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
            if (!playerCollider)
                playerCollider = GetComponent<Collider2D>();

            if (spriteRenderer)
                _originalColor = spriteRenderer.color;
        }

        private void CacheLayers()
        {
            _aliveLayer = LayerMask.NameToLayer(aliveLayerName);
            _ghostLayer = LayerMask.NameToLayer(ghostLayerName);

            // Fallback to Default if layers don't exist
            if (_aliveLayer == -1)
            {
                Debug.LogWarning($"[PlayerState] Layer '{aliveLayerName}' not found. Using Default.");
                _aliveLayer = 0;
            }
            
            if (_ghostLayer == -1)
            {
                Debug.LogWarning($"[PlayerState] Layer '{ghostLayerName}' not found. Using Default.");
                _ghostLayer = 0;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            if (!ActivePlayers.Contains(this))
                ActivePlayers.Add(this);
            
            // Subscribe to state changes
            IsAlive.OnValueChanged += OnAliveStateChanged;
            
            // Apply initial state (handles late joiners)
            ApplyState(IsAlive.Value);
            
            Debug.Log($"[PlayerState] Player {OwnerClientId} spawned. IsAlive={IsAlive.Value}");
        }

        public override void OnNetworkDespawn()
        {
            if (ActivePlayers.Contains(this))
                ActivePlayers.Remove(this);
            
            IsAlive.OnValueChanged -= OnAliveStateChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Called when IsAlive value changes on any client.
        /// </summary>
        private void OnAliveStateChanged(bool previousValue, bool newValue)
        {
            Debug.Log($"[PlayerState] Player {OwnerClientId} state changed: {previousValue} -> {newValue}");
            ApplyState(newValue);
        }

        /// <summary>
        /// Applies the appropriate mode based on alive state.
        /// </summary>
        private void ApplyState(bool isAlive)
        {
            if (isAlive)
                ApplyAliveMode();
            else
                ApplyGhostMode();
        }

        /// <summary>
        /// Transitions this player to ghost mode.
        /// Called on all clients when the player dies.
        /// </summary>
        public void ApplyGhostMode()
        {
            // Change layer to Ghost (no collisions with walls/players)
            SetLayerRecursively(gameObject, _ghostLayer);
            
            // Set tag to Ghost for easy identification
            gameObject.tag = "Ghost";
            
            // Make sprite semi-transparent
            if (spriteRenderer)
            {
                Color ghostColor = _originalColor;
                ghostColor.a = ghostAlpha;
                spriteRenderer.color = ghostColor;
            }
            
            // Optionally disable the collider entirely for ghosts
            // (Layer collision matrix should handle this, but this is a fallback)
            if (playerCollider)
            {
                // Keep collider enabled but set as trigger so ghost can still be detected
                // but won't physically collide
                playerCollider.isTrigger = true;
            }

            Debug.Log($"[PlayerState] Player {OwnerClientId} is now a GHOST");
        }

        /// <summary>
        /// Restores this player to alive mode.
        /// Used for initial spawn and potential revive mechanics.
        /// </summary>
        public void ApplyAliveMode()
        {
            // Restore layer to Alive
            SetLayerRecursively(gameObject, _aliveLayer);
            
            // Set tag back to Player
            gameObject.tag = "Player";
            
            // Restore original sprite color
            if (spriteRenderer)
            {
                spriteRenderer.color = _originalColor;
            }
            
            // Restore normal collision
            if (playerCollider)
            {
                playerCollider.isTrigger = false;
            }

            Debug.Log($"[PlayerState] Player {OwnerClientId} is now ALIVE");
        }

        /// <summary>
        /// SERVER ONLY: Kills this player, transitioning them to ghost state.
        /// </summary>
        /// <param name="spawnBody">If true, spawns a dead body. Set to false for meeting eliminations.</param>
        public void Kill(bool spawnBody = true)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[PlayerState] Kill() called on client - ignored. Use KillerAbility.RequestKillServerRpc() instead.");
                return;
            }
            
            if (!IsAlive.Value)
            {
                Debug.LogWarning($"[PlayerState] Player {OwnerClientId} is already dead.");
                return;
            }
            
            IsAlive.Value = false;
            Debug.Log($"[PlayerState] SERVER: Player {OwnerClientId} has been killed (spawnBody={spawnBody}).");
            
            // Fire event for win condition checking (and body spawning if spawnBody is true)
            if (spawnBody)
            {
                OnPlayerKilled?.Invoke(this);
            }
        }

        /// <summary>
        /// SERVER ONLY: Revives this player back to alive state.
        /// </summary>
        public void Revive()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[PlayerState] Revive() called on client - ignored.");
                return;
            }
            
            if (IsAlive.Value)
            {
                Debug.LogWarning($"[PlayerState] Player {OwnerClientId} is already alive.");
                return;
            }
            
            IsAlive.Value = true;
            Debug.Log($"[PlayerState] SERVER: Player {OwnerClientId} has been revived.");
        }

        /// <summary>
        /// SERVER ONLY: Force sets the alive state, bypassing normal checks.
        /// Used for restoring player state after meeting respawn.
        /// </summary>
        public void ForceSetAliveState(bool alive)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[PlayerState] ForceSetAliveState() called on client - ignored.");
                return;
            }
            
            Debug.Log($"[PlayerState] SERVER: Force setting Player {OwnerClientId} IsAlive={alive}");
            IsAlive.Value = alive;
            
            // Don't invoke OnPlayerKilled since this is a state restoration, not a kill
        }

        /// <summary>
        /// Helper to set layer on GameObject and all children.
        /// </summary>
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Check if this player can interact with alive-only systems (tasks, buttons, meetings).
        /// </summary>
        public bool CanInteractAsAlive()
        {
            return IsAlive.Value;
        }

        /// <summary>
        /// Check if this player can see ghost-only UI elements.
        /// </summary>
        public bool CanSeeGhostUI()
        {
            return !IsAlive.Value;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor helper to test ghost mode.
        /// </summary>
        [ContextMenu("Debug: Toggle Ghost Mode")]
        private void DebugToggleGhostMode()
        {
            if (Application.isPlaying && IsServer)
            {
                if (IsAlive.Value)
                    Kill();
                else
                    Revive();
            }
        }
#endif
    }
}
