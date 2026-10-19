using Brawler.Presentation;
using UnityEngine;

namespace Brawler.Level
{
    /// <summary>Applies the framing of the <see cref="CameraZone"/> the player is in to the <see cref="CameraRig"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraZoneDirector : MonoBehaviour
    {
        [SerializeField] private CameraRig _rig;

        [SerializeField] private Transform _player;

        private CameraZone _current;
        private bool _applied;

        private void LateUpdate()
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

            _current = zone;
            _applied = true;
            if (zone != null)
            {
                _rig.SetFraming(zone.ExtraDistance, zone.Offset, zone.Yaw);
            }
            else
            {
                _rig.SetFraming(0f, Vector3.zero);
            }
        }
    }
}
