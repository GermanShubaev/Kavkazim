namespace Minigames.Base
{
    /// <summary>
    /// Interface for minigame registry that maps MinigameType to factory functions.
    /// </summary>
    public interface IMinigameRegistry
    {
        /// <summary>
        /// Registers a factory function for a minigame type.
        /// </summary>
        /// <param name="type">The minigame type</param>
        /// <param name="factory">Factory function that creates the minigame instance</param>
        void Register(MinigameType type, System.Func<IMinigame> factory);

        /// <summary>
        /// Creates a minigame instance for the given type.
        /// </summary>
        /// <param name="type">The minigame type to create</param>
        /// <returns>The created minigame instance, or null if not registered</returns>
        IMinigame Create(MinigameType type);

        /// <summary>
        /// Checks if a minigame type is registered.
        /// </summary>
        /// <param name="type">The minigame type to check</param>
        /// <returns>True if registered, false otherwise</returns>
        bool IsRegistered(MinigameType type);
    }
}
