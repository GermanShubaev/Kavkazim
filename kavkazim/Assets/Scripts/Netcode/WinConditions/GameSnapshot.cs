using System.Collections.Generic;

namespace Kavkazim.Netcode.WinConditions
{
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

    public class GameSnapshot
    {
        public IReadOnlyList<PlayerSnapshot> AllPlayers { get; }
        
        public int AliveKavkaziCount { get; }
        
        public int AliveInnocentCount { get; }
        
        public int TotalAliveCount => AliveKavkaziCount + AliveInnocentCount;
        
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
