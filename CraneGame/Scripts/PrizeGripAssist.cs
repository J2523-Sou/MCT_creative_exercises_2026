using UnityEngine;
using UnityEngine.Events;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    public sealed class PrizeGripAssist : MonoBehaviour
    {
        [SerializeField] Transform gripAnchor;
        [SerializeField] ClawContactSensor leftSensor;
        [SerializeField] ClawContactSensor rightSensor;
        [SerializeField] bool assistEnabled = true;
        [SerializeField, Min(0.1f)] float maximumFollowDistance = 1.1f;
        [SerializeField, Range(0f, 1f)] float slipChancePerSecond = 0.04f;
        [SerializeField, Min(0.1f)] float jointBreakForce = 5000f;
        [SerializeField, Min(0.1f)] float jointBreakTorque = 5000f;
        [SerializeField] UnityEvent onPrizeGrabbed;
        [SerializeField] UnityEvent onPrizeReleased;

        Prize heldPrize;
        FixedJoint heldJoint;

        public Prize HeldPrize => heldPrize;

        public bool TryAttach()
        {
            Release();
            if (!assistEnabled || gripAnchor == null || leftSensor == null || rightSensor == null)
            {
                Debug.Log("Grip assist disabled or missing claw contact sensors.", this);
                return false;
            }

            Prize closest = null;
            float closestDistance = float.MaxValue;
            foreach (Prize candidate in leftSensor.Contacts)
            {
                if (candidate == null || candidate.Body == null || !rightSensor.Contains(candidate))
                {
                    continue;
                }
                float distance = (candidate.transform.position - gripAnchor.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }

            if (closest == null)
            {
                Debug.Log("Grip assist found no shared prize. left=[" + leftSensor.DescribeContacts() +
                    "] right=[" + rightSensor.DescribeContacts() + "]", this);
                return false;
            }

#if UNITY_6000_0_OR_NEWER
            closest.Body.linearVelocity = Vector3.zero;
#else
            closest.Body.velocity = Vector3.zero;
#endif
            closest.Body.angularVelocity = Vector3.zero;
            heldPrize = closest;
            heldJoint = closest.gameObject.AddComponent<FixedJoint>();
            heldJoint.connectedBody = GetComponent<Rigidbody>();
            heldJoint.breakForce = jointBreakForce;
            heldJoint.breakTorque = jointBreakTorque;
            heldJoint.enableCollision = false;
            heldPrize.SetHeld(true);
            Debug.Log("Prize grabbed by both claws: " + heldPrize.name, heldPrize);
            onPrizeGrabbed?.Invoke();
            return true;
        }

        void FixedUpdate()
        {
            if (heldPrize == null || heldPrize.Body == null || gripAnchor == null)
            {
                return;
            }

            if (heldJoint == null)
            {
                Debug.Log("Prize released: grip joint broke.", heldPrize);
                Release();
                return;
            }

            Rigidbody body = heldPrize.Body;
            float distance = Vector3.Distance(body.position, gripAnchor.position);
            if (distance > maximumFollowDistance || Random.value < slipChancePerSecond * Time.fixedDeltaTime)
            {
                Debug.Log(distance > maximumFollowDistance
                    ? "Prize slipped: exceeded follow distance."
                    : "Prize slipped: gameplay variance.", heldPrize);
                Release();
                return;
            }

        }

        public void Release()
        {
            if (heldPrize != null)
            {
                if (heldJoint != null)
                {
                    Destroy(heldJoint);
                    heldJoint = null;
                }
                heldPrize.SetHeld(false);
                Debug.Log("Prize released: " + heldPrize.name, heldPrize);
                heldPrize = null;
                onPrizeReleased?.Invoke();
            }
        }

        public void Configure(Transform anchor, ClawContactSensor left, ClawContactSensor right)
        {
            gripAnchor = anchor;
            leftSensor = left;
            rightSensor = right;
        }

        void OnDrawGizmosSelected()
        {
            if (gripAnchor == null)
            {
                return;
            }
            Gizmos.color = new Color(1f, 0.65f, 0f, 0.35f);
            Gizmos.DrawWireSphere(gripAnchor.position, 0.08f);
        }
    }
}
