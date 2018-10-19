﻿/*
    Copyright (C) 2016-2018 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PEBakery.Core.Commands
{
    public static class CommandHash
    {
        public static List<LogInfo> Hash(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            CodeInfo_Hash info = cmd.Info.Cast<CodeInfo_Hash>();

            string hashTypeStr = StringEscaper.Preprocess(s, info.HashType);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);
            Debug.Assert(hashTypeStr != null, $"{nameof(hashTypeStr)} != null");
            Debug.Assert(filePath != null, $"{nameof(filePath)} != null");

            string digest;
            HashHelper.HashType hashType = HashHelper.ParseHashType(hashTypeStr);
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] rawDigest = HashHelper.GetHash(hashType, fs);
                digest = StringHelper.ToHexStr(rawDigest);
            }

            logs.Add(new LogInfo(LogState.Success, $"Hash [{hashType}] digest of [{filePath}] is [{digest}]"));
            List<LogInfo> varLogs = Variables.SetVariable(s, info.DestVar, digest);
            logs.AddRange(varLogs);

            return logs;
        }
    }
}
