/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace MemorySnapshotAnalyzer.AbstractMemorySnapshot
{
    /// <summary>
    /// An interface for reporting warnings that can be aggregated for later inspection and reporting.
    /// </summary>
    /// <remarks>
    /// We create a separate logger object for each analysis context.
    /// </remarks>
    public interface ILogger
    {
        /// <summary>
        /// Clears all warnings reported from the given source.
        /// </summary>
        /// <remarks>
        /// We create one logger object per context, and analysis phases ("sources") that can find conditions
        /// worth warning about can be rerun if analysis options are changed. This method helps make sure that
        /// only the warnings from the last run of that analysis phase are reported.
        /// </remarks>
        public void Clear(string source);

        /// <summary>
        /// Logs a warning message from a specific source and associated with a specific context.
        /// </summary>
        /// <param name="source">The source that discovered the warning, e.g., an analysis phase.</param>
        /// <param name="context">The context to associate the warning with, e.g., the filename and line number
        /// of a specific reference classifier rule. (Not to be confused with an analysis context.)</param>
        /// <param name="message">A free-form message.</param>
        public void Log(string source, string context, string message);

        /// <summary>
        /// Provides a summary of new warnings logged since the last time a summary was produced
        /// (or that warnings were cleared or flushed).
        /// </summary>
        /// <param name="writeLine">Called for each text line in the summary.</param>
        public void SummarizeNew(Action<string> writeLine);

        /// <summary>
        /// Outputs all warnings encountered, and clears them from this logger object.
        /// </summary>
        /// <param name="writeLine">Called for each text line.</param>
        public void Flush(Action<string> writeLine);
    }
}
