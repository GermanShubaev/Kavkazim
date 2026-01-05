using System;
using Unity.Collections;
using Unity.Netcode;

namespace Kavkazim.Netcode.Meeting
{
    /// <summary>
    /// Type of meeting trigger.
    /// </summary>
    public enum MeetingType : byte
    {
        /// <summary>Dead body was reported.</summary>
        BodyReport = 0,
        
        /// <summary>Emergency meeting button was pressed.</summary>
        Emergency = 1
    }

    /// <summary>
    /// Data passed when starting a meeting.
    /// Network-serializable for RPC transmission.
    /// </summary>
    [Serializable]
    public struct MeetingStartData : INetworkSerializable, IEquatable<MeetingStartData>
    {
        /// <summary>Type of meeting (body report or emergency).</summary>
        public MeetingType Type;
        
        /// <summary>ClientId of the player who called the meeting.</summary>
        public ulong CallerId;
        
        /// <summary>Name of the player who called the meeting (for UI display).</summary>
        public FixedString64Bytes CallerName;
        
        /// <summary>
        /// ClientId of the victim (for body reports only).
        /// Set to ulong.MaxValue if not applicable.
        /// </summary>
        public ulong VictimId;
        
        /// <summary>Name of the victim (for body reports only).</summary>
        public FixedString64Bytes VictimName;
        
        /// <summary>Server timestamp when meeting was called.</summary>
        public float Timestamp;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref CallerId);
            serializer.SerializeValue(ref CallerName);
            serializer.SerializeValue(ref VictimId);
            serializer.SerializeValue(ref VictimName);
            serializer.SerializeValue(ref Timestamp);
        }

        public bool Equals(MeetingStartData other) =>
            Type == other.Type &&
            CallerId == other.CallerId &&
            CallerName.Equals(other.CallerName) &&
            VictimId == other.VictimId &&
            VictimName.Equals(other.VictimName) &&
            Math.Abs(Timestamp - other.Timestamp) < 0.01f;

        public override bool Equals(object obj) => obj is MeetingStartData other && Equals(other);
        
        public override int GetHashCode() => HashCode.Combine(Type, CallerId, VictimId);
        
        public override string ToString() =>
            Type == MeetingType.BodyReport
                ? $"Body Report by {CallerName} (victim: {VictimName})"
                : $"Emergency Meeting by {CallerName}";
    }

    /// <summary>
    /// Represents a vote target (either a player or skip).
    /// </summary>
    [Serializable]
    public struct VoteTarget : INetworkSerializable, IEquatable<VoteTarget>
    {
        /// <summary>
        /// ClientId of the target player.
        /// Set to ulong.MaxValue if voting to skip.
        /// </summary>
        public ulong TargetClientId;
        
        /// <summary>True if this is a skip vote.</summary>
        public bool IsSkip;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TargetClientId);
            serializer.SerializeValue(ref IsSkip);
        }

        public bool Equals(VoteTarget other) =>
            TargetClientId == other.TargetClientId &&
            IsSkip == other.IsSkip;

        public override bool Equals(object obj) => obj is VoteTarget other && Equals(other);
        
        public override int GetHashCode() => HashCode.Combine(TargetClientId, IsSkip);
        
        public override string ToString() =>
            IsSkip ? "Skip" : $"Player {TargetClientId}";

        /// <summary>Creates a skip vote.</summary>
        public static VoteTarget CreateSkip() => new VoteTarget
        {
            TargetClientId = ulong.MaxValue,
            IsSkip = true
        };

        /// <summary>Creates a vote for a specific player.</summary>
        public static VoteTarget CreatePlayerVote(ulong clientId) => new VoteTarget
        {
            TargetClientId = clientId,
            IsSkip = false
        };
    }

    /// <summary>
    /// Result of a meeting after voting concludes.
    /// </summary>
    [Serializable]
    public struct MeetingResult : INetworkSerializable, IEquatable<MeetingResult>
    {
        /// <summary>
        /// ClientId of the eliminated player.
        /// Set to ulong.MaxValue if no elimination (tie or skip won).
        /// </summary>
        public ulong EliminatedId;
        
        /// <summary>Name of the eliminated player (for UI display).</summary>
        public FixedString64Bytes EliminatedName;
        
        /// <summary>True if the result was a tie (no elimination).</summary>
        public bool IsTie;
        
        /// <summary>True if skip won the vote.</summary>
        public bool SkipWon;
        
        /// <summary>
        /// Total number of votes cast.
        /// Can be less than total alive players if timeout occurred.
        /// </summary>
        public int TotalVotes;
        
        /// <summary>Number of votes the eliminated player received (for display).</summary>
        public int EliminatedVoteCount;
        
        /// <summary>Number of skip votes.</summary>
        public int SkipVoteCount;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EliminatedId);
            serializer.SerializeValue(ref EliminatedName);
            serializer.SerializeValue(ref IsTie);
            serializer.SerializeValue(ref SkipWon);
            serializer.SerializeValue(ref TotalVotes);
            serializer.SerializeValue(ref EliminatedVoteCount);
            serializer.SerializeValue(ref SkipVoteCount);
        }

        public bool Equals(MeetingResult other) =>
            EliminatedId == other.EliminatedId &&
            EliminatedName.Equals(other.EliminatedName) &&
            IsTie == other.IsTie &&
            SkipWon == other.SkipWon &&
            TotalVotes == other.TotalVotes &&
            EliminatedVoteCount == other.EliminatedVoteCount &&
            SkipVoteCount == other.SkipVoteCount;

        public override bool Equals(object obj) => obj is MeetingResult other && Equals(other);
        
        public override int GetHashCode() => HashCode.Combine(EliminatedId, IsTie, SkipWon);
        
        public override string ToString()
        {
            if (IsTie) return "Tie - No Elimination";
            if (SkipWon) return "Skip Won - No Elimination";
            if (EliminatedId == ulong.MaxValue) return "No Elimination";
            return $"{EliminatedName} Eliminated ({EliminatedVoteCount} votes)";
        }

        /// <summary>Creates a "no elimination" result.</summary>
        public static MeetingResult CreateNoElimination(bool isTie, int skipCount, int totalVotes) => new MeetingResult
        {
            EliminatedId = ulong.MaxValue,
            EliminatedName = "",
            IsTie = isTie,
            SkipWon = !isTie, // If not tie, then skip won
            TotalVotes = totalVotes,
            EliminatedVoteCount = 0,
            SkipVoteCount = skipCount
        };

        /// <summary>Creates an elimination result.</summary>
        public static MeetingResult CreateElimination(ulong clientId, string name, int voteCount, int skipCount, int totalVotes) => new MeetingResult
        {
            EliminatedId = clientId,
            EliminatedName = name,
            IsTie = false,
            SkipWon = false,
            TotalVotes = totalVotes,
            EliminatedVoteCount = voteCount,
            SkipVoteCount = skipCount
        };
    }
}
