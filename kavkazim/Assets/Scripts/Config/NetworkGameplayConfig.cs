using UnityEngine;

namespace Kavkazim.Config
{
    [CreateAssetMenu(menuName = "Kavkazim/Network Gameplay Config", fileName = "NetworkGameplayConfig")]
    public class NetworkGameplayConfig : ScriptableObject
    {
        [Header("Movement")]
        [Range(0.5f, 10f)] public float moveSpeed = 3.5f;
        [Range(10f, 120f)] public float inputSendRate = 30f; // client → server RPC rate
        [Range(10f, 120f)] public float stateSendRate = 20f; // server → clients (NetworkTransform or manual)

        [Header("Kill")]
        [Tooltip("Maximum distance for a kill to be valid")]
        [Range(0.5f, 5f)] public float killRange = 2.0f;
        
        [Tooltip("Cooldown between kills in seconds")]
        [Range(5f, 60f)] public float killCooldown = 15f;

        [Header("Ghost")]
        [Tooltip("Sprite alpha for ghost players (0 = invisible, 1 = fully visible)")]
        [Range(0.1f, 0.7f)] public float ghostAlpha = 0.5f;
    }
}