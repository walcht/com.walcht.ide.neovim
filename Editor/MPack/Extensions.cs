using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MPack
{
    internal static class Extensions
    {
        public static object GetDefaultType(this Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        public static bool IsByteArrayEqual(this byte[] a, byte[] b)
        {
#if NET6_0_OR_GREATER
            ReadOnlySpan<byte> a1 = a;
            ReadOnlySpan<byte> b1 = b;
            return a1.SequenceEqual(b1);
#else
            return a.SequenceEqual(b);
#endif
        }

        public static bool IsInDecimalRange(this MToken type)
        {
            if (type.IsInteger())
            {
                return true;
            }

            if (type.IsFloatingPoint())
            {
                var d = type.To<double>();
                return d >= Convert.ToDouble(decimal.MinValue) && d <= Convert.ToDouble(decimal.MaxValue);
            }

            return false;
        }

        public static bool IsNull(this MToken type)
        {
            return type.ValueType == MTokenType.Null;
        }

        public static bool IsNumber(this MToken type)
        {
            return type.IsInteger() || type.IsFloatingPoint();
        }

        public static bool IsInteger(this MToken token)
        {
            return token.ValueType == MTokenType.SInt || token.ValueType == MTokenType.UInt;
        }

        public static bool IsBoolean(this MToken token)
        {
            return token.ValueType == MTokenType.Bool;
        }

        public static bool IsFloatingPoint(this MToken token)
        {
            return token.ValueType == MTokenType.Single || token.ValueType == MTokenType.Double;
        }

        public static bool IsClassRecord(this TypeInfo type)
        {
            // The only truly unique thing about a C# 9 record class is the presence of a <Clone>$ method,
            // which cannot be declared in C# because of the reserved characters in its name.
            return type.IsClass
                && type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is object;
        }

        private const string EX_STREAMEND = "Stream ended but expecting more data. Data may be incorrupt or stream ended prematurely.";

        public static byte[] Read(this Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int read = FillBuffer_internal(stream, buffer, 0, count);
            if (read != count)
                throw new InvalidDataException(EX_STREAMEND);
            return buffer;
        }

        public static byte[] Read(this Stream stream, uint count)
        {
            byte[] buffer = new byte[count];
            int index = 0;
            int read = 0;
            if (count > int.MaxValue)
            {
                read = FillBuffer_internal(stream, buffer, 0, int.MaxValue);
                if (read != int.MaxValue)
                    throw new InvalidDataException(EX_STREAMEND);
                index = int.MaxValue;
            }
            read += FillBuffer_internal(stream, buffer, index, (int)(count - read));
            if (read != count)
                throw new InvalidDataException(EX_STREAMEND);
            return buffer;
        }

        private static int FillBuffer_internal(Stream stream, byte[] buffer, int offset, int length)
        {
            int totalRead = 0;
            while (length > 0)
            {
                var read = stream.Read(buffer, offset, length);
                if (read == 0)
                    return totalRead;
                offset += read;
                length -= read;
                totalRead += read;
            }
            return totalRead;
        }

        public static async Task<byte> ReadByteAsync(this Stream stream, CancellationToken token)
        {
            var b = await stream.ReadAsync(1, token);
            return b[0];
        }

        public static async Task<byte[]> ReadAsync(this Stream stream, int count, CancellationToken token)
        {
            byte[] buffer = new byte[count];
            int read = await FillBufferAsync_internal(stream, buffer, 0, count, token);
            if (read != count)
                throw new InvalidDataException(EX_STREAMEND);
            return buffer;
        }

        public static async Task<byte[]> ReadAsync(this Stream stream, uint count, CancellationToken token)
        {
            byte[] buffer = new byte[count];
            int index = 0;
            int read = 0;
            if (count > int.MaxValue)
            {
                read = await FillBufferAsync_internal(stream, buffer, 0, int.MaxValue, token);
                if (read != int.MaxValue)
                    throw new InvalidDataException(EX_STREAMEND);
                index = int.MaxValue;
            }
            read += await FillBufferAsync_internal(stream, buffer, index, (int)(count - read), token);
            if (read != count)
                throw new InvalidDataException(EX_STREAMEND);
            return buffer;
        }

        private static async Task<int> FillBufferAsync_internal(Stream stream, byte[] buffer, int offset, int length, CancellationToken token)
        {
            int totalRead = 0;
            while (length > 0 && !token.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, offset, length, token);
                if (read == 0)
                    return totalRead;
                offset += read;
                length -= read;
                totalRead += read;
            }
            return totalRead;
        }
    }
}
