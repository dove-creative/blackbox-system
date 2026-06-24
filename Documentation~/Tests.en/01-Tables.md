# Blackbox Test State-Behavior Tables

This document organizes object-level states and behaviors before writing new tests for the Blackbox framework.

Blackbox is a framework where recorded target objects, log contexts, disposable handles, and export tools work together. Therefore, the test tables are not written as one large feature group. They start from low-level value/handle objects and move upward toward the external entry point `BlackboxHandle` and the export flow.

---

## Notation

- **StateName**: State changes after the behavior.
- `[Behavior]`: Performs behavior unique to that state.
- `<Behavior>`: Internally continues into another behavior.
- `<`: A sub-item that belongs to the same feature group as the behavior on the left.
- `^`: Follows the behavior or result of the cell above.
- _Exception_: Throws that exception.
- `-`: Normally ignores the combination without state change, or keeps the existing value.
- `X`: A meaningless combination in that state or condition.
- **UseBlackboxOff**: The runtime axis where `UseBlackbox` is off. This axis verifies that the public API returns original messages or default handles and does not create actual records.

---

## 1. Value and Formatting Units

### 1-1. `LogData`

`LogData` is the structured value of one log line. Tests verify that constructor and output conversion paths preserve the stored values.

| LogData | - | Create | ToString | MutateForExport |
| --- | --- | --- | --- | --- |
|  | - |  |  |  |
|  | - |  |  |  |
| Value | - | **Value** | `[RenderTextLine]` | `[update ScopeDepth/IsValid/Message]` |
| Tagged | - | **Tagged** | `[RenderTaggedMessage]` | `[update Tags]` |
| Interaction | - | **Interaction** | `[RenderInteraction]` | `[update Interaction merge info]` |

### 1-2. `LogFormatter`

`LogFormatter` is a static tool that converts `LogData` into output strings rather than owning state.

| LogFormatter | - | RenderMessage | RenderTextLine | RenderMethodLabel | Arrow | TagRef |
| --- | --- | --- | --- | --- | --- | --- |
|  | - |  |  |  |  |  |
|  | - |  |  |  |  |  |
| Ready | - | `[resolve tag placeholders]` | `[display thread/sequence/scope/interaction]` | `[label by scope type]` | `[direction display]` | `[target display]` |

### 1-3. `BlackboxHandle.WriteHandler`

`WriteHandler` is a public API helper value that assembles strings only when the handle is valid in interpolated string calls. Tests verify not the string assembly itself, but whether formatted value evaluation and log assembly are skipped for invalid handles.

| WriteHandler | - | Construct | AppendLiteral | AppendFormatted | GetTextAndClear |
| --- | --- | --- | --- | --- | --- |
|  | - | ValidHandle | Literal | Value |  |
|  | - | InvalidHandle |  | NullValue |  |
| Active | - | **Active** | `[store literal]` | `[stringify value]` | `[return message + clear]` |
| Skipped | - | **Skipped** | - | - | `[return empty string]` |
| Active | - | ^ | ^ | `[stringify null]` | ^ |

---

## 2. Handle Units

### 2-1. `TagHandle`

`TagHandle` is the one-shot connection point that attaches related targets after `Write(...)` or `Scope(...)`.

| TagHandle | - | With | < | < | < | ToString |
| --- | --- | --- | --- | --- | --- | --- |
|  | - | ValidTarget | NullTarget | NullArray | TargetTypesLast |  |
|  | - |  |  |  |  |  |
| Default | - | - | - | - | - | `[fallback empty string]` |
| FallbackMessage | - | - | - | - | - | `[fallback message]` |
| SourceLog | - | `[fix tag]` | `[null tag]` | `[null tag]` | `[apply display policy]` | `[resolved message]` |

### 2-2. `ScopeHandle`

`ScopeHandle` is a disposable handle that closes an opened scope.

| ScopeHandle | - | With | Dispose | < | < |
| --- | --- | --- | --- | --- | --- |
|  | - | ValidTarget | SameThread | DifferentThread | Printed |
|  | - |  |  |  |  |
| Default | - | - | - | - | - |
| Alive | - | `[attach tag to open log]` | **Disposed** | **Disposed** + warning | **Disposed** |
| Disposed | - | - | - | - | - |

### 2-3. `ExertHandle`

`ExertHandle` is a disposable handle that tries to merge a receiving-side scope after an interaction log.

| ExertHandle | - | Dispose | < | < | < |
| --- | --- | --- | --- | --- | --- |
|  | - | MergeTargetExists | MergeTargetMissing | DifferentThread | Printed |
|  | - |  |  |  |  |
| Default | - | - | - | - | - |
| Alive | - | **Disposed** + `[merge]` | **Disposed** | **Disposed** + warning | **Disposed** |
| Disposed | - | - | - | - | - |

