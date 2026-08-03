using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkVRAvatar :
    NetworkBehaviour
{
    [Header("Network Visuals")]
    [SerializeField]
    private Transform networkHead;

    [SerializeField]
    private Transform networkLeftHand;

    [SerializeField]
    private Transform networkRightHand;

    [Header("Hide for local owner")]
    [SerializeField]
    private Renderer[] renderersHiddenFromOwner;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }

        if (LocalXRReferences.Instance == null)
        {
            Debug.LogError(
                "O jogador local não encontrou " +
                "LocalXRReferences na cena."
            );

            return;
        }

        /*
         * O jogador não deve enxergar
         * sua própria cabeça por dentro.
         *
         * Essa ocultação é somente local.
         */
        SetLocalRenderersVisible(false);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            SetLocalRenderersVisible(true);
        }
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !IsOwner)
        {
            return;
        }

        LocalXRReferences local =
            LocalXRReferences.Instance;

        if (local == null)
        {
            return;
        }

        CopyPose(
            local.Head,
            networkHead
        );

        CopyPose(
            local.LeftHand,
            networkLeftHand
        );

        CopyPose(
            local.RightHand,
            networkRightHand
        );
    }

    private void CopyPose(
        Transform source,
        Transform destination
    )
    {
        if (
            source == null
            || destination == null
        )
        {
            return;
        }

        destination.SetPositionAndRotation(
            source.position,
            source.rotation
        );
    }

    private void SetLocalRenderersVisible(
        bool visible
    )
    {
        foreach (
            Renderer currentRenderer
            in renderersHiddenFromOwner
        )
        {
            if (currentRenderer != null)
            {
                currentRenderer.enabled =
                    visible;
            }
        }
    }
}