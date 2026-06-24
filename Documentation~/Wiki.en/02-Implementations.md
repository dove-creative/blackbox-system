# Table of Contents

1. Blackbox Structure
2. Detailed Implementation Documents
3. Core Components
4. Recording Pipeline
5. Thread-Based Recording Safety
6. Structural Intent
7. Implementation Considerations

## 1. Blackbox Structure

Blackbox is a framework that places an internal engine for recording one object's execution context at the center, and stores object-to-object interactions and scope flow inside the same log system.

The implementation of this framework is broadly divided into ten elements.

- `Blackbox`: The central object that applies the actual recording rules and manages per-object log flow
- `BlackboxRuntime`: Runtime state that issues global ids, interaction ids, and sequences
- `LogContext`: Execution context responsible for thread-local log buffers, open scope lists, and interaction-merge search
- `LogData`: The information unit for one log line
- `LogFormatter`: A formatting tool that converts `LogData` into strings readable in text/HTML output
- `BlackboxRegistry`: A global lookup structure that connects objects to `Blackbox`
- `BlackboxHandle`: The entry point used externally to call recording features
- `ScopeHandle`: An `IDisposable` handle that closes an open scope
- `ExertHandle`: An `IDisposable` handle that tries to merge the receiving-side scope after an object-to-object interaction
- `HandleManager<T>`: An id lifecycle manager that prevents duplicate `Dispose()` calls for value-type handles

The core of this structure is separating the recording center, execution context, output formatting, external usage interface, and `Dispose()` post-processing units. Actual recording rules gather in `Blackbox`; log buffers and scope state that differ per thread are separated into `LogContext`; output string assembly is handled by `LogFormatter`. External code uses this structure indirectly through `BlackboxHandle`. `ScopeHandle` and `ExertHandle` are not one action-based handle as before; each has its own post-processing meaning to close.

## 2. Detailed Implementation Documents

This document explains the overall structure and representative flows of Blackbox. Detailed implementations that require reading several objects together are covered in separate documents below.

- `02.1-Tag-Flow.md`: Source logs, target logs, display policies, and placeholder processing connected through `.With(...)`
- `02.2-Export-Pipeline.md`: Export graph collection, scope display conversion, and text/HTML output flow
- `02.3-Handle-Lifecycle.md`: Lifecycle of `ScopeHandle`, `ExertHandle`, and `HandleManager<T>`, and duplicate `Dispose()` prevention

## 3. Core Components

### 3-1. Blackbox

`Blackbox` is the recording engine corresponding to one object. It coordinates the recording subject, scope rules, interaction rules, and overall output order.

The important point in this implementation is that `Blackbox` is not a simple string collector. `Blackbox` carries the following responsibilities together.

- Maintain the recording subject
- Forward recording requests to the current thread's `LogContext`
- Issue scope ids and record open logs
- Reflect object-to-object call relationships on both sides
- Reflect tag-target logs as auxiliary records on the peer object side
- Reorder logs from multiple threads by global sequence

Held information:

- `Owner` / `OwnerString` / `Id`: The recorded target object, output name, and internal Blackbox identifier
- `_strongOwner` / `_weakOwner` / `_ownerDescription`: Owner retention method and fallback description string for reference loss
- `_logContext`: The `LogContext` corresponding to the current thread
- `_scopeId`: A number for connecting scope open/close pairs inside this owner

Provided services and reasons:

- `Write(...)`: Records progress inside one object and returns a value that can later attach tag targets when needed
- `Scope(...)`: Issues a scope id, writes an open log, and returns `ScopeHandle`
- `Tag(...)`: Stores a tag log on the target side connected by `.With(...)` from a source log
- `ExertMessage(...)`: Stores an object-to-object interaction as one log line on both sides
- `Exert(...)`: Records an object-to-object interaction and returns `ExertHandle`, which tries to merge on `Dispose()`
- `GetLogsByContext(...)` / `GetLogs(...)`: Gathers logs from multiple thread contexts and sorts them by global sequence

In short, `Blackbox` is less a log storage itself and more the central engine that coordinates per-object recording rules and overall output order.

### 3-2. BlackboxRuntime

`BlackboxRuntime` is the global number-issuing point shared by `Blackbox` instances.

Object ids, interaction ids, and log sequences are not local state of one owner. Since several `Blackbox` instances record together in the same execution, these values are atomically increased by `BlackboxRuntime`. This lets logs collected later from several owners and several thread contexts be sorted again into one order.

Held information:

