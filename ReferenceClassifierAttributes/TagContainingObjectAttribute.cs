/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace MemorySnapshotAnalyzer.ReferenceClassifierAttributes
{
    public enum TagCondition
    {
        FIELD_IS_ZERO,
        FIELD_IS_NONZERO,
    }

    /// <summary>
    /// Causes the containing object to be assigned one or more tags, based on the value of the given field.
    /// </summary>
    /// <remarks>
    /// When using MemorySnapshotAnalyzer to diagnose issues within a heap snapshot, these tags can make the
    /// state of an object immediately apparent. One commonly-used tag is "disposed", which should be assigned
    /// to an object that, e.g., has a non-zero "isDisposed_" field or a "handle_" field that is zeroed on dispose.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class TagContainingObjectAttribute : Attribute
    {
        readonly string m_tag;
        readonly TagCondition m_tagCondition;

        public TagContainingObjectAttribute(string tag, TagCondition tagCondition)
        {
            m_tag = tag;
            m_tagCondition = tagCondition;
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
        /// The condition under which to assign the tag(s) to the containing object.
        /// </summary>
        public TagCondition TagCondition => m_tagCondition;

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
