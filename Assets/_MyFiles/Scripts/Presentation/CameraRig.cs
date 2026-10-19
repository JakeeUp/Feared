using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// Fixed-angle follow camera (Octopath / Paper Mario style). Hierarchy:
    /// <c>CameraRig</c> (this, follows the target) → <c>Pivot</c> (pitch/yaw) → <c>ShakeRoot</c> (ScreenShake offsets
    /// its localPosition) → <c>Main Camera</c> (local position (0, 0, -Distance)).
    /// </summary>
    /// <remarks>
    /// Follow runs in LateUpdate with <b>scaled</b> time (during hitstop the target is frozen, so the rig is too).
    /// Runs before <see cref="BillboardSprite"/> via <see cref="ExecutionOrder"/>.
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Script execution order. Late so the target has finished moving; billboards run after this.</summary>
        public const int ExecutionOrder = 100;

        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Transform to follow (the Player root). Null = the rig stays where it is.")]
        [SerializeField] private Transform _target;

        [Tooltip("Angle, distance, FOV and follow tuning.")]
        [SerializeField] private CameraRigSettings _settings;

        [Tooltip("Child that carries the pitch/yaw rotation. The camera sits under it (via ShakeRoot) at local (0, 0, -Distance).")]
        [SerializeField] private Transform _pivot;

        [Tooltip("The rig's camera. Its FOV is driven by the settings.")]
        [SerializeField] private Camera _camera;

        // ============ State ============

        /// <summary>SmoothDamp velocity (ref parameter); reset on snap.</summary>
        private Vector3 _followVelocity;

        /// <summary>SmoothDamp velocity for the (lazier) vertical follow.</summary>
        private float _followVelocityY;

        // Level framing on top of the settings (camera zones): current values ease toward the requested ones.
        private float _framingDistance;
        private Vector3 _framingOffset;
        private float _framingDistanceGoal;
        private Vector3 _framingOffsetGoal;
        private float _framingYaw;
        private float _framingYawGoal;

        /// <summary>How fast framing changes settle (1/s, exponential on real time).</summary>
        private const float FramingRate = 2.5f;

        /// <summary>Vertical follow smooth time = FollowSmoothTime × this.</summary>
        private const float VerticalLag = 3f;

        // ============ Properties ============

        /// <summary>The rig camera (for systems that need projection, e.g. future screen-space UI).</summary>
        public Camera Camera => _camera;

        /// <summary>Current SmoothDamp velocity (debug/inspection).</summary>
        public Vector3 FollowVelocity => _followVelocity;

        // ============ Lifecycle ============

        private void Awake()
        {
            ApplySettings();
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            UpdateFraming();
            if (_target == null)
            {
                return;
            }

            Vector3 goal = GetGoalPosition();
            float smoothTime = _settings != null ? _settings.FollowSmoothTime : 0f;
            if (smoothTime <= 0f)
            {
                transform.position = goal;
                return;
            }

            // Scaled time: the camera holds still during hitstop.
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            // Y follows lazier than X/Z so jumps don't bob the whole screen.
            Vector3 current = transform.position;
            Vector3 planar = Vector3.SmoothDamp(new Vector3(current.x, 0f, current.z), new Vector3(goal.x, 0f, goal.z), ref _followVelocity, smoothTime, Mathf.Infinity, dt);
            float y = Mathf.SmoothDamp(current.y, goal.y, ref _followVelocityY, smoothTime * VerticalLag, Mathf.Infinity, dt);
            transform.position = new Vector3(planar.x, y, planar.z);
        }

        private void OnValidate()
        {
            // Editor preview: lets you tweak the settings asset / rig in edit mode and see the result.
            ApplySettings();
        }

        // ============ API ============

        /// <summary>Teleports the rig to the target (plus offset) and clears follow velocity. Use on spawn / respawn / scene load.</summary>
        public void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            transform.position = GetGoalPosition();
            _followVelocity = Vector3.zero;
            _followVelocityY = 0f;
        }

        /// <summary>
        /// Applies <see cref="CameraRigSettings"/> to the hierarchy: pivot rotation (pitch, yaw), camera local position
        /// (0, 0, -Distance) and field of view. Called from Awake and OnValidate.
        /// </summary>
        /// <summary>
        /// Level framing on top of the settings: pulls the camera back by <paramref name="extraDistance"/>, shifts what it
        /// looks at by <paramref name="extraOffset"/> and swings it round by <paramref name="extraYaw"/> degrees (a slight
        /// angle shows the sides of the 3D set). Eases in over about a second. (0, zero, 0) restores the default.
        /// </summary>
        public void SetFraming(float extraDistance, Vector3 extraOffset, float extraYaw = 0f)
        {
            _framingDistanceGoal = extraDistance;
            _framingOffsetGoal = extraOffset;
            _framingYawGoal = extraYaw;
        }

        [ContextMenu("Apply Settings")]
        public void ApplySettings()
        {
            if (_settings == null)
            {
                return;
            }

            if (_pivot != null)
            {
                _pivot.localRotation = Quaternion.Euler(_settings.Pitch, _settings.Yaw + _framingYaw, 0f);
            }

            if (_camera != null)
            {
                _camera.transform.localPosition = new Vector3(0f, 0f, -(_settings.Distance + _framingDistance));
                _camera.fieldOfView = _settings.FieldOfView;
            }
        }

        // ============ Helpers ============

        private Vector3 GetGoalPosition()
        {
            Vector3 offset = _settings != null ? _settings.TargetOffset : Vector3.zero;
            return _target.position + offset + _framingOffset;
        }

        private void UpdateFraming()
        {
            if (Mathf.Approximately(_framingDistance, _framingDistanceGoal) && _framingOffset == _framingOffsetGoal
                && Mathf.Approximately(_framingYaw, _framingYawGoal))
            {
                return;
            }

            // Real time, so framing still settles during hitstop; exponential so it starts quick and lands soft.
            float t = 1f - Mathf.Exp(-FramingRate * Time.unscaledDeltaTime);
            _framingDistance = Mathf.Abs(_framingDistance - _framingDistanceGoal) < 0.01f ? _framingDistanceGoal : Mathf.Lerp(_framingDistance, _framingDistanceGoal, t);
            _framingOffset = (_framingOffset - _framingOffsetGoal).sqrMagnitude < 0.0001f ? _framingOffsetGoal : Vector3.Lerp(_framingOffset, _framingOffsetGoal, t);
            _framingYaw = Mathf.Abs(_framingYaw - _framingYawGoal) < 0.05f ? _framingYawGoal : Mathf.Lerp(_framingYaw, _framingYawGoal, t);
            if (_camera != null && _settings != null)
            {
                _camera.transform.localPosition = new Vector3(0f, 0f, -(_settings.Distance + _framingDistance));
            }

            if (_pivot != null && _settings != null)
            {
                _pivot.localRotation = Quaternion.Euler(_settings.Pitch, _settings.Yaw + _framingYaw, 0f);
            }
        }
    }
}
