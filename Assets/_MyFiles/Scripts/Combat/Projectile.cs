using System;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>
    /// A straight-flying attack (fireball, bolt). Carries an <see cref="AttackData"/> for its damage, knockback and
    /// feedback, sweeps a sphere for hurtboxes each frame, and expires on a wall or after its lifetime.
    /// A hit that the target rejects without being invulnerable (a parry) deflects it: it turns around, joins the
    /// parrying side and speeds up, so parrying a fireball sends it back at the caster.
    /// </summary>
    /// <remarks>Pooled by <see cref="ProjectileLauncher"/>; never allocates while flying.</remarks>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        // ============ Constants ============

        private const int MaxOverlaps = 8;

        // ============ Serialized Fields ============

        [SerializeField] private CombatSettings _combatSettings;

        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Hit radius (units).")]
        [SerializeField, Min(0.05f)] private float _radius = 0.3f;

        [Tooltip("Seconds before it fizzles out.")]
        [SerializeField, Min(0.1f)] private float _lifetime = 4f;

        [Tooltip("Solid layers that stop it.")]
        [SerializeField] private LayerMask _solidLayers = ~0;

        [Tooltip("Speed multiplier after a parry deflects it.")]
        [SerializeField, Min(1f)] private float _deflectSpeedup = 1.4f;

        [Tooltip("Visual flipped to face the flight direction (optional).")]
        [SerializeField] private SpriteRenderer _sprite;

        // ============ Runtime ============

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private AttackData _attack;
        private Vector3 _velocity;
        private CombatTeam _team;
        private Transform _owner;
        private Hurtbox _ownerHurtbox;
        private float _age;

        // ============ Events ============

        /// <summary>Raised when the projectile ends (hit, wall, timeout). The launcher returns it to the pool.</summary>
        public event Action<Projectile> Expired;

        // ============ API ============

        /// <summary>Fires the projectile.</summary>
        /// <param name="attack">Damage, knockback and feedback of a hit.</param>
        /// <param name="position">Start point.</param>
        /// <param name="velocity">Flight velocity (units/s).</param>
        /// <param name="team">Side it belongs to (never hurts its own team).</param>
        /// <param name="owner">Source transform for the DamageInfo.</param>
        /// <param name="ownerHurtbox">The shooter's hurtbox, skipped.</param>
        public void Launch(AttackData attack, Vector3 position, Vector3 velocity, CombatTeam team, Transform owner, Hurtbox ownerHurtbox)
        {
            _attack = attack;
            _velocity = velocity;
            _team = team;
            _owner = owner;
            _ownerHurtbox = ownerHurtbox;
            _age = 0f;
            transform.position = position;
            FaceVelocity();
            gameObject.SetActive(true);
        }

        // ============ Lifecycle ============

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            _age += dt;
            if (_age >= _lifetime)
            {
                Expire();
                return;
            }

            Vector3 step = _velocity * dt;
            if (Physics.SphereCast(transform.position, _radius * 0.5f, step.normalized, out RaycastHit wall, step.magnitude,
                    _solidLayers, QueryTriggerInteraction.Ignore) && !(wall.collider is CharacterController))
            {
                Expire();
                return;
            }

            transform.position += step;
            Sweep();
        }

        // ============ Helpers ============

        private void Sweep()
        {
            if (_attack == null || _combatSettings == null)
            {
                return;
            }

            int count = Physics.OverlapSphereNonAlloc(transform.position, _radius, _overlaps, _combatSettings.HurtboxLayers,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (!_overlaps[i].TryGetComponent(out Hurtbox hurtbox)
                    || hurtbox == _ownerHurtbox
                    || (_team != CombatTeam.Neutral && hurtbox.Team == _team)
                    || hurtbox.Damageable == null
                    || hurtbox.IsInvulnerable)
                {
                    continue;
                }

                int facing = _velocity.x >= 0f ? 1 : -1;
                DamageInfo info = _attack.CreateDamageInfo(facing, transform.position, _owner);
                if (!hurtbox.Damageable.TryApplyDamage(info))
                {
                    Deflect(hurtbox);
                    return;
                }

                hurtbox.Knockbackable?.ApplyKnockback(info.Knockback);
                if (_eventBus != null)
                {
                    _eventBus.Publish(new HitEvent(info, hurtbox.OwnerRoot, _attack.HitStopFrames, _attack.ScreenShake));
                }

                Expire();
                return;
            }
        }

        /// <summary>Parried: switch sides and fly back the way it came, faster.</summary>
        private void Deflect(Hurtbox by)
        {
            _team = by.Team;
            _ownerHurtbox = by;
            _owner = by.OwnerRoot;
            _velocity = -_velocity * _deflectSpeedup;
            _age = 0f;
            FaceVelocity();
        }

        private void FaceVelocity()
        {
            if (_sprite != null)
            {
                _sprite.flipX = _velocity.x < 0f;
            }
        }

        private void Expire()
        {
            gameObject.SetActive(false);
            Expired?.Invoke(this);
        }

        // ============ Debug ============

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