- `CurrentSequence`: The last sequence issued so far
- Internal blackbox id / interaction id / sequence counters: Global counters that separately manage object, interaction, and log order

Provided services and reasons:

- `GetNextBlackboxId()`: Issues a `Blackbox` identifier per owner
- `GetNextInteractionId()`: Issues a number that connects both sides of an object-to-object interaction
- `GetNextSequence()`: Issues a number for sorting overall log occurrence order
- `Reset()`: Resets runtime state during `ForceReset()`

### 3-3. LogContext

`LogContext` is an internal structure that separates per-thread log buffers and scope state inside one `Blackbox`.

Each `LogContext` has a ring-buffer log buffer, thread metadata, and a list of currently open scopes. `Blackbox` obtains the context corresponding to the current thread through `ThreadLocal<LogContext>`, and `LogData` creation and storage are performed inside that context.

The current implementation handles scopes in two layers. At recording time, `LogContext` uses the `_openScopes` list to manage the lifecycle of open scopes, and automatically closes later scopes from the same thread when close order is out of order. At output time, `ResolveScopeDepths` reads the open/close logs again and calculates display depth. Therefore runtime scope state and output display depth are separate responsibilities.

Interaction merging is also performed against the ring buffer inside this context. `TryMergeScope(...)` looks for an open scope immediately after an interaction log with the same owner and `InteractionId`; if the condition matches, it marks the interaction log invalid and merges that information into the open scope.

Tag connection starts in the context where the source log was stored. When `.With(...)` follows a source log created by `Write(...)` or `Scope(...)`, `LogContext` fixes the source log's target list and leaves separate tag logs on the necessary target `Blackbox` instances. Source log message placeholder processing and target reference string assembly are handled by `LogFormatter` at output time. The detail level of logs left on targets is controlled by the `TargetTypes` policy.

Held information:

- `_logBuffer` / `_currentLogIndex`: Fixed-size ring buffer and latest recording position
- `_threadId` / `_threadName`: Output metadata for the thread that created this context
- `_openScopes`: Id, method name, and alive state of scopes that have not yet been closed

Provided services and reasons:

- `OpenScope(...)`: Registers a scope in the open-scope list and stores an open log
- `CloseScope(...)`: Stores a scope close log, fixes same-thread out-of-order close, and warns on cross-thread close
- `EnqueueLog(...)`: Fixes the passed scope id, scope type, and interaction information into `LogData` and stores it in the ring buffer
- `ResolveWith(...)`: Fixes the source log's tag-target list and creates target logs
- `RenderMessage(...)`: Returns a message with tag placeholders reflected through `LogFormatter`
- `TryMergeScope(...)`: Determines whether the immediately preceding interaction and following open scope can be merged into one output flow
- `GetLogs(...)`: Passes valid logs from the ring buffer

This separation is needed because the same owner object can be recorded from multiple threads. Even if different threads record the same owner object, each thread's ring buffer and thread metadata remain independent, and output later merges them again by global sequence.

### 3-4. LogData

`LogData` is the minimum recording unit stored by `Blackbox`. One item contains validity, message, time, method name, scope id, scope type, interaction peer, and interaction id together.

The current implementation also stores global `Sequence`, `ThreadId`, and `ThreadName`. These are not merely additional output information; they are the basis for reading logs collected from multiple threads back in order later, and for calculating scope depth at output time.

Also, because `ScopeType` allows normal logs and scope logs to be handled inside the same data type, the recording structure can preserve flow without being split into several separate types. The tag feature additionally stores `Tags`, `TaggedBy`, and `TagTargetTypes` inside the same `LogData`, so source logs and target logs share output connection information inside the same recording unit.

Held information:

- `Owner` / `IsValid` / `Message` / `Time` / `MethodName`: Basic information indicating whether one log line is valid, which object it came from, when it occurred, and with what meaning
- `ScopeId` / `ScopeType` / `ScopeDepth`: Values for connecting open/close logs and for output display depth
- `ExertedBy` / `ExertingTo` / `InteractionId`: Object-to-object interaction direction and connection id
- `Tags` / `TaggedBy` / `TagTargetTypes`: Related target list for the source log and display policy for target logs
- `Sequence` / `ThreadId` / `ThreadName`: Metadata for reading logs gathered from multiple contexts again by order and thread

Provided services and reasons:

- Constructor: Fixes recording values at log creation time into one data value
- `internal set` fields: Adjust `Message`, `InteractionId`, interaction direction, tag information, validity, and scope display information during merge, tag resolution, and output conversion
- `ToString(scopeDepth)`: Receives depth calculated in the output step and provides a one-line text log format through `LogFormatter`

