using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using Kavkazim.Netcode;
using Minigames.Base;

namespace Minigames.Progress
{
    [System.Serializable]
    public class Task
    {
        public MinigameType MinigameType { get; set; }
        public Vector2 Location { get; set; }
        public string Description { get; set; }

        public Task(MinigameType minigameType, Vector2 location, string description)
        {
            MinigameType = minigameType;
            Location = location;
            Description = description;
        }
    }

    public static class TaskDistributor
    {
        public static Dictionary<ulong, List<Task>> DistributeTasksToInnocentPlayers()
        {
            var taskAssignments = new Dictionary<ulong, List<Task>>();

            if (GameSessionManager.Instance == null)
            {
                Debug.LogError("[TaskDistributor] GameSessionManager.Instance is null. Cannot distribute tasks.");
                return taskAssignments;
            }

            int missionsPerInnocent = GameSessionManager.Instance.Settings.Value.MissionsPerInnocent;
            
            List<MinigameTriggerPoint> allTriggerPoints = new List<MinigameTriggerPoint>();
            
            MinigameTriggerPointManager triggerPointManager = MinigameTriggerPointManager.Instance;
            
            if (triggerPointManager != null)
            {
                allTriggerPoints = triggerPointManager.GetSpawnedTriggerPoints();
                
                if (allTriggerPoints == null || allTriggerPoints.Count == 0)
                {
                    Debug.Log("[TaskDistributor] No trigger points spawned yet. Spawning them now...");
                    triggerPointManager.SpawnAllTriggerPoints();
                    allTriggerPoints = triggerPointManager.GetSpawnedTriggerPoints();
                }
                
                Debug.Log($"[TaskDistributor] Found {allTriggerPoints.Count} trigger points from MinigameTriggerPointManager.");
            }
            
            if (allTriggerPoints == null || allTriggerPoints.Count == 0)
            {
                Debug.LogWarning("[TaskDistributor] MinigameTriggerPointManager has no trigger points. Falling back to finding all trigger points in scene.");
                MinigameTriggerPoint[] sceneTriggerPoints = Object.FindObjectsByType<MinigameTriggerPoint>(FindObjectsSortMode.None);
                if (sceneTriggerPoints != null && sceneTriggerPoints.Length > 0)
                {
                    allTriggerPoints = sceneTriggerPoints.ToList();
                    Debug.Log($"[TaskDistributor] Found {allTriggerPoints.Count} trigger points in scene.");
                }
            }
            
            if (allTriggerPoints == null || allTriggerPoints.Count == 0)
            {
                Debug.LogError("[TaskDistributor] No minigame trigger points found. Cannot distribute tasks.");
                return taskAssignments;
            }

            List<Task> availableTasks = allTriggerPoints
                .Where(tp => tp != null)
                .Select(tp => new Task(tp.GameType, tp.Position, GetTaskDescription(tp.GameType)))
                .ToList();

            if (availableTasks.Count == 0)
            {
                Debug.LogWarning("[TaskDistributor] No valid tasks available. Cannot distribute tasks.");
                return taskAssignments;
            }

            List<ulong> innocentPlayerIds = GetInnocentPlayerIds();

            if (innocentPlayerIds.Count == 0)
            {
                Debug.LogWarning("[TaskDistributor] No innocent players found. Cannot distribute tasks.");
                return taskAssignments;
            }

            Debug.Log($"[TaskDistributor] Distributing {missionsPerInnocent} tasks to {innocentPlayerIds.Count} innocent players from {availableTasks.Count} available tasks.");

            int totalTasks = 0;
            foreach (ulong playerId in innocentPlayerIds)
            {
                List<Task> playerTasks = SelectRandomTasks(availableTasks, missionsPerInnocent);
                taskAssignments[playerId] = playerTasks;
                totalTasks += playerTasks.Count;
                
                Debug.Log($"[TaskDistributor] Assigned {playerTasks.Count} tasks to player {playerId}");
            }

            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.TasksLeft.Value = totalTasks;
                Debug.Log($"[TaskDistributor] Initialized TasksLeft to {totalTasks} (={innocentPlayerIds.Count} players * {missionsPerInnocent} missions)");
            }
            else
            {
                Debug.LogWarning("[TaskDistributor] GameSessionManager.Instance is null. Cannot initialize TasksLeft.");
            }

            return taskAssignments;
        }

        private static List<ulong> GetInnocentPlayerIds()
        {
            var innocentPlayerIds = new List<ulong>();

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            {
                Debug.LogWarning("[TaskDistributor] NetworkManager or SpawnManager is null. Cannot get players.");
                return innocentPlayerIds;
            }

            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[TaskDistributor] This method should be called on the server to get true roles.");
                foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
                {
                    var avatar = netObj.GetComponent<PlayerAvatar>();
                    if (avatar != null)
                    {
                        innocentPlayerIds.Add(avatar.OwnerClientId);
                    }
                }
                return innocentPlayerIds;
            }

            foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                var avatar = netObj.GetComponent<PlayerAvatar>();
                if (avatar == null) continue;

                if (avatar.GetTrueRole() == PlayerRoleType.Innocent)
                {
                    innocentPlayerIds.Add(avatar.OwnerClientId);
                }
            }

            return innocentPlayerIds;
        }

        private static List<Task> SelectRandomTasks(List<Task> availableTasks, int count)
        {
            var selectedTasks = new List<Task>();

            if (availableTasks.Count == 0 || count <= 0)
            {
                return selectedTasks;
            }

            if (count > availableTasks.Count)
            {
                Debug.LogWarning($"[TaskDistributor] Requested {count} tasks but only {availableTasks.Count} available. Some tasks will be duplicated.");
                
                selectedTasks.AddRange(availableTasks);
                
                int remaining = count - availableTasks.Count;
                for (int i = 0; i < remaining; i++)
                {
                    int randomIndex = Random.Range(0, availableTasks.Count);
                    selectedTasks.Add(availableTasks[randomIndex]);
                }
            }
            else
            {
                var shuffled = availableTasks.OrderBy(x => Random.value).ToList();
                selectedTasks = shuffled.Take(count).ToList();
            }

            return selectedTasks;
        }

        private static string GetTaskDescription(MinigameType minigameType)
        {
            return minigameType switch
            {
                MinigameType.LezginkaSort => "Dance Lezginka",
                MinigameType.PraySort => "Shabbat prayer",
                MinigameType.PapakhaClick => "Clean Papakha",
                MinigameType.DishClick => "Wash dishes",
                MinigameType.WolfClick => "Blame Amir and Solomon",
                MinigameType.TakedownClick => "Send him 2-3 years Dagestan and forget",
                MinigameType.ShashlikSort => "Make Shashliks",
                MinigameType.RemoteCommonClick => "Its cold",
                MinigameType.LaundrySort => "Sort the laundry",
                MinigameType.TapachkiClick => "Take off shoes",
                MinigameType.EmptyPopup => "",
                _ => ""
            };
        }
    }
}
