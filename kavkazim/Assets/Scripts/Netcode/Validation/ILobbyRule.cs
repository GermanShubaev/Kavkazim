namespace Kavkazim.Netcode.Validation
{
    public interface ILobbyRule
    {
        System.Collections.Generic.IEnumerable<ValidationError> Validate(LobbySettings settings, LobbyRuntimeContext ctx);

        LobbySettings Clamp(LobbySettings settings, LobbyRuntimeContext ctx);
    }
}
