using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class OnlineMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField]
    private GameObject mainPanel;

    [SerializeField]
    private GameObject waitingRoomPanel;

    [Header("General")]
    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private TMP_Text profileText;

    [Header("Create")]
    [SerializeField]
    private TMP_InputField roomNameInput;

    [SerializeField]
    private Button createRoomButton;

    [Header("Join by code")]
    [SerializeField]
    private TMP_InputField joinCodeInput;

    [SerializeField]
    private Button joinByCodeButton;

    [Header("Public sessions")]
    [SerializeField]
    private Button refreshRoomsButton;

    [SerializeField]
    private Transform publicRoomsContent;

    [SerializeField]
    private PublicSessionEntryUI entryPrefab;

    [Header("Recovery")]
    [SerializeField]
    private Button reconnectButton;

    [Header("Waiting room")]
    [SerializeField]
    private TMP_Text roomNameText;

    [SerializeField]
    private TMP_Text joinCodeText;

    [SerializeField]
    private TMP_Text playerCountText;

    [SerializeField]
    private Button startGameButton;

    [SerializeField]
    private Button leaveButton;

    private float nextPeriodicRefresh;

    private OnlineSessionManager Manager =>
        OnlineSessionManager.Instance;

    private void Awake()
    {
        createRoomButton.onClick.AddListener(
            OnCreateRoomClicked
        );

        joinByCodeButton.onClick.AddListener(
            OnJoinByCodeClicked
        );

        refreshRoomsButton.onClick.AddListener(
            OnRefreshRoomsClicked
        );

        reconnectButton.onClick.AddListener(
            OnReconnectClicked
        );

        startGameButton.onClick.AddListener(
            OnStartGameClicked
        );

        leaveButton.onClick.AddListener(
            OnLeaveClicked
        );
    }

    private void OnEnable()
    {
        TrySubscribe();
        RefreshVisualState();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void OnDestroy()
    {
        createRoomButton.onClick.RemoveListener(
            OnCreateRoomClicked
        );

        joinByCodeButton.onClick.RemoveListener(
            OnJoinByCodeClicked
        );

        refreshRoomsButton.onClick.RemoveListener(
            OnRefreshRoomsClicked
        );

        reconnectButton.onClick.RemoveListener(
            OnReconnectClicked
        );

        startGameButton.onClick.RemoveListener(
            OnStartGameClicked
        );

        leaveButton.onClick.RemoveListener(
            OnLeaveClicked
        );
    }

    private void Update()
    {
        /*
         * Isso não realiza uma requisição online.
         * Apenas relê o estado local da Session.
         */
        if (
            Time.unscaledTime
            < nextPeriodicRefresh
        )
        {
            return;
        }

        nextPeriodicRefresh =
            Time.unscaledTime + 0.25f;

        if (Manager == null)
        {
            TrySubscribe();
        }

        RefreshVisualState();
    }

    private void TrySubscribe()
    {
        if (Manager == null)
        {
            return;
        }

        Manager.StateChanged -=
            RefreshVisualState;

        Manager.StateChanged +=
            RefreshVisualState;

        Manager.PublicSessionsChanged -=
            RebuildPublicRooms;

        Manager.PublicSessionsChanged +=
            RebuildPublicRooms;
    }

    private void TryUnsubscribe()
    {
        if (Manager == null)
        {
            return;
        }

        Manager.StateChanged -=
            RefreshVisualState;

        Manager.PublicSessionsChanged -=
            RebuildPublicRooms;
    }

    private async void OnCreateRoomClicked()
    {
        if (Manager == null)
        {
            return;
        }

        await Manager.CreatePublicSessionAsync(
            roomNameInput.text
        );
    }

    private async void OnJoinByCodeClicked()
    {
        if (Manager == null)
        {
            return;
        }

        await Manager.JoinByCodeAsync(
            joinCodeInput.text
        );
    }

    private async void OnRefreshRoomsClicked()
    {
        if (Manager == null)
        {
            return;
        }

        await Manager
            .RefreshPublicSessionsAsync();
    }

    private async void OnReconnectClicked()
    {
        if (Manager == null)
        {
            return;
        }

        await Manager
            .ReconnectLastSessionAsync();
    }

    private void OnStartGameClicked()
    {
        Manager?.StartGameplay();
    }

    private async void OnLeaveClicked()
    {
        if (Manager == null)
        {
            return;
        }

        await Manager.LeaveSessionAsync();
    }

    private async void OnPublicRoomSelected(
        string sessionId
    )
    {
        if (Manager == null)
        {
            return;
        }

        await Manager.JoinBySessionIdAsync(
            sessionId
        );
    }

    private void RebuildPublicRooms(
        IReadOnlyList<PublicSessionSummary> rooms
    )
    {
        foreach (
            Transform existingChild
            in publicRoomsContent
        )
        {
            Destroy(
                existingChild.gameObject
            );
        }

        foreach (
            PublicSessionSummary room
            in rooms
        )
        {
            PublicSessionEntryUI entry =
                Instantiate(
                    entryPrefab,
                    publicRoomsContent
                );

            entry.Configure(
                room,
                OnPublicRoomSelected
            );
        }
    }

    private void RefreshVisualState()
    {
        if (Manager == null)
        {
            statusText.text =
                "OnlineSessionManager não encontrado.";

            return;
        }

        bool hasSession =
            Manager.HasSession;

        bool isBusy =
            Manager.OperationInProgress;

        mainPanel.SetActive(
            !hasSession
        );

        waitingRoomPanel.SetActive(
            hasSession
        );

        statusText.text =
            Manager.StatusMessage;

        profileText.text =
            Manager.IsInitialized
                ? $"Perfil: " +
                  $"{Manager.AuthenticationProfile}\n" +
                  $"Player ID: {Manager.PlayerId}"
                : "Autenticação pendente";

        createRoomButton.interactable =
            Manager.IsInitialized
            && !isBusy
            && !hasSession;

        joinByCodeButton.interactable =
            Manager.IsInitialized
            && !isBusy
            && !hasSession;

        refreshRoomsButton.interactable =
            Manager.IsInitialized
            && !isBusy
            && !hasSession;

        reconnectButton.interactable =
            Manager.IsInitialized
            && !isBusy
            && !hasSession;

        leaveButton.interactable =
            !isBusy
            && hasSession;

        startGameButton.gameObject.SetActive(
            hasSession
            && Manager.IsHost
        );

        startGameButton.interactable =
            hasSession
            && Manager.IsHost
            && !isBusy;

        if (!hasSession)
        {
            roomNameText.text =
                "Nenhuma sala";

            joinCodeText.text =
                "Código: —";

            playerCountText.text =
                "Jogadores: 0";

            return;
        }

        roomNameText.text =
            Manager.CurrentSession.Name;

        joinCodeText.text =
            $"Código: {Manager.JoinCode}";

        playerCountText.text =
            $"Jogadores: " +
            $"{Manager.CurrentPlayerCount}";
    }
}