using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Kavkazim.UI.Meeting
{
    /// <summary>
    /// Visual component for the Skip Vote button.
    /// Handles hover animations and selection border.
    /// </summary>
    public class MeetingSkipView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private Image selectionBorder; // Yellow outline
        [SerializeField] private TextMeshProUGUI labelText;

        [Header("Settings")]
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float animationDuration = 0.2f;

        private bool _isInteractive;
        private System.Action _onClickCallback;
        private Vector3 _originalScale;

        private bool _isSelected = false;

        private void Awake()
        {
            _originalScale = transform.localScale;
            if (selectionBorder != null) selectionBorder.enabled = false;
        }

        /// <summary>
        /// Enable or disable interaction.
        /// </summary>
        public void SetInteractive(bool interactive, System.Action onClick = null)
        {
            _isInteractive = interactive;
            _onClickCallback = onClick;
            
            if (!interactive)
            {
                transform.localScale = _originalScale;
            }
        }

        /// <summary>
        /// Show/hide the selection border (yellow outline).
        /// </summary>
        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            if (selectionBorder != null)
            {
                selectionBorder.enabled = isSelected;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isInteractive) return;

            // Show border on hover
            if (selectionBorder != null) selectionBorder.enabled = true;

            StopAllCoroutines();
            StartCoroutine(AnimateScale(hoverScale));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isInteractive) return;

            // Hide border on exit ONLY if not selected
            if (selectionBorder != null && !_isSelected) selectionBorder.enabled = false;

            StopAllCoroutines();
            StartCoroutine(AnimateScale(1.0f));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isInteractive) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            Debug.Log("[MeetingSkipView] Clicked Skip");
            _onClickCallback?.Invoke();
        }

        private System.Collections.IEnumerator AnimateScale(float targetScaleMult)
        {
            Vector3 targetScale = _originalScale * targetScaleMult;
            float elapsedTime = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = elapsedTime / animationDuration;
                t = t * t * (3f - 2f * t);

                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }
            transform.localScale = targetScale;
        }
    }
}
