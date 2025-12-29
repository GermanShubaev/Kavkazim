using System.Collections.Generic;
using Kavkazim.Netcode.Validation.Rules;

namespace Kavkazim.Netcode.Validation
{
    public class LobbyValidator
    {
        private readonly List<ILobbyRule> _rules;

        public LobbyValidator()
        {
            _rules = new List<ILobbyRule>
            {
                new SimpleRangeRule(),      // Basic ranges first
                new PlayerCountRules(),     // Then player counts
                new KavkaziStrictMinorityRule() // Then complex relations
            };
        }

        public LobbyValidationResult Validate(LobbySettings settings, LobbyRuntimeContext ctx)
        {
            var errors = new List<ValidationError>();
            foreach (var rule in _rules)
            {
                errors.AddRange(rule.Validate(settings, ctx));
            }
            return new LobbyValidationResult(errors);
        }

        public LobbySettings Sanitize(LobbySettings settings, LobbyRuntimeContext ctx)
        {
            LobbySettings current = settings;
            foreach (var rule in _rules)
            {
                current = rule.Clamp(current, ctx);
            }
            return current;
        }
    }
}