### 3-5. LogFormatter

`LogFormatter` is a formatting tool that turns stored `LogData` into strings that humans can read.

At recording time, `LogData` stores information as structured values as much as possible. Actual text output, HTML output, tag placeholder interpretation, and scope method-label assembly are handled by `LogFormatter`, which is closer to output time. Thanks to this separation, `LogData` can focus on storing recorded values, and changing output format does not shake the whole recording path.

Provided services and reasons:

- `RenderMessage(...)`: Creates messages reflecting tag placeholders and target display policies
- `RenderTextLine(...)`: Creates one-line strings used by txt export and `LogData.ToString(...)`
- `RenderMethodLabel(...)`: Creates method labels in open/close/step form depending on scope type
- `Arrow(...)` / `TagRef(...)`: Displays interaction ids and target references as consistent strings

### 3-6. BlackboxRegistry

`BlackboxRegistry` is the global lookup point that connects objects to `Blackbox`.

Even if logs are written several times for the same object, the same `Blackbox` must always be found so logs continue in one context. The registry guarantees this connection. Thanks to this structure, external code can start recording simply by calling `BlackboxHandle.Of(subject)` without a separate registration step. Internally, it reuses the corresponding `Blackbox` based on the same object identity.

Held information:

- `_subjects`: A `ConditionalWeakTable` that stores the connection between target object identity and `Blackbox`
- `_lock`: A synchronization object for safely protecting debug/test-oriented count and reset operations

Provided services and reasons:

- `GetBlackbox(...)`: Looks up or creates the `Blackbox` corresponding to a target object
- `Contains(...)` / `Count()`: Test and debug observation of registry state
- `ForceReset()`: Resets registry, runtime, and handle id state for tests or repeated experiments

### 3-7. BlackboxHandle

`BlackboxHandle` is the entry point that external code actually meets. From an implementer's perspective, however, it is better understood as a thin forwarding layer around the internal structures above rather than as a new recording structure.

This type has the following roles.

- Look up the `Blackbox` corresponding to the target object when needed.
- Forward normal recording, scope recording, and exert recording calls to the internal `Blackbox`.
- Provide a connection point for attaching related targets later to normal logs and scope-open logs.
- Provide external convenience features such as conditional recording, construction recording, error recording, and output.
- Keep external usage code in a simple form.

Held information:

- `_subject`: The source object used by a handle that has not directly captured a `Blackbox` yet to find the target `Blackbox` from the registry
- `_blackbox`: A cache for reusing an already confirmed `Blackbox` without repeated lookup, as after `Construct(..., out handle)`
- `Owner` / `OwnerString` / `Id` / `IsValid`: Forwarding properties that expose internal `Blackbox` state for external users to check
- `LogDirectory` / logger / output option / `MaxLogCount` / `StrongReference` / `TagTargetTypes`: Static properties for controlling `Infrastructure` settings from the external API

Provided services and reasons:

- `Configure(...)` / `ForceReset()` / `Of(...)`: Prepare the recording environment and acquire the target object handle
- `Construct(...)` / `Dispose(...)` / `When(...)`: Express construction/disposal recording and conditional recording entry patterns
- `Write(...)` / `Scope(...)`: Normal recording and scope recording for the current object
- `ExertMessage(...)` / `Exert(...)`: Explicitly state object-to-object interaction direction from the caller's perspective
- `Scope(...).With(...)`: Connect related targets to a scope-open log
- `WriteError(...)` / `CrashExport(...)` / `Export(...)`: Record errors, connect related targets, and perform final output

Therefore, the value of `BlackboxHandle` is not that it has many features, but that it separates external usage style from the internal recording structure.

### 3-8. ScopeHandle

`ScopeHandle` is an `IDisposable` handle that closes an open scope.

`BlackboxHandle.Scope(...)` writes an open log and returns `ScopeHandle`. The handle stores the `LogContext` that opened the scope and the scope id, and forwards a close request to `LogContext.CloseScope(...)` at `Dispose()` time. Also, because a scope-open log can have related targets like a normal log, `ScopeHandle.With(...)` attaches target information to the open log and returns the same scope handle.

Held information:

- `_id`: Copy-safe handle id issued by `HandleManager<ScopeHandle>`
- `_createdThreadId`: The thread id where the handle was created
- `_context`: The `LogContext` to receive the close request
- `_scopeId`: The scope id connecting open/close logs
- `_tagHandle`: Auxiliary value for attaching `.With(...)` targets to the scope-open log

