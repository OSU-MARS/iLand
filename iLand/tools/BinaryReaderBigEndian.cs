using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace iLand.tools
{
    internal class BinaryReaderBigEndian : BinaryReader
    {
        public BinaryReaderBigEndian(Stream input)
            : base(input)
        {
        }

        public BinaryReaderBigEndian(Stream input, Encoding encoding)
            : base(input, encoding)
        {
        }

        public BinaryReaderBigEndian(Stream input, Encoding encoding, bool leaveOpen)
            : base(input, encoding, leaveOpen)
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

        public override int Read(char[] buffer, int index, int count)
        {
            int length = base.Read(buffer, index, count);
            for (int reversingIndex = index; reversingIndex < index + length; ++reversingIndex)
            {
                buffer[reversingIndex] = (char)BinaryPrimitives.ReverseEndianness(buffer[reversingIndex]);
            }
            return length;
        }

        public override int Read(Span<char> buffer)
        {
            int length = base.Read(buffer);
            for (int reversingIndex = 0; reversingIndex < length; ++reversingIndex)
            {
                buffer[reversingIndex] = (char)BinaryPrimitives.ReverseEndianness(buffer[reversingIndex]);
            }
            return length;
        }

        public override char ReadChar()
        {
            return (char)BinaryPrimitives.ReverseEndianness(base.ReadChar());
        }

        public override char[] ReadChars(int count)
        {
            char[] characters = base.ReadChars(count);
            for (int reversingIndex = 0; reversingIndex < characters.Length; ++reversingIndex)
            {
                characters[reversingIndex] = (char)BinaryPrimitives.ReverseEndianness(characters[reversingIndex]);
            }
            return characters;
        }

        public override decimal ReadDecimal()
        {
            // untested!
            // An int[] buffer could be allocated and cached to avoid calling new twice but no Span<int> path seems to be available?
            int[] quadword = new int[4];
            quadword[4] = this.ReadInt32();
            quadword[3] = this.ReadInt32();
            quadword[2] = this.ReadInt32();
            quadword[1] = this.ReadInt32();
            return new decimal(quadword);
        }

        public override double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(this.ReadInt64());
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
            return BitConverter.Int32BitsToSingle(this.ReadInt32());
        }

        public override string ReadString()
        {
            int length = this.ReadInt32();
            if (length < 0)
            {
                throw new NotSupportedException("Strings longer than " + Int32.MaxValue + " characters are not supported by this implementation.");
            }
            return new string(this.ReadChars(length));
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
