using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    public sealed class CraneGrabSequence : MonoBehaviour
    {
        public enum CraneState
        {
            Idle,
            Lowering,
            Closing,
            Lifting,
            MovingToDrop,
            Releasing,
            ReturningHome
        }

        [Header("References")]
        [SerializeField] CraneController controller;
        [SerializeField] Transform carriage;
        [SerializeField] Transform liftAssembly;
        [SerializeField] CraneClawController claw;
        [SerializeField] PrizeGripAssist gripAssist;
        [SerializeField] Transform homePosition;
        [SerializeField] Transform dropPosition;

        [Header("Timing and speed")]
        [SerializeField, Min(0.05f)] float lowerDistance = 3.65f;
        [SerializeField] bool clampLoweringByGripHeight = true;
        [SerializeField] float minimumGripHeight = 0.68f;
        [SerializeField, Min(0.05f)] float lowerSpeed = 0.85f;
        [SerializeField, Min(0.05f)] float liftSpeed = 0.9f;
        [SerializeField, Min(0.05f)] float horizontalSequenceSpeed = 1.35f;
        [SerializeField, Min(0f)] float settleBeforeClose = 0.25f;
        [SerializeField, Min(0f)] float holdAfterClose = 0.25f;
        [SerializeField, Min(0f)] float releaseWait = 0.45f;

        [Header("Events")]
        [SerializeField] UnityEvent onGrabStarted;
        [SerializeField] UnityEvent onMovingToDrop;
        [SerializeField] UnityEvent onPrizeReleased;
        [SerializeField] UnityEvent onReturnedHome;
        [SerializeField] UnityEvent onCraneStateChanged;

        Vector3 liftedLocalPosition;
        Coroutine routine;
        CraneState state = CraneState.Idle;

        public CraneState State => state;
        public bool IsBusy => routine != null || state != CraneState.Idle;

        void Awake()
        {
            if (liftAssembly != null)
            {
                liftedLocalPosition = liftAssembly.localPosition;
            }
        }

        public bool Begin()
        {
            if (IsBusy || controller == null || carriage == null || liftAssembly == null || claw == null)
            {
                return false;
            }
            routine = StartCoroutine(RunSequence());
            return true;
        }

        IEnumerator RunSequence()
        {
            controller.SetSequenceControl(false);
            onGrabStarted?.Invoke();

            SetState(CraneState.Lowering);
            if (settleBeforeClose > 0f)
            {
                yield return new WaitForSeconds(settleBeforeClose);
            }
            Vector3 lowered = CalculateLoweredLocalPosition();
            yield return MoveLocal(liftAssembly, lowered, lowerSpeed);

            SetState(CraneState.Closing);
            yield return claw.AnimateTo(1f);
            // Transform-driven claw rotation is consumed by physics on the next
            // fixed step. Wait before reading the two contact sensors.
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            if (gripAssist != null)
            {
                gripAssist.TryAttach();
            }
            if (holdAfterClose > 0f)
            {
                yield return new WaitForSeconds(holdAfterClose);
            }

            SetState(CraneState.Lifting);
            yield return MoveLocal(liftAssembly, liftedLocalPosition, liftSpeed);

            SetState(CraneState.MovingToDrop);
            onMovingToDrop?.Invoke();
            if (dropPosition != null)
            {
                yield return MoveHorizontal(carriage, dropPosition.position, horizontalSequenceSpeed);
            }

            SetState(CraneState.Releasing);
            yield return claw.AnimateTo(0f);
            if (gripAssist != null)
            {
                gripAssist.Release();
            }
            onPrizeReleased?.Invoke();
            if (releaseWait > 0f)
            {
                yield return new WaitForSeconds(releaseWait);
            }

            SetState(CraneState.ReturningHome);
            if (homePosition != null)
            {
                yield return MoveHorizontal(carriage, homePosition.position, horizontalSequenceSpeed);
            }

            routine = null;
            SetState(CraneState.Idle);
            controller.SetSequenceControl(true);
            onReturnedHome?.Invoke();
        }

        public void ResetInstant()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            if (gripAssist != null)
            {
                gripAssist.Release();
            }
            if (claw != null)
            {
                claw.OpenInstant();
            }
            if (liftAssembly != null)
            {
                liftAssembly.localPosition = liftedLocalPosition;
            }
            if (carriage != null && homePosition != null)
            {
                Vector3 position = carriage.position;
                position.x = homePosition.position.x;
                position.z = homePosition.position.z;
                carriage.position = position;
            }
            SetState(CraneState.Idle);
            if (controller != null)
            {
                controller.SetSequenceControl(true);
            }
        }

        IEnumerator MoveLocal(Transform target, Vector3 destination, float speed)
        {
            while ((target.localPosition - destination).sqrMagnitude > 0.000001f)
            {
                target.localPosition = Vector3.MoveTowards(target.localPosition, destination, speed * Time.deltaTime);
                yield return null;
            }
            target.localPosition = destination;
        }

        Vector3 CalculateLoweredLocalPosition()
        {
            float distance = lowerDistance;
            if (clampLoweringByGripHeight && gripAssist != null && gripAssist.GripAnchor != null && controller != null)
            {
                Transform space = controller.MovementSpace;
                float gripHeight = space.InverseTransformPoint(gripAssist.GripAnchor.position).y;
                distance = Mathf.Min(distance, Mathf.Max(0f, gripHeight - minimumGripHeight));
            }
            Debug.Log("Crane lowering distance: " + distance.ToString("F3"), this);
            return liftedLocalPosition + Vector3.down * distance;
        }

        IEnumerator MoveHorizontal(Transform target, Vector3 destination, float speed)
        {
            Vector3 targetPosition = new Vector3(destination.x, target.position.y, destination.z);
            while ((target.position - targetPosition).sqrMagnitude > 0.000001f)
            {
                target.position = Vector3.MoveTowards(target.position, targetPosition, speed * Time.deltaTime);
                yield return null;
            }
            target.position = targetPosition;
        }

        void SetState(CraneState next)
        {
            if (state == next)
            {
                return;
            }
            state = next;
            Debug.Log("Crane state: " + state, this);
            onCraneStateChanged?.Invoke();
        }

        public void Configure(CraneController newController, Transform newCarriage, Transform newLiftAssembly,
            CraneClawController newClaw, PrizeGripAssist newGripAssist, Transform home, Transform drop)
        {
            controller = newController;
            carriage = newCarriage;
            liftAssembly = newLiftAssembly;
            claw = newClaw;
            gripAssist = newGripAssist;
            homePosition = home;
            dropPosition = drop;
            liftedLocalPosition = newLiftAssembly != null ? newLiftAssembly.localPosition : Vector3.zero;
        }
    }
}
