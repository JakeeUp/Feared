using System;
using Brawler.Combat;
using Brawler.Core;
using UnityEngine;

namespace Brawler.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerAudio : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;
        [SerializeField] private EventBusAsset _eventBus;
        [SerializeField] private AudioSource _source;
        [SerializeField] private AudioClip[] _attack;
        [SerializeField] private AudioClip[] _dash;
        [SerializeField] private AudioClip[] _chain;
        [SerializeField] private AudioClip[] _grapple;
        [SerializeField] private AudioClip[] _zip;
        [SerializeField] private AudioClip[] _hit;
        [SerializeField] private AudioClip[] _hurt;
        [SerializeField] private AudioClip[] _parry;
        [SerializeField, Range(0f, 0.3f)] private float _pitchJitter = 0.06f;

        private Action<HitEvent> _onHit;
        private Action<ParryEvent> _onParry;
        private IState<PlayerContext> _previousState;
        private int _previousAttackIndex = -1;
        private bool _wasChainFlying;

        private void Awake()
        {
            if (_player == null) _player = GetComponent<PlayerController>();
            if (_source == null) _source = GetComponent<AudioSource>();
            _onHit = OnHit;
            _onParry = OnParry;
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onHit);
                _eventBus.Subscribe(_onParry);
            }

            if (_player != null && _player.Context != null)
            {
                _previousState = _player.CurrentState;
                _previousAttackIndex = _player.Context.ComboTracker.CurrentIndex;
                _wasChainFlying = _player.Context.Chain.IsFlying;
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onHit);
                _eventBus.Unsubscribe(_onParry);
            }

            _previousState = null;
            _previousAttackIndex = -1;
            _wasChainFlying = false;
        }

        private void Update()
        {
            if (_player == null || _player.Context == null) return;

            PlayerContext context = _player.Context;
            IState<PlayerContext> state = _player.CurrentState;
            bool entered = !ReferenceEquals(state, _previousState);

            if (ReferenceEquals(state, context.States.Attack))
            {
                int index = context.ComboTracker.CurrentIndex;
                if (entered || index != _previousAttackIndex) Play(_attack);
                _previousAttackIndex = index;
            }
            else if (entered && ReferenceEquals(state, context.States.Dash)) Play(_dash);
            else if (entered && ReferenceEquals(state, context.States.Grapple)) Play(_grapple);
            else if (entered && ReferenceEquals(state, context.States.Zip)) Play(_zip);

            if (context.Chain.IsFlying && !_wasChainFlying) Play(_chain);
            _wasChainFlying = context.Chain.IsFlying;
            _previousState = state;
        }

        private void OnHit(HitEvent evt)
        {
            if (_player == null) return;
            if (evt.Target == transform) Play(_hurt);
            else if (evt.Damage.Source == transform) Play(_hit);
        }

        private void OnParry(ParryEvent evt)
        {
            if (_player != null && evt.Defender == transform) Play(_parry);
        }

        private void Play(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || _source == null) return;
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null) return;
            _source.pitch = 1f + UnityEngine.Random.Range(-_pitchJitter, _pitchJitter);
            _source.PlayOneShot(clip);
        }
    }
}
