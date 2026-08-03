using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public sealed class CubePlayer : NetworkBehaviour
{
    [Header("Spawn")]
    [SerializeField]
    private float playerSpacing = 3f;

    [SerializeField]
    private float spawnHeight = 0.5f;

    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 4f;

    [Tooltip("Limites do cenário nos eixos X e Z.")]
    [SerializeField]
    private Vector2 worldLimits = new Vector2(7f, 5f);

    [Tooltip(
        "Mesmo sem mudança no input, reenviamos periodicamente."
    )]
    [SerializeField]
    private float inputRefreshInterval = 0.1f;

    [Header("Visual")]
    [SerializeField]
    private Renderer cubeRenderer;

    /*
     * Todos podem ler.
     * Somente o servidor pode escrever.
     */
    public NetworkVariable<int> Health =
        new NetworkVariable<int>(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> Score =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    /*
     * Este valor existe separadamente para cada
     * PlayerCube na instância do servidor.
     *
     * O cliente envia apenas sua intenção.
     * O servidor usa essa intenção para mover o objeto.
     */
    private Vector2 serverMoveInput;

    private Vector2 lastSentInput =
        new Vector2(float.NaN, float.NaN);

    private float nextInputRefreshTime;
    private Color ownerColor;

    private void Awake()
    {
        if (cubeRenderer == null)
        {
            cubeRenderer = GetComponent<Renderer>();
        }
    }

    public override void OnNetworkSpawn()
    {
        gameObject.name = $"Player_{OwnerClientId}";

        Health.OnValueChanged += OnHealthChanged;
        Score.OnValueChanged += OnScoreChanged;

        SetOwnerColor();

        /*
         * Apenas o servidor escolhe a posição oficial.
         */
        if (IsServer)
        {
            float xPosition =
                ((float)OwnerClientId * playerSpacing)
                - (playerSpacing / 2f);

            transform.position = new Vector3(
                xPosition,
                spawnHeight,
                0f
            );

            Health.Value = 100;
            Score.Value = 0;
        }

        ApplyVisualState();

        Debug.Log(
            $"Player criado. " +
            $"OwnerClientId: {OwnerClientId}, " +
            $"IsOwner local: {IsOwner}, " +
            $"IsServer: {IsServer}"
        );
    }

    public override void OnNetworkDespawn()
    {
        Health.OnValueChanged -= OnHealthChanged;
        Score.OnValueChanged -= OnScoreChanged;

        serverMoveInput = Vector2.zero;
    }

    private void Update()
    {
        /*
         * Este script existe em todas as cópias
         * de todos os jogadores.
         *
         * Somente a cópia local pertencente ao jogador
         * pode ler o teclado daquela janela.
         */
        if (!IsSpawned || !IsOwner)
        {
            return;
        }

        Vector2 currentInput = ReadKeyboardInput();

        bool inputChanged =
            currentInput != lastSentInput;

        bool refreshIsDue =
            Time.unscaledTime >= nextInputRefreshTime;

        if (inputChanged || refreshIsDue)
        {
            SubmitMoveInputRpc(currentInput);

            lastSentInput = currentInput;

            nextInputRefreshTime =
                Time.unscaledTime
                + inputRefreshInterval;
        }

        /*
         * Novo Input System.
         *
         * Não usamos Input.GetKeyDown,
         * pois o projeto está configurado para
         * Input System Package (New).
         */
        Keyboard keyboard = Keyboard.current;

        if (
            keyboard != null
            && keyboard.hKey.wasPressedThisFrame
        )
        {
            RequestDamageRpc(10);
        }
    }

    private void FixedUpdate()
    {
        /*
         * Somente o servidor altera
         * a posição oficial.
         */
        if (!IsSpawned || !IsServer)
        {
            return;
        }

        Vector3 direction = new Vector3(
            serverMoveInput.x,
            0f,
            serverMoveInput.y
        );

        Vector3 nextPosition =
            transform.position
            + direction
            * moveSpeed
            * Time.fixedDeltaTime;

        /*
         * O servidor impede que o jogador
         * saia da área definida.
         */
        nextPosition.x = Mathf.Clamp(
            nextPosition.x,
            -worldLimits.x,
            worldLimits.x
        );

        nextPosition.z = Mathf.Clamp(
            nextPosition.z,
            -worldLimits.y,
            worldLimits.y
        );

        nextPosition.y = spawnHeight;

        transform.position = nextPosition;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(direction);
        }
    }

    private Vector2 ReadKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (
            keyboard.aKey.isPressed
            || keyboard.leftArrowKey.isPressed
        )
        {
            horizontal -= 1f;
        }

        if (
            keyboard.dKey.isPressed
            || keyboard.rightArrowKey.isPressed
        )
        {
            horizontal += 1f;
        }

        if (
            keyboard.sKey.isPressed
            || keyboard.downArrowKey.isPressed
        )
        {
            vertical -= 1f;
        }

        if (
            keyboard.wKey.isPressed
            || keyboard.upArrowKey.isPressed
        )
        {
            vertical += 1f;
        }

        return Vector2.ClampMagnitude(
            new Vector2(horizontal, vertical),
            1f
        );
    }

    /*
     * Este método é chamado pelo cliente,
     * mas executado no servidor.
     *
     * RequireOwnership = true impede que um cliente
     * execute o RPC no cubo de outro jogador.
     */
    [Rpc(SendTo.Server, RequireOwnership = true)]
    private void SubmitMoveInputRpc(
        Vector2 requestedInput,
        RpcParams rpcParams = default
    )
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        /*
         * Verificação adicional:
         * o remetente precisa ser o dono do cubo.
         */
        if (senderClientId != OwnerClientId)
        {
            Debug.LogWarning(
                $"Cliente {senderClientId} tentou " +
                $"controlar o player de " +
                $"{OwnerClientId}."
            );

            return;
        }

        /*
         * Rejeita valores matematicamente inválidos.
         */
        if (
            float.IsNaN(requestedInput.x)
            || float.IsNaN(requestedInput.y)
            || float.IsInfinity(requestedInput.x)
            || float.IsInfinity(requestedInput.y)
        )
        {
            return;
        }

        /*
         * Mesmo que um cliente modificado envie
         * valores absurdos, o servidor limita o vetor.
         */
        serverMoveInput =
            Vector2.ClampMagnitude(
                requestedInput,
                1f
            );
    }

    /*
     * O cliente não modifica Health diretamente.
     * Ele solicita uma ação ao servidor.
     */
    [Rpc(SendTo.Server, RequireOwnership = true)]
    private void RequestDamageRpc(
        int requestedDamage,
        RpcParams rpcParams = default
    )
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        if (senderClientId != OwnerClientId)
        {
            return;
        }

        /*
         * O servidor não aceita qualquer valor
         * enviado pelo cliente.
         */
        int validatedDamage =
            Mathf.Clamp(
                requestedDamage,
                0,
                25
            );

        Health.Value = Mathf.Max(
            0,
            Health.Value - validatedDamage
        );
    }

    /*
     * Chamado pelo coletável,
     * mas somente no servidor.
     */
    public void AddScoreOnServer(int amount)
    {
        if (!IsServer || amount <= 0)
        {
            return;
        }

        Score.Value += amount;
    }

    private void OnHealthChanged(
        int previousValue,
        int currentValue
    )
    {
        Debug.Log(
            $"Player {OwnerClientId}: " +
            $"vida mudou de {previousValue} " +
            $"para {currentValue}."
        );

        ApplyVisualState();
    }

    private void OnScoreChanged(
        int previousValue,
        int currentValue
    )
    {
        Debug.Log(
            $"Player {OwnerClientId}: " +
            $"pontuação mudou de {previousValue} " +
            $"para {currentValue}."
        );
    }

    private void SetOwnerColor()
    {
        /*
         * OwnerClientId é ulong.
         *
         * Os parênteses são importantes:
         * sem eles, o compilador pode interpretar
         * incorretamente a relação entre % e switch.
         */
        ownerColor =
            (OwnerClientId % 3UL) switch
            {
                0UL => Color.blue,
                1UL => Color.red,
                _ => Color.green
            };
    }

    private void ApplyVisualState()
    {
        if (cubeRenderer == null)
        {
            return;
        }

        if (Health.Value <= 0)
        {
            cubeRenderer.material.color =
                Color.gray;

            return;
        }

        /*
         * Quanto menor a vida,
         * mais escuro o cubo fica.
         */
        float brightness = Mathf.Lerp(
            0.3f,
            1f,
            Health.Value / 100f
        );

        cubeRenderer.material.color =
            ownerColor * brightness;
    }

    private void OnGUI()
    {
        /*
         * Cada janela mostra somente os dados
         * do jogador local daquela janela.
         */
        if (!IsSpawned || !IsOwner)
        {
            return;
        }

        GUILayout.BeginArea(
            new Rect(10, 210, 290, 190),
            GUI.skin.box
        );

        GUILayout.Label(
            $"Jogador local: {OwnerClientId}"
        );

        GUILayout.Label(
            $"Vida: {Health.Value}/100"
        );

        GUILayout.Label(
            $"Pontuação: {Score.Value}"
        );

        GUILayout.Space(8);

        GUILayout.Label(
            "WASD ou setas: mover"
        );

        GUILayout.Label(
            "H: receber 10 de dano"
        );

        GUILayout.Space(8);

        GUILayout.Label(
            IsServer
                ? "Movimento oficial executado no servidor."
                : "Input enviado ao servidor por RPC."
        );

        GUILayout.EndArea();
    }
}