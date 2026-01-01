using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minigames
{
    /// <summary>
    /// A minigame where players click smudges to clean a dish.
    /// The game ends when all 5 smudges have been removed.
    /// </summary>
    public class DishClickGame : ClickGame
    {
        [Header("Dish Settings")]
        [SerializeField] private int numberOfStains = 11;

        private Sprite _dishSprite;
        private Sprite _smudgeSprite;

        private void Awake()
        {
            // Enable 75% screen size popup
            useScreenPercentage = true;
            screenPercentage = 0.75f;
            
            LoadDishImages();
        }

        private void LoadDishImages()
        {
            #if UNITY_EDITOR
            // Load dish image
            string dishPath = "Assets/Art/Images/dishes/dish.png";
            _dishSprite = AssetDatabase.LoadAssetAtPath<Sprite>(dishPath);
            if (_dishSprite == null)
            {
                // Try loading as texture and converting
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dishPath);
                if (tex != null)
                {
                    _dishSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            // Load smudge image
            string smudgePath = "Assets/Art/Images/dishes/smudge.png";
            _smudgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(smudgePath);
            if (_smudgeSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(smudgePath);
                if (tex != null)
                {
                    _smudgeSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            if (_dishSprite != null)
                Debug.Log("[DishClickGame] Loaded dish.png (Editor mode)");
            if (_smudgeSprite != null)
                Debug.Log("[DishClickGame] Loaded smudge.png (Editor mode)");
            #endif

            // Fallback to Resources for runtime
            if (_dishSprite == null)
            {
                _dishSprite = Resources.Load<Sprite>("Art/Images/dishes/dish");
                if (_dishSprite == null)
                    _dishSprite = Resources.Load<Sprite>("dishes/dish");
            }

            if (_smudgeSprite == null)
            {
                _smudgeSprite = Resources.Load<Sprite>("Art/Images/dishes/smudge");
                if (_smudgeSprite == null)
                    _smudgeSprite = Resources.Load<Sprite>("dishes/smudge");
            }

            if (_dishSprite == null)
            {
                Debug.LogError("[DishClickGame] Failed to load dish.png. Make sure the image is either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/dishes/");
                Debug.LogError("  2. Or in Assets/Art/Images/dishes/ (editor only)");
            }

            if (_smudgeSprite == null)
            {
                Debug.LogError("[DishClickGame] Failed to load smudge.png. Make sure the image is either:");
                Debug.LogError("  1. In a Resources folder: Assets/Resources/Art/Images/dishes/");
                Debug.LogError("  2. Or in Assets/Art/Images/dishes/ (editor only)");
            }
        }

        protected override Sprite GetMainImage()
        {
            return _dishSprite;
        }

        protected override List<StainData> GetStainData()
        {
            List<StainData> stains = new List<StainData>();

            if (_smudgeSprite == null)
            {
                Debug.LogWarning("[DishClickGame] Smudge sprite not loaded, using default colored stains.");
            }

            // Positions specifically on the circular plate (centered in the image)
            // The plate occupies roughly a circle in the center
            // Plate bounds approximately: radius of about 0.18 from center
            // Normalized positions (-0.5 to 0.5 relative to main image center)
            Vector2[] stainPositions = new Vector2[]
            {
                new Vector2(-0.08f, 0.05f),   // Left side of plate
                new Vector2(0.07f, 0.08f),    // Upper-right of plate
                new Vector2(0.0f, -0.02f),    // Center of plate
                new Vector2(-0.05f, -0.08f),  // Lower-left of plate
                new Vector2(0.09f, -0.05f),   // Lower-right of plate
                new Vector2(0.02f, 0.1f),     // Top of plate (extra if needed)
            };

            // Randomize rotation for variety
            float[] rotations = new float[] { 0f, 45f, -30f, 90f, -15f, 60f };

            // Size variations for natural look
            Vector2[] sizes = new Vector2[]
            {
                new Vector2(90, 60),
                new Vector2(75, 50),
                new Vector2(100, 65),
                new Vector2(80, 55),
                new Vector2(95, 62),
                new Vector2(85, 58),
            };

            int count = Mathf.Min(numberOfStains, stainPositions.Length);
            for (int i = 0; i < count; i++)
            {
                StainData stain = new StainData
                {
                    stainSprite = _smudgeSprite,
                    normalizedPosition = stainPositions[i],
                    size = sizes[i],
                    rotation = rotations[i],
                    stainColor = new Color(0.6f, 0.5f, 0.3f, 0.85f) // Brown for food smudge
                };
                stains.Add(stain);
            }

            return stains;
        }

        protected override void OnStainRemoved(ClickableStain stain)
        {
            base.OnStainRemoved(stain);
            Debug.Log($"[DishClickGame] Smudge cleaned! {GetStainsRemaining()} smudges remaining.");
        }

        protected override void OnAllStainsRemoved()
        {
            Debug.Log("[DishClickGame] Dish is clean! Game complete.");
            base.OnAllStainsRemoved();
        }

        protected override void OnGameComplete()
        {
            // Show success message or play celebration effect
            Debug.Log("[DishClickGame] Congratulations! The dish is now sparkling clean.");
            
            // Close after a short delay to let player see the clean dish
            StartCoroutine(CloseAfterDelay(1.5f));
        }
    }
}

