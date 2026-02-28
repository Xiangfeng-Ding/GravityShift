using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LocalCrystalPickup : MonoBehaviour
{
    [SerializeField] private LocalCrystalGate linkedGate;
    [SerializeField] private bool alsoCountToLevelProgress = true;
    [SerializeField] private float spinSpeed = 92f;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobSpeed = 2f;

    private Vector3 baseLocalPosition;
    private bool collected;
    private int lastHandledFrame = -1;

    public LocalCrystalGate LinkedGate => linkedGate;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
        baseLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        collected = false;
        baseLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.localPosition = baseLocalPosition + Vector3.up * bobOffset;
    }

    public void Configure(LocalCrystalGate gate, bool includeInLevelProgress)
    {
        linkedGate = gate;
        alsoCountToLevelProgress = includeInLevelProgress;
    }

    private void OnTriggerEnter(Collider other)
    {
        lastHandledFrame = Time.frameCount;
        TryCollect(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (lastHandledFrame == Time.frameCount)
        {
            return;
        }

        lastHandledFrame = Time.frameCount;
        TryCollect(other);
    }

    private void TryCollect(Collider other)
    {
        if (collected || other == null)
        {
            return;
        }

        GameDirector director = GameDirector.Instance;
        if (director != null && director.State != GameState.Playing)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerGravityMotor>() == null)
        {
            return;
        }

        collected = true;

        if (linkedGate != null)
        {
            linkedGate.RegisterCrystalCollected();
        }

        if (alsoCountToLevelProgress)
        {
            LevelProgress.Instance?.AddCrystal();
        }

        Destroy(gameObject);
    }
}
