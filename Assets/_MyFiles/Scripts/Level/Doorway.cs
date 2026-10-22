using System.Collections;
using Brawler.Player;
using Brawler.Presentation;
using UnityEngine;

namespace Brawler.Level
{
    /// <summary>
    /// A door between two areas that don't touch in the world (the courtyard and the nave): walking in fades the screen
    /// to black, moves the player to <see cref="_destination"/>, cuts the camera there and fades back in.
    /// </summary>
    public sealed class Doorway : PlayerTrigger
    {
        [Tooltip("Where the player comes out.")]
        [SerializeField] private Transform _destination;

        [Tooltip("Full-screen black overlay faded in and out. Optional: without one the cut is instant.")]
        [SerializeField] private CanvasGroup _fade;

        [Tooltip("Camera rig to cut to the new position.")]
        [SerializeField] private CameraRig _rig;

        [Tooltip("Seconds for each half of the fade (unscaled).")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.35f;

        private bool _busy;

        /// <inheritdoc />
        protected override void OnPlayerEnter(PlayerController player)
        {
            if (!_busy && _destination != null)
            {
                StartCoroutine(Pass(player));
            }
        }

        private IEnumerator Pass(PlayerController player)
        {
            _busy = true;
            yield return Fade(0f, 1f);
            player.Teleport(_destination.position);
            if (_rig != null)
            {
                _rig.SnapToTarget();
            }

            yield return Fade(1f, 0f);
            _busy = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_fade == null)
            {
                yield break;
            }

            _fade.blocksRaycasts = to > 0f;
            for (float t = 0f; t < _fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _fade.alpha = Mathf.Lerp(from, to, t / _fadeSeconds);
                yield return null;
            }

            _fade.alpha = to;
        }
    }
}
