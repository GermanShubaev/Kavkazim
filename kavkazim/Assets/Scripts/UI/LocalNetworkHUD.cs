// Assets/Scripts/UI/LocalNetworkHUD.cs
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class LocalNetworkHUD : MonoBehaviour
{
    /*
    [SerializeField] private NetworkManager networkManager; // drag NetworkRuntime here (optional)
    [SerializeField] private UnityTransport transport;      // drag UnityTransport here (optional)

    private string address = "127.0.0.1";
    private ushort port = 7777;

    private void Awake()
    {
        if (!networkManager) networkManager = GetComponent<NetworkManager>();
        if (!transport) transport = GetComponent<UnityTransport>();
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 260, 180), GUI.skin.box);
        GUILayout.Label("Local Net Test");

        if (!networkManager)
            GUILayout.Label("<color=red>Missing NetworkManager</color>");
        if (!transport)
            GUILayout.Label("<color=red>Missing UnityTransport</color>");
        if (networkManager && networkManager.NetworkConfig.PlayerPrefab == null)
            GUILayout.Label("<color=yellow>Player Prefab not assigned</color>");

        address = GUILayout.TextField(address);
        ushort.TryParse(GUILayout.TextField(port.ToString()), out port);

        GUI.enabled = networkManager && transport && networkManager.NetworkConfig.PlayerPrefab != null;

        if (GUILayout.Button("Start Host"))
        {
            transport.SetConnectionData(address, port);   // Unity 6/NGO 2.x.
            networkManager.StartHost();
        }

        if (GUILayout.Button("Start Client"))
        {
            transport.SetConnectionData(address, port);
            networkManager.StartClient();
        }

        if (GUILayout.Button("Shutdown"))
        {
            if (NetworkManager.Singleton) NetworkManager.Singleton.Shutdown();
        }

        GUI.enabled = true;
        GUILayout.EndArea();
    }
    */
}