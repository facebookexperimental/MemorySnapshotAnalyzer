# MemorySnapshotAnalyzer

MemorySnapshotanalyzer is an analyzer for memory snapshots. While designed to support multiple backends/file formats, the initial version only has support for Unity Memory Snapshots captured through the [Unity Memory Profiler window](https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.0/manual/index.html) or Unity's [`MemoryProfiler.TakeSnapshot`](https://docs.unity3d.com/ScriptReference/Profiling.Memory.Experimental.MemoryProfiler.TakeSnapshot.html) API.

## Building and Running

MemorySnapshotAnalyzer was developed in C# and only tested with Visual Studio 2022 and .NET 6.0 on Windows.

Use the solution file `MemorySnapshotAnalyzer.sln` to build and run the console application.

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
* Heap tracing assigns an object index to each live object.
* Indices can be specific to the context (see below) and can change with different analysis options (e.g., heap stitching).
* (Only of interest to developers working on the analyzer itself: Internally, the root set also assigns a root index to each root, and backtracing assigns a node index to each node that can be part of a backtrace - or of the dominator tree.)

### Pagination

Output of commands is paginated to the console window height. Type `q` to get back to the command prompt or hit space for the next screenful of output. Commands can be interrupted using `Ctrl-C` at the time they output another line of text.

### Loading Snapshots into Contexts

MemorySnapshotAnalyzer allows for several snapshots to be loaded at the same time. For instance, this allows snapshots taken at different times during execution of the same application to be compared.

When the tool starts up, it runs in a context with ID `0` and no snapshot loaded. You can use `context` command with an integer ID to switch to another context (which will be created if it didn't exist).

Use the `load` command to load a snapshot; by default, this will be loaded into a new context unless the current context had no snapshot loaded. You can use `load` with the `'replace` option to force loading into the current context.

## The Analysis Stack

MemorySnapshotAnalyzer allows analysis of what's in a heap snapshot at different levels of abstraction, ranging from bytes within ranges of committed memory up to object graphs. The levels of analysis, and which of them have been computed within a given context, are listed by the `context` command. Note that analyses at these levels are computed implicitly by commands as needed, which will be increasingly more expensive.
* **Types:**
  * Given that we are processing memory snapshots from managed runtimes, some type information will be available for any heap snapshot. Use `stats 'types` to print high-level type system information (such as high-level properties of object representation in memory), and `dumptype` to dump loaded types and their layout. Use `dumpobj 'memory 'astype` to dump the contents of a given address interpreted as the given type.
* **Memory:**
  * Note that depending on how the memory snapshot was produced and the file format, the actual contents of heap memory may or may not be available in the snapshot. Some commands can be used to find out if heap memory is available, and to inspect it.
  * Use `listsegs` to list regions of committed memory ("memory segments") and `stats 'heap`, `dumpseg`, `describe`, or `dump` to print out the meaning or contents of memory addresses without specific interpretation as objects. This can be useful when investigating a memory corruption. Use `find` to find bit patterns in the managed heap. This can be useful to find garbage objects of specific types.
* **Root set:**
  * Use `dumproots` to get information about memory locations that are considered roots for the purpose of garbage collection.
  * Use `dumproots 'invalid` to get information about roots that do not seem to point to valid objects. This can be useful to find inconsistencies in the heap (though this can also just be a built-in or preallocated object).
  * Use `options 'rootobject 0x12345678` to ignore the root set and instead consider the given object address as the single root. This allows to get analysis results restricted to the graph of objects reachable from the specified object. The address does not need to be that of an object that is reachable using the snapshot's root set, which can be useful to look at "garbage" as if it was still live.
* **Traced heap:**
  * Use `dumpobj 'list` to dump the objects that are currently considered "live" for the purpose of the garbage collector. This causes the entire heap to be traced, starting from the root set and following all managed pointers within reachable objects. Use `dumpobj 'list 'type` to list objects of specific types on the heap, or `dumpobj` with an object index or address to dump the given object.
  * Use `dumpinvalidrefs` to dump references that are no valid (e.g., do not point into the managed heap or to a managed object). This can be useful to find inconsistencies in the heap.
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

* **Root set option `'rootobject`:** Only analyze the graph of objects reachable from the given object. This can be used to answer the question, "what are the objects reachable from this object" or "what is the total size of objects reachable from this object." Also, when configured with a key object responsible for managing large parts of an application's state, this can be used to reduce the problem of too many nodes "floating" to the process node when computing the dominator tree.
* **Backtracer option `'groupstatics`:** Introduces additional, "virtual" nodes within backtraces to serve as containers for related objects/object graphs. E.g., it can be a common occurrence that objects "float" to the process node in the dominator tree that are reachable from different static variables only within a single assembly, namespace, or type, and this option allows to group such objects accordingly.
* **Backtracer option `'fuseobjectpairs`:** Each pair of a managed and a native object that reference one another is considered to be "inseparable". Usually, other native objects pointing to the native part and other managed pointers pointing to the managed half can create graphs in which the dominating nodes for both parts could be different. With this option, all pointers to either part are considered to point to the managed object (and the managed object is the only object that points to the native object), ensuring that the objects stay together in the dominator tree.
* **Backtracer option `'fusegchandles`:** Usually, GC handles are roots, meaning that their target objects will be near the root of the dominator tree. With this option, GC handles whose targets are objects that are also reachable from other objects will be considered just a part of the target object and can appear lower in the dominator tree.
* **Dominator tree option: `'weakgchandles`:** Often, objects are reachable from a specific static variable in the root set as well as one or more GC handles. In this case, it can be helpful to consider only the static variable to be an "owning" reference to the object, and only report GC handles as "owning" if they are truly the only reason that a given object is live. This option allows you to do that.

## Visualization

The output of the `heapdom` command is written to a JavaScript file representing the dominator tree as a JSON data structure.

It can be helpful to visualize the dominator tree as a [treemap](https://en.wikipedia.org/wiki/Treemapping). In our case, the sizes of nodes correspond to the sizes in bytes of objects or subtrees, and nesting corresponds to the dominator relationship - a node that is dominated by another will visually be nested within that other node's rectangle.

To display the treemap, edit the `<script src="data.js">` at the top of `TreemapViewer/treemap.html` to point to the file written by your `heapdom` command, and load the `treemap.html` file in a Browser tab. (Note that nodes can have a subnode with the name `intrinsic` to indicate the node's own intrinsic size, which - for arrays - can be significantly larger than the sizes of contained objects.)

A word of warning: Chrome restricts the memory consumed by an individual browser tab to 4 GiB, which typically allows to process dominator trees consisting of up to ~1.5M nodes. Use the `heapdom` options to limit depth, width, or minimum object size, or `options 'rootobject`, to reduce the granularity of larger graphs than that. You will see nodes with the name `elided` appear that summarize the size of the objects or subtrees that have been elided from the output.

Within the `treemap.html` file, you can also edit the value of the `colorGrouping` constant to select different rectangle fill color schemes, to visualize dominator trees by:
* **colorGrouping = 0**: Darker rectangles indicate deeper nesting levels.
* **colorGrouping = 1**: Different colors are used to make it easy to distinguish, e.g., boxed objects or arrays from regular objects.
* **colorGrouping = 2**: When comparing an "after" heap snapshot against a "before" heap snapshot, highlight the nodes that are only present in the "after" snapshot.

## License

MemorySnapshotAnalyzer is licensed under the [MIT License](LICENSE).
