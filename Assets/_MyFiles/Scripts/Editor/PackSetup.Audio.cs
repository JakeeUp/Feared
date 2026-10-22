using System.IO;
using System.Linq;
using System.Text;
using Brawler.Core;
using Brawler.Player;
using UnityEditor;
using UnityEngine;

namespace Brawler.Editor
{
    public static partial class PackSetup
    {
        private const string SfxRoot = Root + "/Audio/SFX";

        internal static PlayerAudio BuildAudio(PlayerController player, EventBusAsset eventBus, StringBuilder log)
        {
            var audio = EditorUtil.GetOrAdd<PlayerAudio>(player.gameObject);
            var source = EditorUtil.GetOrAdd<AudioSource>(player.gameObject);
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            var so = new SerializedObject(audio);
            so.FindProperty("_player").objectReferenceValue = player;
            so.FindProperty("_eventBus").objectReferenceValue = eventBus;
            so.FindProperty("_source").objectReferenceValue = source;

            // Built by Tools/make_sfx.py from CC0 foley; each slot takes every numbered variation of its sound.
            var assignments = new (string property, string prefix)[]
            {
                ("_attack", "swing_"), ("_dash", "dash_"), ("_chain", "chain_throw_"), ("_grapple", "chain_latch_"),
                ("_zip", "chain_zip_"), ("_hit", "hit_flesh_"), ("_hurt", "hurt_"), ("_parry", "parry_"),
            };
            foreach (var assignment in assignments)
            {
                SerializedProperty property = so.FindProperty(assignment.property);
                bool assigned = Enumerable.Range(0, property.arraySize)
                    .Any(i => property.GetArrayElementAtIndex(i).objectReferenceValue != null);
                if (assigned) continue;
                AudioClip[] clips = Directory.Exists(SfxRoot)
                    ? AssetDatabase.FindAssets(assignment.prefix + " t:AudioClip", new[] { SfxRoot })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(assignment.prefix))
                        .OrderBy(path => path)
                        .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                        .ToArray()
                    : System.Array.Empty<AudioClip>();
                if (clips.Length == 0)
                {
                    log.AppendLine($"  ! no {assignment.prefix}* clips in {SfxRoot} (run Tools/make_sfx.py)");
                    continue;
                }

                EditorUtil.SetArray(property, clips);
                log.AppendLine($"  * audio {assignment.property}: {clips.Length} x {assignment.prefix}*");
            }

            so.ApplyModifiedProperties();
            return audio;
        }
    }
}
