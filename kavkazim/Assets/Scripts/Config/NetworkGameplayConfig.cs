using UnityEngine;

namespace Kavkazim.Config
{
    [CreateAssetMenu(menuName = "Kavkazim/Network Gameplay Config", fileName = "NetworkGameplayConfig")]
    public class NetworkGameplayConfig : ScriptableObject
    {
        [Header("Kill")]
        [Tooltip("Maximum distance for a kill to be valid")]
        [Range(0.5f, 5f)] public float killRange = 2.0f;

        [Header("Ghost")]
        [Tooltip("Sprite alpha for ghost players (0 = invisible, 1 = fully visible)")]
        [Range(0.1f, 0.7f)] public float ghostAlpha = 0.5f;

        [Header("Report")]
        [Tooltip("Maximum distance to report a dead body")]
        [Range(0.5f, 5f)] public float reportRange = 2.5f;


    }
}