using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Progression
{
    /// <summary>
    /// Turns <see cref="EnemyDefeatedEvent"/>s into bursts of <see cref="XpOrb"/>s that home in on the player.
    /// Builds a fixed pool of glowing spheres in Awake (no allocation per drop); if every orb is busy the oldest is
    /// collected instantly so no XP is ever lost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XpOrbSpawner : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Shared event bus. Listens for EnemyDefeatedEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [SerializeField] private ProgressionSettings _settings;

        [Tooltip("Who receives the XP (the player's PlayerProgression).")]
        [SerializeField] private PlayerProgression _collector;

        [Tooltip("What orbs fly to (the player root).")]
        [SerializeField] private Transform _target;

        [Header("Look")]
        [Tooltip("Orb material. An HDR unlit colour makes it glow through bloom.")]
        [SerializeField] private Material _material;

        [Tooltip("Orb diameter (units).")]
        [SerializeField, Min(0.01f)] private float _size = 0.14f;

        [Tooltip("Orbs in the pool.")]
        [SerializeField, Min(1)] private int _capacity = 40;

        // ============ State ============

        private XpOrb[] _orbs;
        private int _next;
        private Action<EnemyDefeatedEvent> _onDefeated;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onDefeated = OnDefeated;
            _orbs = new XpOrb[_capacity];
            for (int i = 0; i < _capacity; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"XpOrb_{i}";
                Destroy(go.GetComponent<Collider>()); // orbs are visual; collection is distance-based
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * _size;
                var meshRenderer = go.GetComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                if (_material != null)
                {
                    meshRenderer.sharedMaterial = _material;
                }

                go.SetActive(false);
                _orbs[i] = go.AddComponent<XpOrb>();
            }
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onDefeated);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onDefeated);
            }
        }

        // ============ API ============

        /// <summary>Bursts <paramref name="xp"/> worth of orbs out of <paramref name="position"/> (enemy deaths, opened chests).</summary>
        public void Burst(Vector3 position, int xp)
        {
            if (_settings == null || xp <= 0)
            {
                return;
            }

            // Split the XP into orbs; any remainder rides on the first orb.
            int count = Mathf.Clamp(xp / _settings.XpPerOrb, 1, _settings.MaxOrbsPerDrop);
            int each = xp / count;
            int remainder = xp - each * count;
            Vector3 origin = position + Vector3.up * 0.5f;

            for (int i = 0; i < count; i++)
            {
                XpOrb orb = _orbs[_next];
                _next = (_next + 1) % _orbs.Length;
                if (orb.IsActive)
                {
                    // Pool exhausted: grant the old orb's XP now rather than lose it.
                    orb.CollectNow();
                }

                float angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                var burst = new Vector3(Mathf.Cos(angle), 1.4f, Mathf.Sin(angle) * 0.6f) * _settings.OrbPopSpeed;
                orb.Launch(origin, burst, each + (i == 0 ? remainder : 0), _target, _collector, _settings);
            }
        }

        // ============ Helpers ============

        private void OnDefeated(EnemyDefeatedEvent evt)
        {
            Burst(evt.Position, evt.Xp);
        }
    }
}
