/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public sealed class FileOutput : IOutput, IDisposable
    {
        readonly Dictionary<int, string> m_indents = new();
        FileStream? m_fileStream;
        readonly TextWriter m_writer;

        public FileOutput(string outputFilename, bool useUnixNewlines)
        {
            Prompt = ">";
            m_fileStream = new FileStream(outputFilename, FileMode.Create, FileAccess.Write);
            m_writer = new StreamWriter(m_fileStream);
            if (useUnixNewlines)
            {
                m_writer.NewLine = "\n";
            }
        }

        public FileOutput(TextWriter writer)
        {
            Prompt = ">";
            m_writer = writer;
        }

        public void Dispose()
        {
            if (m_fileStream != null)
            {
                m_writer.Close();
                m_fileStream.Dispose();
                m_fileStream = null;
            }
        }

        public string Prompt { get; set; }

        public void DoPrompt()
        {
        }

        public void ExecutionStart()
        {
        }

        public void ExecutionEnd(int exitCode)
        {
        }

        public void Clear()
        {
        }

        public bool CancellationRequested()
        {
            return false;
        }

        public void Write(string message)
        {
            m_writer.Write(message);
        }

        public void Write(string format, params object[] args)
        {
            m_writer.Write(format, args);
        }

        public void WriteLine()
        {
            m_writer.WriteLine();
        }

        public void WriteLine(string message)
        {
            m_writer.WriteLine(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            m_writer.WriteLine(format, args);
        }

        public void WriteLineIndented(int indent, string format, params object[] args)
        {
            string? indentString;
            if (!m_indents.TryGetValue(indent, out indentString))
            {
                indentString = new string(' ', indent * 2);
                m_indents[indent] = indentString;
            }
            m_writer.Write(indentString);
            m_writer.WriteLine(format, args);
        }

        public void CheckForCancellation()
        {
        }
    }
}
