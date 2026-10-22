using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Brawler.Presentation
{
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        public const int ExecutionOrder = 100;

        [SerializeField] private Transform _target;
        [SerializeField] private CameraRigSettings _settings;
        [SerializeField] private Transform _pivot;
        [SerializeField] private Camera _camera;
        [SerializeField, HideInInspector] private CinemachineCamera _defaultCamera;

        private CinemachineCamera _framingCamera;

        public Camera Camera => _camera;
        public event Action BeforeSnap;

        private void Awake()
        {
            ApplySettings();
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void OnValidate()
        {
            if (_defaultCamera != null)
            {
                ConfigureCamera(_defaultCamera, 0f, Vector3.zero);
            }
            else if (_settings != null)
            {
                if (_pivot != null)
                {
                    _pivot.localRotation = Quaternion.Euler(_settings.Pitch, _settings.Yaw, 0f);
                }

                if (_camera != null)
                {
                    _camera.transform.localPosition = Vector3.back * _settings.Distance;
                    _camera.fieldOfView = _settings.FieldOfView;
                }
            }
        }

        public void SnapToTarget()
        {
            if (_target == null || _camera == null)
            {
                return;
            }

            BeforeSnap?.Invoke();
            CinemachineCamera activeCamera = null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var virtualCamera in root.GetComponentsInChildren<CinemachineCamera>(true))
                {
                    if (virtualCamera.Follow != _target)
                    {
                        continue;
                    }

                    CenterCamera(virtualCamera);
                    virtualCamera.PreviousStateIsValid = false;
                    virtualCamera.InternalUpdateCameraState(Vector3.up, -1f);
                    if (virtualCamera.isActiveAndEnabled && (activeCamera == null || virtualCamera.Priority.Value > activeCamera.Priority.Value))
                    {
                        activeCamera = virtualCamera;
                    }
                }
            }

            _camera.GetComponent<CinemachineBrain>()?.ResetState();
            if (activeCamera != null)
            {
                _camera.transform.SetPositionAndRotation(activeCamera.State.GetFinalPosition(), activeCamera.State.GetFinalOrientation());
            }
        }

        public void SetFraming(float extraDistance, Vector3 extraOffset, float extraYaw = 0f)
        {
            if (extraDistance == 0f && extraOffset == Vector3.zero && extraYaw == 0f)
            {
                if (_framingCamera != null)
                {
                    _framingCamera.Priority = -1;
                    _framingCamera.Prioritize();
                }

                return;
            }

            if (_framingCamera == null)
            {
                _framingCamera = CreateCamera(transform, "Framing Camera", extraDistance, extraOffset, extraYaw);
            }

            ConfigureCamera(_framingCamera, extraDistance, extraOffset, extraYaw);
            _framingCamera.Priority = 10;
            _framingCamera.Prioritize();
        }

        [ContextMenu("Apply Settings")]
        public void ApplySettings()
        {
            if (_camera == null)
            {
                return;
            }

            if (_camera.transform != transform)
            {
                _camera.transform.SetParent(transform, true);
            }

            var brain = _camera.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = _camera.gameObject.AddComponent<CinemachineBrain>();
            }

            brain.IgnoreTimeScale = false;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.8f);
            if (_defaultCamera == null)
            {
                _defaultCamera = CreateCamera(transform, "Follow Camera", 0f, Vector3.zero);
            }

            ConfigureCamera(_defaultCamera, 0f, Vector3.zero);
            _defaultCamera.Priority = 0;
            _defaultCamera.Prioritize();
        }

        public CinemachineCamera CreateCamera(Transform parent, string cameraName, float extraDistance, Vector3 extraOffset, float extraYaw = 0f,
                                              float pitch = 0f, float fieldOfView = 0f)
        {
            var cameraObject = new GameObject(cameraName);
            cameraObject.transform.SetParent(parent, false);
            var virtualCamera = cameraObject.AddComponent<CinemachineCamera>();
            cameraObject.AddComponent<CinemachinePositionComposer>();
            var listener = cameraObject.AddComponent<CinemachineImpulseListener>();
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.UseCameraSpace = true;
            listener.ApplyAfter = CinemachineCore.Stage.Noise;
            listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.UseLargest;
            virtualCamera.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            virtualCamera.Priority = -1;
            ConfigureCamera(virtualCamera, extraDistance, extraOffset, extraYaw, pitch, fieldOfView);
            return virtualCamera;
        }

        /// <param name="pitch">Pitch override in degrees; 0 keeps the settings' pitch.</param>
        /// <param name="fieldOfView">Vertical field of view override; 0 keeps the settings' value.</param>
        public void ConfigureCamera(CinemachineCamera virtualCamera, float extraDistance, Vector3 extraOffset, float extraYaw = 0f,
                                    float pitch = 0f, float fieldOfView = 0f)
        {
            if (pitch <= 0f)
            {
                pitch = _settings != null ? _settings.Pitch : 25f;
            }

            if (fieldOfView <= 0f)
            {
                fieldOfView = _settings != null ? _settings.FieldOfView : 30f;
            }

            float yaw = (_settings != null ? _settings.Yaw : 0f) + extraYaw;
            float distance = Mathf.Max(0.1f, (_settings != null ? _settings.Distance : 11f) + extraDistance);
            float damping = _settings != null ? _settings.FollowSmoothTime : 0.12f;
            virtualCamera.Follow = _target;
            virtualCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            var lens = _camera != null ? LensSettings.FromCamera(_camera) : LensSettings.Default;
            lens.FieldOfView = fieldOfView;
            virtualCamera.Lens = lens;

            var composer = virtualCamera.GetComponent<CinemachinePositionComposer>();
            composer.CameraDistance = distance;
            composer.CenterOnActivate = false;
            composer.DeadZoneDepth = 4.4f * Mathf.Sin(pitch * Mathf.Deg2Rad);
            composer.TargetOffset = (_settings != null ? _settings.TargetOffset : Vector3.up) + extraOffset;
            composer.Damping = new Vector3(damping, damping * 3f, damping);
            float screenHeight = 2f * distance * Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f);
            float deadZoneHeight = Mathf.Clamp01(2.8f * Mathf.Cos(pitch * Mathf.Deg2Rad) / screenHeight);
            composer.Composition = new ScreenComposerSettings
            {
                ScreenPosition = new Vector2(0f, deadZoneHeight * (0.6f / 2.8f - 0.5f)),
                DeadZone = new ScreenComposerSettings.DeadZoneSettings { Enabled = true, Size = new Vector2(0f, deadZoneHeight) },
                HardLimits = new ScreenComposerSettings.HardLimitSettings { Enabled = true, Size = new Vector2(0.9f, 0.9f) }
            };
            if (!virtualCamera.PreviousStateIsValid && _target != null)
            {
                CenterCamera(virtualCamera);
            }
        }

        private static void CenterCamera(CinemachineCamera virtualCamera)
        {
            var composer = virtualCamera.GetComponent<CinemachinePositionComposer>();
            var target = virtualCamera.Follow;
            Vector3 position = target.position + target.rotation * composer.TargetOffset
                - virtualCamera.transform.forward * composer.CameraDistance;
            virtualCamera.ForceCameraPosition(position, virtualCamera.transform.rotation);
        }
    }
}
