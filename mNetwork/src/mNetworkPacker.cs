using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace mNetwork
{
    public class mNetworkPacker
    {
        public int PacketId { get; }
        public byte[] Data { get; }

        private readonly List<byte> _data;
        private readonly object _packetMsg;

        public mNetworkPacker(object packetMsg)
        {
            var cachedMessage = mNetworkHelper.GetCachedMessage(packetMsg.GetType().GUID);
            if (cachedMessage == null)
                return;

            _packetMsg = packetMsg;
            _data = new List<byte>();

            for (int i = 0; i < cachedMessage.CachedFields.Length; i++)
            {
                if (cachedMessage.CachedFields[i].FieldInfo.FieldType.IsArray)
                    PackArray(cachedMessage.CachedFields[i]);
                else
                    PackField(cachedMessage.CachedFields[i]);
            }

            PacketId = cachedMessage.PacketId;
            Data = _data.ToArray();
        }

        private void PackField(mNetworkHelper.CachedFieldInfo field)
        {
            var fieldType = field.FieldInfo.FieldType;
            var value = field.Getter(_packetMsg);

            if (fieldType.IsPrimitive)
            {
                GetBytes(fieldType, value);
            }
            else if (fieldType == typeof (string))
            {
                GetStringBytes((string) value);
            }
            else if (fieldType == typeof (decimal))
            {
                GetDecimalBytes((decimal) value);
            }
        }

        private void GetStringBytes(string value)
        {
            var strBytes = Encoding.UTF8.GetBytes(value);
            var lengthBytes = BitConverter.GetBytes((ushort) strBytes.Length);

            _data.AddRange(lengthBytes);
            _data.AddRange(strBytes);
        }

        private void GetDecimalBytes(decimal value)
        {
            int[] bits = decimal.GetBits((decimal)value);
            var i1 = BitConverter.GetBytes(bits[0]);
            var i2 = BitConverter.GetBytes(bits[1]);
            var i3 = BitConverter.GetBytes(bits[2]);
            var i4 = BitConverter.GetBytes(bits[3]);

            _data.AddRange(new byte[]
            {
                i1[0], i1[1], i1[2], i1[3],
                i2[0], i2[1], i2[2], i2[3],
                i3[0], i3[1], i3[2], i3[3],
                i4[0], i4[1], i4[2], i4[3],
            });
        }

        private void GetBytes(Type fieldType, object value)
        {
            if (fieldType == typeof (bool))
                _data.Add((bool) value ? (byte)1 : (byte)0);
            if (fieldType == typeof(byte))
                _data.Add((byte)value);
            if (fieldType == typeof(short))
                _data.AddRange(BitConverter.GetBytes((short)value));
            if (fieldType == typeof(ushort))
                _data.AddRange(BitConverter.GetBytes((ushort)value));
            if (fieldType == typeof(int))
                _data.AddRange(BitConverter.GetBytes((int)value));
            if (fieldType == typeof(uint))
                _data.AddRange(BitConverter.GetBytes((uint)value));
            if (fieldType == typeof(long))
                _data.AddRange(BitConverter.GetBytes((long)value));
            if (fieldType == typeof(ulong))
                _data.AddRange(BitConverter.GetBytes((ulong)value));
            if (fieldType == typeof(char))
                _data.AddRange(BitConverter.GetBytes((char)value));
            if (fieldType == typeof(double))
                _data.AddRange(BitConverter.GetBytes((double)value));
            if (fieldType == typeof(float))
                _data.AddRange(BitConverter.GetBytes((float)value));
        }

        private void PackArray(mNetworkHelper.CachedFieldInfo field)
        {
            var fieldType = field.FieldInfo.FieldType;
            var elementType = fieldType.GetElementType();
            var value = field.Getter(_packetMsg);
            var length = (ushort)((Array) value).Length;

            _data.AddRange(BitConverter.GetBytes(length));

            if (elementType.IsPrimitive)
            {
                IEnumerable enumerable = (IEnumerable)value;
                if (enumerable != null)
                {
                    foreach (object element in enumerable)
                    {
                        GetBytes(elementType, element);
                    }
                }
            }
            else if (elementType == typeof(string))
            {
                IEnumerable enumerable = (IEnumerable)value;
                if (enumerable != null)
                {
                    foreach (object element in enumerable)
                    {
                        GetStringBytes((string) element);
                    }
                }
            }
            else if (elementType == typeof(decimal))
            {
                IEnumerable enumerable = (IEnumerable)value;
                if (enumerable != null)
                {
                    foreach (object element in enumerable)
                    {
                        GetDecimalBytes((decimal) element);
                    }
                }
            }
        }
    }
}
