using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Minigames
{
    /// <summary>
    /// Helper class for setting up minigame trigger points in the scene.
    /// </summary>
    public class MinigameTriggerPointHelper : MonoBehaviour
    {
#if UNITY_EDITOR
        [MenuItem("GameObject/Minigame/Create Trigger Point", false, 10)]
        public static void CreateTriggerPoint()
        {
            GameObject triggerObj = new GameObject("MinigameTriggerPoint");
            MinigameTriggerPoint trigger = triggerObj.AddComponent<MinigameTriggerPoint>();
            
            // Set default values
            // Note: These will be set via reflection or we can use SerializedObject
            // For now, the default values in MinigameTriggerPoint will be used
            
            // Select the newly created object
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
                    // Use reflection to set the position field
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
            
            // Set position to (-25, 13)
            var positionField = typeof(MinigameTriggerPoint).GetField("position", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (positionField != null)
            {
                positionField.SetValue(trigger, new Vector2(-25f, 13f));
            }
            
            // Set radius to 2
            var radiusField = typeof(MinigameTriggerPoint).GetField("radius", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (radiusField != null)
            {
                radiusField.SetValue(trigger, 2f);
            }
            
            // Set game type to LezginkaSort
            var gameTypeField = typeof(MinigameTriggerPoint).GetField("gameType", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (gameTypeField != null)
            {
                gameTypeField.SetValue(trigger, MinigameType.LezginkaSort);
            }
            
            // Set transform position as well for visual reference
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
            
            // Set position to (37, 18)
            var positionField = typeof(MinigameTriggerPoint).GetField("position", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (positionField != null)
            {
                positionField.SetValue(trigger, new Vector2(37f, 18f));
            }
            
            // Set radius to 2
            var radiusField = typeof(MinigameTriggerPoint).GetField("radius", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (radiusField != null)
            {
                radiusField.SetValue(trigger, 2f);
            }
            
            // Set game type to PapakhaClick
            var gameTypeField = typeof(MinigameTriggerPoint).GetField("gameType", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (gameTypeField != null)
            {
                gameTypeField.SetValue(trigger, MinigameType.PapakhaClick);
            }
            
            // Set transform position as well for visual reference
            triggerObj.transform.position = new Vector3(37f, 18f, 0f);
            
            Selection.activeGameObject = triggerObj;
            EditorUtility.SetDirty(trigger);
            
            Debug.Log("Created PapakhaClickGame trigger at position (37, 18) with radius 2");
        }
#endif
    }
}

