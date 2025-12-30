using System.Collections.Generic;

namespace Kavkazim.Netcode.WinConditions
{
    /// <summary>
    /// Result of a win condition evaluation.
    /// Contains all information needed to display the win screen.
    /// </summary>
    public class WinResult
    {
        /// <summary>The team that won.</summary>
        public TeamEnum WinningTeamEnum { get; }
        
        /// <summary>Client IDs of the winning players.</summary>
        public IReadOnlyList<ulong> WinnerClientIds { get; }
        
        /// <summary>Display names of the winning players.</summary>
        public IReadOnlyList<string> WinnerNames { get; }
        
        /// <summary>
        /// Localization key for the win reason.
        /// Examples: "imposter_majority", "all_imposters_eliminated", "missions_complete"
        /// </summary>
        public string ReasonKey { get; }
        
        /// <summary>
        /// Whether to show all team members or only alive winners.
        /// Default: true (show all team members).
        /// </summary>
        public bool ShowAllTeamMembers { get; }

        public WinResult(
            TeamEnum winningTeamEnum, 
            IReadOnlyList<ulong> winnerClientIds, 
            IReadOnlyList<string> winnerNames, 
            string reasonKey,
            bool showAllTeamMembers = true)
        {
            WinningTeamEnum = winningTeamEnum;
            WinnerClientIds = winnerClientIds;
            WinnerNames = winnerNames;
            ReasonKey = reasonKey;
            ShowAllTeamMembers = showAllTeamMembers;
        }
        
        /// <summary>
        /// Creates a WinResult from a GameSnapshot for the specified team.
        /// </summary>
        /// <param name="snapshot">Current game state.</param>
        /// <param name="winningTeamEnum">The team that won.</param>
        /// <param name="reasonKey">Reason key for UI.</param>
        /// <param name="onlyAlive">If true, only include alive players as winners.</param>
        public static WinResult FromSnapshot(
            GameSnapshot snapshot, 
            TeamEnum winningTeamEnum, 
            string reasonKey,
            bool onlyAlive = false)
        {
            var players = onlyAlive 
                ? snapshot.GetAlivePlayersOnTeam(winningTeamEnum) 
                : snapshot.GetAllPlayersOnTeam(winningTeamEnum);
            
            var clientIds = new List<ulong>();
            var names = new List<string>();
            
            foreach (var player in players)
            {
                clientIds.Add(player.ClientId);
                names.Add(player.PlayerName);
            }
            
            return new WinResult(winningTeamEnum, clientIds, names, reasonKey, !onlyAlive);
        }
    }
}
