using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace iLand.Input.Tree
{
    internal class StampReaderBigEndian : BinaryReader
    {
        public StampReaderBigEndian(Stream input)
            : base(input, Encoding.BigEndianUnicode)
        {
        }

        // Nominally, these overrides are not needed due to single byte reads...
        //   public override bool ReadBoolean();
        //   public override byte ReadByte();
        //   public override sbyte ReadSByte();
        // ...but not overriding these methods is more dangerous as callers which accept a BinaryReader may not behave
        // correctly.
        //   public override Read(byte[] buffer, int index, int count)
        //   public override int Read(Span<byte> buffer)
        //   public override byte[] ReadBytes(int count)
        // Due to use of big endian unicode, these overrides are not needed.
        //   public override int Read(char[] buffer, int index, int count)
        //   public override int Read(Span<char> buffer)
        //   public override char ReadChar()
        //   public override char[] ReadChars(int count)

        //public override int Read(char[] buffer, int index, int count)
        //{
        //    int length = base.Read(buffer, index, count);
        //    for (int reversingIndex = index; reversingIndex < index + length; ++reversingIndex)
        //    {
        //        buffer[reversingIndex] = (char)BinaryPrimitives.ReverseEndianness(buffer[reversingIndex]);
        //    }
        //    return length;
        //}

        //public override int Read(Span<char> buffer)
        //{
        //    int length = base.Read(buffer);
        //    for (int reversingIndex = 0; reversingIndex < length; ++reversingIndex)
        //    {
        //        buffer[reversingIndex] = (char)BinaryPrimitives.ReverseEndianness(buffer[reversingIndex]);
        //    }
        //    return length;
        //}

        //public override char ReadChar()
        //{
        //    return (char)BinaryPrimitives.ReverseEndianness(base.ReadChar());
        //}

        //public override char[] ReadChars(int count)
        //{
        //    char[] characters = base.ReadChars(count);
        //    for (int reversingIndex = 0; reversingIndex < characters.Length; ++reversingIndex)
        //    {
        //        characters[reversingIndex] = (char)BinaryPrimitives.ReverseEndianness(characters[reversingIndex]);
        //    }
        //    return characters;
        //}

        public override decimal ReadDecimal()
        {
            // untested!
            // An int[] buffer could be allocated and cached to avoid calling new twice but no Span<int> path seems to be available?
            int[] quadword = new int[4];
            quadword[4] = ReadInt32();
            quadword[3] = ReadInt32();
            quadword[2] = ReadInt32();
            quadword[1] = ReadInt32();
            return new decimal(quadword);
        }

        public override double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(ReadInt64());
        }

        public override short ReadInt16()
        {
            return BinaryPrimitives.ReverseEndianness(base.ReadInt16());
        }

        public override int ReadInt32()
        {
            return BinaryPrimitives.ReverseEndianness(base.ReadInt32());
        }

        public override long ReadInt64()
        {
            return BinaryPrimitives.ReverseEndianness(base.ReadInt64());
        }

        public override float ReadSingle()
        {
            return BitConverter.Int32BitsToSingle(ReadInt32());
        }

        public override string ReadString()
        {
            int lengthInBytes = ReadInt32();
            if (lengthInBytes < 0)
            {
                throw new NotSupportedException("Strings longer than " + int.MaxValue + " characters are not supported by this implementation.");
            }
            return new string(ReadChars(lengthInBytes / 2));
        }

        public override ushort ReadUInt16()
        {
            return BinaryPrimitives.ReverseEndianness(base.ReadUInt16());
        }

        public override uint ReadUInt32()
        {
            return BinaryPrimitives.ReverseEndianness(base.ReadUInt32());
        }

        public override ulong ReadUInt64()
        {
            return BinaryPrimitives.ReverseEndianness(base.ReadUInt64());
        }
    }
}
