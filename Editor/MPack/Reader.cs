using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MPack
{
    internal class Reader
    {
        private static readonly FamilyReaderLookup _lookup = new FamilyReaderLookup();
        private static Encoding _encoding = Encoding.UTF8;
        private static IBitConverter _convert = EndianBitConverter.Big;

        static Reader()
        {
            //positive fixint	0xxxxxxx	0x00 - 0x7f
            _lookup.AddRange(0, 0x7F,
                (b, s) => new MToken(b, MTokenType.SInt),
                (b, s, c) => Task.FromResult(new MToken(b, MTokenType.SInt)));

            //positive fixint	0xxxxxxx	0x00 - 0x7f
            _lookup.AddRange(0x80, 0x8F, (b, s) =>
            {
                MDict map = new MDict();
                int len = b - 0x80;
                for (int i = 0; i < len; i++)
                {
                    map.Add(ParseFromStream(s), ParseFromStream(s));
                }
                return map;
            }, async (b, s, c) =>
            {
                MDict map = new MDict();
                int len = b - 0x80;
                for (int i = 0; i < len; i++)
                {
                    map.Add(await ParseFromStreamAsync(s, c), await ParseFromStreamAsync(s, c));
                }
                return map;
            });

            //fixarray	1001xxxx	0x90 - 0x9f
            _lookup.AddRange(0x90, 0x9f, (b, s) =>
            {
                MArray array = new MArray();
                int len = b - 0x90;
                for (int i = 0; i < len; i++)
                {
                    array.Add(ParseFromStream(s));
                }
                return array;
            }, async (b, s, c) =>
            {
                MArray array = new MArray();
                int len = b - 0x90;
                for (int i = 0; i < len; i++)
                {
                    array.Add(await ParseFromStreamAsync(s, c));
                }
                return array;
            });

            // fixstr	101xxxxx	0xa0 - 0xbf
            _lookup.AddRange(0xA0, 0xBF, (b, s) =>
            {
                int len = b - 0xA0;
                return new MToken(_encoding.GetString(s.Read(len)), MTokenType.String);
            }, async (b, s, c) =>
            {
                int len = b - 0xA0;
                return new MToken(_encoding.GetString(await s.ReadAsync(len, c)), MTokenType.String);
            });

            // negative fixnum stores 5-bit negative integer 111xxxxx
            _lookup.AddRange(0xE0, 0xFF,
                (b, s) => new MToken((sbyte)b, MTokenType.SInt),
                (b, s, c) => Task.FromResult(new MToken((sbyte)b, MTokenType.SInt)));

            // null
            _lookup.Add(0xC0,
                (b, s) => MToken.Null(),
                (b, s, c) => Task.FromResult(MToken.Null()));

            // note, no 0xC1, it's not used.

            // bool: false
            _lookup.Add(0xC2,
                (b, s) => new MToken(false, MTokenType.Bool),
                (b, s, c) => Task.FromResult(new MToken(false, MTokenType.Bool)));

            // bool: true
            _lookup.Add(0xC3,
                (b, s) => new MToken(true, MTokenType.Bool),
                (b, s, c) => Task.FromResult(new MToken(true, MTokenType.Bool)));

            // binary array, max 255
            _lookup.Add(0xC4, (b, s) =>
            {
                byte len = (byte)s.ReadByte();
                return new MToken(s.Read(len), MTokenType.Binary);
            }, async (b, s, c) =>
            {
                byte len = await s.ReadByteAsync(c);
                return new MToken(await s.ReadAsync(len, c), MTokenType.Binary);
            });

            // binary array, max 65535
            _lookup.Add(0xC5, (b, s) =>
            {
                int len = _convert.ToUInt16(s.Read(2), 0);
                return new MToken(s.Read(len), MTokenType.Binary);
            }, async (b, s, c) =>
            {
                int len = _convert.ToUInt16((await s.ReadAsync(2, c)), 0);
                return new MToken(await s.ReadAsync(len, c), MTokenType.Binary);
            });

            // binary max: 2^32-1                
            _lookup.Add(0xC6, (b, s) =>
            {
                uint len = _convert.ToUInt32(s.Read(4), 0);
                return new MToken(s.Read(len), MTokenType.Binary);
            }, async (b, s, c) =>
            {
                uint len = _convert.ToUInt32((await s.ReadAsync(4, c)), 0);
                return new MToken(await s.ReadAsync(len, c), MTokenType.Binary);
            });

            // note 0xC7, 0xC8, 0xC9, not used.

            // float 32 stores a floating point number in IEEE 754 single precision floating point number     
            _lookup.Add(0xCA, (b, s) =>
            {
                var raw = s.Read(4);
                return new MToken(_convert.ToSingle(raw, 0), MTokenType.Single);
            }, async (b, s, c) =>
            {
                var raw = (await s.ReadAsync(4, c));
                return new MToken(_convert.ToSingle(raw, 0), MTokenType.Single);
            });

            // float 64 stores a floating point number in IEEE 754 double precision floating point number        
            _lookup.Add(0xCB, (b, s) =>
            {
                var raw = s.Read(8);
                return new MToken(_convert.ToDouble(raw, 0), MTokenType.Double);
            }, async (b, s, c) =>
            {
                var raw = (await s.ReadAsync(8, c));
                return new MToken(_convert.ToDouble(raw, 0), MTokenType.Double);
            });

            // uint8   0xcc   xxxxxxxx
            _lookup.Add(0xCC,
                (b, s) => new MToken((byte)s.ReadByte(), MTokenType.UInt),
                async (b, s, c) => new MToken((byte)await s.ReadByteAsync(c), MTokenType.UInt));

            // uint16   0xcd xxxxxxxx xxxxxxxx   
            _lookup.Add(0xCD, (b, s) =>
            {
                var v = _convert.ToUInt16(s.Read(2), 0);
                return new MToken(v, MTokenType.UInt);
            }, async (b, s, c) =>
            {
                var v = _convert.ToUInt16((await s.ReadAsync(2, c))
                    , 0);
                return new MToken(v, MTokenType.UInt);
            });

            // uint32   0xce xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx  
            _lookup.Add(0xCE, (b, s) =>
            {
                var v = _convert.ToUInt32(s.Read(4), 0);
                return new MToken(v, MTokenType.UInt);
            }, async (b, s, c) =>
            {
                var v = _convert.ToUInt32((await s.ReadAsync(4, c))
                    , 0);
                return new MToken(v, MTokenType.UInt);
            });

            // uint64   0xcF   xxxxxxxx (x4)
            _lookup.Add(0xCF, (b, s) =>
            {
                var v = _convert.ToUInt64(s.Read(8), 0);
                return new MToken(v, MTokenType.UInt);
            }, async (b, s, c) =>
            {
                var v = _convert.ToUInt64((await s.ReadAsync(8, c)), 0);
                return new MToken(v, MTokenType.UInt);
            });

            // array (uint16 length)
            _lookup.Add(0xDC, (b, s) =>
            {
                ushort len = _convert.ToUInt16(s.Read(2), 0);
                MArray array = new MArray();
                for (ushort i = 0; i < len; i++)
                {
                    array.Add(ParseFromStream(s));
                }
                return array;
            }, async (b, s, c) =>
            {
                ushort len = _convert.ToUInt16((await s.ReadAsync(2, c)), 0);
                MArray array = new MArray();
                for (ushort i = 0; i < len; i++)
                {
                    array.Add(await ParseFromStreamAsync(s, c));
                }
                return array;
            });

            // array (uint32 length)
            _lookup.Add(0xDD, (b, s) =>
            {
                uint len = _convert.ToUInt32(s.Read(4), 0);
                MArray array = new MArray();
                for (uint i = 0; i < len; i++)
                {
                    array.Add(ParseFromStream(s));
                }
                return array;
            }, async (b, s, c) =>
            {
                uint len = _convert.ToUInt32(await s.ReadAsync(4, c), 0);
                MArray array = new MArray();
                for (uint i = 0; i < len; i++)
                {
                    array.Add(await ParseFromStreamAsync(s, c));
                }
                return array;
            });

            // map (uint16 length)
            _lookup.Add(0xDE, (b, s) =>
            {
                ushort len = _convert.ToUInt16(s.Read(2), 0);
                MDict map = new MDict();
                for (ushort i = 0; i < len; i++)
                {
                    var flag = (byte)s.ReadByte();
                    var key = ReadString(flag, s);
                    var val = ParseFromStream(s);
                    map.Add(key, val);
                }
                return map;
            }, async (b, s, c) =>
            {
                ushort len = _convert.ToUInt16(await s.ReadAsync(2, c), 0);
                MDict map = new MDict();
                for (ushort i = 0; i < len; i++)
                {
                    var flag = await s.ReadByteAsync(c);
                    var key = await ReadStringAsync(flag, s, c);
                    var val = await ParseFromStreamAsync(s, c);
                    map.Add(key, val);
                }
                return map;
            });

            // map (uint32 length)
            _lookup.Add(0xDF, (b, s) =>
            {
                uint len = _convert.ToUInt32(s.Read(4), 0);
                MDict map = new MDict();
                for (uint i = 0; i < len; i++)
                {
                    var flag = (byte)s.ReadByte();
                    var key = ReadString(flag, s);
                    var val = ParseFromStream(s);
                    map.Add(key, val);
                }
                return map;
            }, async (b, s, c) =>
            {
                uint len = _convert.ToUInt32((await s.ReadAsync(4, c)), 0);
                MDict map = new MDict();
                for (uint i = 0; i < len; i++)
                {
                    var flag = await s.ReadByteAsync(c);
                    var key = await ReadStringAsync(flag, s, c);
                    var val = await ParseFromStreamAsync(s, c);
                    map.Add(key, val);
                }
                return map;
            });

            //  str family
            _lookup.AddRange(0xD9, 0xDB,
                (b, s) => new MToken(ReadString(b, s), MTokenType.String),
                async (b, s, c) => new MToken((await ReadStringAsync(b, s, c)), MTokenType.String));

            // int8   0xD0   xxxxxxxx
            _lookup.Add(0xD0,
                (b, s) => new MToken((sbyte)s.ReadByte(), MTokenType.SInt),
                async (b, s, c) => new MToken((sbyte)await s.ReadByteAsync(c), MTokenType.SInt));

            // int16   0xd1 xxxxxxxx xxxxxxxx   
            _lookup.Add(0xD1, (b, s) =>
            {
                var v = _convert.ToInt16(s.Read(2), 0);
                return new MToken(v, MTokenType.SInt);
            }, async (b, s, c) =>
            {
                var v = _convert.ToInt16((await s.ReadAsync(2, c))
                    , 0);
                return new MToken(v, MTokenType.SInt);
            });

            // int32    0xD2 xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx  
            _lookup.Add(0xD2, (b, s) =>
            {
                var v = _convert.ToInt32(s.Read(4), 0);
                return new MToken(v, MTokenType.SInt);
            }, async (b, s, c) =>
            {
                var v = _convert.ToInt32((await s.ReadAsync(4, c))
                    , 0);
                return new MToken(v, MTokenType.SInt);
            });

            // int64      0xD3 xxxxxxxx (x8)
            _lookup.Add(0xD3, (b, s) =>
            {
                var v = _convert.ToInt64(s.Read(8), 0);
                return new MToken(v, MTokenType.SInt);
            }, async (b, s, c) =>
            {
                var v = _convert.ToInt64((await s.ReadAsync(8, c))
                    , 0);
                return new MToken(v, MTokenType.SInt);
            });
        }

        public static MToken ParseFromStream(Stream stream)
        {
            byte selector = (byte)stream.ReadByte();
            return _lookup[selector].Read(selector, stream);
        }
        public static async Task<MToken> ParseFromStreamAsync(Stream stream, CancellationToken token)
        {
            byte selector = await stream.ReadByteAsync(token);
            return await _lookup[selector].ReadAsync(selector, stream, token);
        }

        private static string ReadString(byte flag, Stream ms)
        {
            //fixstr stores a byte array whose length is upto 31 bytes:
            //+--------+========+
            //|101XXXXX|  data  |
            //+--------+========+
            //
            //str 8 stores a byte array whose length is upto (2^8)-1 bytes:
            //+--------+--------+========+
            //|  0xd9  |YYYYYYYY|  data  |
            //+--------+--------+========+
            //
            //str 16 stores a byte array whose length is upto (2^16)-1 bytes:
            //+--------+--------+--------+========+
            //|  0xda  |ZZZZZZZZ|ZZZZZZZZ|  data  |
            //+--------+--------+--------+========+
            //
            //str 32 stores a byte array whose length is upto (2^32)-1 bytes:
            //+--------+--------+--------+--------+--------+========+
            //|  0xdb  |AAAAAAAA|AAAAAAAA|AAAAAAAA|AAAAAAAA|  data  |
            //+--------+--------+--------+--------+--------+========+
            //
            //where
            //* XXXXX is a 5-bit unsigned integer which represents N
            //* YYYYYYYY is a 8-bit unsigned integer which represents N
            //* ZZZZZZZZ_ZZZZZZZZ is a 16-bit big-endian unsigned integer which represents N
            //* AAAAAAAA_AAAAAAAA_AAAAAAAA_AAAAAAAA is a 32-bit big-endian unsigned integer which represents N
            //* N is the length of data   

            uint len = 0;
            if ((flag >= 0xA0) && (flag <= 0xBF))
                len = (byte)(flag - 0xA0);
            else if (flag == 0xD9)
                len = (byte)ms.ReadByte();
            else if (flag == 0xDA)
                len = _convert.ToUInt16(ms.Read(2), 0);
            else if (flag == 0xDB)
                len = _convert.ToUInt32(ms.Read(4), 0);

            return _encoding.GetString(ms.Read(len));
        }
        private static async Task<string> ReadStringAsync(byte flag, Stream ms, CancellationToken token)
        {
            //fixstr stores a byte array whose length is upto 31 bytes:
            //+--------+========+
            //|101XXXXX|  data  |
            //+--------+========+
            //
            //str 8 stores a byte array whose length is upto (2^8)-1 bytes:
            //+--------+--------+========+
            //|  0xd9  |YYYYYYYY|  data  |
            //+--------+--------+========+
            //
            //str 16 stores a byte array whose length is upto (2^16)-1 bytes:
            //+--------+--------+--------+========+
            //|  0xda  |ZZZZZZZZ|ZZZZZZZZ|  data  |
            //+--------+--------+--------+========+
            //
            //str 32 stores a byte array whose length is upto (2^32)-1 bytes:
            //+--------+--------+--------+--------+--------+========+
            //|  0xdb  |AAAAAAAA|AAAAAAAA|AAAAAAAA|AAAAAAAA|  data  |
            //+--------+--------+--------+--------+--------+========+
            //
            //where
            //* XXXXX is a 5-bit unsigned integer which represents N
            //* YYYYYYYY is a 8-bit unsigned integer which represents N
            //* ZZZZZZZZ_ZZZZZZZZ is a 16-bit big-endian unsigned integer which represents N
            //* AAAAAAAA_AAAAAAAA_AAAAAAAA_AAAAAAAA is a 32-bit big-endian unsigned integer which represents N
            //* N is the length of data   

            uint len = 0;
            if ((flag >= 0xA0) && (flag <= 0xBF))
                len = (byte)(flag - 0xA0);
            else if (flag == 0xD9)
                len = (byte)ms.ReadByte();
            else if (flag == 0xDA)
                len = _convert.ToUInt16(await ms.ReadAsync(2, token), 0);
            else if (flag == 0xDB)
                len = _convert.ToUInt32(await ms.ReadAsync(4, token), 0);

            return Encoding.UTF8.GetString(await ms.ReadAsync(len, token));
        }
    }
    internal class FamilyReader
    {
        private readonly Func<byte, Stream, MToken> _sync;
        private readonly Func<byte, Stream, CancellationToken, Task<MToken>> _async;

        public FamilyReader(Func<byte, Stream, MToken> sync, Func<byte, Stream, CancellationToken, Task<MToken>> async)
        {
            _sync = sync;
            _async = async;
        }

        public MToken Read(byte selector, Stream stream)
        {
            return _sync(selector, stream);
        }
        public Task<MToken> ReadAsync(byte selector, Stream stream, CancellationToken token)
        {
            return _async(selector, stream, token);
        }
    }

    internal class FamilyReaderLookup : Dictionary<byte, FamilyReader>
    {
        public void AddRange(byte rangeStart, byte rangeEnd, FamilyReader reader)
        {
            var test = Enumerable.Range(rangeStart, rangeEnd - rangeStart + 1).ToArray();
            foreach (var b in test.Select(i => (byte)i))
            {
                this.Add(b, reader);
            }
        }
        public void AddRange(byte rangeStart, byte rangeEnd, Func<byte, Stream, MToken> sync,
            Func<byte, Stream, CancellationToken, Task<MToken>> async)
        {
            AddRange(rangeStart, rangeEnd, new FamilyReader(sync, async));
        }
        public void Add(byte flag, Func<byte, Stream, MToken> sync,
            Func<byte, Stream, CancellationToken, Task<MToken>> async)
        {
            Add(flag, new FamilyReader(sync, async));
        }
    }
}
