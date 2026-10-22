using System.Collections;
using System.Reflection;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Enemy;
using Brawler.Player;
using Brawler.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Brawler.Tests
{
    public sealed class OpeningSceneTests
    {
        [UnityTest]
        public IEnumerator OpeningRingAndFirstFight_WorkAfterCheckpointRespawn()
        {
            yield return SceneManager.LoadSceneAsync("Cathedral");
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null);
            var audio = player.GetComponent<PlayerAudio>();
            Assert.That(audio, Is.Not.Null);
            foreach (FieldInfo field in typeof(PlayerAudio).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.FieldType == typeof(AudioClip[])) Assert.That((AudioClip[])field.GetValue(audio), Is.Not.Empty.And.All.Not.Null, field.Name);
            }
            Assert.That(ControlHints.Format("{Attack} {Grapple} {Menu}"), Does.Not.Contain("{"));
            Assert.That(GameObject.Find("Hint_Attack").transform.position.x, Is.LessThan(GameObject.Find("Nave_Fight").transform.position.x));

            var ring = GameObject.Find("Ring_Nave_Pit").GetComponent<GrappleAnchor>();
            Assert.That(ring, Is.Not.Null);
            player.Teleport(new Vector3(11.75f, 1.05f, 0f));
            Physics.SyncTransforms();
            Assert.That(ChainTargeting.FindBest(player.Context, out var targetRing, out _), Is.True);
            Assert.That(targetRing, Is.SameAs(ring));
            player.Context.Buffer.Enqueue(BufferedCommand.Grapple, Time.time);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (player.CurrentState != player.Context.States.Grapple && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.CurrentState, Is.SameAs(player.Context.States.Grapple));
            player.Context.Buffer.Enqueue(BufferedCommand.Jump, Time.time);
            yield return null;

            player.Teleport(new Vector3(21.5f, 0.05f, 0f));
            deadline = Time.realtimeSinceStartup + 3f;
            while (Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None), Has.Length.EqualTo(2));

            bool respawned = false;
            player.Respawned += () => respawned = true;
            Health health = player.GetComponent<Health>();
            player.TakeEnvironmentDamage(health.Max);
            deadline = Time.realtimeSinceStartup + 2f;
            while (!respawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(respawned, Is.True);
            yield return null;
            Assert.That(health.Current, Is.EqualTo(health.Max));
            Assert.That(Vector3.Distance(player.transform.position, player.RespawnPoint), Is.LessThan(0.3f));
            Assert.That(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None), Is.Empty);

            player.Teleport(new Vector3(21.5f, 0.05f, 0f));
            deadline = Time.realtimeSinceStartup + 3f;
            while (Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None), Has.Length.EqualTo(2));
        }
    }
}
