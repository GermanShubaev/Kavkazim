using UnityEngine;

namespace Minigames
{
    /// <summary>
    /// Factory for creating minigame instances based on MinigameType.
    /// </summary>
    public static class MinigameFactory
    {
        /// <summary>
        /// Creates and returns a minigame instance based on the specified type.
        /// </summary>
        public static IMinigame CreateMinigame(MinigameType gameType)
        {
            GameObject minigameObj = new GameObject($"{gameType}Instance");
            
            IMinigame minigame = gameType switch
            {
                MinigameType.LezginkaSort => minigameObj.AddComponent<LezginkaSortGame>(),
                MinigameType.EmptyPopup => minigameObj.AddComponent<EmptyPopupMinigame>(),
                MinigameType.PraySortGame => minigameObj.AddComponent<PraySortGame>(),
                MinigameType.PapakhaClick => minigameObj.AddComponent<PapakhaClickGame>(),
                MinigameType.DishClick => minigameObj.AddComponent<DishClickGame>(),
                MinigameType.WolfClick => minigameObj.AddComponent<WolfClickGame>(),
                MinigameType.TakedownClick => minigameObj.AddComponent<TakedownClickGame>(),
                MinigameType.ShashlikSort => minigameObj.AddComponent<ShashlikSortGame>(),
                MinigameType.RemoteCommonClick => minigameObj.AddComponent<RemoteCommonClickGame>(),
                _ => minigameObj.AddComponent<EmptyPopupMinigame>()
            };

            return minigame;
        }
    }
}

