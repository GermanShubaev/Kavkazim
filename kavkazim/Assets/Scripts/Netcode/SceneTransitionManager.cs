using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Manages screen transitions (fades) between scenes and game states.
    /// Persistent singleton that lives across scene loads.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        private const float NearlyOpaqueThreshold = 0.9f;
        private const float NearlyTransparentThreshold = 0.05f;
        private const float RaycastBlockThreshold = 0.1f;
        private const int TopSortingOrder = 9999;

        [Header("Settings")]
        [SerializeField] private float defaultFadeDuration = 1.0f;
        [SerializeField] private Color fadeColor = Color.black;

        private Canvas _transitionCanvas;
        private CanvasGroup _fadeOverlay;
        private Image _fadeImage;
        private Coroutine _activeFade;

        /// <summary>
        /// Set to true to prevent auto-fade-in on the next scene load.
        /// Used when fade-in should be triggered manually (e.g., after respawn).
        /// </summary>
        public bool SuppressNextAutoFadeIn { get; set; }

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
            
            CreateTransitionUI();
            
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void CreateTransitionUI()
        {
            // Create Canvas
            var canvasGo = new GameObject("TransitionCanvas");
            canvasGo.transform.SetParent(transform);
            
            _transitionCanvas = canvasGo.AddComponent<Canvas>();
            _transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _transitionCanvas.sortingOrder = TopSortingOrder;
            
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Create Fade Image (stretched to fill screen)
            var imageGo = new GameObject("FadeImage");
            imageGo.transform.SetParent(canvasGo.transform, false);

            var rect = imageGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            _fadeImage = imageGo.AddComponent<Image>();
            _fadeImage.color = fadeColor;
            _fadeImage.raycastTarget = false;

            _fadeOverlay = imageGo.AddComponent<CanvasGroup>();
            _fadeOverlay.alpha = 0f;
            _fadeOverlay.blocksRaycasts = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (SuppressNextAutoFadeIn)
            {
                SuppressNextAutoFadeIn = false;
                Debug.Log("[SceneTransitionManager] Auto-fade-in suppressed.");
                return;
            }

            // Auto fade-in if screen is currently faded out
            if (_fadeOverlay != null && _fadeOverlay.alpha > NearlyOpaqueThreshold)
            {
                FadeIn(defaultFadeDuration);
            }
        }

        /// <summary>
        /// Fades the screen to black.
        /// </summary>
        public Coroutine FadeOut(float duration, Action onComplete = null)
        {
            StopActiveFade();
            _activeFade = StartCoroutine(FadeRoutine(0f, 1f, duration, onComplete));
            return _activeFade;
        }

        /// <summary>
        /// Fades the screen from black to transparent.
        /// </summary>
        public Coroutine FadeIn(float duration, Action onComplete = null)
        {
            StopActiveFade();
            _activeFade = StartCoroutine(FadeRoutine(1f, 0f, duration, onComplete));
            return _activeFade;
        }

        private void StopActiveFade()
        {
            if (_activeFade != null)
            {
                StopCoroutine(_activeFade);
                _activeFade = null;
            }
        }

        private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration, Action onComplete)
        {
            if (_fadeOverlay == null)
            {
                Debug.LogWarning("[SceneTransitionManager] FadeOverlay is missing!");
                onComplete?.Invoke();
                _activeFade = null;
                yield break;
            }

            bool fadingToOpaque = targetAlpha > RaycastBlockThreshold;
            SetRaycastBlocking(fadingToOpaque);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _fadeOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            _fadeOverlay.alpha = targetAlpha;

            // Unblock raycasts when fully transparent
            if (targetAlpha <= NearlyTransparentThreshold)
            {
                SetRaycastBlocking(false);
            }

            _activeFade = null;
            onComplete?.Invoke();
        }

        private void SetRaycastBlocking(bool blocking)
        {
            if (_fadeOverlay != null) _fadeOverlay.blocksRaycasts = blocking;
            if (_fadeImage != null) _fadeImage.raycastTarget = blocking;
        }
    }
}
