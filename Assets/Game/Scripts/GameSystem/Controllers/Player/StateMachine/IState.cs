namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
{
    public interface IState
    {
        public void Enter();
        public void Exit();

        public void Tick();
    }
}