using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class CubePlayer : NetworkBehaviour
{
    [SerializeField]
    private float playerSpacing = 3f;

    private Renderer cubeRenderer;

    private void Awake()
    {
        cubeRenderer = GetComponent<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        SetVisualColor();

        // Apenas o servidor decide a posição oficial.
        if (IsServer)
        {
            float xPosition =
                ((float)OwnerClientId * playerSpacing)
                - (playerSpacing / 2f);

            transform.position = new Vector3(
                xPosition,
                0.5f,
                0f
            );
        }
    }

    private void SetVisualColor()
    {
        if (cubeRenderer == null)
        {
            return;
        }

        // Host normalmente recebe ID 0.
        // O primeiro cliente normalmente recebe ID 1.
        cubeRenderer.material.color =
            OwnerClientId % 2 == 0
                ? Color.blue
                : Color.red;
    }
}