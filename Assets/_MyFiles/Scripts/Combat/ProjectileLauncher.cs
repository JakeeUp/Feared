using UnityEngine;
using UnityEngine.Pool;

namespace Brawler.Combat
{
    /// <summary>
    /// Fires pooled <see cref="Projectile"/>s for one attacker. Instances live unparented (so they don't move with the
    /// shooter) and are created on demand up to a small cap, then reused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileLauncher : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [SerializeField] private Projectile _prefab;

        [Tooltip("Damage, knockback and feedback of each projectile.")]
        [SerializeField] private AttackData _attack;

        [Tooltip("Spawn point relative to the shooter; X is mirrored by facing.")]
        [SerializeField] private Vector3 _muzzleOffset = new Vector3(0.5f, 0.7f, 0f);

        [Tooltip("Flight speed (units/s).")]
        [SerializeField, Min(0f)] private float _speed = 7f;

        [SerializeField] private CombatTeam _team = CombatTeam.Enemy;

        [Tooltip("The shooter's hurtbox, never hit by its own shots.")]
        [SerializeField] private Hurtbox _ownerHurtbox;

        [SerializeField, Min(1)] private int _maxLive = 4;

        // ============ Runtime ============

        private ObjectPool<Projectile> _pool;

        // ============ Properties ============

        /// <summary>The projectile's attack (damage/feedback).</summary>
        public AttackData Attack => _attack;

        // ============ Lifecycle ============

        private void Awake()
        {
            _pool = new ObjectPool<Projectile>(Create, null, null, p => { if (p != null) Destroy(p.gameObject); },
                collectionCheck: false, defaultCapacity: _maxLive, maxSize: _maxLive);
        }

        private void OnDestroy()
        {
            _pool?.Clear();
        }

        // ============ API ============

        /// <summary>Fires one projectile from the muzzle toward <paramref name="target"/> (or straight ahead without one).</summary>
        /// <param name="facingSign">+1 right, -1 left; mirrors the muzzle and the default direction.</param>
        /// <param name="target">Aim point, or null to fire horizontally.</param>
        /// <param name="aimVertical">When false the shot stays level (ground casters); when true it aims in 3D (fliers).</param>
        public void Fire(int facingSign, Vector3? target, bool aimVertical)
        {
            if (_prefab == null || _attack == null || _pool.CountActive >= _maxLive)
            {
                return;
            }

            Vector3 muzzle = transform.position + new Vector3(_muzzleOffset.x * facingSign, _muzzleOffset.y, _muzzleOffset.z);
            Vector3 direction = new Vector3(facingSign, 0f, 0f);
            if (target.HasValue)
            {
                Vector3 to = target.Value - muzzle;
                if (!aimVertical)
                {
                    to.y = 0f;
                }

                if (to.sqrMagnitude > 0.01f && to.x * facingSign > 0f)
                {
                    direction = to.normalized;
                }
            }

            Projectile projectile = _pool.Get();
            projectile.Launch(_attack, muzzle, direction * _speed, _team, transform, _ownerHurtbox);
        }

        // ============ Pool ============

        private Projectile Create()
        {
            Projectile projectile = Instantiate(_prefab);
            projectile.gameObject.SetActive(false);
            projectile.Expired += p => _pool.Release(p);
            return projectile;
        }
    }
}
