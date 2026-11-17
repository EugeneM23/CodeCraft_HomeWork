using System;
using System.Reflection;

namespace Saving.Objects
{
    /// <summary>A field or a property that a save reads and writes: its name, its key in the data and its type.</summary>
    public sealed class SaveMember
    {
        private readonly FieldInfo _field;
        private readonly PropertyInfo _property;

        internal SaveMember(FieldInfo field, string key, bool moment = false)
        {
            _field = field;
            Key = key ?? field.Name;
            IsMoment = moment;
        }

        internal SaveMember(PropertyInfo property, string key, bool moment = false)
        {
            _property = property;
            Key = key ?? property.Name;
            IsMoment = moment;
        }

        /// <summary>
        /// A moment of Time.time a script keeps to itself (m_LastTimeShot, TimeLastSeenTarget): saved as "how long
        /// ago" and put back against the clock of the load, so the time since the shot is the same after a load —
        /// in the same run of the game and in the next one, where the clock starts from zero.
        /// </summary>
        public bool IsMoment { get; }

        /// <summary>The key a moment is saved under, as seconds ago.</summary>
        internal string AgoKey => Key + "@ago";

        public string Name => _field != null ? _field.Name : _property.Name;
        public string Key { get; }
        public Type Type => _field != null ? _field.FieldType : _property.PropertyType;
        public bool IsProperty => _property != null;

        /// <summary>Holds Unity objects (directly or in a collection): saved as references.</summary>
        public bool IsReference => SaveMembers.HoldsUnityObjects(Type);

        public object GetValue(object target) => _field != null ? _field.GetValue(target) : _property.GetValue(target);

        public void SetValue(object target, object value)
        {
            if (_field != null)
                _field.SetValue(target, value);
            else
                _property.SetValue(target, value);
        }

        public override string ToString() => Key == Name ? Name : $"{Name} ({Key})";
    }
}
