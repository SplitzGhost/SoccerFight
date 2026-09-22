using System;
using System.Text;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Growable byte buffer for one outgoing packet. Positions travel as centimetre shorts where the
    /// range allows it (the arena is ±20 units wide), everything else as plain little-endian values.
    /// </summary>
    public sealed class NetWriter
    {
        public byte[] Data = new byte[1024];
        public int Length;

        public void Clear() => Length = 0;

        void Room(int n)
        {
            if (Length + n <= Data.Length) return;
            int size = Data.Length;
            while (size < Length + n) size *= 2;
            Array.Resize(ref Data, size);
        }

        public void Byte(byte v) { Room(1); Data[Length++] = v; }
        public void SByte(sbyte v) => Byte((byte)v);
        public void Bool(bool v) => Byte(v ? (byte)1 : (byte)0);

        public void Short(short v) { Room(2); Data[Length++] = (byte)v; Data[Length++] = (byte)(v >> 8); }
        public void UShort(ushort v) => Short((short)v);

        public void Int(int v)
        {
            Room(4);
            Data[Length++] = (byte)v; Data[Length++] = (byte)(v >> 8);
            Data[Length++] = (byte)(v >> 16); Data[Length++] = (byte)(v >> 24);
        }

        public void Float(float v) => Int(BitConverter.SingleToInt32Bits(v));

        public void Vec(Vector2 v) { Float(v.x); Float(v.y); }

        /// <summary>Centimetre precision, ±327 units.</summary>
        public void Pos(Vector2 v)
        {
            Short((short)Mathf.Clamp(Mathf.RoundToInt(v.x * 100f), short.MinValue, short.MaxValue));
            Short((short)Mathf.Clamp(Mathf.RoundToInt(v.y * 100f), short.MinValue, short.MaxValue));
        }

        /// <summary>0..1 in one byte.</summary>
        public void Unit(float v) => Byte((byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255));

        public void Color(Color c)
        {
            Color32 k = c;
            Byte(k.r); Byte(k.g); Byte(k.b); Byte(k.a);
        }

        public void String(string s)
        {
            if (s == null) { Short(-1); return; }
            int n = Encoding.UTF8.GetByteCount(s);
            Short((short)n);
            Room(n);
            Encoding.UTF8.GetBytes(s, 0, s.Length, Data, Length);
            Length += n;
        }

        public void Bytes(byte[] src, int offset, int count)
        {
            Room(count);
            Buffer.BlockCopy(src, offset, Data, Length, count);
            Length += count;
        }
    }

    /// <summary>Reads what a <see cref="NetWriter"/> wrote. Reading past the end returns zeros instead of throwing.</summary>
    public sealed class NetReader
    {
        byte[] data;
        int pos, end;

        public bool More => pos < end;
        public bool Overrun { get; private set; }

        public void Reset(byte[] buffer, int length)
        {
            data = buffer;
            pos = 0;
            end = length;
            Overrun = false;
        }

        bool Have(int n)
        {
            if (pos + n <= end) return true;
            pos = end;
            Overrun = true;
            return false;
        }

        public byte Byte() => Have(1) ? data[pos++] : (byte)0;
        public sbyte SByte() => (sbyte)Byte();
        public bool Bool() => Byte() != 0;

        public short Short()
        {
            if (!Have(2)) return 0;
            short v = (short)(data[pos] | (data[pos + 1] << 8));
            pos += 2;
            return v;
        }

        public ushort UShort() => (ushort)Short();

        public int Int()
        {
            if (!Have(4)) return 0;
            int v = data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16) | (data[pos + 3] << 24);
            pos += 4;
            return v;
        }

        public float Float() => BitConverter.Int32BitsToSingle(Int());

        public Vector2 Vec() => new Vector2(Float(), Float());
        public Vector2 Pos() => new Vector2(Short() * 0.01f, Short() * 0.01f);
        public float Unit() => Byte() / 255f;
        public Color Color() => new Color32(Byte(), Byte(), Byte(), Byte());

        public string String()
        {
            int n = Short();
            if (n < 0) return null;
            if (!Have(n)) return "";
            string s = Encoding.UTF8.GetString(data, pos, n);
            pos += n;
            return s;
        }
    }
}
