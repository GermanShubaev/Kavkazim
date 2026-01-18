using Unity.Netcode;
using UnityEngine;

namespace Netcode
{
    public class KeepNetworkManagerAlive : MonoBehaviour
    {
        private void Awake()
        {
            var nm = GetComponent<NetworkManager>();
            if (!nm)
            {
                Debug.LogError("KeepNetworkManagerAlive: No NetworkManager on this GameObject.");
                return;
            }

            var all = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            if (all.Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }
    }
}