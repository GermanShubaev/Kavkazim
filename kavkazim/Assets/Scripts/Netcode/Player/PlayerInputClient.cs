using Kavkazim.Netcode;
using Minigames;
using Minigames.Base;
using Minigames.Progress;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UI;

namespace Netcode.Player
{
    [RequireComponent(typeof(PlayerMotorServer))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputClient : NetworkBehaviour
    {
        private InputAction _move;
        private PlayerAvatar _avatar;
        private IMinigame _currentMinigame;
        private MinigameTriggerPoint _currentTrigger;


        private void Start()
        {
            Debug.Log($"[Input] IsOwner={IsOwner}, MoveFound={_move!=null}");

            var pi = GetComponent<PlayerInput>();
            _move = pi && pi.actions ? pi.actions["Move"] : null;
            if (_move != null && !_move.enabled) _move.Enable();

            _avatar = GetComponent<PlayerAvatar>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (GameSessionManager.Instance != null && 
                GameSessionManager.Instance.CurrentPhase.Value == MatchPhase.Meeting)
            {
                return;
            }

            bool isInMinigame = _currentMinigame != null && _currentMinigame.IsActive;

            if (_move != null && !isInMinigame)
            {
                Vector2 v = _move.ReadValue<Vector2>();
                SubmitInputToServerRpc(v); 
            }
            
            if (isInMinigame)
            {
                SubmitInputToServerRpc(Vector2.zero);
            }

            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            {
                if (_avatar && _avatar.CurrentRole is KavkaziRole kavkazi)
                {
                    kavkazi.TryKill();
                }
            }

            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                HandleMinigameTrigger();
            }

        }

        private void HandleMinigameTrigger()
        {
            if (_currentMinigame != null && _currentMinigame.IsActive)
            {
                _currentMinigame.CloseGame();
                OnMinigameClosed();
                return;
            }

            Vector2 playerPosition = transform.position;

            MinigameManager manager = MinigameManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[PlayerInputClient] MinigameManager not found!");
                return;
            }

            if (manager.GetNearestTriggerPoint(playerPosition, out MinigameTriggerPoint trigger, out float distance))
            {
                Debug.Log($"[PlayerInputClient] Trigger found! Game: {trigger.GameType}, Distance: {distance:F2}");
                
                if (!trigger.IsAssignedToLocalPlayer())
                {
                    Debug.Log($"[PlayerInputClient] Minigame {trigger.GameType} is not assigned to this player. Cannot start.");
                    return;
                }
                
                _currentTrigger = trigger;
                
                _currentMinigame = MinigameFactory.CreateMinigame(trigger.GameType);
                if (_currentMinigame != null)
                {
                    _currentMinigame.StartGame();
                    
                    if (_currentMinigame is BaseMinigame baseMinigame)
                    {
                        StartCoroutine(MonitorMinigameCompletion());
                    }
                }
                else
                {
                    Debug.LogError($"[PlayerInputClient] Failed to create minigame of type {trigger.GameType}");
                    _currentTrigger = null;
                }
            }
            else
            {
                Debug.Log("[PlayerInputClient] No minigame trigger point within range.");
            }
        }
        
        private System.Collections.IEnumerator MonitorMinigameCompletion()
        {
            while (_currentMinigame != null && _currentMinigame.IsActive)
            {
                yield return null;
            }
            
            if (_currentMinigame is BaseMinigame baseMinigame && baseMinigame.WasCompletedSuccessfully)
            {
                MarkTaskAsCompleted();
            }
            
            OnMinigameClosed();
        }
        
        private void OnMinigameClosed()
        {
            _currentMinigame = null;
            _currentTrigger = null;
        }
        
        private void MarkTaskAsCompleted()
        {
            if (_currentTrigger == null || GameplayUI.Instance == null) return;
            
            ulong localClientId = _avatar != null ? _avatar.OwnerClientId : 0;
            var taskAssignments = GameplayUI.Instance.GetTaskAssignments();
            
            if (taskAssignments == null || !taskAssignments.ContainsKey(localClientId))
            {
                return;
            }
            
            var playerTasks = taskAssignments[localClientId];
            float positionTolerance = 0.1f;
            
            foreach (var task in playerTasks)
            {
                if (Vector2.Distance(task.Location, _currentTrigger.Position) < positionTolerance &&
                    task.MinigameType == _currentTrigger.GameType)
                {
                    GameplayUI.Instance.MarkTaskAsCompleted(task);
                    Debug.Log($"[PlayerInputClient] Task completed: {task.Description}");
                    
                    NotifyTaskCompletedServerRpc();
                    break;
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void NotifyTaskCompletedServerRpc(RpcParams rpcParams = default)
        {
            if (GameSessionManager.Instance == null) return;
            
            int currentTasks = GameSessionManager.Instance.TasksLeft.Value;
            if (currentTasks > 0)
            {
                GameSessionManager.Instance.TasksLeft.Value = currentTasks - 1;
                Debug.Log($"[PlayerInputClient] Server: Task completed. TasksLeft: {currentTasks - 1}");
                
                if (GameSessionManager.Instance.TasksLeft.Value == 0)
                {
                    Debug.Log("[PlayerInputClient] Server: All tasks completed! Innocents win!");
                    GameSessionManager.Instance.CheckWinConditions();
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputToServerRpc(Vector2 move)
        {
            var motor = GetComponent<PlayerMotorServer>();
            motor?.ApplyInput(move);
        }

        private void OnDestroy()
        {
            if (_currentMinigame != null && _currentMinigame.IsActive)
            {
                _currentMinigame.CloseGame();
            }
        }
    }
}