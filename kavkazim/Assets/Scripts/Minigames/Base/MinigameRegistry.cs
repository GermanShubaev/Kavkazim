using System;
using System.Collections.Generic;
using UnityEngine;

namespace Minigames.Base
{
    public class MinigameRegistry : IMinigameRegistry
    {
        private readonly Dictionary<MinigameType, Func<IMinigame>> _factories = new Dictionary<MinigameType, Func<IMinigame>>();

        public void Register(MinigameType type, Func<IMinigame> factory)
        {
            if (factory == null)
            {
                Debug.LogWarning($"[MinigameRegistry] Attempted to register null factory for {type}");
                return;
            }

            _factories[type] = factory;
        }

        public IMinigame Create(MinigameType type)
        {
            if (!_factories.TryGetValue(type, out Func<IMinigame> factory))
            {
                Debug.LogError($"[MinigameRegistry] No factory registered for {type}");
                return null;
            }

            try
            {
                return factory();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MinigameRegistry] Error creating minigame {type}: {ex.Message}");
                return null;
            }
        }

        public bool IsRegistered(MinigameType type)
        {
            return _factories.ContainsKey(type);
        }
    }
}
