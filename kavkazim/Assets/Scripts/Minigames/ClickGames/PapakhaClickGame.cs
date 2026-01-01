using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minigames
{
    /// <summary>
    /// A minigame where players click snow stains to clean a papakha (Caucasian fur hat).
    /// The game ends when all 5 snow stains have been removed.
    /// </summary>
    public class PapakhaClickGame : ClickGame
    {
        [Header("Papakha Settings")]
        [SerializeField] private int numberOfStains = 5;

        private Sprite _papakhaSprite;
        private Sprite _snowStainSprite;

        private void Awake()
        {
            // Enable 75% screen size popup
            useScreenPercentage = true;
            screenPercentage = 0.75f;
            
            LoadPapakhaImages();
        }

        private void LoadPapakhaImages()
        {
            #if UNITY_EDITOR
            // Load papakha clean image
            string papakhaPath = "Assets/Art/Images/papakha/papakha_clean.png";
            _papakhaSprite = AssetDatabase.LoadAssetAtPath<Sprite>(papakhaPath);
            if (_papakhaSprite == null)
            {
                // Try loading as texture and converting
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(papakhaPath);
                if (tex != null)
                {
                    _papakhaSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            // Load snow stain image
            string stainPath = "Assets/Art/Images/papakha/snow_stain.png";
            _snowStainSprite = AssetDatabase.LoadAssetAtPath<Sprite>(stainPath);
            if (_snowStainSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(stainPath);
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

            // Fallback to Resources for runtime
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
            List<StainData> stains = new List<StainData>();

            if (_snowStainSprite == null)
            {
                Debug.LogWarning("[PapakhaClickGame] Snow stain sprite not loaded, using default colored stains.");
            }

            // Positions specifically on the papakha hat (fur hat in center of image)
            // The hat occupies roughly the center area, slightly below vertical center
            // Normalized positions (-0.5 to 0.5 relative to main image center)
            // Hat bounds approximately: X: -0.25 to 0.25, Y: -0.15 to 0.1
            Vector2[] stainPositions = new Vector2[]
            {
                new Vector2(-0.15f, 0.02f),   // Left side of hat
                new Vector2(0.12f, 0.05f),    // Right side of hat
                new Vector2(0.0f, -0.02f),    // Center of hat
                new Vector2(-0.08f, -0.08f),  // Lower-left of hat
                new Vector2(0.10f, -0.06f),   // Lower-right of hat
                new Vector2(0.02f, 0.08f),    // Top center of hat (extra if needed)
            };

            // Randomize rotation for variety
            float[] rotations = new float[] { 0f, 15f, -10f, 25f, -20f, 5f };

            // Size variations for natural look (scaled for larger popup)
            Vector2[] sizes = new Vector2[]
            {
                new Vector2(100, 65),
                new Vector2(85, 55),
                new Vector2(110, 70),
                new Vector2(90, 60),
                new Vector2(105, 68),
                new Vector2(95, 62),
            };

            int count = Mathf.Min(numberOfStains, stainPositions.Length);
            for (int i = 0; i < count; i++)
            {
                StainData stain = new StainData
                {
                    stainSprite = _snowStainSprite,
                    normalizedPosition = stainPositions[i],
                    size = sizes[i],
                    rotation = rotations[i],
                    stainColor = new Color(0.9f, 0.95f, 1f, 0.85f) // Light blue-white for snow
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
            // Show success message or play celebration effect
            Debug.Log("[PapakhaClickGame] Congratulations! The papakha is now clean.");
            
            // Close after a short delay to let player see the clean papakha
            StartCoroutine(CloseAfterDelay(1.5f));
        }
    }
}

