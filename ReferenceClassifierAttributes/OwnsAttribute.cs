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
    /// Marks a field or auto-implemented property as being the owning reference for a given (set of) objects.
    /// </summary>
    /// <remarks>
    /// When MemorySnapshotAnalyzer generates a backtrace, or a dominator tree, for a heap snapshot,
    /// owning references disambiguate which referencing object should be considered the owner of a given
    /// object. This is important for memory attribution, such that cross-references into objects from
    /// another (owning) subsystem are not counted against a referencing subsystem.
    ///
    /// If the cross-reference outlasts the owning subsystem, the object will end up getting counted
    /// against the referencing subsystem.
    ///
    /// Each individual object should have only one owning reference to it. Multiple owning references
    /// to the same object are reported as a warning by MemorySnapshotAnalyzer.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class OwnsAttribute : Attribute
    {
        public OwnsAttribute()
        {
            Weight = 1;
        }

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
        /// The weight of this reference (the strength of the bond it forms).
        /// </summary>
        /// <remarks>
        /// Analysis of the object graph allows different weights to be assigned to references originating
        /// from different object fields. When computing backtraces (and the dominator tree), only the
        /// references with the highest weight (for each target object) are retained, and other references
        /// are ignored. This allows for some objects to be considered "owners" of other objects, decluttering
        /// the object graph and resulting in deeper dominator trees for memory attribution.
        ///
        /// A regular reference (emanating from a field without an attribute) is considered to have weight 0.
        /// References of weight 1 or higher are considered "owning" references (overriding regular references).
        /// References of higher weight can be useful to prioritize owners if ownership of an object changes
        /// throughout its lifetime. If more than one reference of weight 1 or greater are found to a single
        /// object, this results in a warning about ambiguous object ownership.
        ///
        /// References of weight -1 or lower are considered "weak" references (overridden by regular references).
        ///
        /// If no weight is given for an OwnsAttribute, it defaults to 1.
        /// </remarks>
        public int Weight { get; set; }

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
