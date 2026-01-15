using Minigames.ClickGames;
using Minigames.SortGames;
using UnityEngine;

namespace Minigames.Base
{
    /// <summary>
    /// Factory for creating minigame instances using a registry pattern.
    /// New minigames can be registered without modifying this class.
    /// </summary>
    public static class MinigameFactory
    {
        private static IMinigameRegistry _registry;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets or creates the minigame registry.
        /// </summary>
        private static IMinigameRegistry Registry
        {
            get
            {
                if (_registry == null)
                {
                    lock (_lock)
                    {
                        if (_registry == null)
                        {
                            _registry = new MinigameRegistry();
                            InitializeDefaultRegistrations();
                        }
                    }
                }
                return _registry;
            }
        }

        /// <summary>
        /// Creates a minigame instance for the given type.
        /// </summary>
        public static IMinigame CreateMinigame(MinigameType gameType)
        {
            return Registry.Create(gameType);
        }

        /// <summary>
        /// Registers a custom factory for a minigame type.
        /// Allows extending the factory without modifying this class.
        /// </summary>
        public static void Register(MinigameType type, System.Func<IMinigame> factory)
        {
            Registry.Register(type, factory);
        }

        /// <summary>
        /// Initializes default minigame registrations.
        /// </summary>
        private static void InitializeDefaultRegistrations()
        {
            var registry = (MinigameRegistry)_registry;
            
            registry.Register(MinigameType.LezginkaSort, () => 
            {
                var obj = new GameObject($"{MinigameType.LezginkaSort}Instance");
                return obj.AddComponent<LezginkaSortGame>();
            });
            registry.Register(MinigameType.PraySort, () => 
            {
                var obj = new GameObject($"{MinigameType.PraySort}Instance");
                return obj.AddComponent<PraySortGame>();
            });
            registry.Register(MinigameType.PapakhaClick, () => 
            {
                var obj = new GameObject($"{MinigameType.PapakhaClick}Instance");
                return obj.AddComponent<PapakhaClickGame>();
            });
            registry.Register(MinigameType.DishClick, () => 
            {
                var obj = new GameObject($"{MinigameType.DishClick}Instance");
                return obj.AddComponent<DishClickGame>();
            });
            registry.Register(MinigameType.WolfClick, () => 
            {
                var obj = new GameObject($"{MinigameType.WolfClick}Instance");
                return obj.AddComponent<WolfClickGame>();
            });
            registry.Register(MinigameType.TakedownClick, () => 
            {
                var obj = new GameObject($"{MinigameType.TakedownClick}Instance");
                return obj.AddComponent<TakedownClickGame>();
            });
            registry.Register(MinigameType.ShashlikSort, () => 
            {
                var obj = new GameObject($"{MinigameType.ShashlikSort}Instance");
                return obj.AddComponent<ShashlikSortGame>();
            });
            registry.Register(MinigameType.RemoteCommonClick, () => 
            {
                var obj = new GameObject($"{MinigameType.RemoteCommonClick}Instance");
                return obj.AddComponent<RemoteCommonClickGame>();
            });
            registry.Register(MinigameType.LaundrySort, () => 
            {
                var obj = new GameObject($"{MinigameType.LaundrySort}Instance");
                return obj.AddComponent<LaundrySortGame>();
            });
            registry.Register(MinigameType.TapachkiClick, () => 
            {
                var obj = new GameObject($"{MinigameType.TapachkiClick}Instance");
                return obj.AddComponent<TapachkiGame>();
            });
        }
    }
}

