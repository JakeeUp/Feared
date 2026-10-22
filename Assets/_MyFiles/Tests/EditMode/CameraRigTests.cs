using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Brawler.Combat;
using Brawler.Level;
using Brawler.Player;
using Brawler.Presentation;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Brawler.Tests
{
    public sealed class CameraRigTests
    {
        private readonly List<Object> _created = new List<Object>();
        private CameraRig _rig;
        private Transform _target;
        private Camera _output;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            CinemachineImpulseManager.Instance.Clear();
            _target = Create("Target").transform;
            var root = Create("CameraRig");
            var pivot = Create("Pivot").transform;
            pivot.SetParent(root.transform, false);
            var shakeRoot = Create("ShakeRoot").transform;
            shakeRoot.SetParent(pivot, false);
            _output = Create("Main Camera").AddComponent<Camera>();
            _output.transform.SetParent(shakeRoot, false);
            _rig = root.AddComponent<CameraRig>();
            var settings = ScriptableObject.CreateInstance<CameraRigSettings>();
            _created.Add(settings);
            Assign(_rig, "_target", _target);
            Assign(_rig, "_camera", _output);
            Assign(_rig, "_pivot", pivot);
            Assign(_rig, "_settings", settings);
            _rig.ApplySettings();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _timeScale;
            CinemachineImpulseManager.Instance.Clear();
            CinemachineImpulseManager.Instance.IgnoreTimeScale = false;
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    if (_created[i] is GameObject gameObject)
                    {
                        if (gameObject.TryGetComponent(out CameraZoneDirector director))
                        {
                            Invoke(director, "OnDisable");
                        }

                        if (gameObject.TryGetComponent(out CameraZone zone))
                        {
                            Invoke(zone, "OnDisable");
                        }

                        if (gameObject.TryGetComponent(out ScreenShake shake))
                        {
                            Invoke(shake, "OnDisable");
                            Invoke(shake, "OnDestroy");
                        }
                    }

                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ApplySettings_ConvertsLegacyHierarchy_WithoutDuplicatingCameras()
        {
            _rig.ApplySettings();
            Assert.That(_output.transform.parent, Is.EqualTo(_rig.transform));
            Assert.That(_output.GetComponents<CinemachineBrain>(), Has.Length.EqualTo(1));
            Assert.That(_rig.GetComponentsInChildren<CinemachineCamera>(), Has.Length.EqualTo(1));
            var brain = _output.GetComponent<CinemachineBrain>();
            Assert.That(brain.IgnoreTimeScale, Is.False);
            Assert.That(brain.DefaultBlend.Time, Is.EqualTo(0.8f));
        }

        [Test]
        public void SnapToTarget_MovesOutputImmediately_AfterTeleport()
        {
            _rig.SnapToTarget();
            Vector3 initial = _output.transform.position;
            Vector3 teleport = new Vector3(50f, 8f, 2f);
            _target.position += teleport;
            _rig.SnapToTarget();
            Assert.That(Vector3.Distance(initial + teleport, _output.transform.position), Is.LessThan(0.001f));
        }

        [Test]
        public void SetFraming_UsesASecondCamera_AndReturnsToDefault()
        {
            _rig.SetFraming(3f, Vector3.up, 12f);
            var cameras = _rig.GetComponentsInChildren<CinemachineCamera>();
            Assert.That(cameras, Has.Length.EqualTo(2));
            var framing = cameras[0].Priority.Value == 10 ? cameras[0] : cameras[1];
            Assert.That(framing.GetComponent<CinemachinePositionComposer>().CameraDistance, Is.EqualTo(17f));
            _rig.SetFraming(0f, Vector3.zero);
            Assert.That(framing.Priority.Value, Is.EqualTo(-1));
        }

        [Test]
        public void Composer_HoldsAJump_AndFollowsPlanarMovement()
        {
            _rig.SnapToTarget();
            var camera = _rig.GetComponentInChildren<CinemachineCamera>();
            Vector3 initial = camera.State.RawPosition;
            _target.position = Vector3.up;
            camera.InternalUpdateCameraState(Vector3.up, 1f / 60f);
            Assert.That(Vector3.Distance(initial, camera.State.RawPosition), Is.LessThan(0.001f));
            _target.position += Vector3.right * 5f;
            camera.InternalUpdateCameraState(Vector3.up, 1f / 60f);
            Assert.That(camera.State.RawPosition.x, Is.GreaterThan(initial.x));
        }

        [Test]
        public void Director_RestoresDefaultCamera_WhenLeavingAZone_AndReusesItOnReturn()
        {
            var zone = Create("Room").AddComponent<CameraZone>();
            zone.GetComponent<BoxCollider>().size = Vector3.one * 10f;
            Invoke(zone, "Awake");
            Invoke(zone, "OnEnable");
            var director = Create("Director").AddComponent<CameraZoneDirector>();
            Assign(director, "_rig", _rig);
            Assign(director, "_player", _target);
            Invoke(director, "LateUpdate");
            var roomCamera = zone.GetComponentInChildren<CinemachineCamera>();
            Assert.That(roomCamera.Priority.Value, Is.EqualTo(10));
            _target.position = Vector3.right * 20f;
            Invoke(director, "LateUpdate");
            Assert.That(roomCamera.Priority.Value, Is.EqualTo(-1));
            _target.position = Vector3.zero;
            Invoke(director, "LateUpdate");
            Assert.That(roomCamera.Priority.Value, Is.EqualTo(10));
            Assert.That(zone.GetComponentsInChildren<CinemachineCamera>(), Has.Length.EqualTo(1));
            Invoke(director, "OnDisable");
            Assert.That(roomCamera.Priority.Value, Is.EqualTo(-1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SnapToTarget_RefreshesZoneFraming_BeforeTheNextLateUpdate(bool respawn)
        {
            PlayerController player = null;
            if (respawn)
            {
                player = _target.gameObject.AddComponent<PlayerController>();
                var health = _target.gameObject.AddComponent<Health>();
                Assign(player, "_health", health);
                Invoke(player, "Awake");
                player.RespawnPoint = Vector3.right * 20f;
            }

            var zone = Create("Room").AddComponent<CameraZone>();
            zone.GetComponent<BoxCollider>().size = Vector3.one * 10f;
            Invoke(zone, "Awake");
            Invoke(zone, "OnEnable");
            var director = Create("Director").AddComponent<CameraZoneDirector>();
            Assign(director, "_rig", _rig);
            Assign(director, "_player", _target);
            Invoke(director, "OnEnable");
            _rig.SnapToTarget();
            var roomCamera = zone.GetComponentInChildren<CinemachineCamera>();
            Assert.That(roomCamera.Priority.Value, Is.EqualTo(10));
            Assert.That(CinemachineCore.GetVirtualCamera(0), Is.SameAs(roomCamera));

            if (respawn)
            {
                Invoke(player, "Respawn");
            }
            else
            {
                _target.position = Vector3.right * 20f;
                _rig.SnapToTarget();
            }

            Assert.That(roomCamera.Priority.Value, Is.EqualTo(-1));
            var followCamera = _rig.GetComponentInChildren<CinemachineCamera>();
            Assert.That(CinemachineCore.GetVirtualCamera(0), Is.SameAs(followCamera));
            Assert.That(Vector3.Distance(_output.transform.position, followCamera.State.GetFinalPosition()), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator Shake_UsesLargestHitInAFrame_AndExpiresDuringHitstop()
        {
            var feedback = Create("Feedback").AddComponent<ScreenShake>();
            var settings = ScriptableObject.CreateInstance<FeedbackSettings>();
            _created.Add(settings);
            Assign(feedback, "_settings", settings);
            Invoke(feedback, "Awake");
            Time.timeScale = 0f;
            feedback.AddTrauma(0.1f);
            feedback.AddTrauma(0.3f);
            feedback.AddTrauma(0.2f);
            Invoke(feedback, "LateUpdate");
            var source = feedback.GetComponent<CinemachineImpulseSource>();
            Assert.That(source.ImpulseDefinition.TimeEnvelope.DecayTime, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(CinemachineImpulseManager.Instance.IgnoreTimeScale, Is.True);
            Assert.That(CinemachineImpulseManager.Instance.GetImpulseAt(Vector3.zero, false, 1, out _, out _), Is.True);
            float deadline = Time.realtimeSinceStartup + 0.15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(CinemachineImpulseManager.Instance.GetImpulseAt(Vector3.zero, false, 1, out _, out _), Is.False);
        }

        private GameObject Create(string name)
        {
            var gameObject = new GameObject(name);
            _created.Add(gameObject);
            return gameObject;
        }

        private static void Assign(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Invoke(Object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }
    }
}
