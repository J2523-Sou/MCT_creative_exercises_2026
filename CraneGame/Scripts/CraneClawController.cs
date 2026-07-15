using System.Collections;
using UnityEngine;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    public sealed class CraneClawController : MonoBehaviour
    {
        [SerializeField] Transform leftClaw;
        [SerializeField] Transform rightClaw;
        [SerializeField] Vector3 localRotationAxis = Vector3.forward;
        [SerializeField] float leftClosedAngle = -18f;
        [SerializeField] float rightClosedAngle = 18f;
        [SerializeField, Min(0.05f)] float closeDuration = 0.65f;

        Quaternion leftOpenRotation;
        Quaternion rightOpenRotation;
        float closedAmount;
        bool initialized;

        public float ClosedAmount => closedAmount;

        void Awake()
        {
            CaptureOpenPose();
        }

        public void CaptureOpenPose()
        {
            if (leftClaw != null)
            {
                leftOpenRotation = leftClaw.localRotation;
            }
            if (rightClaw != null)
            {
                rightOpenRotation = rightClaw.localRotation;
            }
            initialized = leftClaw != null && rightClaw != null;
            // The prefab already stores the authored open pose. Capturing it must not
            // rewrite the nested FBX transform during startup.
            closedAmount = 0f;
        }

        public IEnumerator AnimateTo(float targetAmount)
        {
            if (!initialized)
            {
                CaptureOpenPose();
            }
            float start = closedAmount;
            float elapsed = 0f;
            while (elapsed < closeDuration)
            {
                elapsed += Time.deltaTime;
                Apply(Mathf.Lerp(start, Mathf.Clamp01(targetAmount), elapsed / closeDuration));
                yield return null;
            }
            Apply(targetAmount);
        }

        public void OpenInstant()
        {
            Apply(0f);
        }

        public void Configure(Transform left, Transform right, Vector3 axis)
        {
            leftClaw = left;
            rightClaw = right;
            localRotationAxis = axis.sqrMagnitude > 0.001f ? axis.normalized : Vector3.forward;
            leftClosedAngle = -18f;
            rightClosedAngle = 18f;
            CaptureOpenPose();
        }

        void Apply(float amount)
        {
            closedAmount = Mathf.Clamp01(amount);
            if (leftClaw != null)
            {
                // Pre-multiply so localRotationAxis is interpreted in the parent link's
                // coordinate system. Both claw parents have mirrored X axes.
                leftClaw.localRotation = Quaternion.AngleAxis(leftClosedAngle * closedAmount, localRotationAxis) * leftOpenRotation;
            }
            if (rightClaw != null)
            {
                rightClaw.localRotation = Quaternion.AngleAxis(rightClosedAngle * closedAmount, localRotationAxis) * rightOpenRotation;
            }
        }
    }
}
