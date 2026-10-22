using System.Collections.Generic;
using Brawler.Presentation;
using Unity.Cinemachine;
using UnityEngine;

namespace Brawler.Level
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraZone : MonoBehaviour
    {

        [Tooltip("Extra camera distance on top of the default (units).")]
        [SerializeField] private float _extraDistance = 3f;

        [Tooltip("Shift of the camera's look point (units), e.g. +X to look ahead, +Y to see rings overhead.")]
        [SerializeField] private Vector3 _offset;

        [Tooltip("Extra camera yaw (degrees). A slight angle shows the sides of the 3D set; movement stays on the level's axes.")]
        [SerializeField, Range(-35f, 35f)] private float _yaw;

        [Tooltip("Camera pitch for this zone (degrees); 0 keeps the default. Outdoor dioramas look down more steeply.")]
        [SerializeField, Range(0f, 80f)] private float _pitch;

        [Tooltip("Vertical field of view for this zone; 0 keeps the default. A narrow lens from far away flattens the scene into a miniature.")]
        [SerializeField, Range(0f, 90f)] private float _fieldOfView;

        [Tooltip("When zones overlap, the higher priority wins.")]
        [SerializeField] private int _priority;

        [SerializeField, HideInInspector] private CinemachineCamera _camera;

        private static readonly List<CameraZone> Active = new List<CameraZone>();
        private BoxCollider _box;

        public float ExtraDistance => _extraDistance;

        public Vector3 Offset => _offset;

        public float Yaw => _yaw;

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
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (_camera != null)
            {
                _camera.Priority = -1;
                _camera.Prioritize();
            }
        }

        public CinemachineCamera GetCamera(CameraRig rig)
        {
            if (_camera == null)
            {
                _camera = rig.CreateCamera(transform, "Room Camera", _extraDistance, _offset, _yaw, _pitch, _fieldOfView);
            }
            else
            {
                rig.ConfigureCamera(_camera, _extraDistance, _offset, _yaw, _pitch, _fieldOfView);
            }

            return _camera;
        }

        public static CameraZone Find(Vector3 point)
        {
            CameraZone best = null;
            for (int i = 0; i < Active.Count; i++)
            {
                CameraZone zone = Active[i];
                if (zone != null && zone._box != null && zone._box.bounds.Contains(point) && (best == null || zone._priority > best._priority))
                {
                    best = zone;
                }
            }

            return best;
        }
    }
}
