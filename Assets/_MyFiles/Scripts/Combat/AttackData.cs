using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>
    /// Data for a single attack (one link of a combo): damage, knockback, frame data, hitbox shape and
    /// per-move game-feel values. Hitbox depth (Z) comes from <see cref="CombatSettings.DepthTolerance"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Brawler/Combat/Attack Data", fileName = "Attack_New")]
    public sealed class AttackData : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Header("Damage")]
        [Tooltip("Damage dealt on hit.")]
        [SerializeField, Min(0)] private int _damage = 10;

        [Tooltip("Knockback velocity in units/s. X = along attacker facing, Y = upward (launch).")]
        [SerializeField] private Vector2 _knockback = new Vector2(3f, 0f);

        [Tooltip("Hitstun inflicted on the target, in 60 Hz frames.")]
        [SerializeField, Min(0)] private int _hitstunFrames = 16;

        [Header("Frame Data")]
        [Tooltip("Startup / active / recovery and cancel window, in 60 Hz frames.")]
        [SerializeField] private AttackFrames _frames = new AttackFrames(4, 3, 12, 7, 19);

        [Header("Hitbox")]
        [Tooltip("Hitbox size in units. X = width along facing, Y = height. Depth (Z) = 2 * CombatSettings.DepthTolerance.")]
        [SerializeField] private Vector2 _hitboxSize = new Vector2(1.2f, 0.8f);

        [Tooltip("Hitbox center offset from the Hitbox transform. X = forward (mirrored by facing), Y = up.")]
        [SerializeField] private Vector2 _hitboxOffset = new Vector2(0.75f, 1f);

        [Header("Movement")]
        [Tooltip("Forward lunge speed (units/s) applied during Startup and Active. 0 = plant feet.")]
        [SerializeField, Min(0f)] private float _lungeSpeed = 1.5f;

        [Header("Game Feel")]
        [Tooltip("Hitstop duration on connect, in 60 Hz frames (converted with FrameTime). 0 = none.")]
        [SerializeField, Min(0)] private int _hitStopFrames = 4;

        [Tooltip("Screen shake trauma added on connect (0..1). Squared by ScreenShake for falloff.")]
        [SerializeField, Range(0f, 1f)] private float _screenShake = 0.15f;

        // ============ Properties ============

        /// <summary>Damage dealt on hit.</summary>
        public int Damage => _damage;

        /// <summary>Knockback (X along facing, Y up) in units/s.</summary>
        public Vector2 Knockback => _knockback;

        /// <summary>Hitstun in frames.</summary>
        public int HitstunFrames => _hitstunFrames;

        /// <summary>Frame data.</summary>
        public AttackFrames Frames => _frames;

        /// <summary>Hitbox width/height.</summary>
        public Vector2 HitboxSize => _hitboxSize;

        /// <summary>Hitbox offset (X forward, Y up).</summary>
        public Vector2 HitboxOffset => _hitboxOffset;

        /// <summary>Forward lunge speed during Startup/Active.</summary>
        public float LungeSpeed => _lungeSpeed;

        /// <summary>Hitstop in frames.</summary>
        public int HitStopFrames => _hitStopFrames;

        /// <summary>Screen shake trauma (0..1).</summary>
        public float ScreenShake => _screenShake;

        // ============ API ============

        /// <summary>Builds the <see cref="DamageInfo"/> for a hit made by this attack.</summary>
        /// <param name="facingSign">Attacker facing (+1/-1); mirrors knockback X.</param>
        /// <param name="hitPoint">World-space contact point.</param>
        /// <param name="source">Attacker root transform.</param>
        /// <returns>The damage payload.</returns>
        public DamageInfo CreateDamageInfo(int facingSign, Vector3 hitPoint, Transform source)
        {
            float sign = facingSign < 0 ? -1f : 1f;
            var knockback = new Vector3(_knockback.x * sign, _knockback.y, 0f);
            return new DamageInfo(_damage, knockback, _hitstunFrames, hitPoint, source);
        }

        // ============ Validation ============

        private void OnValidate()
        {
            AttackFrames frames = _frames;
            int active = Mathf.Max(1, frames.Active);
            int cancelEnd = Mathf.Max(frames.CancelWindowStart, frames.CancelWindowEnd);
            if (active != frames.Active || cancelEnd != frames.CancelWindowEnd)
            {
                _frames = new AttackFrames(frames.Startup, active, frames.Recovery, frames.CancelWindowStart, cancelEnd);
                frames = _frames;
            }

            if (frames.CancelWindowEnd > frames.TotalFrames)
            {
                Debug.LogWarning($"{nameof(AttackData)} '{name}': cancel window ends at {frames.CancelWindowEnd}, past TotalFrames ({frames.TotalFrames}).", this);
            }

            int activeEnd = frames.Startup + frames.Active;
            if (frames.CancelWindowStart < frames.CancelWindowEnd && frames.CancelWindowStart < activeEnd)
            {
                Debug.LogWarning($"{nameof(AttackData)} '{name}': cancel window opens at {frames.CancelWindowStart}, before the active frames end ({activeEnd}); cancels may skip hits.", this);
            }
        }
    }
}
