using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    public sealed class PrizeRespawner : MonoBehaviour
    {
        [SerializeField] Transform coordinateSpace;
        [SerializeField] Transform[] respawnPositions;
        [SerializeField] Vector3 localBoundsCenter = new Vector3(0f, 1.2f, 0f);
        [SerializeField] Vector3 localBoundsSize = new Vector3(3.8f, 4f, 2.8f);
        [SerializeField] float minimumY = -1.5f;
        [SerializeField, Min(0f)] float maximumLifetime = 90f;
        [SerializeField, Min(0f)] float respawnDelay = 1.2f;
        [SerializeField, Min(0.05f)] float separationRadius = 0.55f;
        [SerializeField] UnityEvent onPrizeRespawned;

        readonly List<Prize> prizes = new List<Prize>();
        readonly HashSet<Prize> pending = new HashSet<Prize>();
        float nextCheck;

        void Update()
        {
            if (Time.time < nextCheck)
            {
                return;
            }
            nextCheck = Time.time + 0.35f;
            for (int i = prizes.Count - 1; i >= 0; i--)
            {
                Prize prize = prizes[i];
                if (prize == null)
                {
                    prizes.RemoveAt(i);
                    continue;
                }
                if (prize.IsHeld || pending.Contains(prize))
                {
                    continue;
                }
                if (ShouldRespawn(prize))
                {
                    RequestRespawn(prize);
                }
            }
        }

        bool ShouldRespawn(Prize prize)
        {
            if (prize.transform.position.y < minimumY || prize.IsAcquired)
            {
                return true;
            }
            if (maximumLifetime > 0f && prize.Age >= maximumLifetime)
            {
                return true;
            }
            Transform space = coordinateSpace != null ? coordinateSpace : transform;
            Vector3 local = space.InverseTransformPoint(prize.transform.position) - localBoundsCenter;
            Vector3 half = localBoundsSize * 0.5f;
            return Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z;
        }

        public void Register(Prize prize)
        {
            if (prize != null && !prizes.Contains(prize))
            {
                prizes.Add(prize);
            }
        }

        public void Unregister(Prize prize)
        {
            prizes.Remove(prize);
            pending.Remove(prize);
        }

        public void RequestRespawn(Prize prize)
        {
            if (prize != null && pending.Add(prize))
            {
                StartCoroutine(RespawnAfterDelay(prize));
            }
        }

        public void CancelRespawn(Prize prize)
        {
            if (prize != null)
            {
                pending.Remove(prize);
            }
        }

        public void RespawnAll()
        {
            StopAllCoroutines();
            pending.Clear();
            for (int i = 0; i < prizes.Count; i++)
            {
                if (prizes[i] != null)
                {
                    RespawnNow(prizes[i], i);
                }
            }
        }

        IEnumerator RespawnAfterDelay(Prize prize)
        {
            if (respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay);
            }
            pending.Remove(prize);
            // A respawn may have been queued just before the claw captured the prize.
            // Never teleport a held prize out of the claw when that old delay expires.
            if (prize != null && !prize.IsHeld && ShouldRespawn(prize))
            {
                RespawnNow(prize, prizes.IndexOf(prize));
            }
        }

        void RespawnNow(Prize prize, int preferredIndex)
        {
            if (respawnPositions == null || respawnPositions.Length == 0)
            {
                Debug.LogWarning("PrizeRespawner: RespawnPosition が未設定です。", this);
                return;
            }

            int count = respawnPositions.Length;
            for (int offset = 0; offset < count; offset++)
            {
                int index = (Mathf.Max(0, preferredIndex) + offset) % count;
                Transform point = respawnPositions[index];
                if (point == null || IsOccupied(point.position, prize))
                {
                    continue;
                }
                prize.ResetPrize(point.position, point.rotation);
                Debug.Log("Prize respawned: " + prize.name + " -> " + point.name, prize);
                onPrizeRespawned?.Invoke();
                return;
            }

            Transform fallback = respawnPositions[Mathf.Abs(preferredIndex) % count];
            if (fallback != null)
            {
                Vector3 offset = Random.insideUnitSphere * (separationRadius * 0.45f);
                offset.y = Mathf.Abs(offset.y) + 0.15f;
                prize.ResetPrize(fallback.position + offset, fallback.rotation);
                Debug.Log("Prize respawned with separation offset: " + prize.name, prize);
                onPrizeRespawned?.Invoke();
            }
        }

        bool IsOccupied(Vector3 position, Prize except)
        {
            float threshold = separationRadius * separationRadius;
            for (int i = 0; i < prizes.Count; i++)
            {
                Prize other = prizes[i];
                if (other != null && other != except && other.gameObject.activeInHierarchy &&
                    (other.transform.position - position).sqrMagnitude < threshold)
                {
                    return true;
                }
            }
            return false;
        }

        public void Configure(Transform space, Transform[] positions)
        {
            coordinateSpace = space;
            respawnPositions = positions;
        }

        void OnDrawGizmosSelected()
        {
            Transform space = coordinateSpace != null ? coordinateSpace : transform;
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = space.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireCube(localBoundsCenter, localBoundsSize);
            Gizmos.matrix = old;
        }
    }
}
