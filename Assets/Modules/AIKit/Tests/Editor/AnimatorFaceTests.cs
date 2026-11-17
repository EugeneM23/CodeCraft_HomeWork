using System.Collections.Generic;
using AIKit.Mecanim;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The AI Animator's face (IControllerFace): when its controller plays somewhere else than on the Animator (a graph a
    /// bridge put in front of it — the Animator's own Controller field empty), the AI Animator finds the controller's
    /// parameters through its face and writes them there; an action something else shows is left alone.
    /// </summary>
    public sealed class AnimatorFaceTests
    {
        private const float DT = 0.05f;
        private GraphRig _rig;
        private AIAnimator _ai;

        [TearDown]
        public void TearDown()
        {
            GraphRig.DestroyAll();
            if (_controller != null)
                Object.DestroyImmediate(_controller);
        }

        private sealed class FakeFace : IControllerFace
        {
            public readonly Dictionary<int, float> Floats = new();
            public readonly Dictionary<int, bool> Bools = new();
            public readonly List<int> CrossFades = new();
            public readonly HashSet<string> Known = new() { "Speed", "Combat" };
            public RuntimeAnimatorController Controller { get; set; }
            public int LayerCount => 1;
            public string GetLayerName(int layer) => "Base Layer";
            public int GetLayerIndex(string layer) => 0;
            public bool HasParameter(string parameter, AnimatorControllerParameterType type) => Known.Contains(parameter);
            public void SetFloat(int id, float value) => Floats[id] = value;
            public void SetBool(int id, bool value) => Bools[id] = value;
            public void SetInteger(int id, int value) { }
            public void SetTrigger(int id) { }
            public void ResetTrigger(int id) { }
            public bool HasState(int layer, int stateHash) => true;
            public void CrossFadeInFixedTime(int stateHash, float duration, int layer, float offset = 0f) => CrossFades.Add(stateHash);
            public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) => default;
            public AnimatorStateInfo GetNextAnimatorStateInfo(int layer) => default;
            public bool IsInTransition(int layer) => false;
        }

        private FakeFace Shown()
        {
            _rig = new GraphRig(trace: false);
            _rig.Agent.gameObject.SetActive(true);
            var model = new GameObject("Model");
            model.transform.SetParent(_rig.Agent.transform, false);
            model.AddComponent<Animator>();                 // no controller of its own: it plays in front of it
            _ai = model.AddComponent<AIAnimator>();
            Assert.IsTrue(_ai.Attach(_rig.Agent));
            _controller = new UnityEditor.Animations.AnimatorController { name = "Somewhere Else" };
            var face = new FakeFace { Controller = _controller };
            _ai.Face = face;
            return face;
        }

        private UnityEditor.Animations.AnimatorController _controller;

        [Test]
        public void AIAnimator_AFaceInFrontOfTheAnimator_TheParametersAreFoundAndWrittenThroughIt()
        {
            FakeFace face = Shown();
            for (int i = 0; i < 20; i++)
            {
                _rig.Agent.transform.position += Vector3.forward * 2f * DT;
                _ai.FrameTick(DT);
            }

            int speed = Animator.StringToHash(_ai.Parameters.Speed);
            Assert.IsTrue(face.Floats.TryGetValue(speed, out float shown), "the speed is written through the face");
            Assert.Greater(shown, 1f);
            Assert.IsTrue(face.Bools.ContainsKey(Animator.StringToHash(_ai.Parameters.Combat)));
        }

        [Test]
        public void AIAnimator_AnActionShownElsewhere_IsLeftAlone_OthersAreShown()
        {
            FakeFace face = Shown();
            _ai.ShowsActionElsewhere = action => action == "Slash";

            _rig.Context.Actions.Begin("Slash");
            _ai.FrameTick(DT);
            Assert.AreEqual(0, face.CrossFades.Count, "a bridge shows it");
            Assert.IsNull(_ai.CurrentAction);

            _rig.Context.Actions.Begin("Dig");
            _ai.FrameTick(DT);
            Assert.AreEqual(1, face.CrossFades.Count, "its own action is cross-faded through the face");
            Assert.AreEqual("Dig", _ai.CurrentAction);
        }
    }
}
