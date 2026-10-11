// COPYRIGHT 2026 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Orts.Common
{
    /// <summary>
    /// Serializes the command log as JSON, replacing BinaryFormatter (removed in .NET 9).
    /// Like BinaryFormatter, it stores every instance field (including private and inherited ones,
    /// except those marked [NonSerialized]) and recreates objects without running their constructors,
    /// because command constructors add themselves to the live log.
    /// </summary>
    public static class CommandSerializer
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = new CommandBinder(),
            ContractResolver = new FieldContractResolver(),
            Formatting = Formatting.None,
        };

        public static void Serialize(Stream stream, List<ICommand> commands)
        {
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                JsonSerializer.Create(Settings).Serialize(writer, commands, typeof(List<ICommand>));
        }

        public static List<ICommand> Deserialize(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true))
            using (var json = new JsonTextReader(reader))
                return JsonSerializer.Create(Settings).Deserialize<List<ICommand>>(json) ?? new List<ICommand>();
        }

        class FieldContractResolver : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                var members = new List<MemberInfo>();
                for (var type = objectType; type != null && type != typeof(object); type = type.BaseType)
                    members.AddRange(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Where(field => !field.IsNotSerialized));
                return members;
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                // Qualify with the declaring type so that private fields with the same name in base and derived classes do not clash.
                property.PropertyName = member.DeclaringType.Name + "." + member.Name;
                property.Readable = true;
                property.Writable = true;
                property.Ignored = false;
                return property;
            }

            protected override JsonObjectContract CreateObjectContract(Type objectType)
            {
                var contract = base.CreateObjectContract(objectType);
                if (typeof(ICommand).IsAssignableFrom(objectType) && !objectType.IsAbstract)
                    contract.DefaultCreator = () => RuntimeHelpers.GetUninitializedObject(objectType);
                return contract;
            }
        }

        /// <summary>
        /// Only allows command types (and value types they contain) to be created from a replay file.
        /// </summary>
        class CommandBinder : ISerializationBinder
        {
            static readonly Dictionary<string, Type> KnownTypes = new Dictionary<string, Type>();

            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                assemblyName = serializedType.Assembly.GetName().Name;
                typeName = serializedType.FullName;
            }

            public Type BindToType(string assemblyName, string typeName)
            {
                var key = assemblyName + "|" + typeName;
                lock (KnownTypes)
                {
                    if (KnownTypes.TryGetValue(key, out var known))
                        return known;
                    var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
                    var type = assembly?.GetType(typeName, throwOnError: false);
                    if (type == null || !(typeof(ICommand).IsAssignableFrom(type) || type.IsValueType))
                        throw new JsonSerializationException($"Type {typeName} is not allowed in a command log");
                    KnownTypes[key] = type;
                    return type;
                }
            }
        }
    }
}
