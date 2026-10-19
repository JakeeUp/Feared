using Brawler.Player;
using UnityEngine;

namespace Brawler.Level
{
    /// <summary>
    /// Shows a section title the first time the player walks in ("THE NAVE — Where the faithful fell"), or with
    /// Toast Only a one-line hint at the bottom of the screen ("Grapple (E) the glowing rings").
    /// </summary>
    public sealed class ZoneTitle : PlayerTrigger
    {
        [SerializeField] private string _title = "THE NAVE";

        [SerializeField] private string _subtitle = string.Empty;

        [Tooltip("Show the title as a small toast line instead of the big banner (control hints).")]
        [SerializeField] private bool _toastOnly;

        private bool _shown;

        /// <inheritdoc />
        protected override void OnPlayerEnter(PlayerController player)
        {
            if (_shown || LevelBanner.Instance == null)
            {
                return;
            }

            _shown = true;
            if (_toastOnly)
            {
                LevelBanner.Instance.ShowToast(_title, 4f);
            }
            else
            {
                LevelBanner.Instance.ShowTitle(_title, _subtitle);
            }
        }
    }
}
