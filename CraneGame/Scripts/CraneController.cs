using UnityEngine;
using UnityEngine.Events;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    public sealed class CraneController : MonoBehaviour, ICraneCommandReceiver
    {
        [Header("Movement")]
        [SerializeField] Transform movementRoot;
        [SerializeField] Transform movementSpace;
        [SerializeField, Min(0.01f)] float moveSpeed = 1.5f;
        [SerializeField] Vector2 xLimits = new Vector2(-1.7f, 1.7f);
        [SerializeField] Vector2 zLimits = new Vector2(-0.9f, 0.9f);

        [Header("Sequence")]
        [SerializeField] CraneGrabSequence grabSequence;
        [SerializeField] PrizeRespawner prizeRespawner;

        [Header("Events")]
        [SerializeField] UnityEvent onControlEnabled;
        [SerializeField] UnityEvent onControlDisabled;

        Vector2 moveInput;
        bool controlsEnabled = true;

        public bool ControlsEnabled => controlsEnabled && grabSequence != null && !grabSequence.IsBusy;
        public Vector2 MoveInput => moveInput;
        public Transform MovementRoot => movementRoot;
        public Transform MovementSpace => movementSpace != null ? movementSpace : transform;

        void Awake()
        {
            if (grabSequence == null)
            {
                Debug.LogWarning("CraneController: GrabSequence が未設定のため操作を停止します。", this);
                controlsEnabled = false;
            }
        }

        void Update()
        {
            if (!ControlsEnabled || movementRoot == null || moveInput.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Transform space = MovementSpace;
            Vector3 local = space.InverseTransformPoint(movementRoot.position);
            Vector2 input = Vector2.ClampMagnitude(moveInput, 1f);
            local.x = Mathf.Clamp(local.x + input.x * moveSpeed * Time.deltaTime, xLimits.x, xLimits.y);
            local.z = Mathf.Clamp(local.z + input.y * moveSpeed * Time.deltaTime, zLimits.x, zLimits.y);
            movementRoot.position = space.TransformPoint(local);
        }

        public void SetMoveInput(Vector2 input)
        {
            Vector2 next = ControlsEnabled ? Vector2.ClampMagnitude(input, 1f) : Vector2.zero;
            if ((next - moveInput).sqrMagnitude > 0.0001f)
            {
                Debug.Log(next.sqrMagnitude > 0.0001f
                    ? "Crane move input: " + next
                    : "Crane move stopped at local position: " +
                      (movementRoot != null ? MovementSpace.InverseTransformPoint(movementRoot.position).ToString() : "missing MovementRoot"), this);
            }
            moveInput = next;
        }

        public bool TryStartGrab()
        {
            if (!ControlsEnabled)
            {
                Debug.Log("Crane grab request ignored: sequence is busy.", this);
                return false;
            }

            moveInput = Vector2.zero;
            return grabSequence.Begin();
        }

        public void RequestReset()
        {
            moveInput = Vector2.zero;
            if (grabSequence != null)
            {
                grabSequence.ResetInstant();
            }
            if (prizeRespawner != null)
            {
                prizeRespawner.RespawnAll();
            }
            SetSequenceControl(true);
        }

        internal void SetSequenceControl(bool enabled)
        {
            bool changed = controlsEnabled != enabled;
            controlsEnabled = enabled;
            if (!enabled)
            {
                moveInput = Vector2.zero;
            }
            if (!changed)
            {
                return;
            }
            if (enabled)
            {
                onControlEnabled?.Invoke();
            }
            else
            {
                onControlDisabled?.Invoke();
            }
        }

        public void Configure(Transform newMovementRoot, Transform newMovementSpace, CraneGrabSequence sequence, PrizeRespawner respawner)
        {
            movementRoot = newMovementRoot;
            movementSpace = newMovementSpace;
            grabSequence = sequence;
            prizeRespawner = respawner;
        }

        public void SetLimits(Vector2 newXLimits, Vector2 newZLimits)
        {
            xLimits = newXLimits;
            zLimits = newZLimits;
        }

        void OnValidate()
        {
            if (xLimits.x > xLimits.y)
            {
                xLimits = new Vector2(xLimits.y, xLimits.x);
            }
            if (zLimits.x > zLimits.y)
            {
                zLimits = new Vector2(zLimits.y, zLimits.x);
            }
        }
    }
}
