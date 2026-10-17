using UnityEngine;

namespace Brawler.Combat
{
    /// <summary>An ordered attack chain (e.g. Jab1 -&gt; Jab2 -&gt; Finisher). Index into it with <c>ComboTracker</c>.</summary>
    [CreateAssetMenu(menuName = "Brawler/Combat/Combo Data", fileName = "Combo_New")]
    public sealed class ComboData : ScriptableObject
    {
        // ============ Serialized Fields ============

        [Header("Chain")]
        [Tooltip("Attacks in chain order. Element 0 starts the combo; the last element is the finisher (no further chain, dash-cancel only).")]
        [SerializeField] private AttackData[] _attacks = new AttackData[0];

        // ============ Properties ============

        /// <summary>Number of attacks in the chain.</summary>
        public int Count => _attacks != null ? _attacks.Length : 0;

        // ============ API ============

        /// <summary>Returns the attack at <paramref name="index"/>.</summary>
        /// <param name="index">0-based chain index (from <c>ComboTracker.CurrentIndex</c>).</param>
        /// <returns>The attack, or null if out of range.</returns>
        public AttackData GetAttack(int index)
        {
            // Plain data access (not gameplay), so implemented.
            return index >= 0 && index < Count ? _attacks[index] : null;
        }
    }
}
