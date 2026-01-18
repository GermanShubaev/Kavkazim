using System.Collections.Generic;
using UnityEngine;

namespace Kavkazim.Netcode.WinConditions
{
    public class WinConditionEvaluator
    {
        private readonly List<IWinCondition> _conditions = new List<IWinCondition>();

        public void AddCondition(IWinCondition condition)
        {
            if (condition == null)
            {
                Debug.LogWarning("[WinConditionEvaluator] Attempted to add null condition.");
                return;
            }
            
            _conditions.Add(condition);
        }

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

        public static WinConditionEvaluator CreateDefault()
        {
            var evaluator = new WinConditionEvaluator();
            
            evaluator.AddCondition(new ImposterMajorityWinCondition());
            evaluator.AddCondition(new AllTasksCompletedWinCondition());
            evaluator.AddCondition(new AllImpostersEliminatedWinCondition());
            
            return evaluator;
        }
    }
}
