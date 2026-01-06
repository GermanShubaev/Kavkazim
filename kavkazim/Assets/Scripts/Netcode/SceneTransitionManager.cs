using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Manages screen transitions (fades) between scenes and game states.
    /// This is a persistent singleton that lives across scene loads.
    /// 
    /// Transition Flow:
    /// - GameSessionManager triggers TriggerFadeOutClientRpc before state changes
    /// - After delay, scene loads or state changes
    /// - OnSceneLoaded auto-triggers FadeIn (for scene changes)
    /// - TriggerFadeInClientRpc is called explicitly for non-scene transitions (e.g., StartGame)
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        // ========== CONSTANTS ==========
        private const float NEARLY_OPAQUE_THRESHOLD = 0.9f;
        private const float NEARLY_TRANSPARENT_THRESHOLD = 0.05f;
        private const float RAYCAST_BLOCK_THRESHOLD = 0.1f;

        [Header("UI References")]
        [SerializeField] private Canvas transitionCanvas;
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private Image fadeImage;

        [Header("Settings")]
        [SerializeField] private float defaultFadeDuration = 1.0f;
        [SerializeField] private Color fadeColor = Color.black;

        // Track active fade to prevent overlapping
        private Coroutine _activeFade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("SceneTransitionManager");
                go.AddComponent<SceneTransitionManager>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-generate UI if missing
            if (transitionCanvas == null)
            {
                CreateTransitionUI();
            }

            // Ensure UI is setup
            if (fadeImage != null)
            {
                fadeImage.color = fadeColor;
                fadeImage.raycastTarget = false; // Allow clicks until we fade out
            }
            
            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0f; // Start transparent
                fadeOverlay.blocksRaycasts = false;
            }
            
            if (transitionCanvas != null)
            {
                transitionCanvas.sortingOrder = 9999; // Ensure it's on top of everything
            }

            // Defensive: unsubscribe first to prevent double-subscription
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void CreateTransitionUI()
        {
            // Create Canvas
            var canvasGO = new GameObject("TransitionCanvas");
            canvasGO.transform.SetParent(transform);
            transitionCanvas = canvasGO.AddComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 9999;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // Create Fade Image
            var imageGO = new GameObject("FadeImage");
            imageGO.transform.SetParent(canvasGO.transform, false);
            
            // Stretch to fill
            var rect = imageGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            
            fadeImage = imageGO.AddComponent<Image>();
            fadeImage.color = fadeColor;
            fadeImage.raycastTarget = false;
            
            fadeOverlay = imageGO.AddComponent<CanvasGroup>();
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        /// <summary>
        /// Set to true to prevent auto-fade-in on the next scene load.
        /// Used when fade-in should be triggered manually (e.g., after respawn).
        /// </summary>
        public bool SuppressNextAutoFadeIn { get; set; }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Check if auto-fade-in is suppressed
            if (SuppressNextAutoFadeIn)
            {
                SuppressNextAutoFadeIn = false;
                Debug.Log("[SceneTransitionManager] Auto-fade-in suppressed for this scene load.");
                return;
            }
            
            // Automatically fade in when a new scene loads
            // Check if we are nearly opaque to avoid fading in if we weren't faded out
            if (fadeOverlay != null && fadeOverlay.alpha > NEARLY_OPAQUE_THRESHOLD)
            {
                FadeIn(defaultFadeDuration);
            }
        }

        /// <summary>
        /// Fades the screen to black.
        /// Cancels any existing fade in progress.
        /// </summary>
        public Coroutine FadeOut(float duration, Action onComplete = null)
        {
            if (_activeFade != null)
            {
                StopCoroutine(_activeFade);
            }
            _activeFade = StartCoroutine(FadeRoutine(0f, 1f, duration, onComplete));
            return _activeFade;
        }

        /// <summary>
        /// Fades the screen from black to transparent.
        /// Cancels any existing fade in progress.
        /// </summary>
        public Coroutine FadeIn(float duration, Action onComplete = null)
        {
            if (_activeFade != null)
            {
                StopCoroutine(_activeFade);
            }
            _activeFade = StartCoroutine(FadeRoutine(1f, 0f, duration, onComplete));
            return _activeFade;
        }

        private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration, Action onComplete)
        {
            if (fadeOverlay == null)
            {
                Debug.LogWarning("[SceneTransitionManager] FadeOverlay CanvasGroup is missing!");
                onComplete?.Invoke();
                _activeFade = null;
                yield break;
            }

            // Block raycasts if we are fading to opaque
            fadeOverlay.blocksRaycasts = targetAlpha > RAYCAST_BLOCK_THRESHOLD;
            if (fadeImage != null) fadeImage.raycastTarget = targetAlpha > RAYCAST_BLOCK_THRESHOLD;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fadeOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            fadeOverlay.alpha = targetAlpha;
            
            // Unblock raycasts if we are fully transparent
            if (targetAlpha <= NEARLY_TRANSPARENT_THRESHOLD)
            {
                fadeOverlay.blocksRaycasts = false;
                if (fadeImage != null) fadeImage.raycastTarget = false;
            }

            _activeFade = null;
            onComplete?.Invoke();
        }
    }
}
