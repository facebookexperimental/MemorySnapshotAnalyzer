// Copyright(c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.CommandProcessing
{
    sealed class ConsoleOutput : IOutput
    {
        // https://code.visualstudio.com/docs/terminal/shell-integration#_vs-code-custom-sequences-osc-633-st
        readonly string s_markPromptStart = "\u001b]633;A\u0007";
        readonly string s_markPromptEnd = "\u001b]633;B\u0007";
        readonly string s_preExecution = "\u001b]633;C\u0007";
        readonly string s_executionFinished_exitCode = "\u001b]633;D;{0}\u0007";

        string m_prompt;
        readonly Dictionary<int, string> m_indents;
        readonly bool m_useSemanticPrompt;
        int m_windowHeight;
        int m_numberLinesWritten;
        bool m_cancellationRequested;

        public ConsoleOutput()
        {
            m_prompt = "> ";
            m_indents = new Dictionary<int, string>();

            string? termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
            if (termProgram != null && termProgram.Equals("vscode", StringComparison.Ordinal))
            {
                m_useSemanticPrompt = true;
            }

            m_windowHeight = Console.WindowHeight;
            m_numberLinesWritten = 0;
            m_cancellationRequested = false;
            Console.CancelKeyPress += (handler, args) =>
                {
                    m_cancellationRequested = true;
                    args.Cancel = true;
                };
        }

        void IOutput.SetPrompt(string prompt)
        {
            m_prompt = prompt;
        }

        void IOutput.Prompt()
        {
            if (m_useSemanticPrompt)
            {
                Console.Write("{0}{1}{2}",
                    s_markPromptStart,
                    m_prompt,
                    s_markPromptEnd);
            }
            else
            {
                Console.Write(m_prompt);
            }

            m_numberLinesWritten = 0;
            m_windowHeight = Console.WindowHeight;
            m_cancellationRequested = false;
        }

        void IOutput.ExecutionStart()
        {
            if (m_useSemanticPrompt)
            {
                Console.Write(s_preExecution);
            }
        }

        void IOutput.ExecutionEnd(int exitCode)
        {
            if (m_useSemanticPrompt)
            {
                Console.Write(s_executionFinished_exitCode, exitCode);
            }
        }

        void IOutput.Clear()
        {
            Console.Clear();
            m_numberLinesWritten = 0;
        }

        bool IOutput.CancellationRequested()
        {
            return m_cancellationRequested;
        }

        void IOutput.Write(string message)
        {
            Console.Write(message);
        }

        void IOutput.Write(string format, params object[] args)
        {
            Console.Write(format, args);
        }

        void IOutput.WriteLine()
        {
            Console.WriteLine();
            CompleteLine();
        }

        void IOutput.WriteLine(string message)
        {
            Console.WriteLine(message);
            CompleteLine();
        }

        void IOutput.WriteLine(string format, params object[] args)
        {
            Console.WriteLine(format, args);
            CompleteLine();
        }

        void IOutput.WriteLineIndented(int indent, string format, params object[] args)
        {
            string? indentString;
            if (!m_indents.TryGetValue(indent, out indentString))
            {
                indentString = new string(' ', indent * 2);
                m_indents[indent] = indentString;
            }
            Console.Write(indentString);
            if (args.Length > 0)
            {
                Console.WriteLine(format, args);
            }
            else
            {
                // Do not interpret format string.
                Console.WriteLine(format);
            }
            CompleteLine();
        }

        public void CheckForCancellation()
        {
            if (m_cancellationRequested)
            {
                m_cancellationRequested = false;
                throw new OperationCanceledException();
            }
        }

        void CompleteLine()
        {
            if (++m_numberLinesWritten >= m_windowHeight - 2)
            {
                Console.Write(" -- more --");
                Console.TreatControlCAsInput = true;
                ConsoleKeyInfo keyInfo;
                do
                {
                    keyInfo = Console.ReadKey();
                    m_cancellationRequested |= keyInfo.Key == ConsoleKey.Q || keyInfo.Modifiers == ConsoleModifiers.Control && keyInfo.Key == ConsoleKey.C;
                }
                while (keyInfo.Key != ConsoleKey.Enter && keyInfo.Key != ConsoleKey.Spacebar && !m_cancellationRequested);
                Console.TreatControlCAsInput = false;
                Console.WriteLine();

                m_numberLinesWritten = 0;
            }

            CheckForCancellation();
        }
    }
}
