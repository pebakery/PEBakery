﻿/*
    Copyright (C) 2017 Hajin Jang
 
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;
using PEBakery.Tests.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Tests
{
    #region FileHelper
    [TestClass]
    public class FileHelperTests
    {
        #region DetectTextEncoding
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void DetectTextEncoding()
        {
            string tempDir = Path.GetTempFileName();
            File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, "Sample.txt");

            try
            {
                // Empty -> ANSI
                File.Create(tempFile).Close();
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.Default);

                // UTF-16 LE
                FileHelper.WriteTextBom(tempFile, Encoding.Unicode);
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.Unicode);

                // UTF-16 BE
                FileHelper.WriteTextBom(tempFile, Encoding.BigEndianUnicode);
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.BigEndianUnicode);

                // UTF-8
                FileHelper.WriteTextBom(tempFile, Encoding.UTF8);
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.UTF8);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region GetFilesEx
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void GetFilesEx()
        {
            string srcDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "FileHelper");

            // Test 1
            string[] srcFiles = FileHelper.GetFilesEx(srcDir, "*.txt", SearchOption.AllDirectories);
            Assert.IsTrue(srcFiles.Length == 6);
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "A.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "B.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "C.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "Y", "U", "V.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "Z", "X.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "Za", "W.txt"), StringComparer.Ordinal));

            // Test 2
            srcFiles = FileHelper.GetFilesEx(srcDir, "*.txt", SearchOption.TopDirectoryOnly);
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "A.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "B.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "C.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Length == 3);
        }
        #endregion

        #region GetFilesExWithDir
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void GetFilesExWithDir()
        {
            string srcDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "FileHelper");

            // Test 1
            {
                (string Path, bool IsDir)[] paths = FileHelper.GetFilesExWithDirs(srcDir, "*.txt", SearchOption.AllDirectories);

                string[] dirs = paths.Where(x => x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(dirs.Length == 5);
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Z"), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Za"), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Y"), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Y", "U"), StringComparer.Ordinal));

                string[] files = paths.Where(x => !x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(files.Length == 6);
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "C.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "Y", "U", "V.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "Z", "X.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "Za", "W.txt"), StringComparer.Ordinal));
            }
            // Test 2
            {
                (string Path, bool IsDir)[] paths = FileHelper.GetFilesExWithDirs(srcDir, "*.ini", SearchOption.AllDirectories);

                string[] dirs = paths.Where(x => x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(dirs.Length == 2);
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Z"), StringComparer.Ordinal));

                string[] files = paths.Where(x => !x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(files.Length == 2);
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "D.ini"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "Z", "Y.ini"), StringComparer.Ordinal));
            }
            // Test 3
            {
                (string Path, bool IsDir)[] paths = FileHelper.GetFilesExWithDirs(srcDir, "*.txt", SearchOption.TopDirectoryOnly);

                string[] dirs = paths.Where(x => x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(dirs.Length == 1);
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir), StringComparer.Ordinal));

                string[] files = paths.Where(x => !x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(files.Length == 3);
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "C.txt"), StringComparer.Ordinal));
            }
        }
        #endregion

        #region DirectoryCopy
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void DirectoryCopy()
        {
            string srcDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "FileHelper");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void Template(Action action)
            {
                Directory.CreateDirectory(destDir);
                try
                {
                    action.Invoke();
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            // Test 1
            Template(() =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, true, true);
                string[] files = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(files.Length == 8);
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "C.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "D.ini"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Y", "U", "V.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Z", "X.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Z", "Y.ini"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Za", "W.txt"), StringComparer.Ordinal));
            });

            // Test 2
            Template(() =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, false, true);
                string[] files = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(files.Length == 4);
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "C.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "D.ini"), StringComparer.Ordinal));
            });

            // Test 3
            Template(() =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, true, true, "*.txt");
                string[] files = Directory.GetFiles(destDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(files.Length == 6);
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "C.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Y", "U", "V.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Z", "X.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Za", "W.txt"), StringComparer.Ordinal));
            });

            // Test 4
            Template(() =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, false, true, "*.txt");
                string[] files = Directory.GetFiles(destDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(files.Length == 3);
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "C.txt"), StringComparer.Ordinal));
            });
        }
        #endregion
    }
    #endregion

    #region StringHelper
    [TestClass]
    public class StringHelperTests
    {
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("StringHelper")]
        public void SplitEx()
        {
            List<string> strs = StringHelper.SplitEx(@"1|2|3|4|5", "|", StringComparison.Ordinal);
            Assert.AreEqual(5, strs.Count);
            Assert.IsTrue(strs[0].Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(strs[1].Equals("2", StringComparison.Ordinal));
            Assert.IsTrue(strs[2].Equals("3", StringComparison.Ordinal));
            Assert.IsTrue(strs[3].Equals("4", StringComparison.Ordinal));
            Assert.IsTrue(strs[4].Equals("5", StringComparison.Ordinal));

            strs = StringHelper.SplitEx(@"1a2A3a4A5", "a", StringComparison.Ordinal);
            Assert.AreEqual(3, strs.Count);
            Assert.IsTrue(strs[0].Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(strs[1].Equals("2A3", StringComparison.Ordinal));
            Assert.IsTrue(strs[2].Equals("4A5", StringComparison.Ordinal));

            strs = StringHelper.SplitEx(@"1a2A3a4A5", "a", StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(5, strs.Count);
            Assert.IsTrue(strs[0].Equals("1", StringComparison.Ordinal));
            Assert.IsTrue(strs[1].Equals("2", StringComparison.Ordinal));
            Assert.IsTrue(strs[2].Equals("3", StringComparison.Ordinal));
            Assert.IsTrue(strs[3].Equals("4", StringComparison.Ordinal));
            Assert.IsTrue(strs[4].Equals("5", StringComparison.Ordinal));
        }

        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("StringHelper")]
        public void ReplaceEx()
        {
            string str = StringHelper.ReplaceEx(@"ABCD", "AB", "XYZ", StringComparison.Ordinal);
            Assert.IsTrue(str.Equals("XYZCD", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"ABCD", "ab", "XYZ", StringComparison.Ordinal);
            Assert.IsTrue(str.Equals("ABCD", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"abcd", "AB", "XYZ", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(str.Equals("XYZcd", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"abcd", "ab", "XYZ", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(str.Equals("XYZcd", StringComparison.Ordinal));
        }
    }
    #endregion

    #region HashHelper
    [TestClass]
    public class HashHelperTests
    {
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("HashHelper")]
        public void GetHash()
        {
            void ArrayTemplate(HashHelper.HashType type, byte[] input, string expected)
            {
                byte[] digest = HashHelper.GetHash(type, input);
                string actual = StringHelper.ToHexStr(digest);
                Assert.IsTrue(actual.Equals(expected, StringComparison.Ordinal));
            }

            void StreamTemplate(HashHelper.HashType type, Stream stream, string expected)
            {
                stream.Position = 0;
                byte[] digest = HashHelper.GetHash(type, stream);
                string actual = StringHelper.ToHexStr(digest);
                Assert.IsTrue(actual.Equals(expected, StringComparison.Ordinal));
            }

            byte[] buffer = Encoding.UTF8.GetBytes("HelloWorld");
            ArrayTemplate(HashHelper.HashType.MD5, buffer, "68e109f0f40ca72a15e05cc22786f8e6");
            ArrayTemplate(HashHelper.HashType.SHA1, buffer, "db8ac1c259eb89d4a131b253bacfca5f319d54f2");
            ArrayTemplate(HashHelper.HashType.SHA256, buffer, "872e4e50ce9990d8b041330c47c9ddd11bec6b503ae9386a99da8584e9bb12c4");
            ArrayTemplate(HashHelper.HashType.SHA384, buffer, "293cd96eb25228a6fb09bfa86b9148ab69940e68903cbc0527a4fb150eec1ebe0f1ffce0bc5e3df312377e0a68f1950a");
            ArrayTemplate(HashHelper.HashType.SHA512, buffer, "8ae6ae71a75d3fb2e0225deeb004faf95d816a0a58093eb4cb5a3aa0f197050d7a4dc0a2d5c6fbae5fb5b0d536a0a9e6b686369fa57a027687c3630321547596");

            using (MemoryStream ms = new MemoryStream(buffer))
            {
                StreamTemplate(HashHelper.HashType.MD5, ms, "68e109f0f40ca72a15e05cc22786f8e6");
                StreamTemplate(HashHelper.HashType.SHA1, ms, "db8ac1c259eb89d4a131b253bacfca5f319d54f2");
                StreamTemplate(HashHelper.HashType.SHA256, ms, "872e4e50ce9990d8b041330c47c9ddd11bec6b503ae9386a99da8584e9bb12c4");
                StreamTemplate(HashHelper.HashType.SHA384, ms, "293cd96eb25228a6fb09bfa86b9148ab69940e68903cbc0527a4fb150eec1ebe0f1ffce0bc5e3df312377e0a68f1950a");
                StreamTemplate(HashHelper.HashType.SHA512, ms, "8ae6ae71a75d3fb2e0225deeb004faf95d816a0a58093eb4cb5a3aa0f197050d7a4dc0a2d5c6fbae5fb5b0d536a0a9e6b686369fa57a027687c3630321547596");
            }
        }

        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("HashHelper")]
        public void GetHashProgress()
        {
            void ArrayTemplate(HashHelper.HashType type, byte[] input, string expected)
            {
                int i = 0;
                IProgress<(long Position, long Length)> progress = new Progress<(long Position, long Length)>(x =>
                {
                    Assert.AreEqual((i + 1) * HashHelper.ReportInterval, x.Position);
                    Assert.AreEqual(input.Length, x.Length);
                    i += 1;
                });

                byte[] digest = HashHelper.GetHash(type, input, progress);
                string actual = StringHelper.ToHexStr(digest);
                Assert.IsTrue(actual.Equals(expected, StringComparison.Ordinal));
                Assert.AreEqual(3, i);
            }

            void StreamTemplate(HashHelper.HashType type, Stream stream, string expected)
            {
                int i = 0;
                IProgress<(long Position, long Length)> progress = new Progress<(long Position, long Length)>(x =>
                {
                    Assert.AreEqual((i + 1) * HashHelper.ReportInterval, x.Position);
                    Assert.AreEqual(stream.Length, x.Length);
                    i += 1;
                });

                stream.Position = 0;
                byte[] digest = HashHelper.GetHash(type, stream, progress);
                string actual = StringHelper.ToHexStr(digest);
                Assert.IsTrue(actual.Equals(expected, StringComparison.Ordinal));
                Assert.AreEqual(3, i);
            }

            string srcDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "HashHelper");
            string srcFile = Path.Combine(srcDir, "sample.bin");

            FileInfo fi = new FileInfo(srcFile);
            byte[] buffer = new byte[fi.Length];

            using (FileStream fs = new FileStream(srcFile, FileMode.Open))
            {
                fs.Read(buffer, 0, buffer.Length);
            }

            const string md5Digest = "656aca9ddca96c931e397c64afa3d838";
            const string sha1Digest = "9c2603f1a3b5156eb66c9975d3ebbe229cc9dbc0";
            const string sha256Digest = "4d9cbaf3aa0935a8c113f139691b3daf9c94c8d6c278aedc8eec66a4b9f6c8ae";
            const string sha384Digest = "4e7027f3ff93f86f805a91abba7f7d16918493f464bdf5211ad8768d2b4a22ca5d7c235f1f81992140cbf3efa405558e";
            const string sha512Digest = "7f1bc5840d0e5b1b1e9aedecfed4d4de3249839de8ab33b1cea1873c1f4e453c3bb5e08260e0a567d561a19cd54597c41c5f983bb0515392caaefdb5158df281";

            ArrayTemplate(HashHelper.HashType.MD5, buffer, md5Digest);
            ArrayTemplate(HashHelper.HashType.SHA1, buffer, sha1Digest);
            ArrayTemplate(HashHelper.HashType.SHA256, buffer, sha256Digest);
            ArrayTemplate(HashHelper.HashType.SHA384, buffer, sha384Digest);
            ArrayTemplate(HashHelper.HashType.SHA512, buffer, sha512Digest);

            using (MemoryStream ms = new MemoryStream(buffer))
            {
                StreamTemplate(HashHelper.HashType.MD5, ms, md5Digest);
                StreamTemplate(HashHelper.HashType.SHA1, ms, sha1Digest);
                StreamTemplate(HashHelper.HashType.SHA256, ms, sha256Digest);
                StreamTemplate(HashHelper.HashType.SHA384, ms, sha384Digest);
                StreamTemplate(HashHelper.HashType.SHA512, ms, sha512Digest);
            }
        }
    }
    #endregion
}
