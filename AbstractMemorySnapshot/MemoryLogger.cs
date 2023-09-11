/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public sealed class MemoryLogger : ILogger
    {
        readonly SortedDictionary<string, SortedDictionary<string, List<string>>> m_log = new();
        readonly SortedDictionary<string, SortedDictionary<string, List<string>>> m_newLog = new();

        public void Clear(string source)
        {
            m_log.Remove(source);
            m_newLog.Remove(source);
        }

        static void Add(string source, string context, string message, SortedDictionary<string, SortedDictionary<string, List<string>>> log)
        {
            SortedDictionary<string, List<string>>? contextToMessage;
            if (!log.TryGetValue(source, out contextToMessage))
            {
                contextToMessage = new();
                log.Add(source, contextToMessage);
            }

            List<string>? messages;
            if (!contextToMessage.TryGetValue(context, out messages))
            {
                messages = new();
                contextToMessage.Add(context, messages);
            }

            messages.Add(message);
        }

        public void Log(string source, string context, string message)
        {
            Add(source, context, message, m_newLog);
        }

        public void SummarizeNew(Action<string> writeLine)
        {
            foreach ((string source, SortedDictionary<string, List<string>> contextToMessage) in m_newLog)
            {
                foreach ((string context, List<string> messages) in contextToMessage)
                {
                    if (messages.Count > 0)
                    {
                        writeLine($"{source}: {context}: {messages[0]}");
                        if (messages.Count == 2)
                        {
                            writeLine($"{source}: {context}: {messages[1]}");
                        }
                        else if (messages.Count > 2)
                        {
                            writeLine($"... and {messages.Count - 1} more for this location");
                        }
                    }

                    foreach (string message in messages)
                    {
                        Add(source, context, message, m_log);
                    }
                }
            }
            m_newLog.Clear();
        }

        public void Flush(Action<string> writeLine)
        {
            foreach ((string source, SortedDictionary<string, List<string>> contextToMessage) in m_newLog)
            {
                foreach ((string context, List<string> messages) in contextToMessage)
                {
                    foreach (string message in messages)
                    {
                        Add(source, context, message, m_log);
                    }
                }
            }
            m_newLog.Clear();

            foreach ((string source, SortedDictionary<string, List<string>> contextToMessage) in m_log)
            {
                foreach ((string context, List<string> messages) in contextToMessage)
                {
                    foreach (string message in messages)
                    {
                        writeLine($"{source}: {context}: {message}");
                    }
                }
            }
            m_log.Clear();
        }
    }
}
