// Most private fields aren't touched until the TODOs below are filled in. Drop this pragma once they are.
#pragma warning disable CS0169, CS0649, CS0414

using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace Brawler.Presentation
{
    /// <summary>
    /// Spawns pooled floating damage numbers at hit points. Listens for <see cref="HitEvent"/> and owns an
    /// <see cref="ObjectPool{T}"/> of <see cref="DamageNumber"/> (Unity's built-in pool — no custom pooling).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("References")]
        [Tooltip("Shared event bus asset (Data/Core/EventBus.asset). Listens for HitEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("Lifetime, rise, spawn offset/spread and pool sizes.")]
        [SerializeField] private FeedbackSettings _settings;

        [Tooltip("Prefab with a DamageNumber + TextMesh (Art/Prefabs/P_DamageNumber.prefab).")]
        [SerializeField] private DamageNumber _prefab;

        [Tooltip("Camera transform; numbers copy its rotation so they always face the screen.")]
        [SerializeField] private Transform _cameraTransform;

        // ============ State ============

        /// <summary>The pool. Created in Awake; cleared in OnDestroy.</summary>
        private ObjectPool<DamageNumber> _pool;

        /// <summary>Cached delegate so subscribe/unsubscribe don't allocate and match by reference.</summary>
        private Action<HitEvent> _onHit;

        // ============ Properties ============

        /// <summary>The damage-number pool (null if the spawner is mis-wired).</summary>
        public IObjectPool<DamageNumber> Pool => _pool;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onHit = OnHit;

            if (_settings == null || _prefab == null)
            {
                Debug.LogWarning($"{name}: DamageNumberSpawner needs Settings and Prefab wired; numbers are disabled.", this);
                return;
            }

            // Wiring: method groups are converted to delegates once, here (no per-spawn allocations).
            _pool = new ObjectPool<DamageNumber>(
                CreateNumber,
                OnGetNumber,
                OnReleaseNumber,
                OnDestroyNumber,
                collectionCheck: Application.isEditor,
                defaultCapacity: _settings.DamageNumberPoolCapacity,
                maxSize: Mathf.Max(_settings.DamageNumberPoolCapacity, _settings.DamageNumberPoolMaxSize));
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onHit);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onHit);
            }
        }

        private void OnDestroy()
        {
            // Destroys pooled (inactive) instances via OnDestroyNumber. Active numbers are children of this
            // transform, so they are destroyed with it.
            _pool?.Clear();
        }

        // ============ API ============

        /// <summary>Shows <paramref name="amount"/> floating up from <paramref name="worldPosition"/>.</summary>
        /// <param name="amount">Damage to display.</param>
        /// <param name="worldPosition">Hit point in world space (spawn offset and random spread are added).</param>
        public void Spawn(int amount, Vector3 worldPosition)
        {
            // TODO: get a number from the pool and play it at the hit point + spawn offset, facing the camera.
            // - Spread along the camera's right axis rather than world X so it reads the same at any yaw.
            // - Use UnityEngine.Random explicitly, `using System;` makes plain Random ambiguous.
            // - Skip amount <= 0 for now.
            // Pool max size only limits what's kept on Release; Get always succeeds.
        }

        // ============ Pool Callbacks (cached method groups) ============

        /// <summary>Pool create: instantiates a number under this transform and hands it its pool once.</summary>
        private DamageNumber CreateNumber()
        {
            // Wiring (not gameplay): instantiate inactive-by-default instance and inject dependencies once.
            DamageNumber number = Instantiate(_prefab, transform);
            number.gameObject.SetActive(false);
            number.Initialize(_pool, _settings);
            return number;
        }

        /// <summary>Pool get: activate.</summary>
        /// <param name="number">Instance leaving the pool.</param>
        private void OnGetNumber(DamageNumber number)
        {
            // TODO: activate it. Play() positions it in the same frame so nothing draws in the old spot.
        }

        /// <summary>Pool release: deactivate.</summary>
        /// <param name="number">Instance returning to the pool.</param>
        private void OnReleaseNumber(DamageNumber number)
        {
            // TODO: deactivate it. collectionCheck throws on a double release in the editor - DamageNumber's flag guards that.
        }

        /// <summary>Pool destroy: called when the pool is over maxSize or cleared.</summary>
        /// <param name="number">Instance being discarded.</param>
        private void OnDestroyNumber(DamageNumber number)
        {
            // Wiring: destroy the GameObject (guard: may already be destroyed during scene teardown).
            if (number != null)
            {
                Destroy(number.gameObject);
            }
        }

        // ============ Event Handlers ============

        /// <summary>HitEvent listener: number at the contact point.</summary>
        /// <param name="evt">The hit.</param>
        private void OnHit(HitEvent evt)
        {
            // TODO: spawn a number for the hit. HitEvent can fire right before hitstop freezes time - fine, numbers run on
            // unscaled time.
        }
    }
}
