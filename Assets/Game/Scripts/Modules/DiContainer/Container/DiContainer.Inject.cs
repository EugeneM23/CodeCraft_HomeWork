namespace Gameplay
{
    public partial class DiContainer
    {
        public void Inject(object target)
        {
            if (target == null) return;

            InjectMethods(target);
            InjectFields(target);
            InjectProperties(target);
        }
    }
}