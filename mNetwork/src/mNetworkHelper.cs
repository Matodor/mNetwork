using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace mNetwork
{
    public class mHeader
    {
        public int MessageType { get; set; }
        public int MessageLength { get; set; }
    }

    public static class mNetworkHelper
    {
        public static TraceListener Logger { get; set; } = new ConsoleTraceListener();

        public static byte[] Header { get; set; } = { 4, 8, 15, 16, 23, 42 };
        public static int HeaderSize { get { return Header.Length + SizeOfTwoInt; } }

        private const int SizeOfInt = 4;
        private const int SizeOfTwoInt = SizeOfInt*2;

        public static byte[] CreateHeader(mHeader header)
        {
            var buffer = new byte[HeaderSize];
            for (int i = 0; i < Header.Length; i++)
                buffer[i] = Header[i];
            var typeBuffer = BitConverter.GetBytes(header.MessageType);
            var sizeBuffer = BitConverter.GetBytes(header.MessageLength);
            for (int i = 0; i < SizeOfInt; i++)
                buffer[Header.Length + i] = typeBuffer[i];
            for (int i = 0; i < SizeOfInt; i++)
                buffer[Header.Length + SizeOfInt + i] = sizeBuffer[i];
            return buffer;
        }

        public static bool ParseHeader(byte[] buffer, out mHeader header)
        {
            if (buffer.Length == HeaderSize)
            {
                for (int i = 0; i < Header.Length; i++)
                {
                    if (buffer[i] != Header[i])
                    {
                        header = null;
                        return false;
                    }

                    var type = BitConverter.ToInt32(buffer, Header.Length);
                    var size = BitConverter.ToInt32(buffer, Header.Length + SizeOfInt);
                    header = new mHeader {MessageType = type, MessageLength = size};
                    return true;
                }
            }

            header = null;
            return false;
        }

        public static CachedMessage GetCachedMessage(int type)
        {
            return _cachedMessages.FirstOrDefault(p => p.Value.PacketID == type).Value;
        }

        public static CachedMessage GetCachedMessage(Guid guid)
        {
            CachedMessage @out;
            if (_cachedMessages.TryGetValue(guid, out @out))
                return @out;
            return null;
        }

        private static readonly Dictionary<Guid, CachedMessage> _cachedMessages = new Dictionary<Guid, CachedMessage>();

        public static void Init(params string[] libs)
        {
            foreach (var lib in libs)
                AppDomain.CurrentDomain.Load(lib);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) 
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var attrs = type.GetCustomAttributes(typeof (mNetworkPacketAttribute), false);
                    if (attrs.Length > 0)
                    {
                        Logger.WriteLine("[helper] Add packet: " + type.Name); 
                        var packetId = ((mNetworkPacketAttribute) attrs[0]).PacketID;
                        var cachedMessage = new CachedMessage() {PacketID = packetId, MessageType = type};
                        CreateMessageCachedFields(type, cachedMessage);
                        _cachedMessages.Add(type.GUID, cachedMessage);
                    }
                }
            }
        }

        public delegate object LateBoundFieldGet(object target);
        public delegate void LateBoundFieldSet(object target, object value);
        //public delegate void LateBoundPropertySet(object target, object value);

        private static LateBoundFieldGet CreateFieldGetter(FieldInfo field)
        {
            var method = new DynamicMethod("Get" + field.Name, typeof(object), new[] { typeof(object) }, field.DeclaringType, true);
            var gen = method.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Castclass, field.DeclaringType); // Cast to source type
            gen.Emit(OpCodes.Ldfld, field);

            if (field.FieldType.IsValueType)
                gen.Emit(OpCodes.Box, field.FieldType);

            gen.Emit(OpCodes.Ret);

            var callback = (LateBoundFieldGet)method.CreateDelegate(typeof(LateBoundFieldGet));
            return callback;
        }

        private static LateBoundFieldSet CreateFieldSetter(FieldInfo field)
        {
            var method = new DynamicMethod("Set" + field.Name, null, new[] { typeof(object), typeof(object) }, field.DeclaringType, true);
            var gen = method.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0); // Load target to stack
            gen.Emit(OpCodes.Castclass, field.DeclaringType); // Cast target to source type
            gen.Emit(OpCodes.Ldarg_1); // Load value to stack
            gen.Emit(OpCodes.Unbox_Any, field.FieldType); // Unbox the value to its proper value type
            gen.Emit(OpCodes.Stfld, field); // Set the value to the input field
            gen.Emit(OpCodes.Ret);

            var callback = (LateBoundFieldSet)method.CreateDelegate(typeof(LateBoundFieldSet));
            return callback;
        }

        public class CachedMessage
        {
            public CachedFieldInfo[] CachedFields;
            public int PacketID;
            public Type MessageType;
        }

        public class CachedFieldInfo
        {
            public FieldInfo FieldInfo { get; }
            public LateBoundFieldSet Setter { get; }
            public LateBoundFieldGet Getter { get; }

            public CachedFieldInfo(FieldInfo fieldInfo, LateBoundFieldSet set, LateBoundFieldGet get)
            {
                FieldInfo = fieldInfo;
                Getter = get;
                Setter = set;
            }
        }

        private static void CreateMessageCachedFields(Type type, CachedMessage message)
        {
            var fields = type.GetFields().ToArray();
            message.CachedFields = new CachedFieldInfo[fields.Length];
            int i = 0;
            foreach (var fieldInfo in fields)
            {
                var cachedFieldInfo = new CachedFieldInfo(fieldInfo, CreateFieldSetter(fieldInfo), CreateFieldGetter(fieldInfo));
                message.CachedFields[i] = cachedFieldInfo;
                i++;
            }
        }

        public static mNetworkMessage Serialize(object anySerializableObject)
        {
            var packer = new mNetworkPacker(anySerializableObject);
            return new mNetworkMessage() { Type = packer.PacketID, Data = packer.Data };

            /*
                using (var memoryStream = new MemoryStream())
                {
                    new BinaryFormatter().Serialize(memoryStream, anySerializableObject);
                    return new mNetworkMessage { Data = memoryStream.ToArray() };
                }
            */
        }

        public static object Deserialize(mNetworkMessage message)
        {
            var unpacker = new mNetworkUnpacker(message.Data, message.Type);
            return unpacker.Object;

            /*
                using (var memoryStream = new MemoryStream(message.Data))
                    return new BinaryFormatter().Deserialize(memoryStream);
            */
        }
    }
}
