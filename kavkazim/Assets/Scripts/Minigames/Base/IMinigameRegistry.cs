namespace Minigames.Base
{
    public interface IMinigameRegistry
    {
        void Register(MinigameType type, System.Func<IMinigame> factory);
        IMinigame Create(MinigameType type);
        bool IsRegistered(MinigameType type);
    }
}
