using System.Collections.Generic;

namespace Kavkazim.Netcode.WinConditions
{
    /// <summary>
    /// Represents a player's state for win condition evaluation.
    /// </summary>
    public readonly struct PlayerSnapshot
    {
        public readonly ulong ClientId;
        public readonly string PlayerName;
        public readonly TeamEnum TeamEnum;
        public readonly bool IsAlive;

        public PlayerSnapshot(ulong clientId, string playerName, TeamEnum teamEnum, bool isAlive)
        {
            ClientId = clientId;
            PlayerName = playerName;
            TeamEnum = teamEnum;
            IsAlive = isAlive;
        }
    }

    /// <summary>
    /// Immutable snapshot of the current game state.
    /// Used by win conditions to evaluate without side effects.
    /// </summary>
    public class GameSnapshot
    {
        /// <summary>All players in the game with their current state.</summary>
        public IReadOnlyList<PlayerSnapshot> AllPlayers { get; }
        
        /// <summary>Number of alive Kavkazi players.</summary>
        public int AliveKavkaziCount { get; }
        
        /// <summary>Number of alive Innocent players.</summary>
        public int AliveInnocentCount { get; }
        
        /// <summary>Total number of alive players.</summary>
        public int TotalAliveCount => AliveKavkaziCount + AliveInnocentCount;
        
        /// <summary>
        /// Creates a new game snapshot from player data.
        /// </summary>
        public GameSnapshot(IReadOnlyList<PlayerSnapshot> players)
        {
            AllPlayers = players;
            
            int kavkaziCount = 0;
            int innocentCount = 0;
            
            foreach (var player in players)
            {
                if (!player.IsAlive) continue;
                
                switch (player.TeamEnum)
                {
                    case TeamEnum.Kavkazi:
                        kavkaziCount++;
                        break;
                    case TeamEnum.Innocent:
                        innocentCount++;
                        break;
                }
            }
            
            AliveKavkaziCount = kavkaziCount;
            AliveInnocentCount = innocentCount;
        }
        
        /// <summary>
        /// Gets all alive players on a specific team.
        /// </summary>
        public List<PlayerSnapshot> GetAlivePlayersOnTeam(TeamEnum teamEnum)
        {
            var result = new List<PlayerSnapshot>();
            foreach (var player in AllPlayers)
            {
                if (player.IsAlive && player.TeamEnum == teamEnum)
                {
                    result.Add(player);
                }
            }
            return result;
        }
        
        /// <summary>
        /// Gets all players (alive or dead) on a specific team.
        /// </summary>
        public List<PlayerSnapshot> GetAllPlayersOnTeam(TeamEnum teamEnum)
        {
            var result = new List<PlayerSnapshot>();
            foreach (var player in AllPlayers)
            {
                if (player.TeamEnum == teamEnum)
                {
                    result.Add(player);
                }
            }
            return result;
        }
    }
}
