using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ScreenNavigation.Tests
{
    /// <summary>Stack rules on fake screens that write every call into one shared log.</summary>
    public class ScreenStackTests
    {
        private sealed class FakeScreen : IScreen
        {
            private readonly List<string> _log;
            private readonly string _name;

            public FakeScreen(string name, List<string> log, bool overlay, bool canGoBack)
            {
                _name = name;
                _log = log;
                IsOverlay = overlay;
                CanGoBack = canGoBack;
            }

            public bool IsOverlay { get; }
            public bool CanGoBack { get; }

            /// <summary>Runs inside Show — to call the stack from a screen callback.</summary>
            public Action OnShow { get; set; }

            public void Open() => _log.Add($"{_name}.Open");
            public void Close() => _log.Add($"{_name}.Close");

            public void Show()
            {
                _log.Add($"{_name}.Show");
                OnShow?.Invoke();
            }

            public void Hide() => _log.Add($"{_name}.Hide");
            public void Focus() => _log.Add($"{_name}.Focus");
            public void Blur() => _log.Add($"{_name}.Blur");
            public override string ToString() => _name;
        }

        private readonly List<string> _log = new();
        private ScreenStack _stack;
        private int _changed;

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _changed = 0;
            _stack = new ScreenStack();
            _stack.Changed += () => _changed++;
        }

        private FakeScreen Screen(string name, bool overlay = false, bool canGoBack = true) =>
            new(name, _log, overlay, canGoBack);

        private void AssertLog(params string[] expected) => CollectionAssert.AreEqual(expected, _log);

        [Test]
        public void Push_OnEmpty_OpensShowsAndFocuses()
        {
            FakeScreen a = Screen("A");

            _stack.Push(a);

            AssertLog("A.Open", "A.Show", "A.Focus");
            Assert.AreSame(a, _stack.Top);
            Assert.AreEqual(1, _stack.Count);
            Assert.IsTrue(_stack.Contains(a));
        }

        [Test]
        public void Push_Opaque_BlursAndHidesPrevious()
        {
            _stack.Push(Screen("A"));
            _log.Clear();

            _stack.Push(Screen("B"));

            AssertLog("A.Blur", "B.Open", "A.Hide", "B.Show", "B.Focus");
        }

        [Test]
        public void Push_Overlay_KeepsPreviousVisible()
        {
            _stack.Push(Screen("A"));
            _log.Clear();

            _stack.Push(Screen("D", overlay: true));

            AssertLog("A.Blur", "D.Open", "D.Show", "D.Focus");
        }

        [Test]
        public void Pop_Opaque_ShowsAndFocusesPrevious()
        {
            FakeScreen a = Screen("A");
            _stack.Push(a);
            _stack.Push(Screen("B"));
            _log.Clear();

            bool popped = _stack.Pop();

            Assert.IsTrue(popped);
            AssertLog("B.Blur", "B.Hide", "B.Close", "A.Show", "A.Focus");
            Assert.AreSame(a, _stack.Top);
        }

        [Test]
        public void Pop_Overlay_RefocusesPreviousWithoutShowing()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("D", overlay: true));
            _log.Clear();

            _stack.Pop();

            AssertLog("D.Blur", "D.Hide", "D.Close", "A.Focus");
        }

        [Test]
        public void Pop_Empty_ReturnsFalseWithoutChanged()
        {
            Assert.IsFalse(_stack.Pop());
            Assert.AreEqual(0, _changed);
            Assert.IsNull(_stack.Top);
            Assert.IsTrue(_stack.IsEmpty);
        }

        [Test]
        public void Visibility_OverlayChain_ShowsDownToFirstOpaque()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B"));
            _stack.Push(Screen("C", overlay: true));
            _stack.Push(Screen("D", overlay: true));
            _log.Clear();

            _stack.Clear();

            // A was hidden under B; B, C and D are visible and hide top-down
            AssertLog("D.Blur", "D.Hide", "C.Hide", "B.Hide", "D.Close", "C.Close", "B.Close", "A.Close");
            Assert.IsTrue(_stack.IsEmpty);
        }

        [Test]
        public void Back_RootCannotGoBack_KeepsScreen()
        {
            FakeScreen root = Screen("Root", canGoBack: false);
            _stack.Push(root);
            _log.Clear();
            _changed = 0;

            Assert.IsFalse(_stack.Back());

            AssertLog();
            Assert.AreSame(root, _stack.Top);
            Assert.AreEqual(0, _changed);
        }

        [Test]
        public void Back_CanGoBack_Pops()
        {
            FakeScreen a = Screen("A");
            _stack.Push(a);
            _stack.Push(Screen("B"));

            Assert.IsTrue(_stack.Back());

            Assert.AreSame(a, _stack.Top);
        }

        [Test]
        public void Back_Empty_ReturnsFalse()
        {
            Assert.IsFalse(_stack.Back());
            Assert.AreEqual(0, _changed);
        }

        [Test]
        public void Replace_OpaqueOverOpaque_DoesNotShowScreenBelow()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B"));
            _log.Clear();

            _stack.Replace(Screen("C"));

            AssertLog("B.Blur", "C.Open", "B.Hide", "B.Close", "C.Show", "C.Focus");
            Assert.AreEqual(2, _stack.Count);
        }

        [Test]
        public void Replace_WithOverlay_ShowsScreenBelow()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B"));
            _log.Clear();

            _stack.Replace(Screen("D", overlay: true));

            AssertLog("B.Blur", "D.Open", "B.Hide", "B.Close", "A.Show", "D.Show", "D.Focus");
        }

        [Test]
        public void Replace_Empty_Pushes()
        {
            _stack.Replace(Screen("A"));

            AssertLog("A.Open", "A.Show", "A.Focus");
        }

        [Test]
        public void Clear_OverlayOverScreen_HidesAndClosesTopDown()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B", overlay: true));
            _log.Clear();

            _stack.Clear();

            AssertLog("B.Blur", "B.Hide", "A.Hide", "B.Close", "A.Close");
        }

        [Test]
        public void Clear_Empty_DoesNotRaiseChanged()
        {
            _stack.Clear();

            Assert.AreEqual(0, _changed);
        }

        [Test]
        public void Changed_RaisedOncePerOperation()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B"));
            _stack.Replace(Screen("C"));
            _stack.Back();
            _stack.Pop();

            Assert.AreEqual(5, _changed);
        }

        [Test]
        public void Push_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _stack.Push(null));
        }

        [Test]
        public void Push_SameScreenTwice_Throws()
        {
            FakeScreen a = Screen("A");
            _stack.Push(a);

            Assert.Throws<InvalidOperationException>(() => _stack.Push(a));
            Assert.AreEqual(1, _stack.Count);
        }

        [Test]
        public void Replace_WithScreenInStack_Throws()
        {
            FakeScreen a = Screen("A");
            _stack.Push(a);
            _stack.Push(Screen("B"));

            Assert.Throws<InvalidOperationException>(() => _stack.Replace(a));
        }

        [Test]
        public void Push_FromScreenCallback_Throws()
        {
            FakeScreen a = Screen("A");
            a.OnShow = () => _stack.Push(Screen("B"));

            Assert.Throws<InvalidOperationException>(() => _stack.Push(a));
        }

        [Test]
        public void Push_FromChangedHandler_Allowed()
        {
            FakeScreen b = Screen("B");
            bool pushed = false;
            _stack.Changed += () =>
            {
                if (pushed)
                    return;
                pushed = true;
                _stack.Push(b);
            };

            _stack.Push(Screen("A"));

            Assert.AreSame(b, _stack.Top);
            Assert.AreEqual(2, _stack.Count);
        }
    }
}
