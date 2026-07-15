using System.Collections.Generic;
using UnityEngine;

namespace MCT.CraneGame
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ClawContactSensor : MonoBehaviour
    {
        readonly HashSet<Prize> contacts = new HashSet<Prize>();

        public IEnumerable<Prize> Contacts
        {
            get
            {
                RefreshContacts();
                contacts.RemoveWhere(prize => prize == null || !prize.gameObject.activeInHierarchy);
                return contacts;
            }
        }

        public bool Contains(Prize prize)
        {
            RefreshContacts();
            return prize != null && contacts.Contains(prize);
        }

        public string DescribeContacts()
        {
            RefreshContacts();
            if (contacts.Count == 0) return "none";
            List<string> names = new List<string>();
            foreach (Prize prize in contacts)
            {
                if (prize != null) names.Add(prize.name);
            }
            return string.Join(",", names);
        }

        void Awake()
        {
            Collider sensor = GetComponent<Collider>();
            sensor.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            AddContact(other);
        }

        void OnTriggerStay(Collider other)
        {
            AddContact(other);
        }

        void OnTriggerExit(Collider other)
        {
            Prize prize = other.GetComponentInParent<Prize>();
            if (prize != null)
            {
                contacts.Remove(prize);
            }
        }

        void OnDisable()
        {
            contacts.Clear();
        }

        void AddContact(Collider other)
        {
            Prize prize = other.GetComponentInParent<Prize>();
            if (prize != null && !prize.IsAcquired)
            {
                contacts.Add(prize);
            }
        }

        void RefreshContacts()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }

            contacts.Clear();
            Vector3 scale = transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Vector3 center = transform.TransformPoint(box.center);
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents, transform.rotation,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                AddContact(overlap);
            }
        }
    }
}
