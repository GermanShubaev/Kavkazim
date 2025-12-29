using System;
using Unity.Collections;
using Unity.Netcode;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Match phase enum - determines what state the game session is in.
    /// </summary>
    public enum MatchPhase : byte
    {
        /// <summary>Lobby is open, players can join and ready up.</summary>
        LobbyOpen = 0,
        
        /// <summary>Match is in progress. Late joiners wait for next round.</summary>
        MatchInProgress = 1,
        
        /// <summary>Post-match results screen before returning to lobby.</summary>
        PostMatch = 2
    }

    /// <summary>
    /// Lobby settings that the host can configure.
    /// Server-authoritative, synced to all clients.
    /// </summary>
    [Serializable]
    public struct LobbySettings : INetworkSerializable, IEquatable<LobbySettings>
    {
        /// <summary>Maximum players allowed in the match (4-15).</summary>
        public int MaxPlayers;
        
        /// <summary>Number of Kavkazi (imposters) in the match (1-3).</summary>
        public int KavkaziCount;
        
        /// <summary>Voting time in seconds (30-180).</summary>
        public float VotingTime;
        
        /// <summary>Player movement speed (0.5-5.0).</summary>
        public float MoveSpeed;
        
        /// <summary>Kill cooldown in seconds (5-60).</summary>
        public float KillCooldown;
        
        /// <summary>Number of missions each Innocent must complete (1-10).</summary>
        public int MissionsPerInnocent;

        /// <summary>
        /// Default lobby settings.
        /// </summary>
        public static LobbySettings Default => new()
        {
            MaxPlayers = 10,
            KavkaziCount = 2,
            VotingTime = 60f,
            MoveSpeed = 3.5f,
            KillCooldown = 15f,
            MissionsPerInnocent = 3
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MaxPlayers);
            serializer.SerializeValue(ref KavkaziCount);
            serializer.SerializeValue(ref VotingTime);
            serializer.SerializeValue(ref MoveSpeed);
            serializer.SerializeValue(ref KillCooldown);
            serializer.SerializeValue(ref MissionsPerInnocent);
        }

        public bool Equals(LobbySettings other) =>
            MaxPlayers == other.MaxPlayers &&
            KavkaziCount == other.KavkaziCount &&
            Math.Abs(VotingTime - other.VotingTime) < 0.01f &&
            Math.Abs(MoveSpeed - other.MoveSpeed) < 0.01f &&
            Math.Abs(KillCooldown - other.KillCooldown) < 0.01f &&
            MissionsPerInnocent == other.MissionsPerInnocent;

        public override bool Equals(object obj) => obj is LobbySettings other && Equals(other);
        
        public override int GetHashCode() => HashCode.Combine(MaxPlayers, KavkaziCount, VotingTime, MoveSpeed);
        
        public override string ToString() => 
            $"MaxPlayers={MaxPlayers}, Kavkazi={KavkaziCount}, VotingTime={VotingTime}s, " +
            $"Speed={MoveSpeed}, Cooldown={KillCooldown}s, Missions={MissionsPerInnocent}";
    }

    /// <summary>
    /// Player data stored in the lobby's networked player list.
    /// This is the single source of truth for player info in lobby.
    /// </summary>
    [Serializable]
    public struct PlayerSessionData : INetworkSerializable, IEquatable<PlayerSessionData>
    {
        /// <summary>The client's network ID.</summary>
        public ulong ClientId;
        
        /// <summary>Display name (max 32 chars due to FixedString).</summary>
        public FixedString32Bytes PlayerName;
        
        /// <summary>Whether this player has readied up.</summary>
        public bool IsReady;
        
        /// <summary>Whether this player is the host.</summary>
        public bool IsHost;
        
        /// <summary>
        /// True if this player joined while a match was in progress.
        /// They must wait for the next round to play.
        /// </summary>
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
}
