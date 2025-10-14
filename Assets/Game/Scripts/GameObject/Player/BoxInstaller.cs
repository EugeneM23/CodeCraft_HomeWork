namespace Gameplay
{
    public class BoxInstaller : Installer
    {
        public override void Install(DiContainer container)
        {
            container.BindSingle("HELLO WORLD!");
        }
    }
}