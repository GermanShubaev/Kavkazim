using System;
using Unity.Collections;
using Unity.Netcode;

namespace Kavkazim.Netcode
{
    public enum MatchPhase : byte
    {
        LobbyOpen = 0,
        
        MatchInProgress = 1,
        
        PostMatch = 2,
        
        Meeting = 3
    }

    [Serializable]
    public struct LobbySettings : INetworkSerializable, IEquatable<LobbySettings>
    {
        public int MaxPlayers;
        
        public int KavkaziCount;
        
        public float VotingTime;
        
        public float MoveSpeed;
        
        public float KillCooldown;
        
        public int MissionsPerInnocent;
        
        public bool TestMode;

        public static LobbySettings Default => new()
        {
            MaxPlayers = 10,
            KavkaziCount = 2,
            VotingTime = 60f,
            MoveSpeed = 3.5f,
            KillCooldown = 15f,
            MissionsPerInnocent = 3,
            TestMode = false
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MaxPlayers);
            serializer.SerializeValue(ref KavkaziCount);
            serializer.SerializeValue(ref VotingTime);
            serializer.SerializeValue(ref MoveSpeed);
            serializer.SerializeValue(ref KillCooldown);
            serializer.SerializeValue(ref MissionsPerInnocent);
            serializer.SerializeValue(ref TestMode);
        }

        public bool Equals(LobbySettings other) =>
            MaxPlayers == other.MaxPlayers &&
            KavkaziCount == other.KavkaziCount &&
            Math.Abs(VotingTime - other.VotingTime) < 0.01f &&
            Math.Abs(MoveSpeed - other.MoveSpeed) < 0.01f &&
            Math.Abs(KillCooldown - other.KillCooldown) < 0.01f &&
            MissionsPerInnocent == other.MissionsPerInnocent &&
            TestMode == other.TestMode;
        
        public override string ToString() => 
            $"MaxPlayers={MaxPlayers}, Kavkazi={KavkaziCount}, VotingTime={VotingTime}s, " +
            $"Speed={MoveSpeed}, Cooldown={KillCooldown}s, Missions={MissionsPerInnocent}";
    }

    [Serializable]
    public struct PlayerSessionData : INetworkSerializable, IEquatable<PlayerSessionData>
    {
        public ulong ClientId;
        
        public FixedString32Bytes PlayerName;
        
        public bool IsReady;
        
        public bool IsHost;
        
        public bool JoinedDuringMatch;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsHost);
            serializer.SerializeValue(ref JoinedDuringMatch);
        }

        public bool Equals(PlayerSessionData other) =>
            ClientId == other.ClientId &&
            PlayerName.Equals(other.PlayerName) &&
            IsReady == other.IsReady &&
            IsHost == other.IsHost &&
            JoinedDuringMatch == other.JoinedDuringMatch;

        public override bool Equals(object obj) => obj is PlayerSessionData other && Equals(other);
        
        public override int GetHashCode() => ClientId.GetHashCode();
        
        public override string ToString() => 
            $"{PlayerName} (Client {ClientId}){(IsHost ? " [HOST]" : "")}{(IsReady ? " ✓" : "")}{(JoinedDuringMatch ? " [WAITING]" : "")}";
    }

    [Serializable]
    public struct WinResultData : INetworkSerializable, IEquatable<WinResultData>
    {
        public byte WinningTeam;
        
        public FixedString512Bytes WinnerNames;
        
        public FixedString64Bytes ReasonKey;
        
        public bool HasEnded;

        public static WinResultData Empty => new WinResultData
        {
            WinningTeam = 0,
            WinnerNames = "",
            ReasonKey = "",
            HasEnded = false
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WinningTeam);
            serializer.SerializeValue(ref WinnerNames);
            serializer.SerializeValue(ref ReasonKey);
            serializer.SerializeValue(ref HasEnded);
        }

        public bool Equals(WinResultData other) =>
            WinningTeam == other.WinningTeam &&
            WinnerNames.Equals(other.WinnerNames) &&
            ReasonKey.Equals(other.ReasonKey) &&
            HasEnded == other.HasEnded;
        
        public string[] GetWinnerNamesList()
        {
            string names = WinnerNames.ToString();
            if (string.IsNullOrEmpty(names)) return System.Array.Empty<string>();
            return names.Split(',');
        }
        
        public string GetWinningTeamDisplay()
        {
            return WinningTeam switch
            {
                1 => "Innocents",
                2 => "Kavkazis",
                _ => "Unknown"
            };
        }
        
        public string GetReasonDisplay()
        {
            string key = ReasonKey.ToString();
            return key switch
            {
                "imposter_majority" => "Kavkazis achieved majority!",
                "all_imposters_eliminated" => "All Kavkazis have been eliminated!",
                "missions_complete" => "All missions completed!",
                "all_tasks_completed" => "All tasks completed!",
                "sabotage" => "Critical sabotage successful!",
                _ => key
            };
        }
    }
    
    [Serializable]
    public struct NetworkTaskData : INetworkSerializable, IEquatable<NetworkTaskData>
    {
        public byte MinigameType;
        public float LocationX;
        public float LocationY;
        public FixedString64Bytes Description;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MinigameType);
            serializer.SerializeValue(ref LocationX);
            serializer.SerializeValue(ref LocationY);
            serializer.SerializeValue(ref Description);
        }

        public bool Equals(NetworkTaskData other) =>
            MinigameType == other.MinigameType &&
            Math.Abs(LocationX - other.LocationX) < 0.01f &&
            Math.Abs(LocationY - other.LocationY) < 0.01f &&
            Description.Equals(other.Description);

        public override bool Equals(object obj) => obj is NetworkTaskData other && Equals(other);
        
        public override int GetHashCode() => HashCode.Combine(MinigameType, LocationX, LocationY);
    }
}
