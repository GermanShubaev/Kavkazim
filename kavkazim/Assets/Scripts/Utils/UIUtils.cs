using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kavkazim.Utils
{
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
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        
        public static readonly Color ColorHost = new(1f, 0.84f, 0f);
        public static readonly Color ColorReady = new(0.2f, 0.8f, 0.2f);
        public static readonly Color ColorNotReady = new(0.8f, 0.2f, 0.2f);
        public static readonly Color ColorWaiting = new(0.5f, 0.5f, 0.5f);
        public static readonly Color ColorWhite = Color.white;
        public static readonly Color ColorOrange = new(1f, 0.5f, 0f);
    }
}
