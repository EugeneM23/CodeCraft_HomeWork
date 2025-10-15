using System;
using JetBrains.Annotations;

namespace Gameplay
{
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public class InjectAttribute : Attribute
    {
    }
}