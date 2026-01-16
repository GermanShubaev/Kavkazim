using System.Collections.Generic;
using Minigames.Base;
using Minigames.Base.Strategies;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigames.ClickGames
{
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

        private GameObject _mainImageObject;
        private Image _mainImageComponent;
        private readonly List<ClickableStain> _activeStains = new List<ClickableStain>();
        private int _stainsRemaining;

        [System.Serializable]
        public class StainData
        {
            public Sprite stainSprite;
            public Vector2 normalizedPosition;
            public Vector2 size = new Vector2(60, 60);
            public Color stainColor = new Color(0.3f, 0.2f, 0.1f, 0.8f);
            public float rotation = 0f;
        }

        protected override void InitializeGameUI()
        {
            if (_winConditionStrategy == null)
            {
                _winConditionStrategy = new ClickGameWinConditionStrategy();
            }
            
            if (_uiBuilder == null)
            {
                _uiBuilder = new Base.UI.ClickGameUIBuilder();
            }
            
            if (useScreenPercentage)
            {
                ResizeContentPanelToScreenPercentage();
            }
            
            CreateMainImage();
            CreateStains();
            _stainsRemaining = _activeStains.Count;
        }

        protected virtual void ResizeContentPanelToScreenPercentage()
        {
            if (_contentPanel == null) return;

            RectTransform contentRect = _contentPanel.GetComponent<RectTransform>();
            if (contentRect == null) return;

            const float referenceWidth = 2560f;
            const float referenceHeight = 1440f;
            
            var targetWidth = referenceWidth * screenPercentage;
            var targetHeight = referenceHeight * screenPercentage;
            
            contentRect.sizeDelta = new Vector2(targetWidth, targetHeight);
            
            const float padding = 40f;
            mainImageSize = new Vector2(targetWidth - padding, targetHeight - padding);
        }

        protected virtual void CreateMainImage()
        {
            _mainImageObject = new GameObject("MainImage");
            _mainImageObject.transform.SetParent(_contentPanel.transform, false);

            _mainImageComponent = _mainImageObject.AddComponent<Image>();
            _mainImageComponent.sprite = GetMainImage();
            _mainImageComponent.preserveAspect = true;
            _mainImageComponent.raycastTarget = false;

            RectTransform rect = _mainImageObject.GetComponent<RectTransform>();
            rect.sizeDelta = mainImageSize;
            rect.anchoredPosition = Vector2.zero;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
        }

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
                var data = stains[i];
                Vector2 position = randomizeStainPositions 
                    ? positions[i] 
                    : new Vector2(data.normalizedPosition.x * mainImageSize.x, 
                                  data.normalizedPosition.y * mainImageSize.y);

                var stain = CreateStain(i, data, position);
                _activeStains.Add(stain);
            }
        }

        protected virtual ClickableStain CreateStain(int index, StainData data, Vector2 position)
        {
            GameObject stainObj = new GameObject($"Stain_{index}");
            stainObj.transform.SetParent(_mainImageObject.transform, false);

            Image stainImage = stainObj.AddComponent<Image>();
            
            if (data.stainSprite != null)
            {
                stainImage.sprite = data.stainSprite;
                stainImage.color = Color.white;
            }
            else
            {
                stainImage.color = data.stainColor;
            }

            RectTransform rect = stainObj.GetComponent<RectTransform>();
            rect.sizeDelta = data.size;
            rect.anchoredPosition = position;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.Euler(0, 0, data.rotation);

            ClickableStain stain = stainObj.AddComponent<ClickableStain>();
            stain.Initialize(index, this, data);

            return stain;
        }

        protected virtual List<Vector2> GenerateRandomPositions(int count)
        {
            var positions = new List<Vector2>();
            const float margin = 0.1f;
            var halfWidth = mainImageSize.x * (0.5f - margin);
            var halfHeight = mainImageSize.y * (0.5f - margin);
            var minDistance = Mathf.Min(mainImageSize.x, mainImageSize.y) * 0.15f;

            var maxAttempts = 100;
            for (var i = 0; i < count; i++)
            {
                Vector2 position = Vector2.zero;
                var validPosition = false;
                var attempts = 0;

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

        public virtual void OnStainClicked(ClickableStain stain)
        {
            if (!_activeStains.Contains(stain))
                return;

            StartCoroutine(RemoveStainCoroutine(stain));
        }

        protected virtual System.Collections.IEnumerator RemoveStainCoroutine(ClickableStain stain)
        {
            Image stainImage = stain.GetComponent<Image>();
            
            if (stainFadeOutDuration > 0 && stainImage != null)
            {
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

            _activeStains.Remove(stain);
            _stainsRemaining--;
            
            if (stain != null && stain.gameObject != null)
            {
                Destroy(stain.gameObject);
            }

            OnStainRemoved(stain);

            if (_stainsRemaining <= 0)
            {
                OnAllStainsRemoved();
            }
        }

        protected virtual void OnStainRemoved(ClickableStain stain)
        {
        }

        protected virtual void OnAllStainsRemoved()
        {
            if (_winConditionStrategy != null && _winConditionStrategy.CheckWinCondition(this))
            {
                _winConditionStrategy.OnWin(this);
            }
        }

        public override void OnGameComplete()
        {
            base.OnGameComplete();
            StartCoroutine(CloseAfterDelay(1f));
        }

        protected virtual System.Collections.IEnumerator CloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseGame();
        }

        protected virtual Sprite GetMainImage()
        {
            return mainImage;
        }
        
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

        public int GetStainsRemaining() => _stainsRemaining;
    }

    public class ClickableStain : MonoBehaviour, IPointerClickHandler
    {
        private ClickGame _game;
        private bool _isRemoved = false;

        public void Initialize(int index, ClickGame game, ClickGame.StainData data)
        {
            _game = game;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isRemoved || _game == null)
                return;

            _isRemoved = true;
            _game.OnStainClicked(this);
        }
    }
}

