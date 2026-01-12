using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using Kavkazim.Netcode;
using Minigames.Base;
using Minigames;

namespace Minigames.Progress
{
    /// <summary>
    /// Represents a task that a player needs to complete.
    /// Contains the minigame type and its location on the map.
    /// </summary>
    [System.Serializable]
    public class Task
    {
        /// <summary>The type of minigame to complete.</summary>
        public MinigameType MinigameType { get; set; }
        
        /// <summary>The location (position) where this minigame is located.</summary>
        public Vector2 Location { get; set; }

        public string Description { get; set; }

        public Task(MinigameType minigameType, Vector2 location, string description)
        {
            MinigameType = minigameType;
            Location = location;
            Description = description;
        }

        public override string ToString()
        {
            return $"Task: {MinigameType} at ({Location.x}, {Location.y})";
        }
    }

    /// <summary>
    /// Responsible for distributing random tasks from existing minigames to players.
    /// Each innocent player gets a number of tasks specified in the lobby screen "mission count".
    /// Tasks are comprised of minigame type and location, so one minigame type can be present
    /// in different places across the map.
    /// </summary>
    public static class TaskDistributor
    {
        /// <summary>
        /// Distributes random tasks to all innocent players.
        /// </summary>
        /// <returns>
        /// A dictionary mapping player ClientId to their list of assigned tasks.
        /// Only innocent players will have entries in this dictionary.
        /// </returns>
        public static Dictionary<ulong, List<Task>> DistributeTasksToInnocentPlayers()
        {
            var taskAssignments = new Dictionary<ulong, List<Task>>();

            // Get mission count from lobby settings
            if (GameSessionManager.Instance == null)
            {
                Debug.LogError("[TaskDistributor] GameSessionManager.Instance is null. Cannot distribute tasks.");
                return taskAssignments;
            }

            int missionsPerInnocent = GameSessionManager.Instance.Settings.Value.MissionsPerInnocent;
            
            // Get all available minigame trigger points from MinigameTriggerPointManager
            List<MinigameTriggerPoint> allTriggerPoints = new List<MinigameTriggerPoint>();
            
            // Get or create the MinigameTriggerPointManager instance
            MinigameTriggerPointManager triggerPointManager = MinigameTriggerPointManager.Instance;
            
            // Ensure trigger points are spawned
            if (triggerPointManager != null)
            {
                allTriggerPoints = triggerPointManager.GetSpawnedTriggerPoints();
                
                // If no points are spawned yet, spawn them now
                if (allTriggerPoints == null || allTriggerPoints.Count == 0)
                {
                    Debug.Log("[TaskDistributor] No trigger points spawned yet. Spawning them now...");
                    triggerPointManager.SpawnAllTriggerPoints();
                    allTriggerPoints = triggerPointManager.GetSpawnedTriggerPoints();
                }
                
                Debug.Log($"[TaskDistributor] Found {allTriggerPoints.Count} trigger points from MinigameTriggerPointManager.");
            }
            
            // Fallback: if manager still has no points, find all trigger points in scene
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

            // Convert trigger points to tasks (one task per trigger point)
            List<Task> availableTasks = allTriggerPoints
                .Where(tp => tp != null)
                .Select(tp => new Task(tp.GameType, tp.Position, GetTaskDescription(tp.GameType)))
                .ToList();

            if (availableTasks.Count == 0)
            {
                Debug.LogWarning("[TaskDistributor] No valid tasks available. Cannot distribute tasks.");
                return taskAssignments;
            }

            // Get all innocent players
            List<ulong> innocentPlayerIds = GetInnocentPlayerIds();

            if (innocentPlayerIds.Count == 0)
            {
                Debug.LogWarning("[TaskDistributor] No innocent players found. Cannot distribute tasks.");
                return taskAssignments;
            }

            Debug.Log($"[TaskDistributor] Distributing {missionsPerInnocent} tasks to {innocentPlayerIds.Count} innocent players from {availableTasks.Count} available tasks.");

            // Distribute tasks to each innocent player
            int totalTasks = 0;
            foreach (ulong playerId in innocentPlayerIds)
            {
                List<Task> playerTasks = SelectRandomTasks(availableTasks, missionsPerInnocent);
                taskAssignments[playerId] = playerTasks;
                totalTasks += playerTasks.Count;
                
                Debug.Log($"[TaskDistributor] Assigned {playerTasks.Count} tasks to player {playerId}:");
                foreach (var task in playerTasks)
                {
                    Debug.Log($"  - {task}");
                }
            }

            // Initialize TasksLeft with total task count (number of innocent players * missions per innocent)
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

        /// <summary>
        /// Gets all innocent player ClientIds from spawned player avatars.
        /// </summary>
        private static List<ulong> GetInnocentPlayerIds()
        {
            var innocentPlayerIds = new List<ulong>();

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            {
                Debug.LogWarning("[TaskDistributor] NetworkManager or SpawnManager is null. Cannot get players.");
                return innocentPlayerIds;
            }

            // Only server can see true roles
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[TaskDistributor] This method should be called on the server to get true roles.");
                // Fallback: return all players if not server (for testing)
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

            // Server can access true roles
            foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                var avatar = netObj.GetComponent<PlayerAvatar>();
                if (avatar == null) continue;

                // Check if player is innocent (server can see true role)
                if (avatar.GetTrueRole() == PlayerRoleType.Innocent)
                {
                    innocentPlayerIds.Add(avatar.OwnerClientId);
                }
            }

            return innocentPlayerIds;
        }

        /// <summary>
        /// Selects random tasks from the available tasks list.
        /// Allows duplicates if there aren't enough unique tasks.
        /// </summary>
        private static List<Task> SelectRandomTasks(List<Task> availableTasks, int count)
        {
            var selectedTasks = new List<Task>();

            if (availableTasks.Count == 0 || count <= 0)
            {
                return selectedTasks;
            }

            // If we need more tasks than available, allow duplicates
            if (count > availableTasks.Count)
            {
                Debug.LogWarning($"[TaskDistributor] Requested {count} tasks but only {availableTasks.Count} available. Some tasks will be duplicated.");
                
                // Fill with all available tasks first
                selectedTasks.AddRange(availableTasks);
                
                // Then add random duplicates to reach the count
                int remaining = count - availableTasks.Count;
                for (int i = 0; i < remaining; i++)
                {
                    int randomIndex = Random.Range(0, availableTasks.Count);
                    selectedTasks.Add(availableTasks[randomIndex]);
                }
            }
            else
            {
                // Shuffle and take the requested count
                var shuffled = availableTasks.OrderBy(x => Random.value).ToList();
                selectedTasks = shuffled.Take(count).ToList();
            }

            return selectedTasks;
        }

        /// <summary>
        /// Gets tasks assigned to a specific player.
        /// </summary>
        /// <param name="playerId">The ClientId of the player</param>
        /// <param name="taskAssignments">The dictionary of task assignments from DistributeTasksToInnocentPlayers()</param>
        /// <returns>List of tasks for the player, or empty list if not found</returns>
        public static List<Task> GetTasksForPlayer(ulong playerId, Dictionary<ulong, List<Task>> taskAssignments)
        {
            if (taskAssignments == null || !taskAssignments.ContainsKey(playerId))
            {
                return new List<Task>();
            }

            return taskAssignments[playerId];
        }

        private static string GetTaskDescription(MinigameType minigameType)
        {
            return minigameType switch
            {
                MinigameType.LezginkaSort => "Dance Lezginka",
                MinigameType.PraySortGame => "Shabbat prayer",
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
