using UnityEngine;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class Prize : MonoBehaviour
    {
        [SerializeField] PrizeRespawner respawner;
        [SerializeField] bool acquired;

        Rigidbody body;
        bool held;
        float spawnedAt;

        public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();
        public bool IsHeld => held;
        public bool IsAcquired => acquired;
        public float Age => Time.time - spawnedAt;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            spawnedAt = Time.time;
            if (respawner != null)
            {
                respawner.Register(this);
            }
        }

        public void Configure(PrizeRespawner newRespawner)
        {
            respawner = newRespawner;
        }

        public void SetHeld(bool value)
        {
            held = value;
            if (held && respawner != null)
            {
                respawner.CancelRespawn(this);
            }
        }

        public void MarkAcquired()
        {
            acquired = true;
            held = false;
            if (respawner != null)
            {
                respawner.RequestRespawn(this);
            }
        }

        public void ResetPrize(Vector3 position, Quaternion rotation)
        {
            acquired = false;
            held = false;
            transform.SetPositionAndRotation(position, rotation);
            Rigidbody rb = Body;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
            spawnedAt = Time.time;
            gameObject.SetActive(true);
        }

        void OnDestroy()
        {
            if (respawner != null)
            {
                respawner.Unregister(this);
            }
        }
    }
}
