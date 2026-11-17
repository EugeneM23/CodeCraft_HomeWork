using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The blackboard colours each kind of data as in Unreal (the user's word, 15.11.2026): a number, a flag, an object —
    /// each its own colour, the same as the pins and wires of that kind on the canvas.
    /// </summary>
    public sealed class BlackboardColorTests
    {
        [Test]
        public void EachKind_ItsOwnColour_TheSameAsItsPins()
        {
            Color number = BlackboardPanel.ColorOf(VariableType.Float);
            Color flag = BlackboardPanel.ColorOf(VariableType.Bool);
            Color thing = BlackboardPanel.ColorOf(VariableType.Transform);

            Assert.AreNotEqual(number, flag);
            Assert.AreNotEqual(number, thing);
            Assert.AreNotEqual(flag, thing);
            Assert.AreEqual(number, BlackboardPanel.ColorOf(VariableType.Int), "A whole number is a number pin too");
            Assert.AreEqual(GraphViewCanvas.PinColor(typeof(bool)), flag, "A flag row is the colour of a flag pin");
            Assert.AreEqual(GraphViewCanvas.PinColor(typeof(Transform)), thing);
        }
    }
}
