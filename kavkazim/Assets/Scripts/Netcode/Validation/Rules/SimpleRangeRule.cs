using System.Collections.Generic;
using UnityEngine;

namespace Kavkazim.Netcode.Validation.Rules
{
    public class SimpleRangeRule : ILobbyRule
    {
        public IEnumerable<ValidationError> Validate(LobbySettings s, LobbyRuntimeContext ctx)
        {
            if (s.MaxPlayers < 4 || s.MaxPlayers > 15) yield return new ValidationError("MaxPlayers", "Max Players must be between 4 and 15");
            if (s.KavkaziCount < 1 || s.KavkaziCount > 3) yield return new ValidationError("KavkaziCount", "Kavkazi Count must be between 1 and 3");
            if (s.VotingTime < 30 || s.VotingTime > 180) yield return new ValidationError("VotingTime", "Voting Time must be 30-180s");
            if (s.MoveSpeed < 0.5f || s.MoveSpeed > 5f) yield return new ValidationError("MoveSpeed", "Move Speed must be 0.5-5.0");
            if (s.KillCooldown < 5 || s.KillCooldown > 60) yield return new ValidationError("KillCooldown", "Kill Cooldown must be 5-60s");
            if (s.MissionsPerInnocent < 0 || s.MissionsPerInnocent > 10) yield return new ValidationError("MissionsPerInnocent", "Missions must be 0-10");
        }

        public LobbySettings Clamp(LobbySettings s, LobbyRuntimeContext ctx)
        {
            s.MaxPlayers = Mathf.Clamp(s.MaxPlayers, 4, 15);
            s.KavkaziCount = Mathf.Clamp(s.KavkaziCount, 1, 3);
            s.VotingTime = Mathf.Clamp(s.VotingTime, 30f, 180f);
            s.MoveSpeed = Mathf.Clamp(s.MoveSpeed, 0.5f, 5f);
            s.KillCooldown = Mathf.Clamp(s.KillCooldown, 5f, 60f);
            s.MissionsPerInnocent = Mathf.Clamp(s.MissionsPerInnocent, 0, 10);
            return s;
        }
    }
}
