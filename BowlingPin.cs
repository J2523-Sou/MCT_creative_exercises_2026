using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BowlingPin : MonoBehaviour
{
    [SerializeField] private BowlingPinManager manager;
    [SerializeField] private bool deactivateAtDespawnHeight = true;

    private Rigidbody pinRigidbody;
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;
    private bool countedAsDespawned;

    public bool CountedAsDespawned => countedAsDespawned;

    private void Awake()
    {
        pinRigidbody = GetComponent<Rigidbody>();
        CaptureRespawnPose();
    }

    private void Start()
    {
        if (manager == null)
        {
            manager = GetComponentInParent<BowlingPinManager>();
        }

        if (manager != null)
        {
            manager.RegisterPin(this);
        }
    }

    private void Update()
    {
        if (manager == null)
        {
            return;
        }

        if (countedAsDespawned)
        {
            return;
        }

        if (transform.position.y <= manager.DespawnHeight)
        {
            countedAsDespawned = true;
            manager.NotifyPinReachedDespawnHeight(this);

            if (deactivateAtDespawnHeight)
            {
                gameObject.SetActive(false);
            }
        }
    }

    public void CaptureRespawnPose()
    {
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;
    }

    public void Respawn()
    {
        gameObject.SetActive(true);
        countedAsDespawned = false;

        transform.SetPositionAndRotation(respawnPosition, respawnRotation);

        if (pinRigidbody == null)
        {
            pinRigidbody = GetComponent<Rigidbody>();
        }

        pinRigidbody.linearVelocity = Vector3.zero;
        pinRigidbody.angularVelocity = Vector3.zero;
        pinRigidbody.Sleep();
    }

    public void SetManager(BowlingPinManager pinManager)
    {
        manager = pinManager;
    }
}
