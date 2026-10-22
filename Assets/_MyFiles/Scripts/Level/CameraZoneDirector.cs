using Brawler.Player;
using Brawler.Presentation;
using Unity.Cinemachine;
using UnityEngine;

namespace Brawler.Level
{
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class CameraZoneDirector : MonoBehaviour
    {
        [SerializeField] private CameraRig _rig;

        [SerializeField] private Transform _player;

        private CameraZone _current;
        private PlayerController _controller;
        private CinemachineCamera _currentCamera;
        private bool _applied;

        private void OnEnable()
        {
            if (_rig != null)
            {
                _rig.BeforeSnap += Refresh;
                _controller = _player != null ? _player.GetComponent<PlayerController>() : null;
                if (_controller != null)
                {
                    _controller.Respawned += _rig.SnapToTarget;
                }
            }
        }

        private void OnDisable()
        {
            if (_rig != null)
            {
                _rig.BeforeSnap -= Refresh;
                if (_controller != null)
                {
                    _controller.Respawned -= _rig.SnapToTarget;
                }
            }

            if (_currentCamera != null)
            {
                _currentCamera.Priority = -1;
                _currentCamera.Prioritize();
            }

            _applied = false;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_rig == null || _player == null)
            {
                return;
            }

            CameraZone zone = CameraZone.Find(_player.position + Vector3.up * 0.5f);
            if (_applied && zone == _current)
            {
                return;
            }

            if (_currentCamera != null)
            {
                _currentCamera.Priority = -1;
                _currentCamera.Prioritize();
            }

            _current = zone;
            _applied = true;
            _currentCamera = zone != null ? zone.GetCamera(_rig) : null;
            if (_currentCamera != null)
            {
                _currentCamera.Priority = 10;
                _currentCamera.Prioritize();
            }
        }
    }
}
