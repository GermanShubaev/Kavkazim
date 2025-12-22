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
#endif
    }
}

