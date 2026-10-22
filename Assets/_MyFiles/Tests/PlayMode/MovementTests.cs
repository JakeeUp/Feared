using System.Collections;
using Brawler.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Brawler.Tests
{
    /// <summary>Real keyboard input through the scene's own input wiring moves the hero, outdoors and in the nave.</summary>
    public sealed class MovementTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator HoldingRight_MovesTheHero()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("Cathedral");
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null);

            foreach (Vector3 start in new[] { player.transform.position, new Vector3(4f, 0.05f, 0f) })
            {
                player.Teleport(start);
                yield return new WaitForSeconds(0.2f);
                float x = player.transform.position.x;
                Press(keyboard.dKey);
                yield return new WaitForSeconds(0.5f);
                Release(keyboard.dKey);
                Assert.That(player.transform.position.x, Is.GreaterThan(x + 1f), $"hero should run right from {start}");

                float z = player.transform.position.z;
                Press(keyboard.wKey);
                yield return new WaitForSeconds(0.3f);
                Release(keyboard.wKey);
                Assert.That(player.transform.position.z, Is.GreaterThan(z + 0.3f), $"hero should walk into the depth from {start}");
            }
        }
    }
}
