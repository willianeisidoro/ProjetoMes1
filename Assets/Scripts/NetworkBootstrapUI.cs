using Unity.Netcode;
using UnityEngine;

public sealed class NetworkBootstrapUI : MonoBehaviour
{
    private void OnGUI()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            return;
        }

        GUILayout.BeginArea(
            new Rect(10, 10, 220, 190),
            GUI.skin.box
        );

        GUILayout.Label("Teste multiplayer local");

        bool networkIsStopped =
            !networkManager.IsClient &&
            !networkManager.IsServer;

        if (networkIsStopped)
        {
            if (GUILayout.Button("Start Host"))
            {
                networkManager.StartHost();
            }

            if (GUILayout.Button("Start Client"))
            {
                networkManager.StartClient();
            }

            if (GUILayout.Button("Start Server"))
            {
                networkManager.StartServer();
            }
        }
        else
        {
            string currentMode;

            if (networkManager.IsHost)
            {
                currentMode = "Host";
            }
            else if (networkManager.IsServer)
            {
                currentMode = "Server";
            }
            else
            {
                currentMode = "Client";
            }

            GUILayout.Label($"Modo: {currentMode}");
            GUILayout.Label(
                $"Client ID local: {networkManager.LocalClientId}"
            );

            if (GUILayout.Button("Desconectar"))
            {
                networkManager.Shutdown();
            }
        }

        GUILayout.EndArea();
    }
}
