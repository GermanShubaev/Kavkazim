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
    }
}