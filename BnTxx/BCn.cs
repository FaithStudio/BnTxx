﻿using System;
using System.Drawing;

namespace BnTxx
{
    static class BCn
    {
        public static Bitmap DecodeBC1(byte[] Data, int Width, int Height)
        {
            byte[] Output = new byte[Width * Height * 4];

            int XB = CountZeros(Width  / 4);
            int YB = CountZeros(Height / 4);

            for (int Y = 0; Y < Height / 4; Y++)
            {
                for (int X = 0; X < Width / 4; X++)
                {
                    int Offset = GetSwizzledAddressBC1(X, Y, XB, YB) * 8;

                    byte[] Tile = BCnDecodeTile(Data, Offset, true);

                    int TOffset = 0;

                    for (int TY = 0; TY < 4; TY++)
                    {
                        for (int TX = 0; TX < 4; TX++)
                        {
                            int OOffset = (X * 4 + TX + (Y * 4 + TY) * Width) * 4;

                            Output[OOffset + 0] = Tile[TOffset + 0];
                            Output[OOffset + 1] = Tile[TOffset + 1];
                            Output[OOffset + 2] = Tile[TOffset + 2];
                            Output[OOffset + 3] = Tile[TOffset + 3];

                            TOffset += 4;
                        }
                    }
                }
            }

            return PixelDecoder.GetBitmap(Output, Width, Height);
        }

        public static Bitmap DecodeBC2(byte[] Data, int Width, int Height)
        {
            byte[] Output = new byte[Width * Height * 4];

            int XB = CountZeros(Width  / 4);
            int YB = CountZeros(Height / 4);

            for (int Y = 0; Y < Height / 4; Y++)
            {
                for (int X = 0; X < Width / 4; X++)
                {
                    int Offset = GetSwizzledAddressBC2_3(X, Y, XB, YB) * 16;

                    byte[] Tile = BCnDecodeTile(Data, Offset + 8, false);

                    int AlphaLow  = Get32(Data, Offset + 0);
                    int AlphaHigh = Get32(Data, Offset + 4);

                    ulong AlphaCh = (uint)AlphaLow | (ulong)AlphaHigh << 32;

                    int TOffset = 0;

                    for (int TY = 0; TY < 4; TY++)
                    {
                        for (int TX = 0; TX < 4; TX++)
                        {
                            ulong Alpha = (AlphaCh >> (TY * 16 + TX * 4)) & 0xf;

                            int OOffset = (X * 4 + TX + (Y * 4 + TY) * Width) * 4;

                            Output[OOffset + 0] = Tile[TOffset + 0];
                            Output[OOffset + 1] = Tile[TOffset + 1];
                            Output[OOffset + 2] = Tile[TOffset + 2];
                            Output[OOffset + 3] = (byte)(Alpha | (Alpha << 4));

                            TOffset += 4;
                        }
                    }
                }
            }

            return PixelDecoder.GetBitmap(Output, Width, Height);
        }

        public static Bitmap DecodeBC3(byte[] Data, int Width, int Height)
        {
            byte[] Output = new byte[Width * Height * 4];

            int XB = CountZeros(Width  / 4);
            int YB = CountZeros(Height / 4);

            for (int Y = 0; Y < Height / 4; Y++)
            {
                for (int X = 0; X < Width / 4; X++)
                {
                    int Offset = GetSwizzledAddressBC2_3(X, Y, XB, YB) * 16;

                    byte[] Tile = BCnDecodeTile(Data, Offset + 8, false);

                    byte[] Alpha = new byte[8];

                    Alpha[0] = Data[Offset + 0];
                    Alpha[1] = Data[Offset + 1];

                    CalculateBC3Alpha(Alpha);

                    int AlphaLow  = Get32(Data, Offset + 2);
                    int AlphaHigh = Get16(Data, Offset + 6);

                    ulong AlphaCh = (uint)AlphaLow | (ulong)AlphaHigh << 32;

                    int TOffset = 0;

                    for (int TY = 0; TY < 4; TY++)
                    {
                        for (int TX = 0; TX < 4; TX++)
                        {
                            int OOffset = (X * 4 + TX + (Y * 4 + TY) * Width) * 4;

                            Output[OOffset + 0] = Tile[TOffset + 0];
                            Output[OOffset + 1] = Tile[TOffset + 1];
                            Output[OOffset + 2] = Tile[TOffset + 2];
                            Output[OOffset + 3] = Alpha[(AlphaCh >> (TY * 12 + TX * 3)) & 7];

                            TOffset += 4;
                        }
                    }
                }
            }

            return PixelDecoder.GetBitmap(Output, Width, Height);
        }

        private static int CountZeros(int Value)
        {
            int Count = 0;

            for (int i = 0; i < 32; i++)
            {
                if ((Value & (1 << i)) != 0)
                {
                    break;
                }

                Count++;
            }

            return Count;
        }

