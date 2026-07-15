using UnityEngine;

namespace MCT.CraneGame
{
    public interface ICraneCommandReceiver
    {
        void SetMoveInput(Vector2 input);
        bool TryStartGrab();
        void RequestReset();
    }
}
