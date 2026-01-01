using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Netcode.Player;

namespace Kavkazim.Netcode.Reporting
{
    /// <summary>
    /// Networked dead body entity that appears when a player is killed.
    /// Implements IReportable for the report system.
    /// Server spawns this prefab; all clients see it.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class DeadBody : NetworkBehaviour, IReportable
    {
        [Header("Visual Settings")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color bodyColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        // Networked properties
        private NetworkVariable<ulong> _victimPlayerId = new NetworkVariable<ulong>();
        private NetworkVariable<FixedString32Bytes> _victimName = new NetworkVariable<FixedString32Bytes>();
        private NetworkVariable<float> _timeOfDeath = new NetworkVariable<float>();
        private NetworkVariable<bool> _hasBeenReported = new NetworkVariable<bool>(false);
        
        // Report range (set from config)
        private static float _reportRange = 2.5f;

        // Cached sprite to avoid excessive allocations
        private static Sprite _cachedCircleSprite;
        
        // IReportable implementation
        public ulong VictimPlayerId => _victimPlayerId.Value;
        public string VictimName => _victimName.Value.ToString();
        public Vector3 Position => transform.position;
        public bool IsReportable => !_hasBeenReported.Value;
        
        /// <summary>
        /// Time.time when this body was created.
        /// </summary>
        public float TimeOfDeath => _timeOfDeath.Value;
        
        /// <summary>
        /// Whether this body has already been reported.
        /// </summary>
        public bool HasBeenReported => _hasBeenReported.Value;

        private void Awake()
        {
            // Try to get existing SpriteRenderer
            if (!spriteRenderer)
                spriteRenderer = GetComponent<SpriteRenderer>();
            
            // Add SpriteRenderer if missing
            if (!spriteRenderer)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                Debug.Log("[DeadBody] Added SpriteRenderer component.");
            }
                
            // Create sprite if none assigned
            if (spriteRenderer.sprite == null)
            {
                if (_cachedCircleSprite == null)
                {
                    _cachedCircleSprite = CreateCircleSprite(64);
                    Debug.Log("[DeadBody] Created and cached circle sprite.");
                }
                spriteRenderer.sprite = _cachedCircleSprite;
            }
            
            // Set the color
            spriteRenderer.color = bodyColor;
            
            // Make sure it's visible (sorting order)
            spriteRenderer.sortingOrder = 5; // Above floor, visible
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // Apply visual appearance
            if (spriteRenderer)
            {
                spriteRenderer.color = bodyColor;
            }
            
            Debug.Log($"[DeadBody] Spawned body for {VictimName} (ID: {VictimPlayerId}) at {Position}");
        }

        /// <summary>
        /// SERVER ONLY: Initialize this dead body with victim information.
        /// Called by DeadBodySpawner after instantiation.
        /// </summary>
        public void Initialize(ulong victimPlayerId, string victimName, Vector3 position)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[DeadBody] Initialize called on client - ignored.");
                return;
            }
            
            _victimPlayerId.Value = victimPlayerId;
            _victimName.Value = new FixedString32Bytes(victimName);
            _timeOfDeath.Value = Time.time;
            transform.position = position;
            
            Debug.Log($"[DeadBody] SERVER: Initialized body for {victimName} at {position}");
        }

        /// <summary>
        /// Set the report range for all bodies.
        /// </summary>
        public static void SetReportRange(float range)
        {
            _reportRange = range;
        }

        /// <summary>
        /// ServerRpc to request reporting this body.
        /// Called by client via ReportService.
        /// Allow any client to report (Everyone permission).
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestReportServerRpc(RpcParams rpcParams = default)
        {
            ulong reporterClientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[DeadBody] SERVER: Received report request from ClientID: {reporterClientId} for body of {VictimName}");
            
            // Validate body is still reportable
            if (_hasBeenReported.Value)
            {
                Debug.LogWarning("[DeadBody] SERVER: Report rejected - body already reported.");
                return;
            }
            
            // Find reporter to validate distance and get name
            PlayerState reporter = FindPlayerByClientId(reporterClientId);
            if (reporter == null)
            {
                Debug.LogWarning("[DeadBody] SERVER: Report rejected - reporter not found.");
                return;
            }
            
            // Check if reporter is alive
            if (!reporter.IsAlive.Value)
            {
                Debug.LogWarning("[DeadBody] SERVER: Report rejected - reporter is dead.");
                return;
            }
            
            // Validate distance
            float distance = Vector3.Distance(reporter.transform.position, Position);
            if (distance > _reportRange)
            {
                Debug.LogWarning($"[DeadBody] SERVER: Report rejected - out of range ({distance:F2} > {_reportRange}).");
                return;
            }

            // Resolve reporter name reliably on server
            string reporterName = $"Player {reporterClientId}";
            var avatar = reporter.GetComponent<PlayerAvatar>();
            if (avatar != null && !string.IsNullOrEmpty(avatar.PlayerName.Value.ToString()))
            {
                reporterName = avatar.PlayerName.Value.ToString();
            }
            
            // Mark body as reported
            _hasBeenReported.Value = true;
            
            // Notify all clients
            AnnounceReportClientRpc(reporterName, reporterClientId, VictimName);
            
            // Notify the static service
            ReportService.NotifyBodyReported(reporterName, VictimName);
            
            Debug.Log($"[DeadBody] SERVER: Report validated successfully.");
        }

        /// <summary>
        /// Client RPC to announce the report.
        /// Also syncs the "has reported" state to all clients.
        /// </summary>
        [ClientRpc]
        private void AnnounceReportClientRpc(string reporterName, ulong reporterClientId, string victimName)
        {
            Debug.Log($"REPORT, Found Body by \"{reporterName}\"");
            
            // Mark this player as having reported on all clients
            ReportService.MarkPlayerAsReported(reporterClientId);
        }

        /// <summary>
        /// Find a player by their client ID.
        /// </summary>
        private PlayerState FindPlayerByClientId(ulong clientId)
        {
            PlayerState[] allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.OwnerClientId == clientId)
                    return player;
            }
            return null;
        }

        /// <summary>
        /// SERVER ONLY: Mark this body as reported.
        /// </summary>
        public void MarkAsReported()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[DeadBody] MarkAsReported called on client - ignored.");
                return;
            }
            
            _hasBeenReported.Value = true;
        }

        /// <summary>
        /// Creates a circle sprite texture programmatically.
        /// </summary>
        public static Sprite CreateCircleSprite(int size = 64)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float center = size / 2f;
            float radius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance);
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
#endif
    }
}
