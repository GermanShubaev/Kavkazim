namespace Kavkazim.Netcode.WinConditions
{
    /// <summary>
    /// Enum representing the team a player belongs to.
    /// Used for win condition evaluation and UI display.
    /// </summary>
    public enum TeamEnum : byte
    {
        /// <summary>No team assigned (spectator, waiting player).</summary>
        None = 0,
        
        /// <summary>Innocent team - wins by eliminating all Kavkazi or completing missions.</summary>
        Innocent = 1,
        
        /// <summary>Kavkazi team - wins by achieving majority or sabotage.</summary>
        Kavkazi = 2
    }
}
