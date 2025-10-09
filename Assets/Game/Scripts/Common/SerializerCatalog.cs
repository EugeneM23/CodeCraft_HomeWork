using System;
using System.Collections.Generic;
using Modules.Entities;
using UnityEngine;

namespace SampleGame.Gameplay
{
    public static class SerializerCatalog
    {
        private static readonly Dictionary<Type, Type> _serializers = new()
        {
            { typeof(Countdown), typeof(CountdownSerealizer) },
            { typeof(Damage), typeof(DamageSerializer) },
            { typeof(Team), typeof(TeamSerializer) },
            { typeof(Transform), typeof(TransformSerializer) },
            { typeof(EntityWorld), typeof(EntityWorldSerializer) },
            { typeof(Health), typeof(HealthSerializer) },
            { typeof(MoveSpeed), typeof(MoveSpeedSerializer) },
            { typeof(ResourceBag), typeof(ResourceBagSerializer) },
            { typeof(DestinationPoint), typeof(DestinationPointSerializer) },
            { typeof(TargetObject), typeof(TargetObjectSerializer) },
            { typeof(ProductionOrder), typeof(ProductionOrderSerializer) }
        };

        public static Type GetSerializer(Type type) => _serializers[type];

        public static bool Has(Type type) => _serializers.ContainsKey(type);
    }
}