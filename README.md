# MemorySnapshotAnalyzer

MemorySnapshotanalyzer is an analyzer for memory snapshots. While designed to support multiple backends/file formats, the initial version only has support for Unity Memory Snapshots captured through the [Unity Memory Profiler window](https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.0/manual/index.html) or Unity's [`MemoryProfiler.TakeSnapshot`](https://docs.unity3d.com/ScriptReference/Profiling.Memory.Experimental.MemoryProfiler.TakeSnapshot.html) API.

## Building and Running

MemorySnapshotAnalyzer was developed in C# and tested with both Visual Studio 2022 and .NET 6.0 on Windows, as well as VSCode and .NET 7.0 on MacOS X.

Use the solution file `MemorySnapshotAnalyzer.sln` to build and run the console application. On MacOS/Linux, you can build and run from the shell using the `./run.sh` script.

## The Read-Eval Print Loop

The interface to the tool is organized around a "read-eval-print loop" that reads commands from the console. Type `help` to list the available commands.

### Interactive Command Syntax

The syntax for command lines is, admittedly, a bit idiosyncratic; see `CommandProcessing/CommandLineParser.cs` for the grammar (note that not all forms specified in the grammar are fully implemented yet). A quick guide:
* The first word is the command name, which can be available as abbreviations (e.g., `dumpsegment` or `ds`). See the help text for available comamnds and their abbreviations.
* Commands can be followed by multiple arguments. Arguments can be expressions that will be evaluated, e.g., `print 0x12345678 + 32`. This allows, e.g., to use addresses and combine them with offsets.
* String arguments need to be double-quoted, e.g., `dumpassemblies "MyAssembly"` lists all assemblies whose names contain the given substring.
* Flag arguments and named arguments are specified using names prefixed with an apostrophe. Examples:
  * `dumptype 'recursive` requests that types are listed with their base types (recursively)
  * `options 'rootobject 0x12345678` sets the `rootobject` option to the given value
  * `options 'nofuseobjectpairs` disables the `fuseobjectpairs` option

Note that some commands take indices of different kinds (as well as addresses). Make sure to not confuse these indices with one another:
* The type system assigns a type index to each type.
* Heap tracing assigns an index to each live object an to each group of roots with the same target, in postorder.
* Indices can be specific to the context (see below) and can change with different analysis options (e.g., heap stitching).
* (Only of interest to developers working on the analyzer itself: Internally, the root set also assigns a root index to each root, and backtracing assigns a node index to each node that can be part of a backtrace - or of the dominator tree.)

Some commands take a "type index or pattern" argument. This can be either:
* an integer representing a type index, or
* a string, representing a regular expression that the fully qualified name of each type is matched against. The string can also be an assembly name (with or without `.dll` extension, and matched case-insensitively) followed by a colon ('`:`') and a regular expression that the fully-qualified type names in that assembly will be matched against.

### Pagination

Output of commands is paginated to the console window height. Type `q` to get back to the command prompt or hit space for the next screenful of output. Commands can be interrupted using `Ctrl-C` at the time they output another line of text.

### Loading Snapshots into Contexts

MemorySnapshotAnalyzer allows for several snapshots to be loaded at the same time. For instance, this allows snapshots taken at different times during execution of the same application to be compared.

