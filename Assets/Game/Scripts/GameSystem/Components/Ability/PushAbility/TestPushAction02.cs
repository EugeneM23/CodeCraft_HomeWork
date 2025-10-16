using UnityEngine;

namespace Gameplay.Ability
{
    public class TestPushAction02 : PushAbility.IAction
    {
        public void Invoke()
        {
                Debug.Log("Action: TestPushAction02");
        }
    }
}