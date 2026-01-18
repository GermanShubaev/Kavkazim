using System.Collections.Generic;
using UnityEngine;

namespace Kavkazim.Netcode.Validation.Rules
{
    public class PlayerCountRules : ILobbyRule
    {
        private const int MIN_PLAYERS_TO_START = 3;

        public IEnumerable<ValidationError> Validate(LobbySettings s, LobbyRuntimeContext ctx)
        {
            if (s.MaxPlayers < ctx.CurrentPlayerCount)
            {
                yield return new ValidationError("MaxPlayers", "Max Players cannot be less than current player count");
            }
            if (!ctx.IsTestMode && ctx.CurrentPlayerCount < MIN_PLAYERS_TO_START)
            {
               yield return new ValidationError("StartGame", $"Need at least {MIN_PLAYERS_TO_START} players to start");
            }
        }

        public LobbySettings Clamp(LobbySettings s, LobbyRuntimeContext ctx)
        {
            if (s.MaxPlayers < ctx.CurrentPlayerCount)
            {
                s.MaxPlayers = Mathf.Clamp(ctx.CurrentPlayerCount, 4, 15);
            }
            return s;
        }
    }
}
