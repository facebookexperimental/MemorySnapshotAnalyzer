/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MemorySnapshotAnalyzer.AbstractMemorySnapshot;

namespace MemorySnapshotAnalyzer.CommandInfrastructure
{
    public sealed class JsonStructuredOutput : IStructuredOutput
    {
        readonly bool m_keepDisplayStrings;
        readonly IStructuredOutput? m_chainedStructuredOutput;
        readonly JsonObject m_root;
        readonly Dictionary<int, string> m_indents = new();
        JsonNode m_cursor;
        Dictionary<JsonObject, StringBuilder> m_displayStrings;

        public JsonStructuredOutput(IStructuredOutput? chainedStructuredOutput, bool keepDisplayStrings)
        {
            m_keepDisplayStrings = keepDisplayStrings;
            m_chainedStructuredOutput = chainedStructuredOutput;

            m_root = new JsonObject();
            m_cursor = m_root;
            m_displayStrings = new();
        }

        StringBuilder? CurrentDisplayString
        {
            get
            {
                if (!m_keepDisplayStrings)
                {
                    return null;
                }

                JsonObject cursor = m_cursor.AsObject();
                if (!m_displayStrings.TryGetValue(cursor, out StringBuilder? sb))
                {
                    sb = new StringBuilder();
                    m_displayStrings.Add(cursor, sb);
                }

                return sb;
            }
        }

        public void AddProperty(string key, string value)
        {
            m_cursor.AsObject().Add(key, value);
            m_chainedStructuredOutput?.AddProperty(key, value);
        }

        public void AddProperty(string key, long value)
        {
            m_cursor.AsObject().Add(key, value);
            m_chainedStructuredOutput?.AddProperty(key, value);
        }

        public void AddProperty(string key, bool value)
        {
            m_cursor.AsObject().Add(key, value);
            m_chainedStructuredOutput?.AddProperty(key, value);
        }

        public void AddDisplayString(string message)
        {
            CurrentDisplayString?.Append(message);
            m_chainedStructuredOutput?.AddDisplayString(message);
        }

        public void AddDisplayString(string format, params object[] args)
        {
            CurrentDisplayString?.AppendFormat(format, args);
            m_chainedStructuredOutput?.AddDisplayString(format, args);
        }

        public void AddDisplayStringLine(string message)
        {
            CurrentDisplayString?.AppendLine(message);
            m_chainedStructuredOutput?.AddDisplayStringLine(message);
        }

        public void AddDisplayStringLine(string format, params object[] args)
        {
            CurrentDisplayString?.AppendFormat(format, args);
            CurrentDisplayString?.AppendLine();
            m_chainedStructuredOutput?.AddDisplayStringLine(format, args);
        }

        public void AddDisplayStringLineIndented(int indent, string format, params object[] args)
        {
            if (!m_indents.TryGetValue(indent, out string? indentString))
            {
                indentString = new string(' ', indent * 2);
                m_indents[indent] = indentString;
            }
            CurrentDisplayString?.Append(indentString);

            if (args.Length > 0)
            {
                CurrentDisplayString?.AppendFormat(format, args);
                CurrentDisplayString?.AppendLine();
            }
            else
            {
                // Do not interpret format string.
                CurrentDisplayString?.AppendLine(format);
            }
        }

        public void BeginArray(string key)
        {
            JsonArray array = new JsonArray();
            m_cursor.AsObject().Add(key, array);
            m_cursor = array;
            m_chainedStructuredOutput?.BeginArray(key);
        }

        public void BeginElement()
        {
            JsonObject element = new JsonObject();
            m_cursor.AsArray().Add(element);
            m_cursor = element;
            m_chainedStructuredOutput?.BeginElement();
        }

        public void EndElement()
        {
            m_cursor = m_cursor.Parent!;
            m_chainedStructuredOutput?.EndElement();
        }

        public void EndArray()
        {
            m_cursor = m_cursor.Parent!;
            m_chainedStructuredOutput?.EndArray();
        }

        public void BeginChild(string key)
        {
            JsonObject child = new JsonObject();
            m_cursor.AsObject().Add(key, child);
            m_cursor = child;
            m_chainedStructuredOutput?.BeginChild(key);
        }

        public void EndChild()
        {
            m_cursor = m_cursor.Parent!;
            m_chainedStructuredOutput?.EndChild();
        }

        public void WriteTo(Stream stream)
        {
            foreach ((JsonObject obj, StringBuilder sb) in m_displayStrings)
            {
                obj.Add("displayString", sb.ToString());
            }

            JsonWriterOptions options = new() { Indented = true };
            using (Utf8JsonWriter writer = new(stream, options))
            {
                m_root.WriteTo(writer);
            }
        }
    }
}
