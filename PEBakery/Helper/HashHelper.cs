﻿/*
    Copyright (C) 2016-2018 Hajin Jang
    Licensed under MIT License.
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Helper
{
    #region HashHelper
    public static class HashHelper
    {
        public enum HashType { None, MD5, SHA1, SHA256, SHA384, SHA512 }

        public const int MD5Len = 128 / 8;
        public const int SHA1Len = 160 / 8;
        public const int SHA256Len = 256 / 8;
        public const int SHA384Len = 384 / 8;
        public const int SHA512Len = 512 / 8;

        public static byte[] CalcHash(HashHelper.HashType type, byte[] data)
        {
            return InternalCalcHash(type, data);
        }

        public static byte[] CalcHash(HashHelper.HashType type, string hex)
        {
            if (!NumberHelper.ParseHexStringToBytes(hex, out byte[] data))
                throw new InvalidOperationException("Failed to parse string into hexadecimal bytes");
            return InternalCalcHash(type, data);
        }

        public static byte[] CalcHash(HashHelper.HashType type, Stream stream)
        {
            return InternalCalcHash(type, stream);
        }

        public static string CalcHashString(HashHelper.HashType type, byte[] data)
        {
            byte[] h = InternalCalcHash(type, data);
            StringBuilder builder = new StringBuilder();
            foreach (byte b in h)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        public static string CalcHashString(HashHelper.HashType type, string hex)
        {
            if (!NumberHelper.ParseHexStringToBytes(hex, out byte[] data))
                throw new InvalidOperationException("Failed to parse string into hexadecimal bytes");
            byte[] h = InternalCalcHash(type, data);
            StringBuilder builder = new StringBuilder();
            foreach (byte b in h)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        public static string CalcHashString(HashHelper.HashType type, Stream stream)
        {
            byte[] h = InternalCalcHash(type, stream);
            StringBuilder builder = new StringBuilder();
            foreach (byte b in h)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        private static byte[] InternalCalcHash(HashHelper.HashType type, byte[] data)
        {
            HashAlgorithm hash;
            switch (type)
            {
                case HashHelper.HashType.MD5:
                    hash = MD5.Create();
                    break;
                case HashHelper.HashType.SHA1:
                    hash = SHA1.Create();
                    break;
                case HashHelper.HashType.SHA256:
                    hash = SHA256.Create();
                    break;
                case HashHelper.HashType.SHA384:
                    hash = SHA384.Create();
                    break;
                case HashHelper.HashType.SHA512:
                    hash = SHA512.Create();
                    break;
                default:
                    throw new InvalidOperationException("Invalid Hash Type");
            }
            return hash.ComputeHash(data);
        }

        private static byte[] InternalCalcHash(HashHelper.HashType type, Stream stream)
        {
            HashAlgorithm hash;
            switch (type)
            {
                case HashHelper.HashType.MD5:
                    hash = MD5.Create();
                    break;
                case HashHelper.HashType.SHA1:
                    hash = SHA1.Create();
                    break;
                case HashHelper.HashType.SHA256:
                    hash = SHA256.Create();
                    break;
                case HashHelper.HashType.SHA384:
                    hash = SHA384.Create();
                    break;
                case HashHelper.HashType.SHA512:
                    hash = SHA512.Create();
                    break;
                default:
                    throw new InvalidOperationException("Invalid Hash Type");
            }
            return hash.ComputeHash(stream);
        }

        public static HashHelper.HashType DetectHashType(byte[] data)
        {
            return InternalDetectHashType(data.Length);
        }

        public static HashHelper.HashType DetectHashType(string hex)
        {
            if (StringHelper.IsHex(hex))
                return HashHelper.HashType.None;
            if (!NumberHelper.ParseHexStringToBytes(hex, out byte[] hashByte))
                return HashHelper.HashType.None;

            return InternalDetectHashType(hashByte.Length);
        }

        private static HashHelper.HashType InternalDetectHashType(int length)
        {
            HashHelper.HashType hashType = HashHelper.HashType.None;

            switch (length)
            {
                case HashHelper.MD5Len * 2:
                    hashType = HashHelper.HashType.MD5;
                    break;
                case HashHelper.SHA1Len * 2:
                    hashType = HashHelper.HashType.SHA1;
                    break;
                case HashHelper.SHA256Len * 2:
                    hashType = HashHelper.HashType.SHA256;
                    break;
                case HashHelper.SHA384Len * 2:
                    hashType = HashHelper.HashType.SHA384;
                    break;
                case HashHelper.SHA512Len * 2:
                    hashType = HashHelper.HashType.SHA512;
                    break;
                default:
                    throw new InvalidOperationException("Cannot recognize valid hash string");
            }

            return hashType;
        }

        public static int GetHashByteLen(HashHelper.HashType hashType)
        {
            int byteLen;
            switch (hashType)
            {
                case HashHelper.HashType.MD5:
                    byteLen = 32;
                    break;
                case HashHelper.HashType.SHA1:
                    byteLen = 40;
                    break;
                case HashHelper.HashType.SHA256:
                    byteLen = 64;
                    break;
                case HashHelper.HashType.SHA384:
                    byteLen = 96;
                    break;
                case HashHelper.HashType.SHA512:
                    byteLen = 128;
                    break;
                default:
                    throw new ArgumentException($"Wrong HashType [{hashType}]");
            }
            return byteLen;
        }

        public static HashHelper.HashType ParseHashType(string str)
        {
            HashHelper.HashType hashType;
            if (str.Equals("MD5", StringComparison.OrdinalIgnoreCase))
                hashType = HashHelper.HashType.MD5;
            else if (str.Equals("SHA1", StringComparison.OrdinalIgnoreCase))
                hashType = HashHelper.HashType.SHA1;
            else if (str.Equals("SHA256", StringComparison.OrdinalIgnoreCase))
                hashType = HashHelper.HashType.SHA256;
            else if (str.Equals("SHA384", StringComparison.OrdinalIgnoreCase))
                hashType = HashHelper.HashType.SHA384;
            else if (str.Equals("SHA512", StringComparison.OrdinalIgnoreCase))
                hashType = HashHelper.HashType.SHA512;
            else
                throw new ArgumentException($"Wrong HashType [{str}]");
            return hashType;
        }
    }
    #endregion
}