Provided services and reasons:

- `Dispose()`: Requests closing of the corresponding scope when the `using` block ends
- `With(...)`: Attaches related targets to the scope-open log and keeps the existing scope lifecycle
- `IsAlive` / `IsDisposed` / `ScopeId`: Observe handle lifecycle and scope id
- Thread mismatch warning: Warns when the handle is disposed from a thread different from the one where it was created
- Ignore default/disposed handles: Simplifies call sites in conditional recording or duplicate dispose cases

### 3-9. ExertHandle

`ExertHandle` is an `IDisposable` handle that tries to merge a receiving-side scope after an object-to-object interaction.

`Blackbox.ExertMessage(...)` leaves only logs with the same `InteractionId` on the sending and receiving sides. `Blackbox.Exert(...)` first leaves the same interaction logs and returns `ExertHandle`; at `Dispose()` time, it searches the receiving-side `LogContext` for an open scope immediately after the same interaction and tries to merge them.

If the returned `ExertHandle` is not closed, the result log remains as a one-line interaction record. However, in that case the `ExertHandle` lifecycle is not completed, so simple connection-point flows should use `ExertMessage(...)`.

Held information:

- `_id`: Copy-safe handle id issued by `HandleManager<ExertHandle>`
- `_createdThreadId`: The thread id where the handle was created
- `_targetContext`: The `LogContext` where the receiving-side interaction log was stored
- `_interactionId`: The interaction id connecting both logs

Provided services and reasons:

- `Dispose()`: Merges interaction information into the receiving side's immediately following open scope
- `IsAlive` / `IsDisposed`: Observe handle lifecycle
- Target-context merge: Attempts merging only inside the target context fixed at creation time

### 3-10. HandleManager

`HandleManager<T>` manages the id lifecycle of `ScopeHandle` and `ExertHandle`.

If a handle is a `public readonly struct`, the value can be copied. If every copy repeats dispose post-processing, close logs or merge processing can be duplicated. `HandleManager<T>` centrally manages issued ids as alive/disposed state so that only the first dispose succeeds even when the same handle value is copied, and later disposes are ignored.

Provided services and reasons:

- `GetHandleId()`: Issues a new handle id
- `TryDisposeHandleId(...)`: Changes only a living id to disposed state once
- `IsAlive(...)`: Provides the base state for handle observation properties
- `WarnIfThreadMismatch(...)`: Outputs a warning when creation thread and dispose thread differ
- `Reset()`: Resets handle id state at a safe point for tests or repeated experiments during `BlackboxRegistry.ForceReset()`

## 4. Recording Pipeline

The recording pipeline shows how the objects described above connect in actual call order. Basic recording leaves records inside one object; scopes and exert build on that by adding execution ranges and object-to-object connections.

### 4-1. Normal Recording Flow

The most basic flow is when one object leaves its own state or progress.

1. External code obtains a recording entry point with `BlackboxHandle.Of(subject)`.
2. When a `Write`-family call enters, the handle looks up the `Blackbox` corresponding to the target object.
3. `Blackbox` obtains the current thread's `LogContext`.
4. `Blackbox` forwards recording materials such as message, method name, and sequence to the current context.
5. `LogContext` turns the passed recording values into `LogData` and stores them in its ring buffer.

The important point in this flow is that external code does not handle log storage directly. The outside only forwards recording intent, while the internal structure owns the storage form and context composition.

If `.With(...)` is attached right after recording, the target list is fixed on the same source log. In this case, `LogContext.ResolveWith(...)` fills the source log's `Tags`, and if the target object is a different `Blackbox` from the source, a tag log is also added to the target side. This tag log is not a record that opens a call range like `Exert(...)`; it is an auxiliary connection for reading the source log and related targets together.

### 4-2. Scope Recording Flow

Scope recording is a flow that leaves the start and end of one step as a block.

1. When an API that opens a scope, such as `Scope` or `Construct`, is called, `Blackbox` obtains the current thread's `LogContext`.
2. `Blackbox` issues a new scope id and stores an open log through `LogContext.OpenScope(...)`.
3. `Blackbox` returns `ScopeHandle` containing that context and scope id.
4. Later, when the `using` block ends, `ScopeHandle.Dispose()` is called.
5. `ScopeHandle` forwards a close request to `LogContext.CloseScope(...)`.
6. `LogContext` stores a close log with the same scope id in the same context.

