using System;
using Brawler.Combat;
using Brawler.Core;
using Brawler.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Brawler.Presentation
{
    /// <summary>
    /// Hit streak and score. Every <see cref="HitEvent"/> dealt by the player extends the streak and earns damage ×
    /// a streak multiplier into the <see cref="ScoreWallet"/> (spendable in the shop); the streak ends after
    /// <c>ComboWindow</c> seconds without a hit, or as soon as the player gets hit. Shows the streak with a punch on
    /// each hit and fades it out when it ends; shows the wallet balance as the score.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComboCounter : MonoBehaviour
    {
        // ============ Serialized Fields ============

        [Header("Sources")]
        [Tooltip("Shared event bus. Listens for HitEvent.")]
        [SerializeField] private EventBusAsset _eventBus;

        [Tooltip("The player root: hits whose Source is this count; hits whose Target is this break the streak.")]
        [SerializeField] private Transform _player;

        [Tooltip("Where earned score goes (and what the score readout shows).")]
        [SerializeField] private ScoreWallet _wallet;

        [Header("UI")]
        [SerializeField] private Text _hitsText;
        [SerializeField] private Text _scoreText;

        [Tooltip("Fades the streak display in/out.")]
        [SerializeField] private CanvasGroup _streakGroup;

        [Header("Rules")]
        [Tooltip("Seconds without landing a hit before the streak ends (scaled time: pauses with the game).")]
        [SerializeField, Min(0.1f)] private float _comboWindow = 2f;

        [Tooltip("Score multiplier added per hit in the streak (0.1 = the 10th hit scores 1.9x).")]
        [SerializeField, Min(0f)] private float _multiplierPerHit = 0.1f;

        [Tooltip("Hit-count punch size on each hit.")]
        [SerializeField, Min(1f)] private float _punchScale = 1.35f;

        // ============ State ============

        private Action<HitEvent> _onHit;
        private Action _onWalletChanged;
        private int _hits;
        private int _best;
        private float _lastHitTime;
        private float _punch;

        // ============ Properties ============

        /// <summary>Hits in the current streak.</summary>
        public int Hits => _hits;

        /// <summary>Longest streak this session.</summary>
        public int Best => _best;

        // ============ Lifecycle ============

        private void Awake()
        {
            _onHit = OnHit;
            _onWalletChanged = RefreshScore;
            RefreshScore();
            if (_streakGroup != null)
            {
                _streakGroup.alpha = 0f;
            }
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onHit);
            }

            if (_wallet != null)
            {
                _wallet.Changed += _onWalletChanged;
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onHit);
            }

            if (_wallet != null)
            {
                _wallet.Changed -= _onWalletChanged;
            }
        }

        private void Update()
        {
            if (_hits > 0 && Time.time - _lastHitTime > _comboWindow)
            {
                EndStreak();
            }

            // Punch decays on unscaled time so it still animates through hitstop.
            float dt = Time.unscaledDeltaTime;
            _punch = Mathf.MoveTowards(_punch, 0f, dt * 4f);
            if (_hitsText != null)
            {
                _hitsText.rectTransform.localScale = Vector3.one * (1f + (_punchScale - 1f) * _punch);
            }

            if (_streakGroup != null)
            {
                _streakGroup.alpha = Mathf.MoveTowards(_streakGroup.alpha, _hits > 0 ? 1f : 0f, dt * (_hits > 0 ? 12f : 2f));
            }
        }

        // ============ Helpers ============

        private void OnHit(HitEvent evt)
        {
            if (_player == null)
            {
                return;
            }

            if (evt.Target == _player)
            {
                EndStreak();
                return;
            }

            if (evt.Damage.Source != _player)
            {
                return;
            }

            _hits++;
            _best = Mathf.Max(_best, _hits);
            _lastHitTime = Time.time;
            _punch = 1f;
            if (_wallet != null)
            {
                _wallet.Add(Mathf.RoundToInt(evt.Damage.Amount * (1f + (_hits - 1) * _multiplierPerHit)));
            }

            if (_hitsText != null)
            {
                _hitsText.text = $"{_hits} HITS";
            }
        }

        private void EndStreak()
        {
            _hits = 0;
        }

        private void RefreshScore()
        {
            if (_scoreText != null)
            {
                _scoreText.text = $"SCORE  {(_wallet != null ? _wallet.Balance : 0):N0}";
            }
        }
    }
}
