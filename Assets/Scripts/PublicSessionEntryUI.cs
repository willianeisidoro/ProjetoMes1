using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PublicSessionEntryUI :
    MonoBehaviour
{
    [SerializeField]
    private TMP_Text label;

    [SerializeField]
    private Button joinButton;

    private string sessionId;
    private Action<string> joinAction;

    private void Awake()
    {
        joinButton.onClick.AddListener(
            OnJoinClicked
        );
    }

    private void OnDestroy()
    {
        joinButton.onClick.RemoveListener(
            OnJoinClicked
        );
    }

    public void Configure(
        PublicSessionSummary summary,
        Action<string> requestedJoinAction
    )
    {
        sessionId = summary.Id;
        joinAction = requestedJoinAction;

        string safeName =
            string.IsNullOrWhiteSpace(summary.Name)
                ? "Sala sem nome"
                : summary.Name;

        label.text =
            $"{safeName} — até " +
            $"{summary.MaxPlayers} jogadores";
    }

    private void OnJoinClicked()
    {
        if (
            string.IsNullOrWhiteSpace(sessionId)
            || joinAction == null
        )
        {
            return;
        }

        joinAction.Invoke(sessionId);
    }
}