The key point of this pipeline is that 'the caller that opened the scope' and 'the point where the scope closes' are separated, while still being reconnected by the same `LogContext` and scope id. This separation allows the recording structure to follow the block structure of code.

If a parent scope closes before a child scope, `LogContext.CloseScope(...)` checks the later open scopes. If this happens on the same thread, the later scopes are automatically closed first and a warning is left. If it happens from another thread, a warning is left, but open scopes from the other thread are not automatically closed arbitrarily.

Scope-depth calculation, and the conversion that makes open/close pairs with no child records look like step logs, are performed at output time rather than storage time. The output-side `ResolveScopeDepths` and `FlattenSteps` preserve the source logs and create an output list.

### 4-3. Exert Recording Flow

Exert recording is a structure that records the flow where one object interacts with another object on both sides at the same time.

1. When one object hands work to another object, the handle finds the `Blackbox` instances for both objects.
2. `Blackbox` records logs on both sides sharing the same `InteractionId`.
3. The sending-side log stores `ExertingTo`, and the receiving-side log stores `ExertedBy`.
4. `ExertMessage(...)` ends here, and the two logs remain as one-line interaction records.
5. `Exert(...)` leaves the same records and returns `ExertHandle`.
6. When the returned `ExertHandle` is closed, it searches the target context fixed at creation time for an open scope immediately after an interaction log with the same `InteractionId`.
7. If the condition matches, the interaction log is marked invalid and the following open scope is merged with interaction information.

The advantage of this structure is that reading only one object's logs is enough to know that an interaction occurred, while still being able to naturally follow the corresponding log of another object. Also, because the merge point is tied to `ExertHandle.Dispose()`, a scope opened after the handle closes is not later merged into the same call range.

Here, target context means the context where the receiving-side interaction log was actually stored. `TryMergeScope(...)` merges only when the two logs are immediately adjacent inside this context. Therefore, if an interaction and the receiving-side processing scope are recorded in different thread contexts, as with `Task.Run`, the policy is not to merge them.

### 4-4. Output Formatting Flow

Stored logs are converted into readable strings again at output time.

1. `BlackboxHandle.Export(...)` follows the root `Blackbox` logs and Exert connections to collect peer logs to include. Focused output follows the `ExertedBy` direction, and full output also follows the `ExertingTo` direction.
2. Output tools organize scope display depth and empty-scope display style with `ResolveScopeDepths` and `FlattenSteps`.
3. `TxtExporter` converts each `LogData` into a one-line string through `LogFormatter.RenderTextLine(...)`.
4. `HtmlExporter` uses `LogFormatter.RenderMethodLabel(...)`, `RenderMessage(...)`, and `Arrow(...)` to display the same recorded values in an HTML structure.
5. Messages with tag placeholders are replaced by actual target reference strings in `LogFormatter`.

In this flow, the source log's structural values and output strings are separated. Therefore, even if tag display policy or HTML/text representation changes, the responsibility of the recording creation step remains unchanged.

## 5. Thread-Based Recording Safety

Blackbox partly reflects the assumption that one owner object can be recorded from multiple threads. The important issue here is keeping the storage location of each log line and the output order from being mixed across threads.

The current implementation handles thread-based safety as follows.

- `Blackbox` owns `ThreadLocal<LogContext>`.
- Each thread has a separate ring buffer, open scope list, and thread metadata inside its own `LogContext`.
- Scope open/close logs are stored in the context held by the handle.
- `ScopeHandle` and `ExertHandle` leave warnings if creation thread and dispose thread differ.
- `Exert(...)` merging is attempted only inside the target context where the receiving-side interaction log was stored. If the interaction and receiving-side scope are recorded in different thread contexts, the two logs are not merged.
- `.With(...)` tag resolution starts in the context where the source log was stored, and target logs are stored in the current context of the target `Blackbox`.
- Scope ids, interaction ids, and sequences are issued atomically by `Blackbox` / `BlackboxRuntime`.
- `LogData` stores `ThreadId` and `ThreadName`, so output results can show which thread recorded each log.
- Every log receives a global `Sequence`, which allows logs gathered from multiple contexts to be aligned again by overall occurrence order during output.

This structure focuses on separating thread-local log buffers from global output order. For example, even if thread A opens a scope and thread B logs the same owner object, thread B's record is stored in B's context and merged again by sequence at output time.

