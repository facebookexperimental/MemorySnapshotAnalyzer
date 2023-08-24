/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    public sealed class MemoryLogger : ILogger
    {
        readonly SortedDictionary<string, SortedDictionary<string, List<string>>> m_log = new();

        public void Clear(string source)
        {
            m_log.Remove(source);
        }

        public void Log(string source, string context, string message)
        {
            SortedDictionary<string, List<string>>? contextToMessage;
            if (!m_log.TryGetValue(source, out contextToMessage))
            {
                contextToMessage = new();
                m_log.Add(source, contextToMessage);
            }

            List<string>? messages;
            if (!contextToMessage.TryGetValue(context, out messages))
            {
                messages = new();
                contextToMessage.Add(context, messages);
            }

            messages.Add(message);
        }

        public void Flush(Action<string> writeLine)
        {
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
