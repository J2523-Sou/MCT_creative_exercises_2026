using UnityEngine;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PrizeDropZone : MonoBehaviour
    {
        void Reset()
        {
            Collider target = GetComponent<Collider>();
            target.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            Prize prize = other.GetComponentInParent<Prize>();
            if (prize != null)
            {
                Debug.Log("DropZone entered: " + prize.name + ", held=" + prize.IsHeld, prize);
                if (!prize.IsHeld)
                {
                    prize.MarkAcquired();
                }
            }
        }
    }
}
