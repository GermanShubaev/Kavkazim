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
        PostMatch = 2,
        
        /// <summary>Meeting is in progress (voting). Gameplay frozen.</summary>
        Meeting = 3
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
        /// Developer test mode - allows playing alone with no other players.
        /// When enabled: no minimum player requirement, win conditions disabled.
        /// Only available in Editor/Development builds.
        /// </summary>
        public bool TestMode;

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

    /// <summary>
    /// Network-serializable win result for broadcasting game outcomes.
    /// </summary>
    [Serializable]
    public struct WinResultData : INetworkSerializable, IEquatable<WinResultData>
    {
        /// <summary>The winning team (0=None, 1=Innocent, 2=Kavkazi).</summary>
        public byte WinningTeam;
        
        /// <summary>
        /// Serialized winner names as comma-separated string.
        /// Max 512 bytes for network efficiency.
        /// </summary>
        public FixedString512Bytes WinnerNames;
        
        /// <summary>
        /// Reason key for UI display.
        /// Examples: "imposter_majority", "all_imposters_eliminated"
        /// </summary>
        public FixedString64Bytes ReasonKey;
        
        /// <summary>True if game has ended (prevents re-triggering).</summary>
        public bool HasEnded;

        /// <summary>Creates an empty/default win result.</summary>
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
        
        /// <summary>
        /// Parses winner names from the comma-separated string.
        /// </summary>
        public string[] GetWinnerNamesList()
        {
            string names = WinnerNames.ToString();
            if (string.IsNullOrEmpty(names)) return System.Array.Empty<string>();
            return names.Split(',');
        }
        
        /// <summary>
        /// Gets display text for the winning team.
        /// </summary>
        public string GetWinningTeamDisplay()
        {
            return WinningTeam switch
            {
                1 => "Innocents",
                2 => "Kavkazis",
                _ => "Unknown"
            };
        }
        
        /// <summary>
        /// Gets localized reason text (basic implementation).
        /// </summary>
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
}
