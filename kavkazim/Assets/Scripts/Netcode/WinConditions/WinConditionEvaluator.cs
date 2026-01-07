using System.Collections.Generic;
using UnityEngine;

namespace Kavkazim.Netcode.WinConditions
{
    /// <summary>
    /// Evaluates win conditions in priority order.
    /// Returns the first matching condition's result.
    /// 
    /// Usage:
    ///   var evaluator = new WinConditionEvaluator();
    ///   evaluator.AddCondition(new ImposterMajorityWinCondition());
    ///   evaluator.AddCondition(new AllImpostersEliminatedWinCondition());
    ///   
    ///   if (evaluator.TryEvaluate(snapshot, out var result))
    ///   {
    ///       // Handle win
    ///   }
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
            Debug.Log($"[WinConditionEvaluator] Added condition: {condition.ConditionName}");
        }

        /// <summary>
        /// Removes a win condition by type.
        /// </summary>
        public bool RemoveCondition<T>() where T : IWinCondition
        {
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (_conditions[i] is T)
                {
                    Debug.Log($"[WinConditionEvaluator] Removed condition: {_conditions[i].ConditionName}");
                    _conditions.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Clears all win conditions.
        /// </summary>
        public void ClearConditions()
        {
            _conditions.Clear();
            Debug.Log("[WinConditionEvaluator] Cleared all conditions.");
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
        /// Gets the number of registered conditions.
        /// </summary>
        public int ConditionCount => _conditions.Count;

        /// <summary>
        /// Creates an evaluator with the default win conditions:
        /// 1. ImposterMajorityWinCondition (Kavkazi >= 50% alive)
        /// 2. AllTasksCompletedWinCondition (All tasks completed - Innocents win)
        /// 3. AllImpostersEliminatedWinCondition (All Kavkazi dead)
        /// </summary>
        public static WinConditionEvaluator CreateDefault()
        {
            var evaluator = new WinConditionEvaluator();
            
            // Add conditions in priority order
            // Imposter majority is checked first (prevents voting phases continuing after majority)
            evaluator.AddCondition(new ImposterMajorityWinCondition());
            // Tasks completed is checked before imposters eliminated (tasks are primary win condition for innocents)
            evaluator.AddCondition(new AllTasksCompletedWinCondition());
            evaluator.AddCondition(new AllImpostersEliminatedWinCondition());
            
            return evaluator;
        }
    }
}
