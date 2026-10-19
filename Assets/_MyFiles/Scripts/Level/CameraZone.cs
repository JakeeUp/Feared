using System.Collections.Generic;
using UnityEngine;

namespace Brawler.Level
{
    /// <summary>
    /// A region of the level with its own camera framing (pulled back for grapple swings, looking ahead down a
    /// descent...). The <see cref="CameraZoneDirector"/> picks the zone the player stands in every frame, so walking
    /// back into an earlier room restores its framing too. Uses this object's BoxCollider bounds (a trigger).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraZone : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Tooltip("Extra camera distance on top of the default (units).")]
        [SerializeField] private float _extraDistance = 3f;

        [Tooltip("Shift of the camera's look point (units), e.g. +X to look ahead, +Y to see rings overhead.")]
        [SerializeField] private Vector3 _offset;

        [Tooltip("Extra camera yaw (degrees). A slight angle shows the sides of the 3D set; movement stays on the level's axes.")]
        [SerializeField, Range(-35f, 35f)] private float _yaw;

        [Tooltip("When zones overlap, the higher priority wins.")]
        [SerializeField] private int _priority;

        // ============ Runtime ============

        private static readonly List<CameraZone> Active = new List<CameraZone>();
        private BoxCollider _box;

        // ============ Properties ============

        /// <summary>Extra camera distance (units).</summary>
        public float ExtraDistance => _extraDistance;

        /// <summary>Look-point shift (units).</summary>
        public Vector3 Offset => _offset;

        /// <summary>Extra camera yaw (degrees).</summary>
        public float Yaw => _yaw;

        // ============ Lifecycle ============

        /// <summary>
        /// Clears the list on entering Play Mode. The project disables domain reload, so statics would otherwise keep
        /// destroyed objects from the previous session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
        }

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        // ============ API ============

        /// <summary>The highest-priority zone containing <paramref name="point"/>, or null.</summary>
        public static CameraZone Find(Vector3 point)
        {
            CameraZone best = null;
            for (int i = 0; i < Active.Count; i++)
            {
                CameraZone zone = Active[i];
                if (zone._box != null && zone._box.bounds.Contains(point) && (best == null || zone._priority > best._priority))
                {
                    best = zone;
                }
            }

            return best;
        }
    }
}
