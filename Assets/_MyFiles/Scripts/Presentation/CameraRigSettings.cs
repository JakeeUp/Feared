using UnityEngine;

namespace Brawler.Presentation
{
    [CreateAssetMenu(menuName = "Brawler/Presentation/Camera Rig Settings", fileName = "CameraRigSettings")]
    public sealed class CameraRigSettings : ScriptableObject
    {

        [Header("Angle")]
        [Tooltip("Downward pitch in degrees (applied to the rig pivot X rotation).")]
        [SerializeField, Range(0f, 89f)] private float _pitch = 25f;

        [Tooltip("Yaw in degrees. 0 = looking down world +Z, so input X maps to world X and input Y to world Z.")]
        [SerializeField, Range(-180f, 180f)] private float _yaw = 0f;

        [Tooltip("Camera distance from the pivot along its local -Z, in units.")]
        [SerializeField, Min(0.1f)] private float _distance = 14f;

        [Tooltip("Vertical field of view. Low FOV (~25-35) flattens perspective for the diorama look.")]
        [SerializeField, Range(10f, 90f)] private float _fieldOfView = 30f;

        [Header("Follow")]
        [Tooltip("World offset added to the target position (aim point, e.g. chest height).")]
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1f, 0f);

        [Tooltip("Cinemachine position damping in seconds. 0 = rigid follow.")]
        [SerializeField, Min(0f)] private float _followSmoothTime = 0.12f;

        public float Pitch => _pitch;

        public float Yaw => _yaw;

        public float Distance => _distance;

        public float FieldOfView => _fieldOfView;

        public Vector3 TargetOffset => _targetOffset;

        public float FollowSmoothTime => _followSmoothTime;
    }
}
