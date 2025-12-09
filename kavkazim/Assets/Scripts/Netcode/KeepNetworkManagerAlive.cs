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

            // If another NetworkManager already exists (e.g., from previous scene), destroy this one
            var all = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            if (all.Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            // Persist across scene loads
            DontDestroyOnLoad(gameObject);
        }
    }
}