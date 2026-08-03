using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class NetworkGrabOwnership :
    NetworkBehaviour
{
    private const ulong NoHolder =
        ulong.MaxValue;

    public NetworkVariable<ulong> CurrentHolder =
        new NetworkVariable<ulong>(
            NoHolder,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable =
            GetComponent<XRGrabInteractable>();
    }

    public override void OnNetworkSpawn()
    {
        grabInteractable
            .selectEntered
            .AddListener(OnSelectEntered);

        grabInteractable
            .selectExited
            .AddListener(OnSelectExited);

        CurrentHolder.OnValueChanged +=
            OnHolderChanged;

        ApplyInteractionAvailability(
            CurrentHolder.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        grabInteractable
            .selectEntered
            .RemoveListener(OnSelectEntered);

        grabInteractable
            .selectExited
            .RemoveListener(OnSelectExited);

        CurrentHolder.OnValueChanged -=
            OnHolderChanged;
    }

    private void OnSelectEntered(
        SelectEnterEventArgs arguments
    )
    {
        if (!IsSpawned)
        {
            return;
        }

        RequestOwnershipRpc();
    }

    private void OnSelectExited(
        SelectExitEventArgs arguments
    )
    {
        if (!IsSpawned)
        {
            return;
        }

        ReleaseOwnershipRpc();
    }

    /*
     * O objeto começa pertencendo ao servidor.
     *
     * Por isso qualquer cliente precisa poder
     * solicitar ownership.
     */
    [Rpc(
        SendTo.Server,
        RequireOwnership = false
    )]
    private void RequestOwnershipRpc(
        RpcParams rpcParams = default
    )
    {
        ulong requester =
            rpcParams.Receive.SenderClientId;

        /*
         * O servidor atua como árbitro.
         */
        if (CurrentHolder.Value != NoHolder)
        {
            Debug.Log(
                $"Ownership negado para " +
                $"{requester}. " +
                $"Objeto já pertence a " +
                $"{CurrentHolder.Value}."
            );

            return;
        }

        CurrentHolder.Value = requester;

        NetworkObject.ChangeOwnership(
            requester
        );

        Debug.Log(
            $"Ownership entregue ao " +
            $"cliente {requester}."
        );
    }

    /*
     * RequireOwnership é false para cobrir
     * uma liberação muito rápida.
     *
     * O servidor valida o remetente.
     */
    [Rpc(
        SendTo.Server,
        RequireOwnership = false
    )]
    private void ReleaseOwnershipRpc(
        RpcParams rpcParams = default
    )
    {
        ulong requester =
            rpcParams.Receive.SenderClientId;

        if (
            CurrentHolder.Value
            != requester
        )
        {
            return;
        }

        CurrentHolder.Value = NoHolder;

        /*
         * O objeto volta para o servidor.
         */
        NetworkObject.RemoveOwnership();

        Debug.Log(
            $"Cliente {requester} " +
            $"soltou o objeto."
        );
    }

    private void OnHolderChanged(
        ulong previousHolder,
        ulong currentHolder
    )
    {
        ApplyInteractionAvailability(
            currentHolder
        );
    }

    private void ApplyInteractionAvailability(
        ulong currentHolder
    )
    {
        NetworkManager manager =
            NetworkManager.Singleton;

        if (
            manager == null
            || !manager.IsClient
        )
        {
            return;
        }

        ulong localClientId =
            manager.LocalClientId;

        /*
         * O cliente pode interagir quando:
         *
         * 1. o objeto está livre;
         * 2. ele próprio é o atual dono temporário.
         */
        bool canInteract =
            currentHolder == NoHolder
            || currentHolder == localClientId;

        grabInteractable.enabled =
            canInteract;
    }
}