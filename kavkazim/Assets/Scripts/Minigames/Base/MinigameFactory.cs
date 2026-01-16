using Minigames.ClickGames;
using Minigames.GeneralGames;
using Minigames.SortGames;
using UnityEngine;

namespace Minigames.Base
{
    public static class MinigameFactory
    {
        private static IMinigameRegistry _registry;
        private static readonly object _lock = new object();

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

        public static IMinigame CreateMinigame(MinigameType gameType)
        {
            return Registry.Create(gameType);
        }

        public static void Register(MinigameType type, System.Func<IMinigame> factory)
        {
            Registry.Register(type, factory);
        }

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
            registry.Register(MinigameType.Wolf, () => 
            {
                var obj = new GameObject($"{MinigameType.Wolf}Instance");
                return obj.AddComponent<WolfGame>();
            });
            registry.Register(MinigameType.Takedown, () => 
            {
                var obj = new GameObject($"{MinigameType.Takedown}Instance");
                return obj.AddComponent<TakedownGame>();
            });
            registry.Register(MinigameType.ShashlikSort, () => 
            {
                var obj = new GameObject($"{MinigameType.ShashlikSort}Instance");
                return obj.AddComponent<ShashlikSortGame>();
            });
            registry.Register(MinigameType.Remote, () => 
            {
                var obj = new GameObject($"{MinigameType.Remote}Instance");
                return obj.AddComponent<RemoteCommonClickGame>();
            });
            registry.Register(MinigameType.LaundrySort, () => 
            {
                var obj = new GameObject($"{MinigameType.LaundrySort}Instance");
                return obj.AddComponent<LaundrySortGame>();
            });
            registry.Register(MinigameType.Tapachki, () => 
            {
                var obj = new GameObject($"{MinigameType.Tapachki}Instance");
                return obj.AddComponent<TapachkiGame>();
            });
        }
    }
}

