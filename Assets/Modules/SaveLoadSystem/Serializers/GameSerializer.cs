using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Zenject;

namespace SaveLoadSystem
{
    public abstract class GameSerializer<TService, TData> : IGameSerializer
    {
        protected virtual string Key => typeof(TData).Name;

        [Inject] protected TService _service;

        void IGameSerializer.Serialize(IDictionary<string, string> saveData)
        {
            TData data = Serialize(_service);
            saveData[Key] = JsonConvert.SerializeObject(data);
        }

        void IGameSerializer.Deserialize(IDictionary<string, string> LoadData)
        {
            if (!LoadData.TryGetValue(Key, out string json))
                return;

            TData data = JsonConvert.DeserializeObject<TData>(json);
            Deserialize(_service, data);
        }

        protected abstract TData Serialize(TService service);

        protected virtual void Deserialize(TService service, TData data)
        {
            var serviceMembers = typeof(TService).GetMembersWithAttribute<SaveFieldAttribute>();
            var dataMembers = typeof(TData).GetMembersWithAttribute<SaveFieldAttribute>()
                .ToDictionary(m => m.Name);

            foreach (MemberInfo member in serviceMembers)
            {
                if (dataMembers.TryGetValue(member.Name, out var dataMember))
                {
                    object value = dataMember.GetMemberValue(data);
                    member.SetMemberValue(service, value);
                }
            }
        }
    }
}