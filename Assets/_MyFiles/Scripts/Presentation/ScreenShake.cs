using System;
using Brawler.Combat;
using Brawler.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Brawler.Presentation
{
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class ScreenShake : MonoBehaviour
    {
        [SerializeField] private EventBusAsset _eventBus;
        [SerializeField] private FeedbackSettings _settings;
        private CinemachineImpulseSource _source;
        private NoiseSettings _noise;
        private float _pendingTrauma;
        private Action<HitEvent> _onHit;
        private Action<ParryEvent> _onParry;

        private void Awake()
        {
            _onHit = OnHit;
            _onParry = OnParry;
            _source = GetComponent<CinemachineImpulseSource>();
            if (_source == null)
            {
                _source = gameObject.AddComponent<CinemachineImpulseSource>();
            }

            _noise = ScriptableObject.CreateInstance<NoiseSettings>();
            _noise.hideFlags = HideFlags.HideAndDontSave;
            _noise.PositionNoise = new[]
            {
                new NoiseSettings.TransformNoiseParams
                {
                    X = new NoiseSettings.NoiseParams { Frequency = 1f, Amplitude = 2f },
                    Y = new NoiseSettings.NoiseParams { Frequency = 1.13f, Amplitude = 2f }
                }
            };
            _source.ImpulseDefinition = new CinemachineImpulseDefinition
            {
                ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Legacy,
                RawSignal = _noise,
                ImpactRadius = 100000f,
                DissipationDistance = 0f,
                PropagationSpeed = float.MaxValue
            };
            _source.DefaultVelocity = Vector3.down;
            CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onHit);
                _eventBus.Subscribe(_onParry);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe(_onHit);
                _eventBus.Unsubscribe(_onParry);
            }

            _pendingTrauma = 0f;
        }

        private void OnDestroy()
        {
            if (_noise != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_noise);
                }
                else
                {
                    DestroyImmediate(_noise);
                }
            }
        }

        private void LateUpdate()
        {
            float trauma = _pendingTrauma;
            _pendingTrauma = 0f;
            if (trauma <= 0f || _settings == null || _source == null)
            {
                return;
            }

            _source.ImpulseDefinition.AmplitudeGain = _settings.ShakeMaxOffset;
            _source.ImpulseDefinition.FrequencyGain = _settings.ShakeFrequency;
            _source.ImpulseDefinition.TimeEnvelope = new CinemachineImpulseManager.EnvelopeDefinition
            {
                DecayTime = trauma / Mathf.Max(0.01f, _settings.TraumaDecayPerSecond)
            };
            _source.GenerateImpulseWithForce(trauma * trauma);
        }

        public void AddTrauma(float amount)
        {
            if (amount > 0f)
            {
                _pendingTrauma = Mathf.Max(_pendingTrauma, Mathf.Clamp01(amount));
            }
        }

        private void OnHit(HitEvent evt)
        {
            AddTrauma(evt.ScreenShake);
        }

        private void OnParry(ParryEvent evt)
        {
            AddTrauma(evt.ScreenShake);
        }
    }
}