---

## 3. Runtime Storage Units

### 3-1. `BlackboxRuntime`

`BlackboxRuntime` issues global ids, interaction ids, and sequences.

| BlackboxRuntime | - | GetNextBlackboxId | GetNextInteractionId | GetNextSequence | Reset |
| --- | --- | --- | --- | --- | --- |
|  | - |  |  |  |  |
|  | - |  |  |  |  |
| Ready | - | `[increase id]` | `[increase interaction id]` | `[increase sequence]` | **Ready** |

### 3-2. `BlackboxRegistry`

`BlackboxRegistry` connects target objects to `Blackbox`.

| BlackboxRegistry | - | GetBlackbox | < | < | < | Contains | < | Count | ForceReset |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  | - | ValidSubject | NullSubject | StrongReferenceOn | StrongReferenceOff | ValidSubject | NullSubject |  |  |
|  | - |  |  |  |  |  |  |  |  |
| Empty | - | **Registered** | _ArgumentNullException_ | **Registered** | **Registered** | `[false]` | _ArgumentNullException_ | `[0]` | - |
| Registered | - | `[reuse same owner]` | _ArgumentNullException_ | `[strong owner]` | `[weak owner]` | `[true]` | _ArgumentNullException_ | `[count]` | **Empty** |
| Any | - | ^ | ^ | ^ | ^ | ^ | ^ | X | `<runtime/handle reset>` |

### 3-3. `Infrastructure`

`Infrastructure` stores Blackbox global settings and export-completed state.

Settings include `LogDirectory`, logger, `MaxLogCount`, `StrongReference`, `DefaultRecursionDepth`, `ExportFormat`, `FullExportOption`, `OpenLogOption`, `ExceptionHandlingOption`, and `TagTargetTypes`. A `BasedOnSettings` value must not overwrite existing settings, and must resolve to the current setting at resolve time.

| Infrastructure | - | Configure/Set | Resolve | Log | TryMarkPrinted | ForceResetRuntimeState |
| --- | --- | --- | --- | --- | --- | --- |
|  | - |  |  |  |  |  |
|  | - |  |  |  |  |  |
| Ready | - | `[update settings]` | `[resolve explicit/default]` | `[call logger]` | **Printed** | - |
| Printed | - | `[update settings]` | `[resolve explicit/default]` | `[call logger]` | - | **Ready** |

---

## 4. Log Context Units

### 4-1. `LogContext`

`LogContext` manages per-thread log buffers and scope lifecycles inside one `Blackbox`.

| LogContext | - | EnqueueLog | OpenScope | CloseScope | < | < | ResolveWith | TryMergeScope | < | GetLogs |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  | - |  |  | CloseNewest | CloseOuterFirst | CloseFromDifferentThread | ValidTarget | MergeAdjacent | MergeNonAdjacent |  |
|  | - |  |  |  |  |  |  |  |  |  |
| Ready | - | `[store log]` | **ScopeOpen** | _Warning_ | _Warning_ | _Warning_ | - | - | - | `[valid logs]` |
| ScopeOpen | - | `[store scoped log]` | **ScopeOpen** | **Ready** | **Ready** + auto close | `[requested close]` + warning | `[tag resolve]` | `[merge]` | - | `[valid logs]` |
| RingBufferFull | - | `[overwrite old log]` | `[old log may be overwritten]` | `[available range]` | `[available range]` | `[available range]` | `[available log only]` | `[available range only]` | - | `[recent logs]` |

### 4-2. `Blackbox`

`Blackbox` applies actual recording rules for one owner.

| Blackbox | - | Write | Scope | ExertMessage | < | < | Exert | < | < | GetLogs | GetLogsByContext |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  | - |  |  | OtherNull | OtherSelf | OtherPeer | OtherNull | OtherSelf | OtherPeer | MultiContext |  |
|  | - |  |  |  |  |  |  |  |  |  |  |
| Ready | - | `[store self log]` | `[store open scope]` | _ArgumentNullException_ | `[self interaction]` | `[two-sided interaction]` | _ArgumentNullException_ | `[self exert handle]` | `[interaction + handle]` | `[sort by sequence]` | `[logs by context]` |
| Printed | - | - | - | - | - | - | - | - | - | `[snapshot]` | `[snapshot]` |
| OwnerReferenceLost | - | `[fallback owner string]` | `[fallback owner string]` | `[fallback owner string]` | ^ | ^ | `[fallback owner string]` | ^ | ^ | `[fallback owner string]` | `[fallback owner string]` |

