namespace Kavkazim.Netcode.Validation
{
    public interface ILobbyRule
    {
        /// <summary>
        /// Validates the settings against the rule.
        /// Returns a list of errors (empty if valid).
        /// </summary>
        System.Collections.Generic.IEnumerable<ValidationError> Validate(LobbySettings settings, LobbyRuntimeContext ctx);

        /// <summary>
        /// Clamps/Adjusts the settings to satisfy the rule.
        /// Returns the modified settings.
        /// </summary>
        LobbySettings Clamp(LobbySettings settings, LobbyRuntimeContext ctx);
    }
}
