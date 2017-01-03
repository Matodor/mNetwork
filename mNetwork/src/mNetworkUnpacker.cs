using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mNetwork
{
    public class mNetworkUnpacker
    {
        public object Object { get; }

        private readonly byte[] _data;
        private int _currentPos;

        public mNetworkUnpacker(byte[] data, int type)
        {
            Object = null;

            var cachedMessage = mNetworkHelper.GetCachedMessage(type);
            if (cachedMessage == null)
                return;

            _currentPos = 0;
            _data = data;

            Object = Activator.CreateInstance(cachedMessage.MessageType);

            for (int i = 0; i < cachedMessage.CachedFields.Length; i++)
            {
                if (cachedMessage.CachedFields[i].FieldInfo.FieldType.IsArray)
                    UnPackArray(cachedMessage.CachedFields[i]);
                else
                    UnPackField(cachedMessage.CachedFields[i]);
            }
        }

        private void UnPackField(mNetworkHelper.CachedFieldInfo field)
        {
            var fieldType = field.FieldInfo.FieldType;

            if (fieldType.IsPrimitive)
            {
                field.Setter(Object, GetObject(fieldType));
            }
            else if (fieldType == typeof(string))
            {
                field.Setter(Object, GetString());
            }
            else if (fieldType == typeof(decimal))
            {
                field.Setter(Object, GetDecimal());
            }
        }

        private decimal GetDecimal()
        {
            var i1 = BitConverter.ToInt32(_data, _currentPos);
            var i2 = BitConverter.ToInt32(_data, _currentPos + 4);
            var i3 = BitConverter.ToInt32(_data, _currentPos + 8);
            var i4 = BitConverter.ToInt32(_data, _currentPos + 12);
            _currentPos += 16;
            return new decimal(new int[] {i1, i2, i3, i4});
        }

        private string GetString()
        {
            var length = BitConverter.ToUInt16(_data, _currentPos);
            _currentPos += 2;
            var str = Encoding.UTF8.GetString(_data, _currentPos, length);
            _currentPos += length;
            return str;
        }

        private object GetObject(Type fieldType)
        {
            object obj = null; 
            if (fieldType == typeof (bool))
            {
                obj = _data[_currentPos] == 1;
                _currentPos++;
            }
            if (fieldType == typeof(byte))
            {
                obj = _data[_currentPos];
                _currentPos++;
            }
            if (fieldType == typeof(short))
            {
                obj = BitConverter.ToInt16(_data, _currentPos);
                _currentPos += 2;
            }
            if (fieldType == typeof(ushort))
            {
                obj = BitConverter.ToUInt16(_data, _currentPos);
                _currentPos += 2;
            }
            if (fieldType == typeof(int))
            {
                obj = BitConverter.ToInt32(_data, _currentPos);
                _currentPos += 4;
            }
            if (fieldType == typeof(uint))
            {
                obj = BitConverter.ToUInt32(_data, _currentPos);
                _currentPos += 4;
            }
            if (fieldType == typeof(long))
            {
                obj = BitConverter.ToInt64(_data, _currentPos);
                _currentPos += 8;
            }
            if (fieldType == typeof(ulong))
            {
                obj = BitConverter.ToUInt64(_data, _currentPos);
                _currentPos += 8;
            }
            if (fieldType == typeof(char))
            {
                obj = BitConverter.ToChar(_data, _currentPos);
                _currentPos += 2;
            }
            if (fieldType == typeof(double))
            {
                obj = BitConverter.ToDouble(_data, _currentPos);
                _currentPos += 8;
            }
            if (fieldType == typeof(float))
            {
                obj = BitConverter.ToSingle(_data, _currentPos);
                _currentPos += 4;
            }
            return obj;
        }

        private void UnPackArray(mNetworkHelper.CachedFieldInfo field)
        {
            var fieldType = field.FieldInfo.FieldType;
            var elementType = fieldType.GetElementType();

            var length = BitConverter.ToUInt16(_data, _currentPos);
            _currentPos += 2;

            var inputArray = new object[length];
            var arr = Array.CreateInstance(elementType, length);
            
            if (elementType.IsPrimitive)
            {
                for (int i = 0; i < length; i++)
                {
                    inputArray[i] = GetObject(elementType);
                }
            }
            else if (elementType == typeof(string))
            {
                for (int i = 0; i < length; i++)
                {
                    inputArray[i] = GetString();
                }
            } 
            else if (elementType == typeof(decimal))
            {
                for (int i = 0; i < length; i++)
                {
                    inputArray[i] = GetDecimal();
                }
            }

            Array.Copy(inputArray, arr, inputArray.Length);
            field.Setter(Object, arr);
        }
    }
}
