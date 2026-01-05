using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Kavkazim.UI.Meeting
{
    /// <summary>
    /// Visual component for a single player slot in the meeting UI.
    /// Handles hover animations, selection visuals, and input events.
    /// </summary>
    public class MeetingPlayerSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image characterImage;
        [SerializeField] private Image selectionRing; // The "Glowy" ring
        [SerializeField] private Image deadOverlay;   // Visual for dead players
        [SerializeField] private Image votedCheckmark; // Visual for "Has Voted"

        [Header("Settings")]
        [SerializeField] private Color localPlayerNameColor = Color.yellow;
        [SerializeField] private Color normalPlayerNameColor = Color.white;
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float animationDuration = 0.2f;

        private ulong _clientId;
        private bool _isInteractive;
        private System.Action<ulong> _onClickCallback;
        private Vector3 _originalScale;
        
        private bool _isSelected = false;

        private void Awake()
        {
            _originalScale = transform.localScale;

            // Auto-wire references if missing (compatibility with old hierarchy)
            if (selectionRing == null)
            {
                var t = transform.Find("SelectionHighlight");
                if (t) selectionRing = t.GetComponent<Image>();
            }
            if (deadOverlay == null)
            {
                var t = transform.Find("DeadOverlay");
                if (t) deadOverlay = t.GetComponent<Image>();
            }
            if (votedCheckmark == null)
            {
                var t = transform.Find("VotedCheckmark");
                if (t) votedCheckmark = t.GetComponent<Image>();
            }
            if (characterImage == null)
            {
                // Try "CharacterSprite" (old name) or just find first image
                var t = transform.Find("CharacterSprite");
                if (t) characterImage = t.GetComponent<Image>();
            }
            if (playerNameText == null)
            {
                playerNameText = GetComponentInChildren<TextMeshProUGUI>();
            }
            
            // Force disable ring immediately to avoid visual glitches
            if (selectionRing != null) 
            {
                selectionRing.enabled = false;
                selectionRing.raycastTarget = false; // Ensure it never blocks
            }
            
            // Ensure we have a raycast target for events
            if (characterImage != null)
            {
                characterImage.raycastTarget = true; 
            }
        }

        public void Setup(ulong clientId, string playerName, bool isLocalPlayer, bool isDead)
        {
            _clientId = clientId;

            if (playerNameText != null)
            {
                playerNameText.text = isLocalPlayer ? $"{playerName} (You)" : playerName;
                playerNameText.color = isLocalPlayer ? localPlayerNameColor : normalPlayerNameColor;
                playerNameText.raycastTarget = false; // Ensure text doesn't block
            }

            if (deadOverlay != null)
            {
                deadOverlay.enabled = isDead;
                deadOverlay.raycastTarget = false;
            }

            // Reset state
            SetSelected(false);
            SetVoted(false);
            
            // Reset scale just in case
            transform.localScale = _originalScale;
        }

        /// <summary>
        /// Enable or disable interaction (hover/click).
        /// </summary>
        public void SetInteractive(bool interactive, System.Action<ulong> onClick = null)
        {
            _isInteractive = interactive;
            _onClickCallback = onClick;
            
            // If not interactive, ensure scale is reset
            if (!interactive)
            {
                transform.localScale = _originalScale;
                // If interactive disabled (e.g. voted), we might want to keep selection visuals
                // The selection ring is controlled by _isSelected, so it stays
            }
        }

        /// <summary>
        /// Show/hide the selection ring (glow).
        /// </summary>
        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            if (selectionRing != null)
            {
                selectionRing.enabled = isSelected;
            }
        }

        /// <summary>
        /// Show/hide the "Voted" checkmark.
        /// </summary>
        public void SetVoted(bool hasVoted)
        {
            if (votedCheckmark != null)
            {
                votedCheckmark.enabled = hasVoted;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isInteractive) return;
            
            // Show glow on hover
            if (selectionRing != null) selectionRing.enabled = true;

            StopAllCoroutines();
            StartCoroutine(AnimateScale(hoverScale));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isInteractive) return;
            
            // Hide glow on exit ONLY if not selected
            if (selectionRing != null && !_isSelected) selectionRing.enabled = false;

            StopAllCoroutines();
            StartCoroutine(AnimateScale(1.0f));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isInteractive)
            {
                Debug.Log($"[MeetingPlayerSlotView] Click ignored (Not Interactive) on {_clientId}");
                return;
            }

            // Only left clicks
            if (eventData.button != PointerEventData.InputButton.Left) return;

            Debug.Log($"[MeetingPlayerSlotView] CLICK REGISTERED on {_clientId}. Invoking callback...");
            _onClickCallback?.Invoke(_clientId);
        }

        private System.Collections.IEnumerator AnimateScale(float targetScaleMult)
        {
            Vector3 targetScale = _originalScale * targetScaleMult;
            float elapsedTime = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsedTime < animationDuration)
            {
                // Use unscaled time as requested
                elapsedTime += Time.unscaledDeltaTime;
                float t = elapsedTime / animationDuration;
                // Smooth step for nicer ease
                t = t * t * (3f - 2f * t);
                
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }
            transform.localScale = targetScale;
        }
    }
}
