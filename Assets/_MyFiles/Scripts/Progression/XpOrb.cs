using Brawler.Core;
using UnityEngine;

namespace Brawler.Progression
{
    /// <summary>
    /// One glowing XP pickup. Bursts out of a defeated enemy, tumbles and settles on the floor, then homes in on the
    /// player once they're within the magnet radius (or after a while regardless) and hands its XP to the
    /// <see cref="PlayerProgression"/>. Owned and recycled by <see cref="XpOrbSpawner"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XpOrb : MonoBehaviour
    {
        // ============ Constants ============

        private const float Gravity = -18f;
        private const float Bounce = 0.35f;
        private const float GroundFriction = 6f;

        /// <summary>Homing acceleration (units/s^2); speed ramps up so the orb visibly gets sucked in.</summary>
        private const float HomingAcceleration = 30f;

        /// <summary>Collection distance from the target's chest (units).</summary>
        private const float CollectDistance = 0.25f;

        /// <summary>Height of the target's chest above its root (units).</summary>
        private const float ChestHeight = 0.5f;

        /// <summary>Max downward probe for the floor when dropped (units).</summary>
        private const float FloorProbe = 4f;

        // ============ State ============

        private ProgressionSettings _settings;
        private Transform _target;
        private PlayerProgression _collector;
        private Vector3 _velocity;
        private float _floorY;
        private float _age;
        private int _value;
        private bool _homing;

        // ============ Properties ============

        /// <summary>True while the orb is in play.</summary>
        public bool IsActive => gameObject.activeSelf;

        // ============ API ============

        /// <summary>Drops the orb at <paramref name="position"/> with an initial <paramref name="velocity"/>.</summary>
        public void Launch(Vector3 position, Vector3 velocity, int value, Transform target, PlayerProgression collector, ProgressionSettings settings)
        {
            _settings = settings;
            _target = target;
            _collector = collector;
            _velocity = velocity;
            _value = value;
            _age = 0f;
            _homing = false;

            // Settle on whatever is below the drop point (platforms included); triggers like hurtboxes are ignored.
            _floorY = Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, FloorProbe, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : position.y;

            transform.position = position;
            gameObject.SetActive(true);
        }

        /// <summary>Gives the XP immediately and removes the orb (used when the pool needs it back).</summary>
        public void CollectNow()
        {
            if (IsActive)
            {
                Collect();
            }
        }

        // ============ Lifecycle ============

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _settings == null)
            {
                return;
            }

            _age += dt;
            Vector3 position = transform.position;

            if (!_homing && _target != null && _age >= _settings.CollectDelay)
            {
                float distance = Vector3.Distance(position, Chest());
                _homing = distance <= _settings.MagnetRadius || _age >= _settings.AutoCollectAfter;
            }

            if (_homing && _target != null)
            {
                Vector3 toChest = Chest() - position;
                if (toChest.magnitude <= CollectDistance)
                {
                    Collect();
                    return;
                }

                float speed = Mathf.Min(_settings.OrbSpeed, _velocity.magnitude + HomingAcceleration * dt);
                _velocity = toChest.normalized * speed;
                transform.position = position + _velocity * dt;
                return;
            }

            // Tumble: gravity, a small bounce, then roll to a stop.
            _velocity.y += Gravity * dt;
            position += _velocity * dt;
            if (position.y <= _floorY)
            {
                position.y = _floorY;
                _velocity.y = Mathf.Abs(_velocity.y) * Bounce;
                Vector3 planar = Vector3.MoveTowards(new Vector3(_velocity.x, 0f, _velocity.z), Vector3.zero, GroundFriction * dt);
                _velocity = new Vector3(planar.x, _velocity.y < 0.3f ? 0f : _velocity.y, planar.z);
            }

            transform.position = position;
        }

        // ============ Helpers ============

        private Vector3 Chest()
        {
            return _target.position + Vector3.up * ChestHeight;
        }

        private void Collect()
        {
            _collector?.AddXp(_value);
            gameObject.SetActive(false);
        }
    }
}
