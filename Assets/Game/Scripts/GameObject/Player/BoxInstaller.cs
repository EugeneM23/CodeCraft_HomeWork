namespace Gameplay
{
    public class BoxInstaller : Installer
    {
        public override void Install(DiContainer container)
        {
            container.Add("HELLO WORLD!");
        }
    }
}