using UnityEngine;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    public sealed class CraneStatusDisplay : MonoBehaviour
    {
        [SerializeField] CraneGrabSequence sequence;
        [SerializeField] TextMesh label;
        CraneGrabSequence.CraneState lastState = (CraneGrabSequence.CraneState)(-1);

        void Update()
        {
            if (sequence == null || label == null || lastState == sequence.State)
            {
                return;
            }
            lastState = sequence.State;
            label.text = sequence.State == CraneGrabSequence.CraneState.Idle
                ? "READY  /  WASD + SPACE"
                : sequence.State.ToString().ToUpperInvariant();
        }

        public void Configure(CraneGrabSequence newSequence, TextMesh newLabel)
        {
            sequence = newSequence;
            label = newLabel;
        }
    }
}
