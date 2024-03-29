/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace MemorySnapshotAnalyzer.ReferenceClassifierAttributes
{
    /// <summary>
    /// Marks a field or auto-implemented property as assigning one or more tags to a given (set of) objects.
    /// </summary>
    /// <remarks>
    /// When using MemorySnapshotAnalyzer to diagnose issues within a heap snapshot, tags can make it very
    /// easy to recognize objects that have a special meaning within the structure of the program.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class TagAttribute : Attribute
    {
        readonly string m_tag;

        public TagAttribute(string tag)
        {
            m_tag = tag;
        }

        /// <summary>
        /// The tag (or tags) to assign to the referenced object(s).
        /// </summary>
        /// <remarks>
        /// Interpreted as a comma-separated list of tags (which are arbitrary strings). Each tag should
        /// respect the syntax "[a-zA-Z_][a-zA-Z0-9_]*".
        /// </remarks>
        public string Tag => m_tag;

        /// <summary>
        /// The name of the group of which this rule should be considered to be a member.
        /// </summary>
        /// <remarks>
        /// Reference classifiers can be enabled or disabled in groups within MemorySnapshotAnalyzer.
        /// This is useful when reference classifiers serve specific purposes, such as leak detection.
        /// </remarks>
        public string? GroupName { get; set; }

        /// <summary>
        /// Path of fields to dereference.
        /// </summary>
        /// <remarks>
        /// An optional sequence of additional fields to dereference. Each field can be either a verbatim
        /// field name, or the special name "[]" to indicate array indexing (performed on each individual
        /// element of the array). Each field name except the first must be preceded by a dot (".").
        ///
        /// Example: To apply to all values in a dictionary, use "_entries[].value".
        /// </remarks>
        public string? Selector { get; set; }

        /// <summary>
        /// Indicates whether field lookup needs to be dynamic.
        /// </summary>
        /// <remarks>
        /// A static field lookup is performed against the declared type of a field. A dynamic lookup
        /// is performed against the run-time type of a field.
        ///
        /// Static field lookups are preferred as they can be evaluated once against a class, instead of
        /// having to be repeatedly evaluated against every object during analysis. This makes dynamic lookups
        /// more expensive, and more error-prone (no warnings are reported if the dynamic lookup never succeeds).
        /// </remarks>
        public bool IsDynamic { get; set; }
    }
}
