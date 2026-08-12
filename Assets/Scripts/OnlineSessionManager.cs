using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PublicSessionSummary
{
    public string Id
    {
        get;
    }

    public string Name
    {
        get;
    }

    public int MaxPlayers
    {
        get;
    }

    public PublicSessionSummary(
        string id,
        string name,
        int maxPlayers
    )
    {
        Id = id;
        Name = name;
        MaxPlayers = maxPlayers;
    }
}

public sealed class OnlineSessionManager : MonoBehaviour
{
    public static OnlineSessionManager Instance
    {
        get;
        private set;
    }

    [Header("Session")]
    [SerializeField]
    [Range(2, 4)]
    private int maxPlayers = 4;

    [SerializeField]
    private string defaultRoomName = "Sala Virtuex";

    [Header("Scenes")]
    [SerializeField]
    private string menuSceneName = "OnlineMenu";

    [SerializeField]
    private string gameplaySceneName = "VRGame";

    [Header("Compatibility")]
    [Tooltip(
        "Clientes e Host precisam usar exatamente " +
        "a mesma versão."
    )]
    [SerializeField]
    private string buildVersion = "semana-3-v1";

    public ISession CurrentSession
    {
        get;
        private set;
    }

    public bool IsInitialized
    {
        get;
        private set;
    }

    public bool OperationInProgress
    {
        get;
        private set;
    }

    public string StatusMessage
    {
        get;
        private set;
    } = "Inicializando serviços...";

    public bool HasSession =>
        CurrentSession != null;

    public bool IsHost =>
        CurrentSession != null
        && CurrentSession.IsHost;

    public string JoinCode =>
        CurrentSession != null
            ? CurrentSession.Code
            : string.Empty;

    public string PlayerId =>
        AuthenticationService.Instance.IsSignedIn
            ? AuthenticationService.Instance.PlayerId
            : string.Empty;

    public string AuthenticationProfile =>
        AuthenticationService.Instance.Profile;

    public int CurrentPlayerCount =>
        CurrentSession != null
            ? CurrentSession.Players.Count
            : 0;

    public event Action StateChanged;

    public event Action<
        IReadOnlyList<PublicSessionSummary>
    > PublicSessionsChanged;

    private QuerySessionsResults lastQueryResults;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        /*
         * Como NetworkManager, UnityTransport e este
         * script estão no mesmo objeto, todos continuam
         * existindo durante a troca de cena.
         */
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        ConfigureNetworkManager();

        await InitializeServicesAsync();
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton
                .OnClientDisconnectCallback
                -= OnClientDisconnected;

