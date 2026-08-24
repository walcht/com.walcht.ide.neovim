// LICENSE INFORMATION ////////////////////////////////////////////////////////////////////////
//                                                                                           //
//                                          MPack                                            //
//                                                                                           //
//  MPack is an implementation of the MessagePack binary serialization format.               //
//  It is a direct mapping to JSON, and the official specification can be found here:        //
//  https://github.com/msgpack/msgpack/blob/master/spec.md                                   //
//                                                                                           //
//  This MPack implementation is inspired by the work of ymofen (ymofen@diocp.org):          //
//  https://github.com/ymofen/SimpleMsgPack.Net                                              //
//                                                                                           //
//  this implementation has been completely reworked from the ground up including many       //
//  bux fixes and an API that remains lightweight compared to the official one,              //
//  while remaining robust and easy to use.                                                  //
//                                                                                           //                 
//  Written by Caelan Sayler git@caesay.com                                                  //
//  Original URL: https://github.com/caesay/MPack                                            //
//  Licensed: MIT                                                                            //
//                                                                                           //
///////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MPack
{
    public class MToken : IEquatable<MToken>, IConvertible, IComparable<MToken>
    {
        public virtual object Value { get { return _value; } }
        public virtual MTokenType ValueType { get { return _type; } }

        internal MToken(object value, MTokenType type)
        {
            _value = value;
            _type = type;
        }
        protected MToken()
        {
        }

        private object _value = null;
        private MTokenType _type = MTokenType.Null;

        public virtual MToken this[int index]
        {
            get
            {
                if (this is MDict)
                    return this[(MToken)index];
                throw new NotSupportedException("Array indexor not supported in this context.");
            }
            set
            {
                if (this is MDict)
                    this[(MToken)index] = value;
                else
                    throw new NotSupportedException("Array indexor not supported in this context.");
            }
        }
        public virtual MToken this[MToken key]
        {
            get
            {
                throw new NotSupportedException("Map indexor not supported in this context.");
            }
            set
            {
                throw new NotSupportedException("Map indexor not supported in this context.");
            }
        }

        public static MToken Null()
        {
            return new MToken() { _type = MTokenType.Null };
        }

        public static MToken From(object value)
        {
            if (value == null)
                return new MToken(null, MTokenType.Null);

            if (value is MToken token)
                return token;

            var type = value.GetType();

            // if type is byte[], convert it to a binary MToken
            if (type == typeof(byte[]))
            {
                return new MToken(value, MTokenType.Binary);
            }

            // if type is an IDictionary, convert it to MDict
            if (value is IDictionary)
            {
                MDict resultDict = new MDict();
                foreach (DictionaryEntry entry in (IDictionary)value)
                {
                    resultDict.Add(From(entry.Key), From(entry.Value));
                }
                return resultDict;
            }

            // if type implements ICollection, convert it to MArray
            if (value is ICollection)
            {
                MArray resultArray = new MArray();
                foreach (var obj in (ICollection)value)
                {
                    resultArray.Add(From(obj));
                }
                return resultArray;
            }

            TypeCode code = Type.GetTypeCode(type);
            switch (code)
            {
                case TypeCode.Boolean:
                    return new MToken(value, MTokenType.Bool);
                case TypeCode.Char:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.SByte:
                    return new MToken(value, MTokenType.SInt);
                case TypeCode.Byte:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.Int16:
                    return new MToken(value, MTokenType.SInt);
                case TypeCode.UInt16:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.Int32:
                    return new MToken(value, MTokenType.SInt);
                case TypeCode.UInt32:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.Int64:
                    return new MToken(value, MTokenType.SInt);
                case TypeCode.UInt64:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.Single:
                    return new MToken(value, MTokenType.Single);
                case TypeCode.Double:
                    return new MToken(value, MTokenType.Double);
                case TypeCode.Decimal:
                    return new MToken((double)(decimal)value, MTokenType.Double);
                case TypeCode.String:
                    return new MToken(value, MTokenType.String);
                case TypeCode.DateTime:
                    return new MToken(((DateTime)value).ToString("o", CultureInfo.InvariantCulture), MTokenType.String);
                case TypeCode.Object:
                    return SerializeObject(value);
            }
            throw new NotSupportedException("Tried to create MPack object from unsupported type: " + type.Name);
        }

        private static List<MemberInfo> GetTypeMembers(Type type)
        {
            DataContractAttribute dataContractAttr = type.GetCustomAttribute<DataContractAttribute>();
            List<MemberInfo> propertiesToSerialize = new List<MemberInfo>();

            if (dataContractAttr != null)
            {
                // if [DataContract] is present, only serialize writable properties and fields with [DataMember] attribute
                propertiesToSerialize.AddRange(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(p => p.CanWrite && p.CanRead && p.GetCustomAttribute<DataMemberAttribute>() != null));
                propertiesToSerialize.AddRange(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => f.GetCustomAttribute<DataMemberAttribute>() != null));
            }
            else
            {
                // if [DataContract] is not present, serialize only read/write public properties which do not have [IgnoreDataMember] attribute
                propertiesToSerialize.AddRange(type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.CanWrite && p.CanRead && p.GetCustomAttribute<IgnoreDataMemberAttribute>() == null));
            }

            return propertiesToSerialize;
        }

        private static MToken SerializeObject(object value)
        {
            var type = value.GetType();
            TypeInfo ti = type.GetTypeInfo();
            var isClass = ti.IsClass || ti.IsInterface || ti.IsAbstract;
            var isClassRecord = isClass && ti.IsClassRecord();
            //var isStruct = ti.IsValueType;

            var propertiesToSerialize = GetTypeMembers(type);

            MDict resultDict = new MDict();
            foreach (var prop in propertiesToSerialize)
            {
                string key = prop.Name;
                object propValue = null;
                Type propType = null;

                if (prop is PropertyInfo pi)
                {
                    propValue = pi.GetValue(value);
                    propType = pi.PropertyType;

                    if (isClassRecord && prop.Name == "EqualityContract") continue;
                }
                else if (prop is FieldInfo fi)
                {
                    propValue = fi.GetValue(value);
                    propType = fi.FieldType;

                    if (fi.IsStatic) continue;
                    if (fi.IsInitOnly) continue;
                    if (fi.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>(true) != null) continue;
                }

                var dataMemberAttr = prop.GetCustomAttribute<DataMemberAttribute>();

                // if [DataMember] is not present, use the property name as the key
                // if [DataMember] is present:
                // - use its Name property as the key if IsNameSetExplicitly is true
                // - do not serialize the property if EmitDefaultValue is false and the value is the default value
                if (dataMemberAttr != null)
                {
                    if (dataMemberAttr.IsNameSetExplicitly)
                    {
                        key = dataMemberAttr.Name;
                    }
                    if (!dataMemberAttr.EmitDefaultValue && !propValue.Equals(propType.GetDefaultType()))
                    {
                        continue;
                    }
                }

                resultDict.Add(key, From(propValue));
            }

            return resultDict;
        }

        public object To(Type t)
        {
            if (ValueType == MTokenType.Null)
                return null;

            if (t == typeof(DateTime))
            {
                return DateTime.Parse(To<string>(), CultureInfo.InvariantCulture);
            }

            if (t == typeof(object))
            {
                if (this is MArray)
                    return ((MArray)this).Select(r => r.To<object>()).ToArray();
                if (this is MDict)
                    return ((MDict)this).ToDictionary(r => r.Key.To<object>(), r => r.Value.To<object>());
                return Value;
            }

            // if we are MArray and the target type is T[], convert all the elements to T
            if (t.IsArray)
            {
                var elementType = t.GetElementType();
                if (elementType == typeof(byte) && ValueType == MTokenType.Binary)
                    return (byte[])Value;

                if (ValueType != MTokenType.Array)
                    throw new NotSupportedException("Cannot convert MPack type " + ValueType + " into type " + t.Name + " (it is not an array).");

                var mpackArray = (MArray)this;
                var count = mpackArray.Count;
                var objArray = Array.CreateInstance(elementType, count);
                for (int i = 0; i < count; i++)
                    objArray.SetValue(mpackArray[i].To(elementType), i);
                return objArray;
            }

            // if we are MDict and the target type is IDictionary, convert all the keys and values to TKey and TValue
            if (typeof(IDictionary).IsAssignableFrom(t))
            {
                if (ValueType != MTokenType.Map)
                    throw new NotSupportedException("Cannot convert MPack type " + ValueType + " into type " + t.Name + " (it is not a dictionary).");

                var keyType = typeof(object);
                var valueType = typeof(object);
                if (t.IsGenericType)
                {
                    keyType = t.GetGenericArguments()[0];
                    valueType = t.GetGenericArguments()[1];
                }

                var mpackDict = (MDict)this;
                var dict = (IDictionary)Activator.CreateInstance(t);
                foreach (var kvp in mpackDict)
                {
                    dict.Add(kvp.Key.To(keyType), kvp.Value.To(valueType));
                }
                return dict;
            }

            // if we are MArray and the target type is IList, convert all the elements to T
            if (typeof(IList).IsAssignableFrom(t))
            {
                if (ValueType != MTokenType.Array)
                    throw new NotSupportedException("Cannot convert MPack type " + ValueType + " into type " + t.Name + " (it is not a collection).");
                var elementType = typeof(object);
                if (t.IsGenericType)
                    elementType = t.GetGenericArguments()[0];
                var mpackArray = (MArray)this;
                var collection = (IList)Activator.CreateInstance(t);
                foreach (var obj in mpackArray)
                {
                    collection.Add(obj.To(elementType));
                }
                return collection;
            }

            // If the target type is an object, and not a primative, deserialize the object.
            if (Type.GetTypeCode(t) == TypeCode.Object && ValueType == MTokenType.Map)
            {
                return DeserializeObject(t);
            }

            return Convert.ChangeType(Value, t);
        }

        private object DeserializeObject(Type t)
        {
            var obj = Activator.CreateInstance(t);
            var propertiesToSerialize = GetTypeMembers(t);
            var dict = (MDict)this;

            foreach (var prop in propertiesToSerialize)
            {
                string key = prop.Name;
                var dataMemberAttr = prop.GetCustomAttribute<DataMemberAttribute>();
                if (dataMemberAttr != null && dataMemberAttr.IsNameSetExplicitly)
                {
                    key = dataMemberAttr.Name;
                }

                if (dict.ContainsKey(key))
                {
                    if (prop is PropertyInfo pi)
                    {
                        pi.SetValue(obj, dict[key].To(pi.PropertyType));
                    }
                    else if (prop is FieldInfo fi)
                    {
                        fi.SetValue(obj, dict[key].To(fi.FieldType));
                    }
                }
            }

            return obj;
        }

        public T To<T>()
        {
            return (T)To(typeof(T));
        }

        public T ToOrDefault<T>(T defaultValue = default)
        {
            try
            {
                return To<T>();
            }
            catch
            {
                return defaultValue;
            }
        }

        public static bool operator ==(MToken m1, MToken m2)
        {
            if (ReferenceEquals(m1, m2)) return true;
            if (!ReferenceEquals(m1, null))
                return m1.Equals(m2);
            return false;
        }

        public static bool operator !=(MToken m1, MToken m2)
        {
            if (ReferenceEquals(m1, m2)) return false;
            if (!ReferenceEquals(m1, null))
                return !m1.Equals(m2);
            return true;
        }

        public static implicit operator MToken(bool value) { return From(value); }
        public static implicit operator MToken(float value) { return From(value); }
        public static implicit operator MToken(double value) { return From(value); }
        public static implicit operator MToken(byte value) { return From(value); }
        public static implicit operator MToken(ushort value) { return From(value); }
        public static implicit operator MToken(uint value) { return From(value); }
        public static implicit operator MToken(ulong value) { return From(value); }
        public static implicit operator MToken(sbyte value) { return From(value); }
        public static implicit operator MToken(short value) { return From(value); }
        public static implicit operator MToken(int value) { return From(value); }
        public static implicit operator MToken(long value) { return From(value); }
        public static implicit operator MToken(string value) { return From(value); }
        public static implicit operator MToken(byte[] value) { return From(value); }
        public static implicit operator MToken(MToken[] value) { return From(value); }

        public static explicit operator bool(MToken value) { return value.To<bool>(); }
        public static explicit operator float(MToken value) { return value.To<float>(); }
        public static explicit operator double(MToken value) { return value.To<double>(); }
        public static explicit operator byte(MToken value) { return value.To<byte>(); }
        public static explicit operator ushort(MToken value) { return value.To<ushort>(); }
        public static explicit operator uint(MToken value) { return value.To<uint>(); }
        public static explicit operator ulong(MToken value) { return value.To<ulong>(); }
        public static explicit operator sbyte(MToken value) { return value.To<sbyte>(); }
        public static explicit operator short(MToken value) { return value.To<short>(); }
        public static explicit operator int(MToken value) { return value.To<int>(); }
        public static explicit operator long(MToken value) { return value.To<long>(); }
        public static explicit operator string(MToken value) { return value.To<string>(); }
        public static explicit operator byte[](MToken value) { return value.To<byte[]>(); }
        public static explicit operator MToken[](MToken value) { return value.To<MToken[]>(); }

        public static MToken ParseFromBytes(byte[] array)
        {
            using (MemoryStream ms = new MemoryStream(array))
                return ParseFromStream(ms);
        }
        public static MToken ParseFromStream(Stream stream)
        {
            return Reader.ParseFromStream(stream);
        }
        public static Task<MToken> ParseFromStreamAsync(Stream stream)
        {
            return ParseFromStreamAsync(stream, CancellationToken.None);
        }
        public static Task<MToken> ParseFromStreamAsync(Stream stream, CancellationToken token)
        {
            return Reader.ParseFromStreamAsync(stream, token);
        }
        public void EncodeToStream(Stream stream)
        {
            Writer.EncodeToStream(stream, this);
        }
        public Task EncodeToStreamAsync(Stream stream)
        {
            return EncodeToStreamAsync(stream, CancellationToken.None);
        }
        public async Task EncodeToStreamAsync(Stream stream, CancellationToken token)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Writer.EncodeToStream(ms, this);

                ms.Position = 0;
                await ms.CopyToAsync(stream, 65535, token);
            }
        }
        public byte[] EncodeToBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Writer.EncodeToStream(ms, this);
                return ms.ToArray();
            }
        }

        public bool Equals(MToken other)
        {
            if (ReferenceEquals(other, null) || other.IsNull())
            {
                return this.IsNull();
            }

            if (this.IsNull())
            {
                return false;
            }

            if (this is MArray && other is MArray)
            {
                var ob1 = (MArray)this;
                var ob2 = (MArray)other;
                if (ob1.Count == ob2.Count)
                {
                    return ob1.SequenceEqual(ob2);
                }
            }
            else if (this is MDict && other is MDict)
            {
                var ob1 = (MDict)this;
                var ob2 = (MDict)other;
                if (ob1.Count == ob2.Count)
                {
                    return ob1.OrderBy(r => r.Key).SequenceEqual(ob2.OrderBy(r => r.Key));
                }
            }
            else if (this.IsNumber() && other.IsNumber())
            {
                if (this.IsFloatingPoint() && Double.IsNaN(Convert.ToDouble(Value)))
                {
                    return Double.IsNaN(Convert.ToDouble(other.Value));
                }
                return CompareTo(other) == 0;
            }
            else if (this.ValueType == MTokenType.Null && other.ValueType == MTokenType.Null)
            {
                return true;
            }
            else if (this.ValueType == MTokenType.Bool && other.ValueType == MTokenType.Bool)
            {
                return (bool)Value == (bool)other.Value;
            }
            else if (this.ValueType == MTokenType.String && other.ValueType == MTokenType.String)
            {
                return (string)Value == (string)other.Value;
            }
            else if (this.ValueType == MTokenType.Binary && other.ValueType == MTokenType.Binary)
            {
                return ((byte[])Value).IsByteArrayEqual((byte[])other.Value);
            }
            else
            {
                return Value.Equals(other.Value);
            }

            return false;
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is MToken)
                return Equals((MToken)obj);
            return false;
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        public override string ToString()
        {
            if (Value == null)
                return "null";
            return Value.ToString();
        }

        TypeCode IConvertible.GetTypeCode()
        {
            if (ValueType == MTokenType.Null)
                return TypeCode.Object;
            return Type.GetTypeCode(Value.GetType());
        }
        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return To<bool>();
        }
        char IConvertible.ToChar(IFormatProvider provider)
        {
            return To<char>();
        }
        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return To<sbyte>();
        }
        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return To<byte>();
        }
        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return To<short>();
        }
        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return To<ushort>();
        }
        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return To<int>();
        }
        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return To<uint>();
        }
        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return To<long>();
        }
        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return To<ulong>();
        }
        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return To<float>();
        }
        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return To<double>();
        }
        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return To<decimal>();
        }
        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            return To<DateTime>();
        }
        string IConvertible.ToString(IFormatProvider provider)
        {
            return To<string>();
        }
        object IConvertible.ToType(Type conversionType, IFormatProvider provider)
        {
            return To(conversionType);
        }

        public int CompareTo(MToken other)
        {
            // This interface is meant to be used when we need to order a MPackMap by its keys.
            // Only makes sense for numeric and string types.
            // Since we can mix numeric and string keys, we choose that numerics come before strings.
            if (this.IsNumber())
            {
                if (other.IsNumber())
                {
                    if (this.IsInDecimalRange() && other.IsInDecimalRange())
                    {
                        // decimal can contain every numeric type except double, including single, long, ulong
                        // etc with no loss of precision or truncation, so we convert to decimal to compare.
                        decimal thisVal = Convert.ToDecimal(Value);
                        decimal otherVal = Convert.ToDecimal(other.Value);
                        return thisVal.CompareTo(otherVal);
                    }
                    else
                    {
                        // the value is out of range for decimal, so we convert to double to compare.
                        double thisVal = Convert.ToDouble(Value);
                        double otherVal = Convert.ToDouble(other.Value);
                        return thisVal.CompareTo(otherVal);
                    }
                }

                if (other.ValueType == MTokenType.String)
                {
                    return -1;
                }
            }

            if (ValueType == MTokenType.String)
            {
                if (other.IsNumber())
                {
                    return 1;
                }

                if (other.ValueType == MTokenType.String)
                {
                    return To<string>().CompareTo(other.To<string>());
                }
            }

            throw new NotSupportedException("Only numeric or string types can be compared.");
        }
    }
}