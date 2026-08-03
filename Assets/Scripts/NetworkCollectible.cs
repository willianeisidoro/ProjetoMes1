using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkCollectible :
    NetworkBehaviour
{
    [SerializeField]
    private int points = 1;

    private bool wasCollected;

    private void OnTriggerEnter(
        Collider other
    )
    {
        /*
         * A colisão pode ser percebida
         * em mais de uma instância.
         *
         * Somente o servidor conclui a coleta.
         */
        if (
            !IsServer
            || !IsSpawned
            || wasCollected
        )
        {
            return;
        }

        CubePlayer player =
            other.GetComponentInParent<CubePlayer>();

        if (
            player == null
            || !player.IsSpawned
        )
        {
            return;
        }

        /*
         * Trava imediatamente.
         *
         * Se dois jogadores encostarem no mesmo frame,
         * a primeira execução aceita pelo servidor vence.
         */
        wasCollected = true;

        player.AddScoreOnServer(points);

        Debug.Log(
            $"Player {player.OwnerClientId} coletou " +
            $"{gameObject.name}."
        );

        /*
         * Somente o servidor chama Despawn().
         */
        NetworkObject.Despawn();
    }
}