---

## 5. External Entry Point Unit

### 5-1. `BlackboxHandle`

`BlackboxHandle` is the public API surface used by external code.

This table has both the default recording axis and the fallback verification axis where `UseBlackbox` is off. The `UseBlackboxOff` row verifies that when the runtime setting is off, the public API returns original messages or default handles and does not create actual records.

| BlackboxHandle | - | Of | < | Construct | When | < | Dispose | Write | Scope | ExertMessage | < | Exert | < |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  | - | ValidSubject | NullSubject |  | True | False |  |  |  | OtherNull | OtherPeer | OtherNull | OtherPeer |
|  | - |  |  |  |  |  |  |  |  |  |  |  |  |
| UseBlackboxOff | - | **Default/Invalid** | X | - | - | - | - | `[return message]` | - | `[return message]` | `[return message]` | - | - |
| Default/Invalid | - | X | X | - | - | - | - | `[return message]` | - | `[return message]` | `[return message]` | - | - |
| Valid | - | `[same owner handle]` | _ArgumentNullException_ | `[ctor scope + cached handle]` | `[stay valid]` | **ConditionalSkipped** | `[disposed log]` | `[self log]` | `[scope handle]` | _ArgumentNullException_ | `[two-sided interaction]` | _ArgumentNullException_ | `[exert handle]` |
| ConditionalSkipped | - | X | X | - | X | - | - | `[return message]` | - | `[return message]` | `[return message]` | - | - |
| Printed | - | `[handle can be created]` | _ArgumentNullException_ | - | `[reflect condition]` | ^ | - | `[return message]` | - | `[return message]` | `[return message]` | - | - |

| BlackboxHandle | - | WriteError | < | < | CrashExport | Export | < | ForceReset |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  | - | ErrorTargetsEmpty | ErrorTargetsPresent | ExceptionHandlingCrashExport |  | FirstExport | DuplicateExport |  |
|  | - |  |  |  |  |  |  |  |
| Default/Invalid | - | `[return message]` | `[return message]` | `[return message]` | `[return message + possible warning]` | `[warning]` | `[warning]` | `<Registry.ForceReset>` |
| Valid | - | `[error log]` | `[error log + tag]` | `<CrashExport>` | `[error log + stack trace + export]` | **Printed** | - | `<Registry.ForceReset>` |
| Printed | - | `[return message]` | `[return message]` | `[return message]` | `[restrict duplicate export]` | - | - | **Default reset state** |

---

## 6. Export Units

### 6-1. Export Tools

Export tools organize source logs into an output graph.

| ExportTools | - | BuildExportGraph | FlattenSteps | ResolveScopeDepths | TrimSmart |
| --- | --- | --- | --- | --- | --- |
|  | - |  |  |  |  |
|  | - |  |  |  |  |
| RootOnly | - | `[root node]` | `[convert empty scope to step]` | `[calculate scope depth]` | X |
| Focused | - | `[include incoming peers]` | `[convert by context]` | `[calculate by context]` | X |
| Full | - | `[include incoming/outgoing peers]` | `[convert by context]` | `[calculate by context]` | X |
| DepthLimited | - | `[include only to recursionDepth]` | `[convert by context]` | `[calculate by context]` | X |
| FileName | - | X | X | X | `[remove invalid chars/limit length/null fallback]` |

### 6-2. Exporters

`TxtExporter` and `HtmlExporter` output the same export graph in different file formats.

| Exporter | - | Export | < | < |
| --- | --- | --- | --- | --- |
|  | - | Normal | Crash | OpenLog |
|  | - |  |  |  |
| LogDirectoryMissing | - | _InvalidOperationException_ | ^ | X |
| TxtReady | - | `[create txt file]` | `[create CRASH prefix file]` | `[try opening file automatically]` |
| HtmlReady | - | `[create html file]` | `[create CRASH prefix file]` | `[try opening file automatically]` |

---

## 7. Integration Flow

Integration tests verify representative usage flows based on behaviors already verified in individual object tables.

| Integration | - | Run |
| --- | --- | --- |
|  | - |  |
|  | - |  |
| SingleOwnerHistory | - | `[Construct -> Scope -> Write -> Export]` |
| TwoOwnerInteraction | - | `[Exert -> target Scope -> verify Export connection]` |
| TagTargetFlow | - | `[Write.With / Scope.With -> verify source/target display]` |
| ErrorFlow | - | `[WriteError / CrashExport -> verify error log + export]` |
| ResetAndRerun | - | `[Export -> ForceReset -> new run]` |
| RingBufferLimit | - | `[keep recent logs when MaxLogCount is exceeded]` |
