using System.Collections.Generic;
using UnityEngine;

namespace Kavkazim.Netcode.Validation.Rules
{
    public class PlayerCountRules : ILobbyRule
    {
        private const int MIN_PLAYERS_TO_START = 3; // Recommendation from prompt

        public IEnumerable<ValidationError> Validate(LobbySettings s, LobbyRuntimeContext ctx)
        {
            // Max Players Not Below Current Rule
            if (s.MaxPlayers < ctx.CurrentPlayerCount)
            {
                yield return new ValidationError("MaxPlayers", "Max Players cannot be less than current player count");
            }
            
            // Min Players To Start (This might block START but not necessarily indicate invalid settings on the slider?)
            // Requirement: "Prevents starting... block Start if currentPlayers < minPlayers"
            // This is a START condition, not purely a settings configuration condition.
            // But if we use the validator for "CanStart", we should include it.
            // Skip this check in test mode - allows playing alone
            if (!ctx.IsTestMode && ctx.CurrentPlayerCount < MIN_PLAYERS_TO_START)
            {
               yield return new ValidationError("StartGame", $"Need at least {MIN_PLAYERS_TO_START} players to start");
            }
        }

        public LobbySettings Clamp(LobbySettings s, LobbyRuntimeContext ctx)
        {
            // MaxPlayers >= currentPlayers
            // If host lowers max below current, we clamp back up.
            if (s.MaxPlayers < ctx.CurrentPlayerCount)
            {
                s.MaxPlayers = Mathf.Clamp(ctx.CurrentPlayerCount, 4, 15);
            }
            return s;
        }
    }
}
