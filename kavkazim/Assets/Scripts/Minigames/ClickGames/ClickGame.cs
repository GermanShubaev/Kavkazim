using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Minigames
{
    /// <summary>
    /// Abstract base class for click-to-remove games.
    /// Displays a main image with clickable "stains" that disappear when clicked.
    /// </summary>
    public abstract class ClickGame : BaseMinigame
    {
        [Header("Click Game Settings")]
        [SerializeField] protected Sprite mainImage;
        [SerializeField] protected Vector2 mainImageSize = new Vector2(800, 600);
        [SerializeField] protected List<StainData> stainDataList = new List<StainData>();
        [SerializeField] protected float stainFadeOutDuration = 0.3f;
        [SerializeField] protected bool randomizeStainPositions = false;
        
        [Header("Popup Size")]
        [SerializeField] protected bool useScreenPercentage = false;
        [SerializeField] [Range(0.1f, 1f)] protected float screenPercentage = 0.75f;

        protected GameObject _mainImageObject;
        protected Image _mainImageComponent;
        protected List<ClickableStain> _activeStains = new List<ClickableStain>();
        protected int _stainsRemaining;

        /// <summary>
        /// Data structure defining a stain's appearance and position.
        /// </summary>
        [System.Serializable]
        public class StainData
        {
            [Tooltip("Optional sprite for the stain. If null, a default circular shape is used.")]
            public Sprite stainSprite;
            
            [Tooltip("Position relative to the main image center (normalized -0.5 to 0.5).")]
            public Vector2 normalizedPosition;
            
            [Tooltip("Size of the stain in pixels.")]
            public Vector2 size = new Vector2(60, 60);
            
            [Tooltip("Color/tint of the stain. Used if no sprite is provided.")]
            public Color stainColor = new Color(0.3f, 0.2f, 0.1f, 0.8f);
            
            [Tooltip("Optional rotation in degrees.")]
            public float rotation = 0f;
        }

        protected override void InitializeGameUI()
        {
            // Resize content panel if using screen percentage
            if (useScreenPercentage)
            {
                ResizeContentPanelToScreenPercentage();
            }
            
            CreateMainImage();
            CreateStains();
            _stainsRemaining = _activeStains.Count;
        }

        /// <summary>
        /// Resizes the content panel to a percentage of the screen size.
        /// </summary>
        protected virtual void ResizeContentPanelToScreenPercentage()
        {
            if (_contentPanel == null) return;

            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            if (contentRect == null) return;

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            
            float targetWidth = screenWidth * screenPercentage;
            float targetHeight = screenHeight * screenPercentage;
            
            contentRect.sizeDelta = new Vector2(targetWidth, targetHeight);
            
            // Also update mainImageSize to match the content panel
            mainImageSize = new Vector2(targetWidth - 40, targetHeight - 40); // Small padding
        }

        /// <summary>
        /// Creates the main background image in the content panel.
        /// </summary>
        protected virtual void CreateMainImage()
        {
            _mainImageObject = new GameObject("MainImage");
            _mainImageObject.transform.SetParent(_contentPanel.transform, false);

            _mainImageComponent = _mainImageObject.AddComponent<Image>();
            _mainImageComponent.sprite = GetMainImage();
            _mainImageComponent.preserveAspect = true;
            _mainImageComponent.raycastTarget = false; // Stains handle clicks

            RectTransform rect = _mainImageObject.GetComponent<RectTransform>();
            rect.sizeDelta = mainImageSize;
            rect.anchoredPosition = Vector2.zero;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// Creates all stain objects based on stain data.
        /// </summary>
        protected virtual void CreateStains()
        {
            _activeStains.Clear();

            List<StainData> stains = GetStainData();
            
            if (stains == null || stains.Count == 0)
            {
                Debug.LogWarning($"{GetType().Name}: No stain data provided!");
                return;
            }

            List<Vector2> positions = randomizeStainPositions 
                ? GenerateRandomPositions(stains.Count) 
                : null;

            for (int i = 0; i < stains.Count; i++)
            {
                StainData data = stains[i];
                Vector2 position = randomizeStainPositions 
                    ? positions[i] 
                    : new Vector2(data.normalizedPosition.x * mainImageSize.x, 
                                  data.normalizedPosition.y * mainImageSize.y);

                ClickableStain stain = CreateStain(i, data, position);
                _activeStains.Add(stain);
            }
        }

        /// <summary>
        /// Creates a single clickable stain.
        /// </summary>
        protected virtual ClickableStain CreateStain(int index, StainData data, Vector2 position)
        {
            GameObject stainObj = new GameObject($"Stain_{index}");
            stainObj.transform.SetParent(_mainImageObject.transform, false);

            // Add Image component
            Image stainImage = stainObj.AddComponent<Image>();
            
            if (data.stainSprite != null)
            {
                stainImage.sprite = data.stainSprite;
                stainImage.color = Color.white; // Use sprite's own colors
            }
            else
            {
                // Create a default circular stain appearance
                stainImage.color = data.stainColor;
            }

            // Setup RectTransform
            RectTransform rect = stainObj.GetComponent<RectTransform>();
            rect.sizeDelta = data.size;
            rect.anchoredPosition = position;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.Euler(0, 0, data.rotation);

            // Add ClickableStain component
            ClickableStain stain = stainObj.AddComponent<ClickableStain>();
            stain.Initialize(index, this, data);

            return stain;
        }

        /// <summary>
        /// Generates random positions within the main image bounds.
        /// </summary>
        protected virtual List<Vector2> GenerateRandomPositions(int count)
        {
            List<Vector2> positions = new List<Vector2>();
            float margin = 0.1f; // 10% margin from edges
            float halfWidth = mainImageSize.x * (0.5f - margin);
            float halfHeight = mainImageSize.y * (0.5f - margin);
            float minDistance = Mathf.Min(mainImageSize.x, mainImageSize.y) * 0.15f;

            int maxAttempts = 100;
            for (int i = 0; i < count; i++)
            {
                Vector2 position = Vector2.zero;
                bool validPosition = false;
                int attempts = 0;

                while (!validPosition && attempts < maxAttempts)
                {
                    position = new Vector2(
                        Random.Range(-halfWidth, halfWidth),
                        Random.Range(-halfHeight, halfHeight)
                    );

                    validPosition = true;
                    foreach (Vector2 existingPos in positions)
                    {
                        if (Vector2.Distance(position, existingPos) < minDistance)
                        {
                            validPosition = false;
                            break;
                        }
                    }
                    attempts++;
                }

                positions.Add(position);
            }

            return positions;
        }

        /// <summary>
        /// Called when a stain is clicked. Handles removal and win condition check.
        /// </summary>
        public virtual void OnStainClicked(ClickableStain stain)
        {
            if (!_activeStains.Contains(stain))
                return;

            // Remove stain with optional animation
            StartCoroutine(RemoveStainCoroutine(stain));
        }

        /// <summary>
        /// Coroutine to animate stain removal.
        /// </summary>
        protected virtual System.Collections.IEnumerator RemoveStainCoroutine(ClickableStain stain)
        {
            Image stainImage = stain.GetComponent<Image>();
            
            if (stainFadeOutDuration > 0 && stainImage != null)
            {
                // Fade out animation
                float elapsed = 0f;
                Color startColor = stainImage.color;

                while (elapsed < stainFadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / stainFadeOutDuration;
                    stainImage.color = new Color(startColor.r, startColor.g, startColor.b, 
                                                  Mathf.Lerp(startColor.a, 0f, t));
                    stain.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.5f, t);
                    yield return null;
                }
            }

            // Remove from active list and destroy
            _activeStains.Remove(stain);
            _stainsRemaining--;
            
            if (stain != null && stain.gameObject != null)
            {
                Destroy(stain.gameObject);
            }

            OnStainRemoved(stain);

            // Check win condition
            if (_stainsRemaining <= 0)
            {
                OnAllStainsRemoved();
            }
        }

        /// <summary>
        /// Called after a stain is removed. Override for custom behavior.
        /// </summary>
        protected virtual void OnStainRemoved(ClickableStain stain)
        {
            // Override in derived classes for effects, sounds, etc.
        }

        /// <summary>
        /// Called when all stains have been removed. Override for custom completion behavior.
        /// </summary>
        protected virtual void OnAllStainsRemoved()
        {
            Debug.Log($"{GetType().Name}: All stains removed! Game complete.");
            OnGameComplete();
        }

        /// <summary>
        /// Called when the game is successfully completed.
        /// </summary>
        protected virtual void OnGameComplete()
        {
            // Default: close the game after a short delay
            StartCoroutine(CloseAfterDelay(1f));
        }

        protected virtual System.Collections.IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseGame();
        }

        /// <summary>
        /// Override to provide the main background image.
        /// </summary>
        protected virtual Sprite GetMainImage()
        {
            return mainImage;
        }

        /// <summary>
        /// Override to provide stain data. By default uses the serialized list.
        /// </summary>
        protected virtual List<StainData> GetStainData()
        {
            return stainDataList;
        }

        protected override void CleanupGameUI()
        {
            StopAllCoroutines();
            _activeStains.Clear();
            _mainImageObject = null;
            _mainImageComponent = null;
        }

        /// <summary>
        /// Returns the number of stains remaining.
        /// </summary>
        public int GetStainsRemaining() => _stainsRemaining;

        /// <summary>
        /// Returns the total number of stains at game start.
        /// </summary>
        public int GetTotalStains() => GetStainData()?.Count ?? 0;
    }

    /// <summary>
    /// Component for clickable stain objects.
    /// </summary>
    public class ClickableStain : MonoBehaviour, IPointerClickHandler
    {
        private int _index;
        private ClickGame _game;
        private ClickGame.StainData _data;
        private bool _isRemoved = false;

        public void Initialize(int index, ClickGame game, ClickGame.StainData data)
        {
            _index = index;
            _game = game;
            _data = data;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isRemoved || _game == null)
                return;

            _isRemoved = true;
            _game.OnStainClicked(this);
        }

        public int GetIndex() => _index;
        public ClickGame.StainData GetData() => _data;
    }
}

