using System.Collections.Generic;
using UnityEngine;

namespace Kavkazim.Netcode.Validation.Rules
{
    public class KavkaziStrictMinorityRule : ILobbyRule
    {
        public IEnumerable<ValidationError> Validate(LobbySettings s, LobbyRuntimeContext ctx)
        {
            if (ctx.IsTestMode)
            {
                yield break;
            }
            
            if (2 * s.KavkaziCount >= ctx.CurrentPlayerCount)
            {
                int maxK = (ctx.CurrentPlayerCount - 1) / 2;
                if (maxK < 1) maxK = 1;
                
                yield return new ValidationError("KavkaziCount", 
                    $"With {ctx.CurrentPlayerCount} players, max Kavkazis is {Mathf.Max(0, (ctx.CurrentPlayerCount - 1) / 2)} (Strict majority).");
            }
        }

        public LobbySettings Clamp(LobbySettings s, LobbyRuntimeContext ctx)
        {
            int maxAllowed = (ctx.CurrentPlayerCount - 1) / 2;
            
            int limit = Mathf.Max(1, maxAllowed);
            
            if (s.KavkaziCount > limit)
            {
                s.KavkaziCount = limit;
            }
            return s;
        }
    }
}
