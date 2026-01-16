using Minigames.Base;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minigames.Base
{
    public class MinigameTriggerPointHelper : MonoBehaviour
    {
#if UNITY_EDITOR
        [MenuItem("GameObject/Minigame/Create Trigger Point", false, 10)]
        public static void CreateTriggerPoint()
        {
            GameObject triggerObj = new GameObject("MinigameTriggerPoint");
            MinigameTriggerPoint trigger = triggerObj.AddComponent<MinigameTriggerPoint>();
            
            Selection.activeGameObject = triggerObj;
            
            Debug.Log("Created MinigameTriggerPoint. Set position, radius, and game type in Inspector.");
        }

        [MenuItem("GameObject/Minigame/Create Trigger Point at (0,0)", false, 11)]
        public static void CreateTriggerPointAtOrigin()
        {
            CreateTriggerPoint();
            GameObject triggerObj = Selection.activeGameObject;
            if (triggerObj != null)
            {
                triggerObj.transform.position = Vector3.zero;
                MinigameTriggerPoint trigger = triggerObj.GetComponent<MinigameTriggerPoint>();
                if (trigger != null)
                {
                    var field = typeof(MinigameTriggerPoint).GetField("position", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(trigger, Vector2.zero);
                    }
                }
            }
        }

        [MenuItem("GameObject/Minigame/Create LezginkaSortGame Trigger (-25, 13)", false, 12)]
        public static void CreateLezginkaTrigger()
        {
            GameObject triggerObj = new GameObject("LezginkaSortGame_Trigger");
            MinigameTriggerPoint trigger = triggerObj.AddComponent<MinigameTriggerPoint>();
            
            var positionField = typeof(MinigameTriggerPoint).GetField("position", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (positionField != null)
            {
                positionField.SetValue(trigger, new Vector2(-25f, 13f));
            }
            
            var radiusField = typeof(MinigameTriggerPoint).GetField("radius", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (radiusField != null)
            {
                radiusField.SetValue(trigger, 2f);
            }
            
            var gameTypeField = typeof(MinigameTriggerPoint).GetField("gameType", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (gameTypeField != null)
            {
                gameTypeField.SetValue(trigger, MinigameType.LezginkaSort);
            }
            
            triggerObj.transform.position = new Vector3(-25f, 13f, 0f);
            
            Selection.activeGameObject = triggerObj;
            EditorUtility.SetDirty(trigger);
            
            Debug.Log("Created LezginkaSortGame trigger at position (-25, 13) with radius 2");
        }

        [MenuItem("GameObject/Minigame/Create PapakhaClickGame Trigger (37, 18)", false, 13)]
        public static void CreatePapakhaTrigger()
        {
            GameObject triggerObj = new GameObject("PapakhaClickGame_Trigger");
            MinigameTriggerPoint trigger = triggerObj.AddComponent<MinigameTriggerPoint>();
            
            var positionField = typeof(MinigameTriggerPoint).GetField("position", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (positionField != null)
            {
                positionField.SetValue(trigger, new Vector2(37f, 18f));
            }
            
            var radiusField = typeof(MinigameTriggerPoint).GetField("radius", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (radiusField != null)
            {
                radiusField.SetValue(trigger, 2f);
            }
            
            var gameTypeField = typeof(MinigameTriggerPoint).GetField("gameType", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (gameTypeField != null)
            {
                gameTypeField.SetValue(trigger, MinigameType.PapakhaClick);
            }
            
            triggerObj.transform.position = new Vector3(37f, 18f, 0f);
            
            Selection.activeGameObject = triggerObj;
            EditorUtility.SetDirty(trigger);
            
            Debug.Log("Created PapakhaClickGame trigger at position (37, 18) with radius 2");
        }
#endif
    }
}

