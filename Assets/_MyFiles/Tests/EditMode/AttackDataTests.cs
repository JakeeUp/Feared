using Brawler.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Brawler.Tests
{
    /// <summary>
    /// Expected behavior of <see cref="AttackData.CreateDamageInfo"/> (knockback mirroring by facing) and
    /// <see cref="ComboData.GetAttack"/> bounds. Uses the AttackData field defaults (ARCHITECTURE.md §7.1):
    /// damage 10, knockback (3, 0), hitstun 16.
    /// </summary>
    public sealed class AttackDataTests
    {
        // ============ Fixture ============

        private const int DefaultDamage = 10;
        private const float DefaultKnockbackX = 3f;
        private const int DefaultHitstun = 16;
        private static readonly Vector3 HitPoint = new Vector3(1f, 2f, 3f);

        private AttackData _attack;
        private ComboData _emptyCombo;
        private GameObject _source;

        /// <summary>Creates throwaway assets/objects for each test.</summary>
        [SetUp]
        public void SetUp()
        {
            _attack = ScriptableObject.CreateInstance<AttackData>();
            _emptyCombo = ScriptableObject.CreateInstance<ComboData>();
            _source = new GameObject("AttackDataTests_Source");
        }

        /// <summary>Destroys everything created in <see cref="SetUp"/>.</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_attack);
            Object.DestroyImmediate(_emptyCombo);
            Object.DestroyImmediate(_source);
        }

        // ============ CreateDamageInfo ============

        /// <summary>Facing right: payload copies damage/hitstun/hit point/source and keeps knockback X positive.</summary>
        [Test]
        public void CreateDamageInfo_FacingRight_CopiesValues_AndKeepsKnockbackX()
        {
            DamageInfo info = _attack.CreateDamageInfo(1, HitPoint, _source.transform);

            Assert.AreEqual(DefaultDamage, info.Amount);
            Assert.AreEqual(DefaultHitstun, info.HitstunFrames);
            Assert.AreEqual(HitPoint, info.HitPoint);
            Assert.AreSame(_source.transform, info.Source);
            Assert.AreEqual(new Vector3(DefaultKnockbackX, 0f, 0f), info.Knockback);
        }

        /// <summary>Facing left mirrors knockback X (attacker pushes the target to the left).</summary>
        [Test]
        public void CreateDamageInfo_FacingLeft_MirrorsKnockbackX()
        {
            DamageInfo info = _attack.CreateDamageInfo(-1, HitPoint, _source.transform);

            Assert.AreEqual(new Vector3(-DefaultKnockbackX, 0f, 0f), info.Knockback);
            Assert.AreEqual(DefaultDamage, info.Amount);
        }

        /// <summary>A bad facing of 0 is treated as +1 so knockback is never zeroed.</summary>
        [Test]
        public void CreateDamageInfo_FacingZero_IsTreatedAsRight()
        {
            DamageInfo info = _attack.CreateDamageInfo(0, HitPoint, null);

            Assert.AreEqual(new Vector3(DefaultKnockbackX, 0f, 0f), info.Knockback);
            Assert.IsNull(info.Source);
        }

        /// <summary>Knockback Z is always 0: attacks push along X (facing) and Y (launch) only.</summary>
        [Test]
        public void CreateDamageInfo_KnockbackZ_IsAlwaysZero()
        {
            Assert.AreEqual(0f, _attack.CreateDamageInfo(1, HitPoint, null).Knockback.z);
            Assert.AreEqual(0f, _attack.CreateDamageInfo(-1, HitPoint, null).Knockback.z);
        }

        // ============ ComboData (implemented plumbing — should already pass) ============

        /// <summary>An empty combo has Count 0 and returns null for any index (AttackState's null guard relies on it).</summary>
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(3)]
        public void ComboData_Empty_GetAttack_ReturnsNull(int index)
        {
            Assert.AreEqual(0, _emptyCombo.Count);
            Assert.IsNull(_emptyCombo.GetAttack(index));
        }
    }
}
