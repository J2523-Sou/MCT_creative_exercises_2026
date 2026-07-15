using UnityEngine;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    public sealed class KeyboardCraneInputAdapter : MonoBehaviour
    {
        [SerializeField] CraneController receiver;
        [SerializeField] KeyCode grabKey = KeyCode.Space;
        [SerializeField] KeyCode alternateGrabKey = KeyCode.G;
        [SerializeField] KeyCode resetKey = KeyCode.R;

        void Update()
        {
            if (receiver == null)
            {
                return;
            }

            float x = 0f;
            float y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            receiver.SetMoveInput(new Vector2(x, y));

            if (Input.GetKeyDown(grabKey) || Input.GetKeyDown(alternateGrabKey))
            {
                receiver.TryStartGrab();
            }
            if (Input.GetKeyDown(resetKey))
            {
                receiver.RequestReset();
            }
        }

        void OnDisable()
        {
            if (receiver != null)
            {
                receiver.SetMoveInput(Vector2.zero);
            }
        }

        public void Configure(CraneController newReceiver)
        {
            receiver = newReceiver;
        }
    }
}
