using System;
using JetBrains.Annotations;

namespace Gameplay
{
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method)]
    public class InjectAttribute : Attribute
    {
    }
}