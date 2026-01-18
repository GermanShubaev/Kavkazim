using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Netcode.Player;

namespace Kavkazim.Netcode.Reporting
{
    [RequireComponent(typeof(NetworkObject))]
    public class DeadBody : NetworkBehaviour, IReportable
    {
        public static readonly System.Collections.Generic.List<DeadBody> ActiveBodies = new System.Collections.Generic.List<DeadBody>();

        [Header("Visual Settings")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color bodyColor = new(0.2f, 0.2f, 0.2f, 1f);
        
        private readonly NetworkVariable<ulong> _victimPlayerId = new();
        private readonly NetworkVariable<FixedString32Bytes> _victimName = new();
        private readonly NetworkVariable<float> _timeOfDeath = new();
        private readonly NetworkVariable<bool> _hasBeenReported = new();
        
        private static float _reportRange = 2.5f;

        private static Sprite _cachedCircleSprite;
        
        public ulong VictimPlayerId => _victimPlayerId.Value;
        public string VictimName => _victimName.Value.ToString();
        public Vector3 Position => transform.position;
        public bool IsReportable => !_hasBeenReported.Value;

        private void Awake()
        {
            if (!spriteRenderer)
                spriteRenderer = GetComponent<SpriteRenderer>();
            
            if (!spriteRenderer)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                Debug.Log("[DeadBody] Added SpriteRenderer component.");
            }
                
            if (spriteRenderer.sprite == null)
            {
                if (_cachedCircleSprite == null)
                {
                    _cachedCircleSprite = CreateCircleSprite(64);
                    Debug.Log("[DeadBody] Created and cached circle sprite.");
                }
                spriteRenderer.sprite = _cachedCircleSprite;
            }
            
            spriteRenderer.color = bodyColor;
            
            spriteRenderer.sortingOrder = 5;
        }

        public override void OnNetworkSpawn()
        {
            if (!ActiveBodies.Contains(this))
                ActiveBodies.Add(this);
                
            base.OnNetworkSpawn();
            
            if (spriteRenderer)
            {
                spriteRenderer.color = bodyColor;
            }
            
            Debug.Log($"[DeadBody] Spawned body for {VictimName} (ID: {VictimPlayerId}) at {Position}");
        }

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

        public static void SetReportRange(float range)
        {
            _reportRange = range;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestReportServerRpc(RpcParams rpcParams = default)
        {
            ulong reporterClientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[DeadBody] SERVER: Received report request from ClientID: {reporterClientId} for body of {VictimName}");
            
            if (_hasBeenReported.Value)
            {
                Debug.LogWarning("[DeadBody] SERVER: Report rejected - body already reported.");
                return;
            }
            
            PlayerState reporter = FindPlayerByClientId(reporterClientId);
            if (reporter == null)
            {
                Debug.LogWarning("[DeadBody] SERVER: Report rejected - reporter not found.");
                return;
            }
            
            if (!reporter.IsAlive.Value)
            {
                Debug.LogWarning("[DeadBody] SERVER: Report rejected - reporter is dead.");
                return;
            }
            
            float distance = Vector3.Distance(reporter.transform.position, Position);
            if (distance > _reportRange)
            {
                Debug.LogWarning($"[DeadBody] SERVER: Report rejected - out of range ({distance:F2} > {_reportRange}).");
                return;
            }

            string reporterName = $"Player {reporterClientId}";
            var avatar = reporter.GetComponent<PlayerAvatar>();
            if (avatar != null && !string.IsNullOrEmpty(avatar.PlayerName.Value.ToString()))
            {
                reporterName = avatar.PlayerName.Value.ToString();
            }
            
            _hasBeenReported.Value = true;
            
            AnnounceReportClientRpc(reporterName, reporterClientId, VictimName);
            
            ReportService.NotifyBodyReported(reporterName, VictimName, reporterClientId, VictimPlayerId);
            
            Debug.Log($"[DeadBody] SERVER: Report validated successfully.");
        }

        [ClientRpc]
        private void AnnounceReportClientRpc(string reporterName, ulong reporterClientId, string victimName)
        {
            Debug.Log($"REPORT, Found Body by \"{reporterName}\"");
        }

        private PlayerState FindPlayerByClientId(ulong clientId)
        {
            var allPlayers = PlayerState.ActivePlayers;
            foreach (var player in allPlayers)
            {
                if (player.OwnerClientId == clientId)
                    return player;
            }
            return null;
        }

        public void MarkAsReported()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[DeadBody] MarkAsReported called on client - ignored.");
                return;
            }
            
            _hasBeenReported.Value = true;
        }

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

        public override void OnNetworkDespawn()
        {
            if (ActiveBodies.Contains(this))
                ActiveBodies.Remove(this);
            base.OnNetworkDespawn();
        }
    }
}