        private static void CalculateBC3Alpha(byte[] Alpha)
        {
            for (int i = 2; i < 8; i++)
            {
                if (Alpha[0] > Alpha[1])
                {
                    Alpha[i] = (byte)(((8 - i) * Alpha[0] + (i - 1) * Alpha[1]) / 7);
                }
                else if (i < 6)
                {
                    Alpha[i] = (byte)(((6 - i) * Alpha[0] + (i - 1) * Alpha[1]) / 7);
                }
                else if (i == 6)
                {
                    Alpha[i] = 0;
                }
                else /* i == 7 */
                {
                    Alpha[i] = 0xff;
                }
            }
        }

        private static byte[] BCnDecodeTile(
            byte[] Input,
            int    Offset,
            bool   IsBC1)
        {
            Color[] CLUT = new Color[4];

            int c0 = Get16(Input, Offset + 0);
            int c1 = Get16(Input, Offset + 2);

            CLUT[0] = DecodeRGB565(c0);
            CLUT[1] = DecodeRGB565(c1);
            CLUT[2] = CalculateCLUT2(CLUT[0], CLUT[1], c0, c1, IsBC1);
            CLUT[3] = CalculateCLUT3(CLUT[0], CLUT[1], c0, c1, IsBC1);

            int Indices = Get32(Input, Offset + 4);

            int IdxShift = 0;

            byte[] Output = new byte[4 * 4 * 4];

            int OOffset = 0;

            for (int TY = 0; TY < 4; TY++)
            {
                for (int TX = 0; TX < 4; TX++)
                {
                    int Idx = (Indices >> IdxShift) & 3;

                    IdxShift += 2;

                    Color Pixel = CLUT[Idx];

                    Output[OOffset + 0] = Pixel.B;
                    Output[OOffset + 1] = Pixel.G;
                    Output[OOffset + 2] = Pixel.R;
                    Output[OOffset + 3] = 0xff;

                    OOffset += 4;
                }
            }

            return Output;
        }

        private static Color CalculateCLUT2(Color C0, Color C1, int c0, int c1, bool IsBC1)
        {
            if (c0 > c1 || !IsBC1)
            {
                return Color.FromArgb(
                    (2 * C0.R + C1.R) / 3,
                    (2 * C0.G + C1.G) / 3,
                    (2 * C0.B + C1.B) / 3);
            }
            else
            {
                return Color.FromArgb(
                    (C0.R + C1.R) / 2,
                    (C0.G + C1.G) / 2,
                    (C0.B + C1.B) / 2);
            }
        }

        private static Color CalculateCLUT3(Color C0, Color C1, int c0, int c1, bool IsBC1)
        {
            if (c0 > c1 || !IsBC1)
            {
                return
                    Color.FromArgb(
                        (2 * C1.R + C0.R) / 3,
                        (2 * C1.G + C0.G) / 3,
                        (2 * C1.B + C0.B) / 3);
            }

            return Color.Black;
        }

        private static Color DecodeRGB565(int Value)
        {
            int B = ((Value >>  0) & 0x1f) << 3;
            int G = ((Value >>  5) & 0x3f) << 2;
            int R = ((Value >> 11) & 0x1f) << 3;

            return Color.FromArgb(
                R | (R >> 5),
                G | (G >> 6),
                B | (B >> 5));
        }

        private static int Get16(byte[] Data, int Address)
        {
            return
                Data[Address + 0] << 0 |
                Data[Address + 1] << 8;
        }

        private static int Get32(byte[] Data, int Address)
        {
            return
                Data[Address + 0] <<  0 |
                Data[Address + 1] <<  8 |
                Data[Address + 2] << 16 |
                Data[Address + 3] << 24;
        }

        private static int GetSwizzledAddressBC1(int X, int Y, int XB, int YB)
        {
            return GetSwizzledAddress(X, Y, XB, YB, true);
        }

        private static int GetSwizzledAddressBC2_3(int X, int Y, int XB, int YB)
        {
            return GetSwizzledAddress(X, Y, XB, YB, false);
        }

        private static int GetSwizzledAddress(int X, int Y, int XB, int YB, bool XFirst)
        {
            /*
             * Examples of patterns:
             *                   x x y x y y x y    64   x 64   dxt5
             *       x x x x x y y y y x y y x y    512  x 512  dxt5
             *   y x x x x x x y y y y x y y x y    1024 x 1024 dxt5
             * y y x x x x x x y y y y x y y x y x  2048 x 2048 dxt1
             * 
             * Read from right to left, LSB first.
             * No idea why BC1/DXT1 starts with X.
             */
            int Size    = XB + YB;
            int XCnt    = 1;
            int YCnt    = 1;
            int Bit     = 0;
            int Address = 0;

            while (Bit < Math.Min(Size, 9))
            {
                int YMask = (1 << YCnt) - 1;

                if (XFirst)
                {
                    Address |= (X & 1)     << Bit;
                    Address |= (Y & YMask) << Bit + XCnt;
                }
                else
                {
                    Address |= (Y & YMask) << Bit;
                    Address |= (X & 1)     << Bit + YCnt;
                }

                X >>= XCnt;
                Y >>= YCnt;

                XB -= XCnt;
                YB -= YCnt;

                Bit += XCnt + YCnt;

                XCnt = Math.Min(XB, 1);
                YCnt = Math.Min(YB, YCnt << 1);
            }

            Address |= X << Bit;
            Address |= Y << Bit + XB;

            return Address;
        }
    }
}