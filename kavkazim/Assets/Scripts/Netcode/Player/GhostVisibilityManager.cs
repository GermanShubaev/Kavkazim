using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace Netcode.Player
{
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
            
            if (renderersToHide == null || renderersToHide.Length == 0)
            {
                renderersToHide = GetComponentsInChildren<Renderer>();
            }
        }

        
        private void LateInit()
        {
            if (_initComplete) return;
            
            Transform nameLabel = transform.Find("NameLabel");
            if (nameLabel != null)
            {
                var list = new List<GameObject>(objectsToHide ?? new GameObject[0]);
                if (!list.Contains(nameLabel.gameObject))
                {
                    list.Add(nameLabel.gameObject);
                    objectsToHide = list.ToArray();
                }
            }
            
            renderersToHide = GetComponentsInChildren<Renderer>();
            
            _initComplete = true;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            _allPlayers.Add(this);
            
            if (IsOwner)
            {
                _localPlayerVisibility = this;
                Debug.Log($"[GhostVisibility] Local player registered: {OwnerClientId}");
            }
            
            _playerState.IsAlive.OnValueChanged += OnThisPlayerStateChanged;
            
            UpdateVisibility();
            
            Debug.Log($"[GhostVisibility] Player {OwnerClientId} spawned. Total players: {_allPlayers.Count}");
        }

        public override void OnNetworkDespawn()
        {
            _playerState.IsAlive.OnValueChanged -= OnThisPlayerStateChanged;
            
            _allPlayers.Remove(this);
            
            if (IsOwner)
            {
                _localPlayerVisibility = null;
            }
            
            base.OnNetworkDespawn();
        }

        
        private void OnThisPlayerStateChanged(bool previousValue, bool newValue)
        {
            Debug.Log($"[GhostVisibility] Player {OwnerClientId} state changed: {previousValue} -> {newValue}");
            
            if (IsOwner)
            {
                Debug.Log($"[GhostVisibility] Local player died/revived. Updating all visibility.");
                UpdateAllPlayersVisibility();
            }
            
            UpdateVisibility();
        }

        public void UpdateVisibility()
        {
            LateInit();
            
            if (IsOwner)
            {
                SetVisible(true);
                return;
            }
            
            if (_localPlayerVisibility == null)
            {
                SetVisible(true);
                return;
            }
            
            bool localPlayerIsAlive = _localPlayerVisibility._playerState.IsAlive.Value;
            bool thisPlayerIsAlive = _playerState.IsAlive.Value;
            
            if (thisPlayerIsAlive)
            {
                SetVisible(true);
            }
            else
            {
                if (localPlayerIsAlive)
                {
                    SetVisible(false);
                    Debug.Log($"[GhostVisibility] Hiding ghost {OwnerClientId} from alive local player");
                }
                else
                {
                    SetVisible(true);
                    Debug.Log($"[GhostVisibility] Showing ghost {OwnerClientId} to ghost local player");
                }
            }
        }

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

        private void SetVisible(bool visible)
        {
            foreach (var renderer in renderersToHide)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
            
            foreach (var obj in objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(visible);
                }
            }
        }

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
    }
}
