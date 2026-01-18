using System;
using Unity.Collections;
using Unity.Netcode;

namespace Kavkazim.Netcode.Meeting
{
    public enum MeetingType : byte
    {
        BodyReport = 0,
        
        Emergency = 1
    }

    [Serializable]
    public struct MeetingStartData : INetworkSerializable, IEquatable<MeetingStartData>
    {
        public MeetingType Type;
        
        public ulong CallerId;
        
        public FixedString64Bytes CallerName;
        
        public ulong VictimId;
        
        public FixedString64Bytes VictimName;
        
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

    [Serializable]
    public struct VoteTarget : INetworkSerializable, IEquatable<VoteTarget>
    {
        public ulong TargetClientId;
        
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

        public static VoteTarget CreateSkip() => new VoteTarget
        {
            TargetClientId = ulong.MaxValue,
            IsSkip = true
        };

        public static VoteTarget CreatePlayerVote(ulong clientId) => new VoteTarget
        {
            TargetClientId = clientId,
            IsSkip = false
        };
    }

    [Serializable]
    public struct MeetingResult : INetworkSerializable, IEquatable<MeetingResult>
    {
        public ulong EliminatedId;
        
        public FixedString64Bytes EliminatedName;
        
        public bool IsTie;
        
        public bool SkipWon;
        
        public int TotalVotes;
        
        public int EliminatedVoteCount;
        
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
        

        public static MeetingResult CreateNoElimination(bool isTie, int skipCount, int totalVotes) => new MeetingResult
        {
            EliminatedId = ulong.MaxValue,
            EliminatedName = "",
            IsTie = isTie,
            SkipWon = !isTie,
            TotalVotes = totalVotes,
            EliminatedVoteCount = 0,
            SkipVoteCount = skipCount
        };

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
