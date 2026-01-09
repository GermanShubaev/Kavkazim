using Kavkazim.Netcode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

namespace UI
{
    /// <summary>
    /// Controller for the WinScreen scene.
    /// Displays winning team, winner names, and handles return to lobby.
    /// This script should be attached to a GameObject in the WinScreen scene.
    /// </summary>
    public class WinScreenSceneController : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float autoReturnDelay = 10f; // Auto-return after 10 seconds

        // UI References (created dynamically or assigned in inspector)
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
            // Get win result from static cache (set before scene load)
            // GameSessionManager.Instance won't exist as it was destroyed with previous scene
            _cachedWinResult = GameSessionManager.CachedWinResult;
            
            // Validate we have valid data
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
            
            // Start countdown for auto-return
            _returnCountdown = autoReturnDelay;
            
            // Wire up button listener
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
            // Update countdown
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

        // Methods removed: CreateUI, CreateText, CreateButton

        private void DisplayWinResult(WinResultData result)
        {
            // Set title with team color
            string teamName = result.GetWinningTeamDisplay();
            Color teamColor = result.WinningTeam == 2 
                ? new Color(1f, 0.3f, 0.3f) // Red for Kavkazi
                : new Color(0.3f, 0.8f, 1f); // Cyan for Innocent

            _titleText.text = $"{teamName.ToUpper()} WIN!";
            _titleText.color = teamColor;

            // Set reason
            _reasonText.text = result.GetReasonDisplay();

            // Set winner names
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
            // Only set returning flag for server or if we want to block input permanently
            
            _returnButton.interactable = false;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                _isReturning = true;
                _countdownText.text = "Returning to lobby...";

                // Use GameSessionManager to properly reset state and return to lobby
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
                // Client just waits - server will trigger scene sync
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
