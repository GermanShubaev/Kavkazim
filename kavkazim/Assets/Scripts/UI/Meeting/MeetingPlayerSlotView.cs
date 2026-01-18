using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Kavkazim.UI.Meeting
{
    public class MeetingPlayerSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image selectionRing;
        [SerializeField] private Image deadOverlay;
        [SerializeField] private Image voteCountShield;
        [SerializeField] private TextMeshProUGUI voteCountText;

        [Header("Settings")]
        [SerializeField] private Color localPlayerNameColor = Color.yellow;
        [SerializeField] private Color normalPlayerNameColor = Color.white;
        [SerializeField] private Color deadPlayerNameColor = Color.gray;
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
            
            if (selectionRing != null) 
            {
                selectionRing.enabled = false;
                selectionRing.raycastTarget = false;
            }
            
            if (voteCountShield != null)
            {
                voteCountShield.enabled = false;
                voteCountShield.raycastTarget = false;
            }
            if (voteCountText != null)
            {
                voteCountText.enabled = false;
                voteCountText.raycastTarget = false;
            }
        }

        public void Setup(ulong clientId, string playerName, bool isLocalPlayer, bool isDead)
        {
            _clientId = clientId;

            if (playerNameText != null)
            {
                playerNameText.text = isLocalPlayer ? $"{playerName} (You)" : playerName;
                
                Color targetColor = isDead ? deadPlayerNameColor : (isLocalPlayer ? localPlayerNameColor : normalPlayerNameColor);
                playerNameText.color = targetColor;
                
                playerNameText.raycastTarget = false;
            }

            if (deadOverlay != null)
            {
                deadOverlay.enabled = isDead;
                deadOverlay.raycastTarget = false;
            }

            SetSelected(false);
            
            transform.localScale = _originalScale;
        }

        public void SetInteractive(bool interactive, System.Action<ulong> onClick = null)
        {
            _isInteractive = interactive;
            _onClickCallback = onClick;
            
            if (!interactive)
            {
                transform.localScale = _originalScale;
            }
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            if (selectionRing != null)
            {
                selectionRing.enabled = isSelected;
            }
        }

        public ulong ClientId => _clientId;

        public void SetVoteCount(int count)
        {
            bool show = count > 0;
            
            if (voteCountShield != null)
            {
                voteCountShield.enabled = show;
            }
            if (voteCountText != null)
            {
                voteCountText.enabled = show;
                voteCountText.text = count.ToString();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isInteractive) return;
            
            if (selectionRing != null) selectionRing.enabled = true;

            StopAllCoroutines();
            StartCoroutine(AnimateScale(hoverScale));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isInteractive) return;
            
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
