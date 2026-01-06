using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Minigames.ClickGames
{
    public class PapakhaClickGame : ClickGame
    {
        private const string PapakhaPath = "Assets/Art/Images/papakha/papakha_clean.png";
        private const string StainPath = "Assets/Art/Images/papakha/snow_stain.png";
        
        [Header("Papakha Settings")]
        [SerializeField] private int numberOfStains = 5;

        private Sprite _papakhaSprite;
        private Sprite _snowStainSprite;

        private void Awake()
        {
            useScreenPercentage = true;
            screenPercentage = 0.75f;
            LoadPapakhaImages();
        }

        private void LoadPapakhaImages()
        {
            #if UNITY_EDITOR
            
            _papakhaSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PapakhaPath);
            if (_papakhaSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PapakhaPath);
                if (tex != null)
                {
                    _papakhaSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            _snowStainSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StainPath);
            if (_snowStainSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(StainPath);
                if (tex != null)
                {
                    _snowStainSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            if (_papakhaSprite != null)
                Debug.Log("[PapakhaClickGame] Loaded papakha_clean.png (Editor mode)");
            if (_snowStainSprite != null)
                Debug.Log("[PapakhaClickGame] Loaded snow_stain.png (Editor mode)");
            #endif

            if (_papakhaSprite == null)
            {
                _papakhaSprite = Resources.Load<Sprite>("Art/Images/papakha/papakha_clean");
                if (_papakhaSprite == null)
                    _papakhaSprite = Resources.Load<Sprite>("papakha/papakha_clean");
            }

            if (_snowStainSprite == null)
            {
                _snowStainSprite = Resources.Load<Sprite>("Art/Images/papakha/snow_stain");
                if (_snowStainSprite == null)
                    _snowStainSprite = Resources.Load<Sprite>("papakha/snow_stain");
            }

            if (_papakhaSprite == null)
            {
                Debug.LogError("[PapakhaClickGame] Failed to load papakha_clean.png. Make sure the image is either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/papakha/");
                Debug.LogError("  2. Or in Assets/Art/Images/papakha/ (editor only)");
            }

            if (_snowStainSprite == null)
            {
                Debug.LogError("[PapakhaClickGame] Failed to load snow_stain.png. Make sure the image is either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/papakha/");
                Debug.LogError("  2. Or in Assets/Art/Images/papakha/ (editor only)");
            }
        }

        protected override Sprite GetMainImage()
        {
            return _papakhaSprite;
        }

        protected override List<StainData> GetStainData()
        {
            var stains = new List<StainData>();

            if (_snowStainSprite == null)
            {
                Debug.LogWarning("[PapakhaClickGame] Snow stain sprite not loaded, using default colored stains.");
            }

            var stainPositions = new Vector2[]
            {
                new Vector2(-0.15f, 0.02f),
                new Vector2(0.12f, 0.05f),
                new Vector2(0.0f, -0.02f),
                new Vector2(-0.08f, -0.08f),
                new Vector2(0.10f, -0.06f),
                new Vector2(0.02f, 0.08f)
            };

            var rotations = new float[] { 0f, 15f, -10f, 25f, -20f, 5f };

            var sizes = new Vector2[]
            {
                new Vector2(100, 65),
                new Vector2(85, 55),
                new Vector2(110, 70),
                new Vector2(90, 60),
                new Vector2(105, 68),
                new Vector2(95, 62),
            };

            var count = Mathf.Min(numberOfStains, stainPositions.Length);
            for (var i = 0; i < count; i++)
            {
                StainData stain = new StainData
                {
                    stainSprite = _snowStainSprite,
                    normalizedPosition = stainPositions[i],
                    size = sizes[i],
                    rotation = rotations[i],
                    stainColor = new Color(0.9f, 0.95f, 1f, 0.85f)
                };
                stains.Add(stain);
            }

            return stains;
        }

        protected override void OnStainRemoved(ClickableStain stain)
        {
            base.OnStainRemoved(stain);
            Debug.Log($"[PapakhaClickGame] Snow removed! {GetStainsRemaining()} stains remaining.");
        }

        protected override void OnAllStainsRemoved()
        {
            Debug.Log("[PapakhaClickGame] Papakha is clean! Game complete.");
            base.OnAllStainsRemoved();
        }

        protected override void OnGameComplete()
        {
            base.OnGameComplete(); // Mark as completed successfully
            Debug.Log("[PapakhaClickGame] Congratulations! The papakha is now clean.");
            StartCoroutine(CloseAfterDelay(1.5f));
        }
    }
}

