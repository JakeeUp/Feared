using System.Collections.Generic;
using UnityEngine;

namespace Brawler.Enemy
{
    /// <summary>
    /// Scene-level brain shared by every enemy: who to fight (<see cref="Target"/>), which camera sprites face, the
    /// <see cref="AttackDirector"/> that makes the group take turns, and a registry that hands each enemy its own
    /// waiting spot so the ones not attacking spread around the target instead of clumping. Enemies receive it from
    /// the spawner (prefabs can't reference scene objects).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyCoordinator : MonoBehaviour
    {
        // ============ Constants ============

        /// <summary>Depth offsets (units) of the waiting spots, assigned by registration order.</summary>
        private static readonly float[] WaitLaneOffsets = { 0f, 1.1f, -1.1f, 2f, -2f, 0.55f, -0.55f };

        // ============ Serialized Fields ============

        [Tooltip("What enemies chase and attack (the player root).")]
        [SerializeField] private Transform _target;

        [Tooltip("Camera transform handed to enemy billboards and health bars.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("How many enemies may close in / attack at once. 1 = strict turns.")]
        [SerializeField, Min(1)] private int _maxAttackers = 1;

        [Tooltip("Minimum seconds between two attacks starting, across the whole group (the fight's rhythm).")]
        [SerializeField, Min(0f)] private float _minAttackInterval = 0.9f;

        // ============ State ============

        private AttackDirector _director;
        private readonly List<EnemyController> _registered = new List<EnemyController>(16);

        // ============ Properties ============

        /// <summary>The enemies' target.</summary>
        public Transform Target => _target;

        /// <summary>Camera for billboards.</summary>
        public Transform CameraTransform => _cameraTransform;

        /// <summary>Turn-taking.</summary>
        public AttackDirector Director => _director ??= new AttackDirector(_maxAttackers, _minAttackInterval);

        // ============ Lifecycle ============

        private void OnDisable()
        {
            _director?.Clear();
            _registered.Clear();
        }

        // ============ API ============

        /// <summary>Adds an enemy to the group (on spawn).</summary>
        public void Register(EnemyController enemy)
        {
            if (enemy != null && !_registered.Contains(enemy))
            {
                _registered.Add(enemy);
            }
        }

        /// <summary>Removes an enemy from the group (on despawn).</summary>
        public void Unregister(EnemyController enemy)
        {
            _registered.Remove(enemy);
            Director.Withdraw(enemy);
        }

        /// <summary>Depth offset of this enemy's waiting spot, so waiting enemies fan out across lanes.</summary>
        public float WaitLaneOffset(EnemyController enemy)
        {
            int index = _registered.IndexOf(enemy);
            return index < 0 ? 0f : WaitLaneOffsets[index % WaitLaneOffsets.Length];
        }
    }
}
