// Decompiled with JetBrains decompiler
// Type: zamboni.PrsCompDecomp
// Assembly: zamboni, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 73B487C9-8F41-4586-BEF5-F7D7BFBD4C55
// Assembly location: D:\Downloads\zamboni_ngs (3)\zamboni.exe

using psu_generic_parser;
using System;

namespace zamboni
{
    public class PrsCompDecomp
    {
        private int ctrlByteCounter;
        private int ctrlBytePos = 0;
        private byte origCtrlByte = 0;
        private byte ctrlByte = 0;
        private byte[] decompBuffer;
        private int currDecompPos = 0;
        private int numCtrlBytes = 1;

        private bool getCtrlBit()
        {
            --this.ctrlByteCounter;
            if (this.ctrlByteCounter == 0)
            {
                this.ctrlBytePos = this.currDecompPos;
                this.origCtrlByte = this.decompBuffer[this.currDecompPos];
                this.ctrlByte = this.decompBuffer[this.currDecompPos++];
                this.ctrlByteCounter = 8;
                ++this.numCtrlBytes;
            }
            bool flag = ((uint)this.ctrlByte & 1U) > 0U;
            this.ctrlByte >>= 1;
            return flag;
        }

        public static byte[] Decompress(byte[] input, uint outCount) => new PrsCompDecomp().localDecompress(input, outCount);

        public byte[] localDecompress(byte[] input, uint outCount)
        {
            byte[] numArray = new byte[(int)outCount];
            this.decompBuffer = input;
            this.ctrlByte = (byte)0;
            this.ctrlByteCounter = 1;
            this.numCtrlBytes = 1;
            this.currDecompPos = 0;
            int num1 = 0;
            //try
            //{
                while ((long)num1 < (long)outCount && this.currDecompPos < input.Length)
                {
                    while (this.getCtrlBit())
                        numArray[num1++] = this.decompBuffer[this.currDecompPos++];
                    int num2;
                    int num3;
                    if (this.getCtrlBit())
                    {
                        if (this.currDecompPos < this.decompBuffer.Length)
                        {
                            int num4 = (int)this.decompBuffer[this.currDecompPos++];
                            int num5 = (int)this.decompBuffer[this.currDecompPos++];
                            int num6 = num4;
                            int num7 = num5;
                            if (num6 != 0 || num7 != 0)
                            {
                                num2 = (num7 << 5) + (num6 >> 3) - 8192;
                                int num8 = num6 & 7;
                                num3 = num8 != 0 ? num8 + 2 : (int)this.decompBuffer[this.currDecompPos++] + 10;
                            }
                            else
                                break;
                        }
                        else
                            break;
                    }
                    else
                    {
                        num3 = 2;
                        if (this.getCtrlBit())
                            num3 += 2;
                        if (this.getCtrlBit())
                            ++num3;
                        num2 = (int)this.decompBuffer[this.currDecompPos++] - 256;
                    }
                    int num9 = num2 + num1;
                    for (int index = 0; index < num3 && num1 < numArray.Length; ++index)
                        numArray[num1++] = numArray[num9++];
                }
            //}
           // catch (Exception ex)
           // {
            //}
            return numArray;
        }

        public static byte[] compress(byte[] toCompress) => new PrsCompressor().compress(toCompress);
    }
}
