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

        public void SummarizeNew(IStructuredOutput output)
        {
            output.BeginChild("logSummary");
            output.BeginChild("sources");
            int totalNewMessages = 0;
            foreach ((string source, SortedDictionary<string, List<string>> contextToMessage) in m_newLog)
            {
                bool first = true;
                foreach ((string context, List<string> messages) in contextToMessage)
                {
                    if (messages.Count > 0)
                    {
                        if (first)
                        {
                            output.BeginChild(source);
                            output.BeginArray("contexts");
                            first = false;
                        }

                        output.BeginElement();

                        output.AddProperty("context", context);
                        output.AddProperty("numberOfMessages", messages.Count);
                        output.AddProperty("firstMessage", messages[0]);

                        output.AddDisplayStringLine("{0}: {1}: {2}", source, context, messages[0]);
                        if (messages.Count == 2)
                        {
                            output.AddDisplayStringLine("{0}: {1}: {2}", source, context, messages[1]);
                        }
                        else if (messages.Count > 2)
                        {
                            output.AddDisplayStringLine("... and {0} more for this location", messages.Count - 1);
                        }

                        output.EndElement();
                    }

                    foreach (string message in messages)
                    {
                        Add(source, context, message, m_log);
                        totalNewMessages++;
                    }
                }

                if (!first)
                {
                    output.EndArray(); // contexts
                    output.EndChild(); // source
                }
            }
            output.EndChild(); // sources
            output.AddProperty("totalNewMessages", totalNewMessages);
            output.EndChild(); // logSummary

            m_newLog.Clear();
        }

        public void Flush(IStructuredOutput output)
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

            output.BeginChild("log");
            int totalMessages = 0;
            foreach ((string source, SortedDictionary<string, List<string>> contextToMessage) in m_log)
            {
                output.BeginArray(source);

                foreach ((string context, List<string> messages) in contextToMessage)
                {
                    output.BeginElement();
                    output.AddProperty("context", context);
                    output.BeginArray("messages");
                    foreach (string message in messages)
                    {
                        output.BeginElement();
                        output.AddProperty("message", message);
                        output.AddDisplayStringLine("{0}: {1}: {2}", source, context, message);
                        output.EndElement();
                        totalMessages++;
                    }
                    output.EndArray();
                    output.EndElement();
                }

                output.EndArray();
            }
            m_log.Clear();
            output.AddProperty("totalMessages", totalMessages);
            output.EndChild();
        }
    }
}