Conversely, directly modifying one `LogContext` from multiple threads at the same time is not considered a stable default usage flow. This is why a warning is left when a handle is disposed on a thread different from the thread where it was created. Global states such as registry reset, setting changes, and output-completed state must also be handled at safe points. In particular, `ForceReset()` is closer to debug or test initialization, and it does not assume that other threads are recording at the same time.

## 6. Structural Intent

The Blackbox implementation has six broad intentions.

### 6-1. Gather Recording Rules in One Center

If actual recording rules are gathered in `Blackbox`, recording style can be maintained in one place even when there are many external call sites. This prevents scope rules, interaction rules, and storage rules from being redundantly implemented in different places.

### 6-2. Separate Thread-Based Execution Contexts

Recording rules are coordinated by `Blackbox`, but thread-based log buffers and open scope lists are separated into `LogContext`. Thanks to this, even if the same owner is recorded from multiple threads, each thread's recording position and thread metadata remain independent.

### 6-3. Keep External Usage Code Simple

External code uses recording in a form such as `BlackboxHandle.Of(this).Write(...)`. Because the internal structure is not exposed directly, callers can focus on 'what context to leave' rather than 'where and how it is stored'.

### 6-4. Split `Dispose()` Post-Processing by Purpose into Handles

Scope close and exert merge both happen at `using` / `Dispose()` time, but their meanings differ. The current implementation does not combine them into one action handle; it separates them into `ScopeHandle` and `ExertHandle`. This makes the information each handle must store and the dispose policy clearer, and `HandleManager` can prevent duplicate dispose caused by struct copies.

### 6-5. Make Logs Easy to Read Later

Blackbox stores scope id, scope type, interaction peer, interaction id, tag target, sequence, and thread information from recording time. Scope depth is calculated at output time. This choice is not only about leaving records; it makes the flow easier to read again during output or later analysis.

In other words, this framework puts more weight on making written logs readable with structure than on simply writing many logs.

### 6-6. Separate Output Format from Recording Data

By keeping `LogFormatter` as a separate tool, `LogData` does not have to own long string rules directly. Recording data keeps structural values such as owner, scope, interaction, and tag, while labels, arrows, and tag reference strings required for text/HTML output are assembled in the formatting step. This separation reduces unnecessary changes to the recording storage path when output representation is adjusted.

## 7. Implementation Considerations

### 7-1. When Runtime Recording Is Off

`BlackboxHandle.Of(subject)` and the main recording methods decide whether to actually record based on the `UseBlackbox` setting. If this value is off, the recording handle may not be valid, or some recording calls may be conditionally skipped. Therefore, when using Blackbox as an actual tracing tool, first check that `UseBlackbox` is enabled during startup configuration.

In Unity, if the `BLACKBOX` symbol is missing, the default state starts with recording off. This symbol is only a default-value signal that decides whether a Unity project starts with recording enabled; the same runtime API remains available either way. In native C# and non-Unity builds, the default recording state is enabled without a separate symbol, and it can be turned off when needed with `BlackboxHandle.UseBlackbox = false` or `Configure(..., useBlackbox: UseBlackboxOption.DoNotUse)`.

### 7-2. Stop Recording After Output

Once output runs, the internal global state changes to output-completed state. After that, additional recording and duplicate output are intentionally restricted. If multiple experiments must be repeated in the same process, reset registry and runtime state with `BlackboxHandle.ForceReset()` at a safe point.

### 7-3. Timing of Global Setting Changes

Values such as `LogDirectory`, logger, output option, `MaxLogCount`, `StrongReference`, and `TagTargetTypes` affect how the recording structure behaves. In particular, `MaxLogCount` is used as the buffer size when a new `Blackbox` creates a `LogContext`, so it is safer to configure it before recording starts rather than changing it arbitrarily during execution.

### 7-4. Safe Point for Handle Reset

`BlackboxHandle.ForceReset()` resets not only registry and runtime state, but also the `HandleManager` state for `ScopeHandle` / `ExertHandle`. This reset must be called only at a safe point where previously issued handles are no longer used. After reset, the dispose or alive lookup of old handles is not meaningfully preserved.

### 7-5. Tag Target Display Policy

Targets connected by `.With(...)` can be displayed directly inside the source log message through placeholders such as `%0` and `%1`. Targets not used in the message are appended as extra tag information during output. Tag logs left on the target side decide, according to `TargetTypes`, how much of the source name, interaction id, and source message to show.

A no-target situation can also be expressed directly with `.With(null)`. `.With()` means there are no targets to connect, while `.With(null)` means one null target is left in a message placeholder or extra tag information.
