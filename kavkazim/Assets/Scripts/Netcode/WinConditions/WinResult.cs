using System.Collections.Generic;

namespace Kavkazim.Netcode.WinConditions
{
    public class WinResult
    {
        public TeamEnum WinningTeamEnum { get; }
        
        public IReadOnlyList<ulong> WinnerClientIds { get; }
        
        public IReadOnlyList<string> WinnerNames { get; }
        
        public string ReasonKey { get; }
        
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
