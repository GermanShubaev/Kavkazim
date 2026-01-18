using System;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Player
{
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerState : NetworkBehaviour
    {
        public static readonly System.Collections.Generic.List<PlayerState> ActivePlayers = new System.Collections.Generic.List<PlayerState>();

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

        public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
            true
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
            
            IsAlive.OnValueChanged += OnAliveStateChanged;
            
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

        private void OnAliveStateChanged(bool previousValue, bool newValue)
        {
            Debug.Log($"[PlayerState] Player {OwnerClientId} state changed: {previousValue} -> {newValue}");
            ApplyState(newValue);
        }

        private void ApplyState(bool isAlive)
        {
            if (isAlive)
                ApplyAliveMode();
            else
                ApplyGhostMode();
        }

        public void ApplyGhostMode()
        {
            SetLayerRecursively(gameObject, _ghostLayer);
            
            gameObject.tag = "Ghost";
            
            if (spriteRenderer)
            {
                Color ghostColor = _originalColor;
                ghostColor.a = ghostAlpha;
                spriteRenderer.color = ghostColor;
            }
            
            if (playerCollider)
            {
                playerCollider.isTrigger = true;
            }

            Debug.Log($"[PlayerState] Player {OwnerClientId} is now a GHOST");
        }

        public void ApplyAliveMode()
        {
            SetLayerRecursively(gameObject, _aliveLayer);
            
            gameObject.tag = "Player";
            
            if (spriteRenderer)
            {
                spriteRenderer.color = _originalColor;
            }
            if (playerCollider)
            {
                playerCollider.isTrigger = false;
            }

            Debug.Log($"[PlayerState] Player {OwnerClientId} is now ALIVE");
        }

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
            
            if (spawnBody)
            {
                OnPlayerKilled?.Invoke(this);
            }
        }

        public void ForceSetAliveState(bool alive)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[PlayerState] ForceSetAliveState() called on client - ignored.");
                return;
            }
            
            Debug.Log($"[PlayerState] SERVER: Force setting Player {OwnerClientId} IsAlive={alive}");
            IsAlive.Value = alive;
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        
    }
}
