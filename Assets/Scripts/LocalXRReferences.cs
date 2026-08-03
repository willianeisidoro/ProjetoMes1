using UnityEngine;

public sealed class LocalXRReferences :
    MonoBehaviour
{
    public static LocalXRReferences Instance
    {
        get;
        private set;
    }

    [Header("Local XR Rig")]
    [SerializeField]
    private Transform head;

    [SerializeField]
    private Transform leftHand;

    [SerializeField]
    private Transform rightHand;

    public Transform Head => head;
    public Transform LeftHand => leftHand;
    public Transform RightHand => rightHand;

    private void Awake()
    {
        if (
            Instance != null
            && Instance != this
        )
        {
            Debug.LogError(
                "Existe mais de um " +
                "LocalXRReferences na cena."
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}