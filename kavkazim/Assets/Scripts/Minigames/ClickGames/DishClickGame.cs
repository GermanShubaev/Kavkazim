using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Minigames.ClickGames
{
    public class DishClickGame : ClickGame
    {
        private const string DishPath = "Assets/Art/Images/dishes/dish.png";
        [Header("Dish Settings")]
        [SerializeField] private int numberOfStains = 11;

        private Sprite _dishSprite;
        private Sprite _smudgeSprite;

        private void Awake()
        {
            useScreenPercentage = true;
            screenPercentage = 0.75f;
            
            LoadDishImages();
        }

        private void LoadDishImages()
        {
            #if UNITY_EDITOR
            
            _dishSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DishPath);
            if (_dishSprite == null)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DishPath);
                if (tex != null)
                {
                    _dishSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

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

            Vector2[] stainPositions = new Vector2[]
            {
                new Vector2(-0.08f, 0.05f),
                new Vector2(0.07f, 0.08f),
                new Vector2(0.0f, -0.02f),
                new Vector2(-0.05f, -0.08f),
                new Vector2(0.09f, -0.05f),
                new Vector2(0.02f, 0.1f),
            };

            var rotations = new float[] { 0f, 45f, -30f, 90f, -15f, 60f };

            Vector2[] sizes = new Vector2[]
            {
                new Vector2(90, 60),
                new Vector2(75, 50),
                new Vector2(100, 65),
                new Vector2(80, 55),
                new Vector2(95, 62),
                new Vector2(85, 58),
            };

            var count = Mathf.Min(numberOfStains, stainPositions.Length);
            for (int i = 0; i < count; i++)
            {
                StainData stain = new StainData
                {
                    stainSprite = _smudgeSprite,
                    normalizedPosition = stainPositions[i],
                    size = sizes[i],
                    rotation = rotations[i],
                    stainColor = new Color(0.6f, 0.5f, 0.3f, 0.85f)
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

        public override void OnGameComplete()
        {
            base.OnGameComplete(); // Mark as completed successfully
            Debug.Log("[DishClickGame] Congratulations! The dish is now sparkling clean.");
            
            StartCoroutine(CloseAfterDelay(1.5f));
        }
    }
}

