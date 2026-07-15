using UnityEngine;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CraneWorldButton : MonoBehaviour
    {
        public enum Command
        {
            MoveLeft,
            MoveRight,
            MoveForward,
            MoveBack,
            Grab,
            Reset
        }

        [SerializeField] CraneController receiver;
        [SerializeField] Command command;

        public void Press()
        {
            if (receiver == null)
            {
                return;
            }
            switch (command)
            {
                case Command.MoveLeft: receiver.SetMoveInput(Vector2.left); break;
                case Command.MoveRight: receiver.SetMoveInput(Vector2.right); break;
                case Command.MoveForward: receiver.SetMoveInput(Vector2.up); break;
                case Command.MoveBack: receiver.SetMoveInput(Vector2.down); break;
                case Command.Grab: receiver.TryStartGrab(); break;
                case Command.Reset: receiver.RequestReset(); break;
            }
        }

        public void Release()
        {
            if (receiver != null && command <= Command.MoveBack)
            {
                receiver.SetMoveInput(Vector2.zero);
            }
        }

        void OnMouseDown()
        {
            Press();
        }

        void OnMouseUp()
        {
            Release();
        }

        void OnDisable()
        {
            Release();
        }

        public void Configure(CraneController newReceiver, Command newCommand)
        {
            receiver = newReceiver;
            command = newCommand;
        }
    }
}
