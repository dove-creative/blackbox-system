# Blackbox Object Diagram

This diagram shows the objects and relationships that participate in Blackbox's core recording paths.

```mermaid
classDiagram
direction TB

class Blackbox {
  +object Owner
  +string OwnerString
  +long Id
  -ThreadLocal~LogContext~ logContext
  -long scopeId
  +Write(message, methodName)
  ~Tag(message, methodName, taggedBy, interactionId, tags, targetTypes)
  +Scope(message, methodName)
  +ExertMessage(other, message, methodName)
  +Exert(other, message, methodName)
  +GetLogsByContext(maxSequence)
  +GetLogs(maxSequence)
}

class BlackboxRuntime {
  +long CurrentSequence
  +GetNextBlackboxId()
  +GetNextInteractionId()
  +GetNextSequence()
  +Reset()
}

class LogContext {
  -LogData[] logBuffer
  -long currentLogIndex
  -int threadId
  -string threadName
  -List~ScopeInfo~ openScopes
  +OpenScope(message, sequence, methodName, scopeId)
  +CloseScope(scopeId, sequence)
  +EnqueueLog(message, sequence, methodName, scopeId, interactionId)
  +EnqueueTagLog(message, sequence, methodName, interactionId)
  +ResolveWith(logIndex, tags, targetTypes)
  +RenderMessage(logIndex, fallbackMessage)
  +TryMergeScope(interactionId)
  +GetLogs(maxSequence)
}

class LogData {
  +Blackbox Owner
  +bool IsValid
  +string Message
  +DateTime Time
  +int ScopeDepth
  +long Sequence
  +string MethodName
  +long ScopeId
  +ScopeType ScopeType
  +int ThreadId
  +string ThreadName
  +Blackbox ExertedBy
  +Blackbox ExertingTo
  +long InteractionId
  +Blackbox TaggedBy
  +TargetTypes TagTargetTypes
  +Tag[] Tags
  +ToString(scopeDepth)
}

class LogFormatter {
  <<static>>
  +RenderMessage(log)
  +RenderTextLine(log, scopeDepth)
  +RenderMethodLabel(log)
  +ObjectRef(blackbox)
  +TagRef(blackbox, interactionId)
  +Arrow(right, interactionId)
}

class BlackboxRegistry {
  -ConditionalWeakTable~object, Blackbox~ subjects
  +GetBlackbox(subject)
  +Contains(subject)
  +Count()
  +ForceReset()
}

class BlackboxHandle {
  +object Owner
  +string OwnerString
  +long Id
  +bool IsValid
  +Of(subject)
  +Configure(...)
  +ForceReset()
  +Construct(message, out handle)
  +When(condition)
  +Dispose(message)
  +Write(message, methodName)
  +Scope(message, methodName)
  +ExertMessage(other, message, methodName)
  +Exert(other, message, methodName)
  +WriteError(message, others)
  +CrashExport(message, others)
  +Export(...)
}

class ScopeType {
  <<enum>>
  None
  Open
  Close
  Step
}

Blackbox --> BlackboxRuntime : requests id and sequence
Blackbox *-- LogContext : owns thread-local contexts
LogContext *-- LogData : owns log buffer
LogContext --> ScopeType : records scope open/close
LogData --> ScopeType : classifies log type
LogData --> Blackbox : references owner and interaction peers
LogData ..> LogFormatter : delegates output string creation
BlackboxRegistry --> Blackbox : manages recording engine per owner
BlackboxHandle --> BlackboxRegistry : looks up owner
BlackboxHandle --> Blackbox : forwards recording calls
```

---

## Items Excluded from the Diagram

For readability, handle-related auxiliary structures such as `TagHandle`, `ScopeHandle`, `ExertHandle`, and `HandleManager<T>` are excluded from this diagram. Target recording connected by `.With(...)` is handled as a flow where `LogContext.ResolveWith(...)` fixes the source log's tag list, then `Blackbox.Tag(...)` adds a target-side tag log.
