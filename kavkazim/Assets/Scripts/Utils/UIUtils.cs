using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kavkazim.Utils
{
    /// <summary>
    /// Helper class for common UI operations to avoid duplication and magic strings.
    /// </summary>
    public static class UIUtils
    {
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[UIUtils] Created EventSystem.");
            }
        }

        public static void EnsureGraphicRaycaster(Canvas canvas)
        {
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log($"[UIUtils] Added GraphicRaycaster to {canvas.name}.");
            }
        }

        public static Font GetDefaultFont()
        {
            // Centralize the font loading
            // If we ever want to change the font from "LegacyRuntime.ttf", we do it here.
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        
        // Common Colors used across UI
        public static readonly Color ColorHost = new(1f, 0.84f, 0f);        // Gold
        public static readonly Color ColorReady = new(0.2f, 0.8f, 0.2f);    // Green
        public static readonly Color ColorNotReady = new(0.8f, 0.2f, 0.2f); // Red
        public static readonly Color ColorWaiting = new(0.5f, 0.5f, 0.5f);  // Gray
        public static readonly Color ColorWhite = Color.white;
        public static readonly Color ColorOrange = new(1f, 0.5f, 0f);   // Orange
    }
}
