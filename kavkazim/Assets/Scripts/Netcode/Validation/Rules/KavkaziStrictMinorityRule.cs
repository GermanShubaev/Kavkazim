using System.Collections.Generic;
using UnityEngine;

namespace Kavkazim.Netcode.Validation.Rules
{
    public class KavkaziStrictMinorityRule : ILobbyRule
    {
        public IEnumerable<ValidationError> Validate(LobbySettings s, LobbyRuntimeContext ctx)
        {
            // Rule: 2 * K < P
            // K <= (P - 1) / 2
            
            // If we don't have enough players to support even 1 Kavkazi (P < 3),
            // and K is 1, this technically fails the strict minority rule (2*1 < 2 is false).
            // But usually this error is covered by "Not enough players to start".
            // However, the prompt specifically asks to show "Invalid: With 4 players, max Kavkazis is 1".
            
            // Let's enforce it strictly.
            if (2 * s.KavkaziCount >= ctx.CurrentPlayerCount)
            {
                int maxK = (ctx.CurrentPlayerCount - 1) / 2; // e.g. P=4 -> 3/2 = 1.
                if (maxK < 1) maxK = 1; // Always show at least 1 in message if we want to be helpful, or strictly 0.
                
                // If P is very low (e.g. 1), max K is 0.
                // We really just want to say "Too many Kavkazis for this player count".
                
                yield return new ValidationError("KavkaziCount", 
                    $"With {ctx.CurrentPlayerCount} players, max Kavkazis is {Mathf.Max(0, (ctx.CurrentPlayerCount - 1) / 2)} (Strict majority).");
            }
        }

        public LobbySettings Clamp(LobbySettings s, LobbyRuntimeContext ctx)
        {
            // Calculate max allowed K for current P
            int maxAllowed = (ctx.CurrentPlayerCount - 1) / 2;
            
            // Ensure we don't go below 1 (game requires at least 1 imposter usually, 
            // though strict minority with P=1 or 2 is impossible).
            // If P=1, maxAllowed=0. We clamp K to 1 (because min is 1). 
            // The validation will still fail, which is correct.
            // But if P=4, maxAllowed=1. If K=2, we clamp to 1.
            
            int limit = Mathf.Max(1, maxAllowed);
            
            if (s.KavkaziCount > limit)
            {
                s.KavkaziCount = limit;
            }
            return s;
        }
    }
}
