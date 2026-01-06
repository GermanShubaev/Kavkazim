using Minigames.ClickGames;
using Minigames.SortGames;
using UnityEngine;

namespace Minigames.Base
{
    public static class MinigameFactory
    {
        public static IMinigame CreateMinigame(MinigameType gameType)
        {
            var minigameObj = new GameObject($"{gameType}Instance");
            
            IMinigame minigame = gameType switch
            {
                MinigameType.LezginkaSort => minigameObj.AddComponent<LezginkaSortGame>(),
                MinigameType.PraySortGame => minigameObj.AddComponent<PraySortGame>(),
                MinigameType.PapakhaClick => minigameObj.AddComponent<PapakhaClickGame>(),
                MinigameType.DishClick => minigameObj.AddComponent<DishClickGame>(),
                MinigameType.WolfClick => minigameObj.AddComponent<WolfClickGame>(),
                MinigameType.TakedownClick => minigameObj.AddComponent<TakedownClickGame>(),
                MinigameType.ShashlikSort => minigameObj.AddComponent<ShashlikSortGame>(),
                MinigameType.RemoteCommonClick => minigameObj.AddComponent<RemoteCommonClickGame>(),
                MinigameType.LaundrySort => minigameObj.AddComponent<LaundrySortGame>(),
                MinigameType.TapachkiClick => minigameObj.AddComponent<TapachkiGame>(),
            };

            return minigame;
        }
    }
}

