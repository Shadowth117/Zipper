// Decompiled with JetBrains decompiler
// Type: psu_generic_parser.PrsCompressor
// Assembly: zamboni, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 73B487C9-8F41-4586-BEF5-F7D7BFBD4C55
// Assembly location: D:\Downloads\zamboni_ngs (3)\zamboni.exe

using System;
using System.Collections.Generic;

namespace psu_generic_parser
{
    public class PrsCompressor
    {
        private byte[] compBuffer;
        private int ctrlByteCounter;
        private int outLoc;
        private int ctrlBitCounter;
        private Tuple<List<int>, int> emptyTuple = new Tuple<List<int>, int>(new List<int>(), 0);

        public byte[] compress(byte[] toCompress)
        {
            Dictionary<byte, Tuple<List<int>, int>> offsetDictionary = this.buildOffsetDictionary(toCompress);
            this.ctrlByteCounter = 0;
            int length = toCompress.Length;
            this.compBuffer = new byte[length];
            this.outLoc = 3;
            this.ctrlBitCounter = 2;
            this.compBuffer[0] = (byte)3;
            Array.Copy((Array)toCompress, 0, (Array)this.compBuffer, 1, 2);
            int currentOffset = 2;
            while (currentOffset < length)
            {
                Tuple<List<int>, int> offsetList = this.getOffsetList(offsetDictionary, toCompress[currentOffset], currentOffset);
                int count = 2;
                int num1 = -1;
                int num2 = currentOffset - 256;
                for (int index = offsetList.Item2; index < offsetList.Item1.Count && offsetList.Item1[index] < currentOffset; ++index)
                {
                    int num3 = offsetList.Item1[index];
                    int num4 = 0;
                    int num5 = Math.Min(length - currentOffset, 256);
                    while (num4 < num5 && (int)toCompress[num3 + num4] == (int)toCompress[currentOffset + num4])
                        ++num4;
                    if ((num4 > 2 || num3 > num2) && (num4 > count || num4 == count && num3 > num1))
                    {
                        count = num4;
                        num1 = num3;
                    }
                }
                if (num1 == -1 || currentOffset - num1 > 256 && count < 3)
                {
                    this.writeRawByte(toCompress[currentOffset++]);
                }
                else
                {
                    if (count < 6 && currentOffset - num1 < 256)
                        this.writeShortReference(count, (byte)(num1 - (currentOffset - 256)));
                    else
                        this.writeLongReference(count, num1 - (currentOffset - 8192));
                    currentOffset += count;
                }
            }
            this.finalizeCompression();
            Array.Resize<byte>(ref this.compBuffer, this.outLoc);
            return this.compBuffer;
        }

        private Tuple<List<int>, int> getOffsetList(
          Dictionary<byte, Tuple<List<int>, int>> offsetDictionary,
          byte currentVal,
          int currentOffset)
        {
            Tuple<List<int>, int> offset = offsetDictionary[currentVal];
            if (offset == null)
                return this.emptyTuple;
            if (offset.Item2 < currentOffset - 8176)
            {
                int index = offset.Item2;
                while (offset.Item1[index] < currentOffset - 8176 && index < offset.Item1.Count)
                    ++index;
                Tuple<List<int>, int> tuple = new Tuple<List<int>, int>(offset.Item1, index);
                offsetDictionary[currentVal] = tuple;
            }
            return offsetDictionary[currentVal];
        }

        private Dictionary<byte, Tuple<List<int>, int>> buildOffsetDictionary(
          byte[] toCompress)
        {
            Dictionary<byte, Tuple<List<int>, int>> dictionary = new Dictionary<byte, Tuple<List<int>, int>>();
            for (int index = 0; index < toCompress.Length; ++index)
            {
                byte key = toCompress[index];
                if (!dictionary.ContainsKey(key))
                    dictionary.Add(key, new Tuple<List<int>, int>(new List<int>(), 0));
                dictionary[key].Item1.Add(index);
            }
            return dictionary;
        }

        private void finalizeCompression()
        {
            this.addCtrlBit(0);
            this.addCtrlBit(1);
            this.compBuffer[this.outLoc++] = (byte)0;
            this.compBuffer[this.outLoc++] = (byte)0;
        }

        private void writeRawByte(byte val)
        {
            this.addCtrlBit(1);
            this.compBuffer[this.outLoc++] = val;
        }

        private void writeShortReference(int count, byte offset)
        {
            this.addCtrlBit(0);
            this.addCtrlBit(0);
            this.addCtrlBit(count - 2 >> 1);
            this.addCtrlBit(count - 2 & 1);
            this.compBuffer[this.outLoc++] = offset;
        }

        private void writeLongReference(int count, int offset)
        {
            this.addCtrlBit(0);
            this.addCtrlBit(1);
            ushort num = (ushort)(offset << 3);
            if (count <= 9)
                num |= (ushort)(count - 2);
            BitConverter.GetBytes(num).CopyTo((Array)this.compBuffer, this.outLoc);
            this.outLoc += 2;
            if (count <= 9)
                return;
            this.compBuffer[this.outLoc++] = (byte)(count - 10);
        }

        private void addCtrlBit(int input)
        {
            if (this.ctrlBitCounter == 8)
            {
                this.ctrlBitCounter = 0;
                this.ctrlByteCounter = this.outLoc++;
            }
            this.compBuffer[this.ctrlByteCounter] |= (byte)(input << this.ctrlBitCounter);
            ++this.ctrlBitCounter;
        }

        private class CompressionBuffer
        {
            private byte[] buffer;
            private int ctrlByteCounter;
            private int outLoc;
            private int ctrlBitCounter;
        }

        private interface CompressionChunk
        {
            void encode(PrsCompressor.CompressionBuffer buff);
        }
    }
}