When the tool starts up, it runs in a context with ID `0` and no snapshot loaded. You can use `context` command with an integer ID to switch to another context (which will be created if it didn't exist).

Use the `load` command to load a snapshot; by default, this will be loaded into a new context unless the current context had no snapshot loaded. You can use `load` with the `'replace` option to force loading into the current context.

## The Analysis Stack

MemorySnapshotAnalyzer allows analysis of what's in a heap snapshot at different levels of abstraction, ranging from bytes within ranges of committed memory up to object graphs. The levels of analysis, and which of them have been computed within a given context, are listed by the `context` command. Note that analyses at these levels are computed implicitly by commands as needed, which will be increasingly more expensive.
* **Types:**
  * Given that we are processing memory snapshots from managed runtimes, some type information will be available for any heap snapshot. Use `stats 'types` to print high-level type system information (such as high-level properties of object representation in memory), and `dumptype` to dump loaded types and their layout. Use `dumpobj 'astype` to dump the contents of a given address interpreted as the given type.
* **Memory:**
  * Note that depending on how the memory snapshot was produced and the file format, the actual contents of heap memory may or may not be available in the snapshot. Some commands can be used to find out if heap memory is available, and to inspect it.
  * Use `listsegs` to list regions of committed memory ("memory segments") and `stats 'heap`, `dumpseg`, `describe`, or `dump` to print out the meaning or contents of memory addresses without specific interpretation as objects. This can be useful when investigating a memory corruption. Use `find` to find bit patterns in the managed heap. This can be useful to find garbage objects of specific types.
* **Root set:**
  * Use `dumproots` to get information about memory locations that are considered roots for the purpose of garbage collection.
  * Use `dumpinvalidreferences 'invalid` to get information about roots that do not seem to point to valid objects. This can be useful to find inconsistencies in the heap (though this can also just be a built-in or preallocated object).
  * Use `options 'rootobject 0x12345678` to ignore the root set and instead consider the given object address as the single root. This allows to get analysis results restricted to the graph of objects reachable from the specified object. The address does not need to be that of an object that is reachable using the snapshot's root set, which can be useful to look at "garbage" as if it was still live.
* **Traced heap:**
  * Use `listobjs` to dump the objects that are currently considered "live" for the purpose of the garbage collector. This causes the entire heap to be traced, starting from the root set and following all managed pointers within reachable objects. Use `listobjs 'type` to list objects of specific types on the heap, or `dumpobj` with a postorder index or address to dump the given object.
  * Use `dumpinvalidrefs` to dump references that are not valid (e.g., do not point into the managed heap or to a managed object). This can be useful to find inconsistencies in the heap.
* **Backtracer:**
  * Use `backtrace` to dump "backtraces" that indicate how a given object is reachable on the heap. This causes the predecessor set to be computed for each object in the object graph. This can be useful to determine why an object is still alive, e.g., when all references to it had been expected to be released and the object was expected to be reclaimed by the garbage collector.
  * The `backtrace` command has various options to try to summarize backtraces for complex object graphs, and also supports output to `dot` files for visualization using GraphViz.
* **Dominator tree:**
  * The `heapdom` command computes the dominator tree for the object graph. A node is "dominated" by another node if all reference paths from the root set to the target object go through the dominating object. This can be useful for multiple scenarios:
  * To answer the question, "what would need to happen such that this object could be released?", the dominator tree gives one kind of answer: If any ancestor node in the dominator tree became unreachable, the given object would become garbage as well.
  * To answer the question, "what is the memory usage by this object and the objects it owns?", it usually wouldn't adequate to say "the sum of the sizes of all objects that are reachable from this object," due to shared nodes in the directed graph (not every reference is an "owning" reference). However, it is valid to say that all descendants of a given node in the dominator tree are "owned" by that dominating node.
  * Unfortunately, if the application whose heap usage you are analyzing is structured in a way such that many objects are reachable from unrelated parts of the root set (e.g., from static variables of different classes), it can happen that the only dominating node is the top-level node of the dominator tree itself (the node representing the entire process heap). `heapdomstats` can be used to get a sense of how much this is happening for the particular snapshots you are looking at.
  * Diffing: `heapdom 'relativeto` can allow you to get an idea of which nodes in the current context's snapshot (the "after" snapshot) were not present in the snapshot loaded into another context (the "before" snapshot). This can be used to get an idea of the cost of non-transient allocations performed by an application action, by capturing a "before" and an "after" snapshot around said action in a manual test run. Note that the simple approach implemented today will only be meaningful with a non-relocating garbage collector (or if no compacting garbage collection was performed between the "before" and "after" snapshots).

### Selecting the Heap to Analyze

Some heap snapshots contain multiple heaps. You can select the heap(s) to analyze as follows:
* `options 'heap "managed"` selects the "managed" heap for analysis. (The exact meaning of "managed" is specific to the format of the loaded snapshot file.) A managed heap should be expected to be fully traceable due to the level of run-time type information contained in the heap.
* `options 'heap "native"` selects the "native" heap for analysis. Due to lower run-time type information available for native objects, it may or may not be possible to infer pointers to other native objects. Due to the lack of a root set, each object may be reported as its own root.
* `options 'heap "stitched"` selects a "stitched" heap, i.e., an integrated view across a "managed" and "native" heap. The managed heap will be considered the "primary" heap and the native heap will be considered the "secondary" heap. This means that the primary heap's root set will be used as the root set for the stitched heap, and native pointers found in reachable objects on the primary heap will be used to determine which objects on the secondary heap are reachable.

### Analysis Options

Some of these analysis are configurable. To set the options for analysis, use the `options` command. To see the currently configured options, use the `context` command.

* **Heap stitching option `'fuseobjectpairs`:** Each pair of a managed and a native object that reference one another is considered to be "inseparable". Usually, other native objects pointing to the native part and other managed pointers pointing to the managed half can create graphs in which the dominating nodes for both parts could be different. With this option, all pointers to either part are considered to point to the managed object (and the managed object is the only object that points to the native object), ensuring that the objects stay together in the dominator tree.
* **Root set option `'rootobject`:** Only analyze the graph of objects reachable from the given object. This can be used to answer the question, "what are the objects reachable from this object" or "what is the total size of objects reachable from this object." Also, when configured with a key object responsible for managing large parts of an application's state, this can be used to reduce the problem of too many nodes "floating" to the process node when computing the dominator tree.
* **Root set option: `'weakgchandles`:** Often, objects are reachable from a specific static variable in the root set as well as one or more GC handles. In this case, it can be helpful to consider only the static variable to be an "owning" reference to the object, and only report GC handles as "owning" if they are truly the only reason that a given object is live. This option allows you to do that.
* **Backtracer option `'groupstatics`:** Introduces additional, "virtual" nodes within backtraces to serve as containers for related objects/object graphs. E.g., it can be a common occurrence that objects "float" to the process node in the dominator tree that are reachable from different static variables only within a single assembly, namespace, or type, and this option allows to group such objects accordingly.
* **Backtracer option `'fuseroots`:** Normally, any objects that are the targets of any roots will be near the root of the dominator tree. With this option, roots whose targets are objects that are also reachable from other objects will be considered just a part of the target object, meaning that the corresponding objects can appear lower in the dominator tree.

## Reference Classifiers

In many scenarios, a given object can be referenced from multiple other objects (such as caches or parent pointers), and it cannot be inferred automatically which is the object that is the primary "owner". This can make for cluttered backtraces, and - worse - make many objects float all the way up to the top of the dominator tree. With this option, you can provide configuration information that marks certain object fields as "owning" references. If an object is found to be referenced by both owning and non-owning references, the non-owning references are discarded from the backtrace.

There are two options for specifying reference classifiers for a given codebase:
* As a text file with a custom format (see below).
* As custom attributes in-source (see doc comments in `ReferenceClassifierAttributes`).

The `referenceclassifier 'load` command takes the name of a configuration file with the following format:

* A hash ('`#`') symbol marks the rest of the line as a comment. Comments and blank lines are ignored.
* A name in square brackets ("`[groupname]`") introduces a *group*, to which all rules until the next group declaration belong. Reference classifiers can be enabled or disabled in groups.
* A *rule* starts with a *type specifier*, followed by a keyword that states the properties of a reference, followed by arguments specific to that keyword. Rules are terminates by a semicolon ('`;`').
* A *type specifier* is either an *assembly-qualified type name* or a regular expression (matched against all fully-qualified type names in all assemblies). A regular expression is given between slashes ('`/`'), e.g., `/Foo.*Node$/`.
* An *assembly-qualified type name* is given in double quotes, and consists of an assembly, a colon ('`:`'), and the fully-qualified type name. The assembly name can be given with or without a `.dll` extension, and is matched case-insensitively. The type name must match exactly the name that is output by, say, the `dumptype` command.
* A *field pattern* is either a field name, which needs to match exactly, or a prefix followed by an asterisk ('`*`').
* A *selector* is a *field pattern* optionally followed by a sequence of field names and/or "`[]`" (indicating array indexing). This provides support for collections; for instance, to indicate that all values in field `foo` of a generic dictionary type are owning references, an appropriate selector could be `"foo._entries[].value"`.

Specific rule keywords are:

* **`OWNS` or `OWNS(weight)`:** An "owns" rule takes a selector as an argument, enclosed in double quotes. An "owns" rule indicates that any object reached from instances of the given type via the selector has the given weight (defaulting to 1). If an object is referenced via multiple references with different weights, all but the references with the highest weight will be ignored when computing backtraces (and the dominator tree). A full example of an "owns" rule is: `"foo.dll:MyNamespace.MyType" OWNS "foo._entries[].value";`.
    * A weight of 1 or greater indicates that the target object is strongly-owned. Note that if multiple strongly-owning references of the same weight, from different objects, are found, an warning is issued for ambiguous ownership.
    * A weight of -1 or less indicates that the object is "weakly" referenced (an analysis concept - not to be confused with a .NET `WeakReference`).
    * Note that the weight of a reference to which no rule applies is considered to be 0 (no explicit "owns" rule may specify weight 0).
    * The option `weakgchandles` (see above) effectively assigns all references emanating from GC handles a weight of -2.
* **`OWNS_DYNAMIC` or `OWNS_DYNAMIC(weight)`:** A variant of "owns" that does not output a warning if a field needs to be looked up dynamically (that is, in the run-time type of an object rather than the declared type of the referencing field).
* **`EXTERNAL`:** An "external" rule takes a field pattern as argument. The field is considered to contain a native-word sized pointer, referencing an object on another heap. This is to be used in conjunction with the `stitched` heap option.
* **`TAG(tag,...)`:** A "tag" rule takes a selector as argument. The object reachable via the selector is assigned the given tags. Object tags are displayed when listing objects and in backtraces.
* **`TAG_DYNAMIC(tag,...)`:** A variant of "tag", as `OWNS_DYNAMIC` is to `OWNS`.
* **`TAG_IF_ZERO(tag,...)`, `TAG_IF_NONZERO(tag,...)`:** A "conditional tag" rule takes a field pattern as argument. If the condition is true for an object (its given field is zero/non-zero), the given tags are assigned to the object containing the field.

`referenceclassifier 'save` can be used to save a set of reference classifiers to a file.

`referenceclassifier 'fromdll` can be used to extract reference classifiers from all `*.dll` files directly within a given directory.

### Identifying the Need for New "Owns" Rules

To help identify where configuring more owning references could be useful, you can use the `heapdomstats` command. When run without arguments, this provides statistics on the most frequent types of objects that have floated all the way to the top of the dominator tree. Running `listobj 'dominatedby -1` (and, optionally, `'type` and a type index or pattern), this lists the specific object instances that have floated to the top. Then use `backtrace 'depth 1` on some of the given object indices to see whether one of the references in the backtrace clearly should be considered the "owning" reference, and add the referring field to the configuration file. To verify the effectiveness of the expanded configuration, reload the updated file by re-issuing `options 'referenceclassifier`, and rerun `heapdomstats` again.

### Enabling Specific Groups for Individual Contexts

All reference classifiers are loaded into a single global store, shared across all analysis contexts. The contents of the store can be examined with the `referenceclassifier 'list` command. When more reference classifiers are loaded using `referenceclassifier 'load`, they are added to the global store.

However, for individual analysis contexts, you can choose to enable a specific set of groups. E.g., `referenceclassifier 'disable 'group "*"` would disable all loaded reference classifier groups in the current context, to be able to see the raw object graph in backtraces. Continuing from the previous example, subsequently running `referenceclassifier 'enable 'group "thirdparty.*"` would enable just the rules for third-party frameworks.

### Default Third-Party Reference Classifier Configuration

`MemorySnapshotAnalyzer` ships with a `thirdparty.rcl` reference classifier configuration file that is automatically read (and enabled for the current context) on startup. It classifies some references in third-party frameworks. Specifically:
* **[third-party.dotnet] Group:** Specifies reference classifiers for some of the .NET framework classes, e.g., network service points and streams.
* **[third-party.mono] Group:** Specifies reference classifiers for Mono-specific internal framework types.
* **[third-party.unity] Group:** Specifies reference classifiers for some of the Unity classes, e.g., addressables or the input system.
* **[third-party.weakdelegates] Group:** Delegates can hold on to references (and, possibly, large object graphs) via their closures, and delegates are often used to connect otherwise independent subsystems for notification delivery. This means that references to other objects from delegate closures usually do not contribute to the "ownership" relationship between objects and are more incidental. With the `thirdparty.weakdelegates` group enabled, such references are treated as "weak" - if objects are reachable both from other object fields and from delegate closures, we will ignore the delegate closure reference. If an object is reachable *only* from the delegate closure, it is a likely leak candidate, which will be shown with a `..` prefix in backtraces.

### Tips for Chasing Down Memory Leaks Using Reference Classifiers and Lifelines

When an object on a managed heap is not considered, according to the design of the program, as one that - after this point in time in the program's execution - should no longer be accessed, but that is still reachable in the object graph. The "leaked" object will, in turn, possibly hold a possibly-substantial graph of objects live (the subtree under the object's node in the dominator tree).

