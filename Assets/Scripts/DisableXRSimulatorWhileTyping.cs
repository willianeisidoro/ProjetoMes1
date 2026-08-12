using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

[RequireComponent(typeof(TMP_InputField))]
public sealed class DisableXRSimulatorWhileTyping :
    MonoBehaviour
{
    [SerializeField]
    private XRInteractionSimulator simulator;

    private TMP_InputField inputField;

    private void Awake()
    {
        inputField =
            GetComponent<TMP_InputField>();

        if (simulator == null)
        {
            simulator =
                FindFirstObjectByType<
                    XRInteractionSimulator
                >();
        }

        inputField.onSelect.AddListener(
            OnInputSelected
        );

        inputField.onEndEdit.AddListener(
            OnInputFinished
        );
    }

    private void OnDestroy()
    {
        inputField.onSelect.RemoveListener(
            OnInputSelected
        );

        inputField.onEndEdit.RemoveListener(
            OnInputFinished
        );
    }

    private void OnInputSelected(string _)
    {
        if (simulator != null)
        {
            simulator.enabled = false;
        }
    }

    private void OnInputFinished(string _)
    {
        if (simulator != null)
        {
            simulator.enabled = true;
        }
    }
}