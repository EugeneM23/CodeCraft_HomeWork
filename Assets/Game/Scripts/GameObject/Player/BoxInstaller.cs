namespace Gameplay
{
    public class BoxInstaller : Installer
    {
        public override void Install(diContainer container)
        {
            container.Add("HELLO WORLD!");
        }
    }
}