Here are some tips that can be helpful for identifying leaked objects and fixing the cause for the leak:
1. If you suspect objects of a specific type to be leaked, construct a reference classifier configuration file that identifies the "owning references" through which instances of this type would usually be reached, before they are leaked. Then use `listobj 'unowned` to dump instances of this type that are still reachable on the heap, but only in other (unintended) ways than the owning reference.
2. Use `backtrace 'lifelines` to dump a compressed view of representative, somewhat short paths to either roots or strongly-owned nodes (according to the reference classifier in use).
3. Inspect the lifeline diagram to find references that should no longer exist (e.g., be nulled out, or removed from a collection).
4. Modify your program accordingly, rerun your scenario, and run the same analysis on a new memory snapshot. Confirm whether the object has become eligible for garbage collection, or inspect the updated lifeline diagram to find other references to break.
5. To identify multiple leaks with a single snapshot while removing the clutter of already-identified leaks, use the `ignorebackref` command, passing a child index and a parent index as arguments. Backtraces will be recomputed while ignoring the given parent-child reference.

## Visualizing the Heap Dominator Tree as a Treemap

This file can be used to visualize the dominator tree as a [treemap](https://en.wikipedia.org/wiki/Treemapping). In the case of a heap dominator tree, the sizes of nodes correspond to the sizes in bytes of objects or subtrees, and nesting corresponds to the dominator relationship - a node that is dominated by another will visually be nested within that other node's rectangle.

When the heap is structured relatively hierarchically, possibly with the help of "owns" reference classifier rules, this visualization nicely displays the relative size cost of different functional elements (components) of an application.

The output of the `heapdom "foo.html"` command is written to an HTML file `"foo.html"` that embeds JavaScript representing the dominator tree as a JSON data structure. To display the treemap, open this file in a Browser. (Note that nodes can have a subnode with the name `intrinsic` to indicate the node's own intrinsic size, which - for arrays - can be significantly larger than the sizes of contained objects.)

A word of warning: Chrome restricts the memory consumed by an individual browser tab to 4 GiB, which has occasionally not been sufficient in practice to render dominator trees consisting of more than ~1.75M nodes. Use the `heapdom` options to limit depth, width, or minimum object size of graphs larger than that, or `options 'rootobject`, to reduce the amount of data displayed. You will see nodes with the name `elided` appear that summarize the size of the objects or subtrees that have been elided from the output. Also note that Chrome may fail to load trees that are deeper than 128 nodes, which accordingly is the default depth limit for the `heapdom` command. In our experience, Firefox Developer Edition may be better at dealing with large (very detailed) treemaps.

Within the `treemap.html` file, you can also edit the value of the `colorGrouping` constant to select different rectangle fill color schemes, to visualize dominator trees by:
* **colorGrouping = 0**: Darker rectangles indicate deeper nesting levels.
* **colorGrouping = 1**: When used for a JavaScript file produced by the `heapdom 'nodetype` command, then different colors are used to easily distinguish, e.g., boxed objects or arrays from regular objects.

## License

MemorySnapshotAnalyzer is licensed under the [MIT License](LICENSE).
