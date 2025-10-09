using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SaveLoadSystem
{
    public static class ReflectionExtensions
    {
        public static IEnumerable<MemberInfo> GetMembersWithAttribute<TAttr>(this Type type, BindingFlags flags)
            where TAttr : Attribute
        {
            return type
                .GetMembers(flags)
                .Where(m => Attribute.IsDefined((MemberInfo)m, typeof(TAttr)));
        }

        public static IEnumerable<MemberInfo> GetMembersWithAttribute<TAttr>(this Type type)
            where TAttr : Attribute
        {
            return type.GetMembersWithAttribute<TAttr>(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        public static object GetMemberValue(this MemberInfo member, object obj)
        {
            if (member is PropertyInfo property)
                return property.GetValue(obj);

            if (member is FieldInfo field)
                return field.GetValue(obj);

            return null;
        }

        public static void SetMemberValue(this MemberInfo member, object obj, object value)
        {
            if (member is PropertyInfo property)
            {
                property.SetValue(obj, value);
            }
            else if (member is FieldInfo field)
            {
                field.SetValue(obj, value);
            }
        }
    }
}