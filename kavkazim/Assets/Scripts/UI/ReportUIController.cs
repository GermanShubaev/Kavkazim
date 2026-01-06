using Kavkazim.Netcode;
using Kavkazim.Netcode.Reporting;
using Netcode.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Kavkazim.UI
{
    /// <summary>
    /// Controls the Report UI icon display.
    /// Shows a report button/icon for all players (Innocent and Kavkazi).
    /// </summary>
    public class ReportUIController
    {
        private readonly Transform _parentTransform;
        private GameObject _reportContainer;
        private Image _reportFill;
        private Text _reportText;
        private bool _isInitialized;

        // Report icon dimensions and positioning
        private const float IconSize = 100f;
        private const float BottomPadding = 60f;
        private const float RightPadding = 60f;
        private const float VerticalSpacing = 130f; // Space above Kill icon

        public ReportUIController(Transform canvasTransform)
        {
            _parentTransform = canvasTransform;
        }

        /// <summary>
        /// Create the Report UI elements.
        /// </summary>
        public void CreateUI()
        {
            if (_isInitialized) return;

            // Container for Report UI (bottom right, above kill icon)
            _reportContainer = new GameObject("ReportUI");
            _reportContainer.transform.SetParent(_parentTransform, false);
            RectTransform containerRect = _reportContainer.AddComponent<RectTransform>();
            
            // Anchor bottom-right, positioned above the kill icon
            containerRect.anchorMin = new Vector2(1, 0);
            containerRect.anchorMax = new Vector2(1, 0);
            containerRect.pivot = new Vector2(1, 0);
            containerRect.sizeDelta = new Vector2(IconSize, IconSize);
            containerRect.anchoredPosition = new Vector2(-RightPadding, BottomPadding + VerticalSpacing);

            // Create circle sprite
            Sprite circleSprite = CreateCircleSprite();

            // Background circle
            GameObject bgCircle = new GameObject("Background");
            bgCircle.transform.SetParent(_reportContainer.transform, false);
            Image bgImage = bgCircle.AddComponent<Image>();
            bgImage.sprite = circleSprite;
            bgImage.color = new Color(0.15f, 0.15f, 0.3f, 0.8f); // Dark blue tint
            RectTransform bgRect = bgCircle.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Fill circle (for future "in range" indicator)
            GameObject fillCircle = new GameObject("Fill");
            fillCircle.transform.SetParent(_reportContainer.transform, false);
            _reportFill = fillCircle.AddComponent<Image>();
            _reportFill.sprite = circleSprite;
            _reportFill.color = new Color(1f, 0.6f, 0f, 0.9f); // Orange when body in range
            _reportFill.type = Image.Type.Filled;
            _reportFill.fillMethod = Image.FillMethod.Radial360;
            _reportFill.fillOrigin = (int)Image.Origin360.Top;
            _reportFill.fillClockwise = true;
            _reportFill.fillAmount = 1f;
            RectTransform fillRect = fillCircle.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.1f, 0.1f);
            fillRect.anchorMax = new Vector2(0.9f, 0.9f);
            fillRect.sizeDelta = Vector2.zero;

            // Exclamation mark icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(_reportContainer.transform, false);
            Text iconText = iconObj.AddComponent<Text>();
            iconText.text = "!";
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = Color.white;
            iconText.fontSize = 40;
            iconText.fontStyle = FontStyle.Bold;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero;
            iconRect.anchoredPosition = new Vector2(0, 5);

            // "REPORT" label text
            GameObject textObj = new GameObject("ReportText");
            textObj.transform.SetParent(_reportContainer.transform, false);
            _reportText = textObj.AddComponent<Text>();
            _reportText.text = "REPORT";
            _reportText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _reportText.alignment = TextAnchor.MiddleCenter;
            _reportText.color = Color.white;
            _reportText.fontSize = 12;
            _reportText.fontStyle = FontStyle.Bold;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = new Vector2(0, -30);

            _isInitialized = true;
            Debug.Log("[ReportUIController] Report UI created.");
        }

        /// <summary>
        /// Update the Report UI state.
        /// </summary>
        /// <param name="playerState">Local player's state</param>
        public void UpdateUI(PlayerState playerState)
        {
            if (!_isInitialized || _reportContainer == null) return;

            // Hide if player is dead (ghosts can't report)
            if (playerState == null || !playerState.IsAlive.Value)
            {
                _reportContainer.SetActive(false);
                return;
            }

            _reportContainer.SetActive(true);

            // Check if there's something reportable in range (body OR emergency button)
            // Note: Body reports are UNLIMITED, emergency meetings are one per player
            // Emergency button has its own cooldown/limit check, so we just check range here
            bool canReport = ReportService.HasReportableInRange(playerState.transform.position);

            // Visual feedback: orange when can report, gray when not
            if (_reportFill != null)
            {
                _reportFill.color = canReport 
                    ? new Color(1f, 0.6f, 0f, 0.9f)   // Orange - can report
                    : new Color(0.4f, 0.4f, 0.4f, 0.6f); // Gray - nothing in range
            }
        }

        /// <summary>
        /// Set visibility of the Report UI.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_reportContainer != null)
            {
                _reportContainer.SetActive(visible);
            }
        }

        /// <summary>
        /// Creates a circle sprite texture programmatically.
        /// </summary>
        private Sprite CreateCircleSprite()
        {
            int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float center = size / 2f;
            float radius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance);
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }

            texture.SetPixels(colors);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