            NetworkManager.Singleton
                .ConnectionApprovalCallback
                -= ApprovalCheck;
        }

        lastQueryResults?.StopPolling();

        Instance = null;
    }

    /*
     * Cada instância local precisa usar um perfil
     * de Authentication diferente.
     *
     * Multiplayer Play Mode normalmente executa
     * as instâncias em caminhos diferentes.
     *
     * Criamos um hash determinístico do caminho
     * do projeto para obter um nome de perfil distinto.
     */
    private static string CreateLocalProfileName()
    {
        const uint initialHash = 2166136261;
        const uint prime = 16777619;

        uint hash = initialHash;

        string source = Application.dataPath;

        foreach (char currentCharacter in source)
        {
            hash ^= currentCharacter;
            hash *= prime;
        }

        return $"player_{hash:X8}";
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            SetStatus(
                "Inicializando Unity Gaming Services..."
            );

            if (
                UnityServices.State
                != ServicesInitializationState.Initialized
            )
            {
                InitializationOptions options =
                    new InitializationOptions();

                options.SetProfile(
                    CreateLocalProfileName()
                );

                await UnityServices.InitializeAsync(
                    options
                );
            }

            SetStatus(
                "Autenticando jogador..."
            );

            if (
                !AuthenticationService
                    .Instance
                    .IsSignedIn
            )
            {
                await AuthenticationService
                    .Instance
                    .SignInAnonymouslyAsync();
            }

            IsInitialized = true;

            SetStatus(
                $"Autenticado. Player ID: {PlayerId}"
            );
        }
        catch (Exception exception)
        {
            IsInitialized = false;

            SetStatus(
                $"Falha ao inicializar serviços: " +
                $"{exception.Message}"
            );

            Debug.LogException(exception);
        }
    }

    private void ConfigureNetworkManager()
    {
        NetworkManager manager =
            NetworkManager.Singleton;

        if (manager == null)
        {
            Debug.LogError(
                "NetworkManager não foi encontrado."
            );

            return;
        }

        manager.NetworkConfig.ConnectionApproval =
            true;

        manager.NetworkConfig.ConnectionData =
            Encoding.UTF8.GetBytes(buildVersion);

        manager.ConnectionApprovalCallback +=
            ApprovalCheck;

        manager.OnClientDisconnectCallback +=
            OnClientDisconnected;
    }

    /*
     * Esta verificação é executada pelo Host.
     *
     * Neste primeiro exercício, verificamos apenas
     * se o cliente está usando a mesma versão.
     */
    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response
    )
    {
        string clientVersion =
            Encoding.UTF8.GetString(
                request.Payload
            );

        bool versionIsValid =
            clientVersion == buildVersion;

        response.Approved =
            versionIsValid;

        response.CreatePlayerObject =
            versionIsValid;

        response.Pending = false;

        if (!versionIsValid)
        {
            response.Reason =
                $"Versão incompatível. " +
                $"Host: {buildVersion}. " +
                $"Cliente: {clientVersion}.";
        }
    }

    public async Task CreatePublicSessionAsync(
        string requestedRoomName
    )
    {
        await RunOperationAsync(
            "Criando sala e alocando Relay...",
            async () =>
            {
                EnsureCanEnterSession();

                string roomName =
                    string.IsNullOrWhiteSpace(
                        requestedRoomName
                    )
                        ? defaultRoomName
                        : requestedRoomName.Trim();

                SessionOptions options =
                    new SessionOptions
                    {
                        Name = roomName,
                        MaxPlayers = maxPlayers,
                        IsPrivate = false
                    }
                    .WithRelayNetwork();

                /*
                 * Esta chamada:
                 *
                 * 1. cria a Session/Lobby;
                 * 2. cria a alocação Relay;
                 * 3. configura o UnityTransport;
                 * 4. inicia o NGO como Host.
                 */
                CurrentSession =
                    await MultiplayerService
                        .Instance
                        .CreateSessionAsync(options);

                SetStatus(
                    $"Sala criada. Código: " +
                    $"{CurrentSession.Code}"
                );
            }
        );
    }

    public async Task JoinByCodeAsync(
        string requestedJoinCode
    )
    {
        await RunOperationAsync(
            "Entrando na sala pelo código...",
            async () =>
            {
                EnsureCanEnterSession();

                string normalizedCode =
                    requestedJoinCode
                        .Trim()
                        .ToUpperInvariant();

                if (
                    string.IsNullOrWhiteSpace(
                        normalizedCode
                    )
                )
                {
                    throw new InvalidOperationException(
                        "Digite um código de entrada."
                    );
                }

                CurrentSession =
                    await MultiplayerService
                        .Instance
                        .JoinSessionByCodeAsync(
                            normalizedCode
                        );

                SetStatus(
                    $"Conectado à sala " +
                    $"{CurrentSession.Name}."
                );
            }
        );
    }

    public async Task JoinBySessionIdAsync(
        string sessionId
    )
    {
        await RunOperationAsync(
            "Entrando na sala selecionada...",
            async () =>
            {
                EnsureCanEnterSession();

                if (
                    string.IsNullOrWhiteSpace(
                        sessionId
                    )
                )
                {
                    throw new InvalidOperationException(
                        "Session ID inválido."
                    );
                }

                CurrentSession =
                    await MultiplayerService
                        .Instance
                        .JoinSessionByIdAsync(
                            sessionId
                        );

                SetStatus(
                    $"Conectado à sala " +
                    $"{CurrentSession.Name}."
                );
            }
        );
    }

    public async Task RefreshPublicSessionsAsync()
    {
        await RunOperationAsync(
            "Procurando salas públicas...",
            async () =>
            {
                EnsureInitialized();

                lastQueryResults?.StopPolling();

                QuerySessionsOptions options =
                    new QuerySessionsOptions();

                lastQueryResults =
                    await MultiplayerService
                        .Instance
                        .QuerySessionsAsync(options);

                List<PublicSessionSummary> summaries =
                    new List<PublicSessionSummary>();

                foreach (
                    var sessionInfo
                    in lastQueryResults.Sessions
                )
                {
                    summaries.Add(
                        new PublicSessionSummary(
                            sessionInfo.Id,
                            sessionInfo.Name,
                            sessionInfo.MaxPlayers
                        )
                    );
                }

                PublicSessionsChanged?.Invoke(
                    summaries
                );

                SetStatus(
                    $"{summaries.Count} sala(s) " +
                    $"pública(s) encontrada(s)."
                );
            }
        );
    }

    public void StartGameplay()
    {
        if (!HasSession)
        {
            SetStatus(
                "Entre em uma sala antes de iniciar."
            );

            return;
        }

        if (!IsHost)
        {
            SetStatus(
                "Somente o Host pode iniciar a partida."
            );

            return;
        }

        NetworkManager manager =
            NetworkManager.Singleton;

        if (
            manager == null
            || !manager.IsListening
            || manager.SceneManager == null
        )
        {
            SetStatus(
                "A conexão de rede ainda não está pronta."
            );

            return;
        }

        /*
         * Não use SceneManager.LoadScene aqui.
         *
         * O Host solicita o carregamento pelo NGO,
         * que envia a mudança aos clientes.
         */
        SceneEventProgressStatus result =
            manager.SceneManager.LoadScene(
                gameplaySceneName,
                LoadSceneMode.Single
            );

        if (
            result
            != SceneEventProgressStatus.Started
        )
        {
            SetStatus(
                $"Não foi possível iniciar a cena. " +
                $"Resultado: {result}."
            );

            return;
        }

        SetStatus(
            "Carregando cena para todos..."
        );
    }

    public async Task LeaveSessionAsync()
    {
        await RunOperationAsync(
            "Saindo da sessão...",
            async () =>
            {
                if (CurrentSession != null)
                {
                    if (CurrentSession.IsHost)
                    {
                        /*
                         * O Host encerra a sala para todos.
                         */
                        await CurrentSession
                            .AsHost()
                            .DeleteAsync();
                    }
                    else
                    {
                        await CurrentSession
                            .LeaveAsync();
                    }
                }

                CurrentSession = null;

                SetStatus(
                    "Sessão encerrada."
                );

                if (
                    SceneManager
                        .GetActiveScene()
                        .name
                    != menuSceneName
                )
                {
                    SceneManager.LoadScene(
                        menuSceneName
                    );
                }
            }
        );
    }

    public async Task ReconnectLastSessionAsync()
    {
        await RunOperationAsync(
            "Procurando sessão anterior...",
            async () =>
            {
                EnsureInitialized();

                List<string> joinedSessionIds =
                    await MultiplayerService
                        .Instance
                        .GetJoinedSessionIdsAsync();

                if (joinedSessionIds.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Nenhuma sessão disponível " +
                        "para reconexão."
                    );
                }

                string sessionId =
                    joinedSessionIds[0];

                CurrentSession =
                    await MultiplayerService
                        .Instance
                        .ReconnectToSessionAsync(
                            sessionId
                        );

                SetStatus(
                    $"Reconectado à sala " +
                    $"{CurrentSession.Name}."
                );
            }
        );
    }

    private void OnClientDisconnected(
        ulong disconnectedClientId
    )
    {
        NetworkManager manager =
            NetworkManager.Singleton;

        if (manager == null)
        {
            return;
        }

        /*
        * O Host também recebe callback quando
        * outro jogador sai.
        *
        * Nesse caso, não queremos mandar o Host
        * de volta para o menu.
        */
        if (
            disconnectedClientId
            != manager.LocalClientId
        )
        {
            StateChanged?.Invoke();
            return;
        }

        string reason =
            manager.DisconnectReason;

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason =
                "A conexão com a sessão foi encerrada.";
        }

        /*
        * Não consideramos mais que existe uma
        * Session ativa localmente.
        */
        CurrentSession = null;

        SetStatus(reason);

        /*
        * Se a queda aconteceu durante VRGame,
        * voltamos localmente ao menu.
        *
        * Aqui usamos o SceneManager normal porque
        * a conexão de rede já foi perdida.
        */
        if (
            SceneManager
                .GetActiveScene()
                .name
            != menuSceneName
        )
        {
            SceneManager.LoadScene(
                menuSceneName
            );
        }
    }

    private async Task RunOperationAsync(
        string initialStatus,
        Func<Task> operation
    )
    {
        if (OperationInProgress)
        {
            SetStatus(
                "Já existe uma operação em andamento."
            );

            return;
        }

        OperationInProgress = true;
        SetStatus(initialStatus);

        try
        {
            await operation();
        }
        catch (SessionException exception)
        {
            SetStatus(
                $"Erro do serviço multiplayer: " +
                $"{exception.Message}"
            );

            Debug.LogException(exception);
        }
        catch (Exception exception)
        {
            SetStatus(
                $"Falha: {exception.Message}"
            );

            Debug.LogException(exception);
        }
        finally
        {
            OperationInProgress = false;
            StateChanged?.Invoke();
        }
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                "Unity Gaming Services ainda " +
                "não foi inicializado."
            );
        }
    }

    private void EnsureCanEnterSession()
    {
        EnsureInitialized();

        if (CurrentSession != null)
        {
            throw new InvalidOperationException(
                "Você já está em uma sessão."
            );
        }

        if (
            NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsListening
        )
        {
            throw new InvalidOperationException(
                "O NGO já possui uma conexão ativa."
            );
        }
    }

    private void SetStatus(
        string message
    )
    {
        StatusMessage = message;

        Debug.Log(
            $"[OnlineSession] {message}"
        );

        StateChanged?.Invoke();
    }
}