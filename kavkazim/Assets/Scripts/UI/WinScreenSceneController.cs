using Kavkazim.Netcode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using Kavkazim.Utils;

namespace UI
{
    public class WinScreenSceneController : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float autoReturnDelay = 10f;

        [Header("UI References")]
        [SerializeField] private GameObject _canvas;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _winnersText;
        [SerializeField] private Text _reasonText;
        [SerializeField] private Button _returnButton;
        [SerializeField] private Text _countdownText;

        private bool _isReturning = false;
        private float _returnCountdown;
        private WinResultData _cachedWinResult;

        private void Start()
        {
            _cachedWinResult = GameSessionManager.CachedWinResult;
            
            if (!_cachedWinResult.HasEnded)
            {
                Debug.LogWarning("[WinScreenSceneController] No cached win result found, using default");
                _cachedWinResult = new WinResultData
                {
                    WinningTeam = 0,
                    WinnerNames = "Unknown",
                    ReasonKey = "unknown",
                    HasEnded = true
                };
            }
            else
            {
                Debug.Log($"[WinScreenSceneController] Loaded win result: {_cachedWinResult.GetWinningTeamDisplay()}");
            }
            
            DisplayWinResult(_cachedWinResult);
            StartCoroutine(FadeIn());
            
            _returnCountdown = autoReturnDelay;
            
            if (_returnButton != null)
            {
                _returnButton.onClick.AddListener(OnReturnClicked);
            }
        }

        private void OnDestroy()
        {
            if (_returnButton != null)
            {
                _returnButton.onClick.RemoveListener(OnReturnClicked);
            }
        }

        private void Update()
        {
            if (!_isReturning && _returnCountdown > 0)
            {
                _returnCountdown -= Time.deltaTime;
                if (_countdownText != null)
                {
                    _countdownText.text = $"Returning to lobby in {Mathf.CeilToInt(_returnCountdown)}s";
                }
                
                if (_returnCountdown <= 0)
                {
                    OnReturnClicked();
                }
            }
        }

        private void DisplayWinResult(WinResultData result)
        {
            string teamName = result.GetWinningTeamDisplay();
            Color teamColor = result.WinningTeam == 2 
                ? UIUtils.ColorNotReady
                : new Color(0.3f, 0.8f, 1f);

            _titleText.text = $"{teamName.ToUpper()} WIN!";
            _titleText.color = teamColor;

            _reasonText.text = result.GetReasonDisplay();

            string[] winners = result.GetWinnerNamesList();
            if (winners.Length > 0)
            {
                _winnersText.text = string.Join("\n", winners);
            }
            else
            {
                _winnersText.text = "(No winners)";
            }
        }

        private void OnReturnClicked()
        {
            if (_isReturning) return;
            
            _returnButton.interactable = false;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                _isReturning = true;
                _countdownText.text = "Returning to lobby...";

                if (GameSessionManager.Instance != null)
                {
                    Debug.Log("[WinScreenSceneController] Host requesting return to lobby via GameSessionManager...");
                    GameSessionManager.Instance.ReturnToLobbyServerRpc();
                }
                else
                {
                    Debug.LogWarning("[WinScreenSceneController] GameSessionManager not found, loading scene directly...");
                    NetworkManager.Singleton.SceneManager.LoadScene("GameSession", LoadSceneMode.Single);
                }
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                Debug.Log("[WinScreenSceneController] Client waiting for server to load lobby scene...");
                _countdownText.text = "Waiting for host...";
            }
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / fadeInDuration);
                yield return null;
            }
            _canvasGroup.alpha = 1;
        }
    }
}
