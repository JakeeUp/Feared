using Brawler.Combat;
using UnityEditor;
using UnityEngine;

namespace Brawler.Editor
{
    internal static class AttackAssets
    {
        internal sealed class Spec
        {
            public int Damage;
            public Vector2 Knockback;
            public int Hitstun = 16;
            public int Startup;
            public int Active;
            public int Recovery;
            public int CancelStart;
            public int CancelEnd;
            public float Lunge;
            public Vector2 HitboxSize;
            public Vector2 HitboxOffset;
            public int HitStop = 4;
            public float Shake = 0.15f;
        }

        internal static AttackData Ensure(string path, Spec spec)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (attack != null)
            {
                return attack;
            }

            attack = ScriptableObject.CreateInstance<AttackData>();
            AssetDatabase.CreateAsset(attack, path);
            EditorUtil.Set(attack, so =>
            {
                so.FindProperty("_damage").intValue = spec.Damage;
                so.FindProperty("_knockback").vector2Value = spec.Knockback;
                so.FindProperty("_hitstunFrames").intValue = spec.Hitstun;
                SerializedProperty frames = so.FindProperty("_frames");
                frames.FindPropertyRelative("_startup").intValue = spec.Startup;
                frames.FindPropertyRelative("_active").intValue = spec.Active;
                frames.FindPropertyRelative("_recovery").intValue = spec.Recovery;
                frames.FindPropertyRelative("_cancelWindowStart").intValue = spec.CancelStart;
                frames.FindPropertyRelative("_cancelWindowEnd").intValue = spec.CancelEnd;
                so.FindProperty("_lungeSpeed").floatValue = spec.Lunge;
                so.FindProperty("_hitboxSize").vector2Value = spec.HitboxSize;
                so.FindProperty("_hitboxOffset").vector2Value = spec.HitboxOffset;
                so.FindProperty("_hitStopFrames").intValue = spec.HitStop;
                so.FindProperty("_screenShake").floatValue = spec.Shake;
            });
            return attack;
        }
    }
}
