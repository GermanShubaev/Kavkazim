using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Kavkazim.Netcode;
using Minigames.Base;
using Minigames.Progress;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minigames
{
    public class MinigameTriggerPoint : MonoBehaviour
    {
        [Header("Trigger Settings")]
        [SerializeField] private Vector2 position = Vector2.zero;
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private MinigameType gameType = MinigameType.LezginkaSort;

        [Header("Debug")]
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor = Color.yellow;

        [Header("Visual Indicator")]
        [SerializeField] private bool showIndicator = true;
        [SerializeField] private float indicatorHeight = 0.05f;
        [SerializeField] private Vector2 indicatorSize = new Vector2(2, 2);

        public Vector2 Position => position;
        public float Radius => radius;
        public MinigameType GameType => gameType;

        private GameObject _indicatorCanvas;
        private Image _indicatorImage;
        private Camera _mainCamera;
        private Sprite _exclamationSprite;
        private PlayerAvatar _localPlayerAvatar;

        private void Awake()
        {
            MinigameManager manager = FindFirstObjectByType<MinigameManager>();
            if (manager != null)
            {
                manager.RegisterTriggerPoint(this);
            }

            LoadExclamationSprite();

            if (showIndicator)
            {
                CreateVisualIndicator();
            }

            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                _mainCamera = FindFirstObjectByType<Camera>();
            }
        }

        private void LoadExclamationSprite()
        {
            #if UNITY_EDITOR
            string exclamationPath = "Assets/Art/Images/icons/exclamation_mark.png";
            _exclamationSprite = AssetDatabase.LoadAssetAtPath<Sprite>(exclamationPath);
            if (_exclamationSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(exclamationPath);
                if (tex != null)
                {
                    _exclamationSprite = Sprite.Create(tex, new Rect(0, 0, 70, 70), new Vector2(0.2f, 0.2f));
                }
            }

            if (_exclamationSprite != null)
                Debug.Log("[MinigameTriggerPoint] Loaded exclamation_mark.png (Editor mode)");
            #endif

            if (_exclamationSprite == null)
            {
                _exclamationSprite = Resources.Load<Sprite>("Art/Images/icons/exclamation_mark");
                if (_exclamationSprite == null)
                    _exclamationSprite = Resources.Load<Sprite>("icons/exclamation_mark");
            }

            if (_exclamationSprite == null)
            {
                Debug.LogError("[MinigameTriggerPoint] Failed to load exclamation_mark.png. Make sure the image is either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/icons/");
                Debug.LogError("  2. Or in Assets/Art/Images/icons/ (editor only)");
            }
        }

        private void OnDestroy()
        {
            MinigameManager manager = FindFirstObjectByType<MinigameManager>();
            if (manager != null)
            {
                manager.UnregisterTriggerPoint(this);
            }

            if (_indicatorCanvas != null)
            {
                Destroy(_indicatorCanvas);
            }
        }

        public bool IsWithinRange(Vector2 playerPosition)
        {
            float distance = Vector2.Distance(playerPosition, position);
            return distance <= radius;
        }

        public float GetDistance(Vector2 playerPosition)
        {
            return Vector2.Distance(playerPosition, position);
        }

        private void CreateVisualIndicator()
        {
            if (_exclamationSprite == null)
            {
                Debug.LogWarning("[MinigameTriggerPoint] Cannot create visual indicator - exclamation sprite not loaded!");
                return;
            }

            _indicatorCanvas = new GameObject("TriggerIndicator");
            _indicatorCanvas.transform.SetParent(transform);
            
            Vector3 triggerWorldPos = new Vector3(position.x, position.y + indicatorHeight, 0);
            _indicatorCanvas.transform.position = triggerWorldPos;

            Canvas canvas = _indicatorCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100; 

            RectTransform canvasRect = _indicatorCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(indicatorSize.x, indicatorSize.y);
            canvasRect.localScale = Vector3.one * 0.2f;

            CanvasScaler scaler = _indicatorCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            if (_mainCamera != null)
            {
                canvas.worldCamera = _mainCamera;
            }
            else
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    _mainCamera = FindFirstObjectByType<Camera>();
                }
                if (_mainCamera != null)
                {
                    canvas.worldCamera = _mainCamera;
                }
            }

            GameObject imageObj = new GameObject("IndicatorImage");
            imageObj.transform.SetParent(_indicatorCanvas.transform, false);

            _indicatorImage = imageObj.AddComponent<Image>();
            _indicatorImage.sprite = _exclamationSprite;
            _indicatorImage.preserveAspect = true;
            _indicatorImage.color = Color.yellow; 
            _indicatorImage.raycastTarget = false; 
            RectTransform imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.sizeDelta = indicatorSize;
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.localPosition = Vector3.zero;
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);

            _indicatorCanvas.SetActive(false);
            
            Debug.Log($"[MinigameTriggerPoint] Created visual indicator at position ({triggerWorldPos.x}, {triggerWorldPos.y}, {triggerWorldPos.z})");
        }

        private void Update()
        {
            if (_indicatorCanvas != null && showIndicator)
            {
                bool shouldShow = ShouldShowIndicator();
                _indicatorCanvas.SetActive(shouldShow);
                
                if (shouldShow)
                {
                    Vector3 triggerWorldPos = new Vector3(position.x, position.y + indicatorHeight, 0);
                    _indicatorCanvas.transform.position = triggerWorldPos;

                    if (_mainCamera != null)
                    {
                        Vector3 directionToCamera = _mainCamera.transform.position - _indicatorCanvas.transform.position;
                        directionToCamera.y = 0;
                        if (directionToCamera != Vector3.zero)
                        {
                            _indicatorCanvas.transform.rotation = Quaternion.LookRotation(-directionToCamera);
                        }
                    }
                    else
                    {
                        _mainCamera = Camera.main;
                        if (_mainCamera == null)
                        {
                            _mainCamera = FindFirstObjectByType<Camera>();
                        }
                        if (_mainCamera != null && _indicatorCanvas != null)
                        {
                            Canvas canvas = _indicatorCanvas.GetComponent<Canvas>();
                            if (canvas != null)
                            {
                                canvas.worldCamera = _mainCamera;
                            }
                        }
                    }
                }
            }
        }

        private bool ShouldShowIndicator()
        {
            if (_localPlayerAvatar == null)
            {
                PlayerAvatar[] avatars = FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
                foreach (var avatar in avatars)
                {
                    if (avatar.IsOwner)
                    {
                        _localPlayerAvatar = avatar;
                        break;
                    }
                }
            }

            if (_localPlayerAvatar == null)
            {
                return false;
            }

            if (_localPlayerAvatar.PerceivedRole != PlayerRoleType.Innocent)
            {
                return false;
            }

            if (UI.GameplayUI.Instance == null)
            {
                return false;
            }

            ulong localClientId = _localPlayerAvatar.OwnerClientId;
            var taskAssignments = UI.GameplayUI.Instance.GetTaskAssignments();
            
            if (taskAssignments == null || !taskAssignments.ContainsKey(localClientId))
            {
                return false;
            }

            var playerTasks = taskAssignments[localClientId];
            foreach (var task in playerTasks)
            {
                float positionTolerance = 0.1f;
                if (Vector2.Distance(task.Location, position) < positionTolerance && 
                    task.MinigameType == gameType)
                {
                    if (UI.GameplayUI.Instance.IsTaskCompleted(task))
                    {
                        return false;
                    }
                    return true;
                }
            }

            return false;
        }

        public bool IsAssignedToLocalPlayer()
        {
            return ShouldShowIndicator();
        }

        private void OnDrawGizmos()
        {
            if (!showGizmo) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(new Vector3(position.x, position.y, 0), radius);
            
            Gizmos.color = Color.red;
            float crossSize = 0.1f;
            Gizmos.DrawLine(
                new Vector3(position.x - crossSize, position.y, 0),
                new Vector3(position.x + crossSize, position.y, 0)
            );
            Gizmos.DrawLine(
                new Vector3(position.x, position.y - crossSize, 0),
                new Vector3(position.x, position.y + crossSize, 0)
            );
        }
    }
}

