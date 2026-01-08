using System.Collections.Generic;
using UnityEngine;

namespace Kavkazim.Netcode.WinConditions
{
    /// <summary>
    /// Evaluates win conditions in priority order.
    /// Returns the first matching condition's result.
    /// </summary>
    public class WinConditionEvaluator
    {
        private readonly List<IWinCondition> _conditions = new List<IWinCondition>();

        /// <summary>
        /// Adds a win condition to be evaluated.
        /// Conditions are evaluated in the order they are added.
        /// </summary>
        public void AddCondition(IWinCondition condition)
        {
            if (condition == null)
            {
                Debug.LogWarning("[WinConditionEvaluator] Attempted to add null condition.");
                return;
            }
            
            _conditions.Add(condition);
        }

        /// <summary>
        /// Evaluates all conditions in order and returns the first match.
        /// </summary>
        /// <param name="snapshot">Current game state.</param>
        /// <param name="result">The win result if a condition is met.</param>
        /// <returns>True if any win condition is met.</returns>
        public bool TryEvaluate(GameSnapshot snapshot, out WinResult result)
        {
            result = null;
            
            foreach (var condition in _conditions)
            {
                if (condition.TryGetWinner(snapshot, out result))
                {
                    Debug.Log($"[WinConditionEvaluator] Win condition met: {condition.ConditionName}");
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Creates an evaluator with the default win conditions:
        /// 1. ImposterMajorityWinCondition (Kavkazi >= 50% alive)
        /// 2. AllImpostersEliminatedWinCondition (All Kavkazi dead)
        /// </summary>
        public static WinConditionEvaluator CreateDefault()
        {
            var evaluator = new WinConditionEvaluator();
            
            // Add conditions in priority order
            // Imposter majority is checked first (prevents voting phases continuing after majority)
            evaluator.AddCondition(new ImposterMajorityWinCondition());
            evaluator.AddCondition(new AllImpostersEliminatedWinCondition());
            
            return evaluator;
        }
    }
}
