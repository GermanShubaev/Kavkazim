using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Services.Authentication;
using System.Collections;
using System.Collections.Generic;
using Netcode.Player;
using UI;
using Unity.Services.Lobbies.Models;

namespace Kavkazim.Netcode
{
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerAvatar : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        [Header("Camera Follow")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -10);
        [SerializeField] private float cameraSmoothSpeed = 5f;
        private Camera _mainCamera;
        
        public NetworkVariable<Unity.Collections.FixedString32Bytes> PlayerName = new();

        public NetworkVariable<PlayerRoleType> Role = new(
                PlayerRoleType.Innocent,
                NetworkVariableReadPermission.Owner
            );

        private Dictionary<ulong, PlayerRoleType> _perceivedRoles = new Dictionary<ulong, PlayerRoleType>();
        
        public PlayerRoleType PerceivedRole { get; private set; } = PlayerRoleType.Innocent;

        private TextMeshPro _nameLabel;
        public PlayerRole CurrentRole { get; private set; }

        private PlayerState _playerState;

        public override void OnNetworkSpawn()
        {
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _playerState = GetComponent<PlayerState>();
            
            SetupNameLabel();
            UpdateVisuals(PerceivedRole);


            if (IsOwner)
            {
                if (GameplayUI.Instance == null)
                {
                    GameObject uiGo = new GameObject("GameplayUIManager");
                    uiGo.AddComponent<GameplayUI>();
                }

                if (string.IsNullOrEmpty(PlayerName.Value.ToString()))
                {
                    string prefsKey = "PlayerName" + GetParrelSyncSuffix();
                    string pName = PlayerPrefs.GetString(prefsKey, "");
                    
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
                    
                    SetPlayerNameServerRpc(pName);
                }

                TryFindCamera();
            }

            UpdateNameLabel(PlayerName.Value);

            PlayerName.OnValueChanged += (oldVal, newVal) => UpdateNameLabel(newVal);
        }

        [Rpc(SendTo.Server)]
        private void SetPlayerNameServerRpc(string name)
        {
            PlayerName.Value = name;
        }

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
        }

        public PlayerRoleType GetTrueRole()
        {
            return Role.Value;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        public void ReceivePerceivedRoleClientRpc(ulong targetNetworkObjectId, PlayerRoleType perceivedRole, RpcParams rpcParams = default)
        {
            _perceivedRoles[targetNetworkObjectId] = perceivedRole;
            
            if (targetNetworkObjectId == NetworkObjectId)
            {
                PerceivedRole = perceivedRole;
                UpdateVisuals(perceivedRole);
            }
            else
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    targetNetworkObjectId, out NetworkObject targetNetObj))
                {
                    PlayerAvatar targetAvatar = targetNetObj.GetComponent<PlayerAvatar>();
                    if (targetAvatar != null)
                    {
                        targetAvatar.ApplyPerceivedRole(perceivedRole);
                    }
                }
            }
        }

        public void ApplyPerceivedRole(PlayerRoleType perceivedRole)
        {
            PerceivedRole = perceivedRole;
            UpdateVisuals(perceivedRole);
        }

        public void SetBodyColor(Color c)
        {
            if (_playerState != null && !_playerState.IsAlive.Value) return;

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

        private IEnumerator SlashRoutine()
        {
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

        private void SetupNameLabel()
        {
            GameObject textObj = new GameObject("NameLabel");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = new Vector3(0, 1.4f, 0);
            
            _nameLabel = textObj.AddComponent<TextMeshPro>();
            _nameLabel.alignment = TextAlignmentOptions.Center;
            _nameLabel.fontSize = 4;
            _nameLabel.color = Color.white;
            _nameLabel.sortingOrder = 10;
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
        
        private static string GetParrelSyncSuffix()
        {
#if UNITY_EDITOR
            try
            {
                var clonesManagerType = System.Type.GetType("ParrelSync.ClonesManager, ParrelSync");
                if (clonesManagerType != null)
                {
                    var isCloneMethod = clonesManagerType.GetMethod("IsClone", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (isCloneMethod != null && (bool)isCloneMethod.Invoke(null, null))
                    {
                        var getArgMethod = clonesManagerType.GetMethod("GetArgument", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        string arg = getArgMethod?.Invoke(null, null) as string ?? "";
                        return string.IsNullOrEmpty(arg) ? "_clone" : $"_clone{arg}";
                    }
                }
            }
            catch
            {
                // ParrelSync - not needed
            }
#endif
            return "";
        }
    }
}