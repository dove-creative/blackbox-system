# Blackbox 객체 다이어그램

이 다이어그램은 Blackbox의 핵심 기록 경로에 참여하는 객체와 관계를 보여준다.

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

Blackbox --> BlackboxRuntime : id와 시퀀스 발급 요청
Blackbox *-- LogContext : 스레드별 문맥 보유
LogContext *-- LogData : 로그 버퍼 보유
LogContext --> ScopeType : 스코프 열기/닫기 기록
LogData --> ScopeType : 로그 종류 분류
LogData --> Blackbox : owner와 상호작용 상대 참조
LogData ..> LogFormatter : 출력 문자열 생성 위임
BlackboxRegistry --> Blackbox : owner별 기록 엔진 관리
BlackboxHandle --> BlackboxRegistry : owner 조회
BlackboxHandle --> Blackbox : 기록 호출 전달
```

---

## 다이어그램 제외 항목

가독성을 위해 `TagHandle`, `ScopeHandle`, `ExertHandle`, `HandleManager<T>` 같은 핸들 계열 보조 구조는 이 다이어그램에서 제외한다. `.With(...)`로 연결한 대상 기록은 `LogContext.ResolveWith(...)`가 원본 로그의 태그 목록을 확정한 뒤, `Blackbox.Tag(...)`를 통해 대상 쪽 태그 로그를 추가하는 흐름으로 처리한다.
