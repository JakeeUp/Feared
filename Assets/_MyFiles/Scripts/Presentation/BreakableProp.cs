using Brawler.Combat;
using Brawler.Progression;
using UnityEngine;

namespace Brawler.Presentation
{
    /// <summary>
    /// A pot (or crate...) that shatters when hit: the sprite is swapped for shards that burst out, bounce and
    /// vanish, a puff plays, and the player's wallet gets a little score. Hits count toward the hit streak like any
    /// other (the Hitbox publishes HitEvent). Optionally comes back after a delay so the arena keeps its props.
    /// </summary>
    /// <remarks>Shard objects are created once in Awake; breaking never allocates.</remarks>
    [DisallowMultipleComponent]
    public sealed class BreakableProp : MonoBehaviour, IDamageable
    {
        // ============ Serialized Fields ============

        [Header("Parts")]
        [Tooltip("The intact prop sprite (on a billboarded child).")]
        [SerializeField] private SpriteRenderer _visual;

        [Tooltip("Hurtbox and solid colliders turned off while broken.")]
        [SerializeField] private Collider[] _colliders = new Collider[0];

        [Tooltip("Shard sprites; one shard is thrown per entry.")]
        [SerializeField] private Sprite[] _shardSprites = new Sprite[0];

        [Tooltip("Material for the shards (same as the visual).")]
        [SerializeField] private Material _shardMaterial;

        [Header("Rules")]
        [Tooltip("Hits needed to break it.")]
        [SerializeField, Min(1)] private int _hitsToBreak = 1;

        [Tooltip("Score added to the wallet when it breaks.")]
        [SerializeField, Min(0)] private int _scoreReward = 25;

        [Tooltip("Wallet that receives the score. Optional.")]
        [SerializeField] private ScoreWallet _wallet;

        [Tooltip("Seconds until it reappears (0 = stays broken).")]
        [SerializeField, Min(0f)] private float _respawnDelay = 20f;

        [Header("Effects")]
        [SerializeField] private VfxPlayer _vfx;
        [SerializeField] private SpriteClip _breakClip;

        [Tooltip("Shard launch speed range (units/s).")]
        [SerializeField] private Vector2 _shardSpeed = new Vector2(2f, 4.5f);

        [Tooltip("Seconds shards stay before vanishing.")]
        [SerializeField, Min(0.1f)] private float _shardLifetime = 1.1f;

        // ============ Constants ============

        private const float Gravity = -22f;
        private const float Bounce = 0.35f;
        private const float WobbleTime = 0.18f;

        // ============ State ============

        private Transform[] _shards;
        private Vector3[] _shardVelocity;
        private float _shardTime;
        private int _hits;
        private bool _broken;
        private float _respawnAt;
        private float _wobble;
        private Vector3 _visualPosition;
        private Transform _camera;

        // ============ Properties ============

        /// <summary>True while shattered.</summary>
        public bool IsBroken => _broken;

        // ============ Lifecycle ============

        private void Awake()
        {
            if (_visual != null)
            {
                _visualPosition = _visual.transform.localPosition;
                var billboard = _visual.GetComponent<BillboardSprite>();
                _camera = billboard != null ? billboard.CameraTransform : null;
            }

            _shards = new Transform[_shardSprites.Length];
            _shardVelocity = new Vector3[_shardSprites.Length];
            for (int i = 0; i < _shardSprites.Length; i++)
            {
                var go = new GameObject($"Shard_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _shardSprites[i];
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (_shardMaterial != null)
                {
                    sr.sharedMaterial = _shardMaterial;
                }

                go.SetActive(false);
                _shards[i] = go.transform;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_wobble > 0f && _visual != null)
            {
                _wobble = Mathf.Max(0f, _wobble - dt);
                float shake = Mathf.Sin(_wobble * 90f) * 0.04f * (_wobble / WobbleTime);
                _visual.transform.localPosition = _visualPosition + new Vector3(shake, 0f, 0f);
            }

            if (_broken)
            {
                UpdateShards(dt);
                if (_respawnDelay > 0f && Time.time >= _respawnAt)
                {
                    Restore();
                }
            }
        }

        // ============ IDamageable ============

        /// <inheritdoc/>
        public bool TryApplyDamage(in DamageInfo info)
        {
            if (_broken)
            {
                return false;
            }

            _hits++;
            if (_hits < _hitsToBreak)
            {
                _wobble = WobbleTime;
                return true;
            }

            Break(info);
            return true;
        }

        // ============ Helpers ============

        private void Break(in DamageInfo info)
        {
            _broken = true;
            _respawnAt = Time.time + _respawnDelay;
            SetIntact(false);

            Vector3 center = transform.position + Vector3.up * 0.3f;
            float away = Mathf.Sign(info.Knockback.x == 0f ? center.x - info.HitPoint.x : info.Knockback.x);
            _shardTime = 0f;
            for (int i = 0; i < _shards.Length; i++)
            {
                // Spread around the hit direction: mostly away from the attacker, some straight up, a little in depth.
                float t = _shards.Length > 1 ? i / (float)(_shards.Length - 1) : 0.5f;
                float speed = Random.Range(_shardSpeed.x, _shardSpeed.y);
                var direction = new Vector3(away * Mathf.Lerp(0.2f, 1f, t), Mathf.Lerp(1.4f, 0.6f, t), Random.Range(-0.5f, 0.5f)).normalized;
                _shardVelocity[i] = direction * speed;
                _shards[i].position = center;
                _shards[i].gameObject.SetActive(true);
            }

            if (_vfx != null)
            {
                _vfx.Play(_breakClip, center);
            }

            if (_wallet != null && _scoreReward > 0)
            {
                _wallet.Add(_scoreReward);
            }
        }

        private void UpdateShards(float dt)
        {
            if (_shards.Length == 0 || _shardTime > _shardLifetime)
            {
                return;
            }

            _shardTime += dt;
            bool expired = _shardTime > _shardLifetime;
            Quaternion facing = _camera != null ? _camera.rotation : Quaternion.identity;
            float floor = transform.position.y;
            float shrink = Mathf.Clamp01((_shardLifetime - _shardTime) / 0.25f); // shrink away over the last quarter second
            for (int i = 0; i < _shards.Length; i++)
            {
                Transform shard = _shards[i];
                if (expired)
                {
                    shard.gameObject.SetActive(false);
                    continue;
                }

                Vector3 v = _shardVelocity[i];
                v.y += Gravity * dt;
                Vector3 p = shard.position + v * dt;
                if (p.y < floor)
                {
                    p.y = floor;
                    v = new Vector3(v.x * 0.6f, -v.y * Bounce, v.z * 0.6f);
                }

                _shardVelocity[i] = v;
                shard.SetPositionAndRotation(p, facing);
                shard.localScale = Vector3.one * shrink;
            }
        }

        private void Restore()
        {
            _broken = false;
            _hits = 0;
            SetIntact(true);
            foreach (Transform shard in _shards)
            {
                shard.gameObject.SetActive(false);
            }
        }

        private void SetIntact(bool intact)
        {
            if (_visual != null)
            {
                _visual.enabled = intact;
                _visual.transform.localPosition = _visualPosition;
            }

            foreach (Collider c in _colliders)
            {
                if (c != null)
                {
                    c.enabled = intact;
                }
            }
        }
    }
}
