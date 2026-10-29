# Blackbox System

[English README](README.md)

Blackbox는 객체별 활동, 실행 스코프, 객체 간 상호작용을 기록하고 나중에 연결된 로그로 다시 읽기 위한 .NET 추적 프레임워크이다.

일반 로그가 한 시점의 메시지를 남기는 데 집중한다면, Blackbox는 '이 객체가 어떤 과정을 거쳤는가'와 '그 과정에서 어떤 객체와 연결되었는가'를 함께 남긴다.

<img src="docs/Images/blackbox-html-export.png" alt="Blackbox HTML export preview" width="900">

## 시작하기 전에

Blackbox는 BlackThunder 프로젝트군의 기반 추적 프레임워크입니다.

이 저장소는 dove-creative가 공개하는 초기 오픈소스 프로젝트입니다. 사용 중 불편한 점이나 개선할 부분을 발견하시면 Discussion을 열어 주시거나 vkdl4062@gmail.com으로 알려 주시면 도움이 됩니다.

Blackbox를 사용해 주셔서 감사합니다.

## 주요 기능

- 객체별 로그 저장소: 대상 객체마다 대응되는 `Blackbox`를 만들고, 실행 중 로그를 누적한다.
- 스코프 기록: `Scope(...)`와 `using`으로 처리 구간의 시작과 끝을 함께 남긴다.
- 상호작용 기록: `Exert(...)`와 `ExertMessage(...)`로 객체 간 호출 관계를 양쪽 로그에 기록한다.
- 태그 연결: `Write(...).With(...)`나 `Scope(...).With(...)`로 원본 로그에 관련 대상을 붙인다.
- 출력: 기록된 로그를 텍스트 또는 HTML 파일로 내보낸다.
- 런타임 스위치: `BlackboxHandle.UseBlackbox`로 기록 핸들 생성과 로그 저장 여부를 제어한다.

## 테스트 세션

```csharp
using var session = BlackboxSession.Begin();
BlackboxHandle.Configure(logDirectory, Console.WriteLine,
    openLogOption: OpenLogOption.Never);
var system = new SystemUnderTest();
await system.RunAsync();
```

세션 안에서 객체를 만들고 모든 자식 작업을 await/join한 뒤 Dispose한다. `Begin()`은 동기 메서드이며 일반적인 Task·Thread의 실행 문맥 전파를 따라 세션이 전달된다. NUnit에서는 SetUp의 AsyncLocal 전파에 의존하지 않고 테스트 본문에서 세션을 연다. 각 세션은 부모 설정을 복사하고 저장소·크래시 동결·리셋을 격리한다.

생성자에서 보관한 핸들을 포함해 모든 핸들은 획득 당시 세션과 세대에 고정된다. 객체를 다른 세션에서 사용해도 핸들이 이동하지 않는다. `Begin()` 없는 코드는 공유 기본 세션을 사용하므로 기본 세션을 리셋·설정하는 테스트는 직렬 실행한다.

현재 흐름과 기본 세션의 키는 `BlackboxSession.CurrentKey`, `BlackboxSession.DefaultKey`로 확인한다. 세션 밖에서 생성자가 `_bb`를 보관한 객체는 세션 안에서 사용해도 유효한 핸들의 키가 기본 세션 키다. 다음 비교로 이 상황을 진단할 수 있다.

```csharp
if (_bb.SessionKey != BlackboxSession.CurrentKey)
{
    /* 이 객체는 다른 세션에서 생성됨 */
}
```

세 속성은 읽기 전용 Guid이며 세션 객체를 노출하지 않고 잠금·할당 없이 읽는다. 무효 핸들(`IsValid == false`)은 `Guid.Empty`를 반환하므로 차이를 해석하기 전에 유효 여부를 확인한다. 키는 세션을 식별하며 리셋된 세대를 구별하는 값은 아니다.

일반 `Export()`는 기록을 중단하지 않으며 반복할 수 있다. `CrashExport()`는 해당 세대만 동결하고 최초 한 번 출력한다. 크래시 출력 실패 후에는 일반 Export로 재시도할 수 있다. 실행 중 출력은 근사 스냅샷이며 작업 종료 뒤에는 링 버퍼에 남아 있는 안정된 기록을 출력한다. 파일명에 짧은 세션 키와 출력 일련번호를 넣어 덮어쓰지 않는다. 누적 파일 정리는 호출자가 담당한다.

`ForceReset()`은 현재 세션을 새 세대로 바꾸며 기존 핸들의 기록을 거부한다. 명시적 세션의 기존 자식 실행 문맥도 이전 세대에 남는다. 바인딩 없는 기본 세션 호출은 공유된 현재 세대를 따른다. 작업과 종료가 겹쳐도 이전 버퍼를 재사용하지 않으며, 진행 중이던 기록은 이전 세대 안에서 완료될 수 있다. 보관된 핸들로 이전 기록을 출력하는 것은 가능하다.

Subject의 `ToString()`을 자동 호출하지 않는다. 객체 이름은 한 번 명시한다.

```csharp
using var scope = BlackboxHandle.Of(this).Label(Name).Construct(out _bb);
```

이름이 있으면 `Forest (MonsterHome #2)`, 없으면 `MonsterHome #2`로 표시한다. 최초의 공백 아닌 이름이 유지된다. 변하는 도메인 상태는 메시지에 명시한다. 기본 Subject 참조는 약하며, `StrongReference`는 Blackbox가 도달 가능한 동안 Subject를 유지하므로 Dispose 후 보관한 핸들도 수명을 늘릴 수 있다. 내부 기록·등록에는 잠금을 사용하지 않지만 불변 로그 게시에는 할당이 필요하다. 런타임·GC·I/O·명시적 포맷팅과 외부 로거는 이 보장의 범위 밖이다.

## 설치

소비 프로젝트는 안정 배포본 `BlackThunder.BlackboxSystem`의 버전을 고정하여 참조한다. 배포된 `.nupkg`를 보관한 폴더를 NuGet 피드로 등록한 뒤 사용한다.

```xml
<ItemGroup>
  <PackageReference Include="BlackThunder.BlackboxSystem" Version="[0.2.0]" />
</ItemGroup>
```

Blackbox 자체를 개발하는 저장소의 샘플은 현재 라이브러리 프로젝트를 `ProjectReference`로 참조한다. 런타임 라이브러리는 UniTest에 의존하지 않는다.

실행 초기에 로그 출력 위치와 로거를 설정한다.

```csharp
BlackboxHandle.Configure(
	logDirectory: Path.Combine(AppContext.BaseDirectory, "BlackboxLogs"),
	logger: Console.WriteLine);
```

기본 기록 상태는 켜져 있다. 실행 중 기록을 멈추고 싶다면 `BlackboxHandle.UseBlackbox = false`를 설정하거나 `Configure(..., useBlackbox: UseBlackboxOption.DoNotUse)`를 사용한다. 이 런타임 스위치가 꺼져 있으면 `BlackboxHandle.Of(subject)`는 유효하지 않은 핸들을 반환하고, 기록 호출은 no-op 또는 기본값 반환으로 빠진다.

## 빠른 시작

### 기본 사용 코드

```csharp
using BlackThunder.BlackboxSystem;

public class Loader
{
    public Loader()
    {
        using var scope = BlackboxHandle.Of(this).Label("Loader").Construct();
    }

    public void Load(Worker worker)
    {
        using var _ = BlackboxHandle.Of(this).Scope("Load");

        BlackboxHandle.Of(this).Write("Prepare %0").With(worker);

        using (BlackboxHandle.Of(this).Exert(worker, "Worker.Load"))
		worker.Load();
    }

    public void ExportLogs()
    {
        BlackboxHandle.Of(this).Export();
    }
}
```

이 예시는 다음 정보를 남긴다.

- `Loader` 객체의 `Load` 스코프
- 스코프 시작 로그
- `worker.Load()` 호출에 대한 양쪽 상호작용 기록
- `ExportLogs()` 호출 시 `Loader`를 중심으로 연결된 로그 출력

### C# 샘플 실행

`samples/Blackbox.NativeCSharp.Samples` 콘솔 샘플에서 기록 흐름을 확인할 수 있다.

```powershell
dotnet run --project samples/Blackbox.NativeCSharp.Samples/Blackbox.NativeCSharp.Samples.csproj
```

실행 후 `sample>` 프롬프트에서 `write`, `exert`, `tag`, `exception` 중 하나를 입력한다. 빈 입력은 다시 입력을 기다리고, `exit`를 입력하면 종료한다.

네이티브 샘플은 기본값인 `UseBlackbox = true` 상태로 실행된다. 로그는 샘플 앱 출력 폴더의 `BlackboxSystem/Samples/NativeCSharp` 아래에 시나리오별로 생성된다.

## 주요 API

- `BlackboxHandle.Of(subject)`: 대상 객체의 기록 진입점을 얻는다.
- `BlackboxSession.Begin()`: 격리된 테스트·디버그 세션을 시작한다.
- `Label(name)`: 최초의 공백 아닌 이름을 저장하고 같은 핸들을 반환한다.
- `Write(message)`: 한 줄 활동 로그를 남긴다.
- `Write(message).With(targets)`: 활동 로그에 관련 대상을 붙인다.
- `Scope(message)`: 스코프 시작 로그를 남기고 `ScopeHandle`을 반환한다.
- `Scope(message).With(targets)`: 스코프 시작 로그에 관련 대상을 붙인다.
- `Exert(other, message)`: 다른 객체와의 상호작용을 남기고, 받는 쪽 스코프 병합을 시도할 `ExertHandle`을 반환한다.
- `ExertMessage(other, message)`: 병합 없이 한 줄 상호작용 기록만 남긴다.
- `WriteError(message)`: 오류 메시지를 로그로 남기고 같은 문자열을 반환한다.
- `Export(...)`: 현재 객체를 중심으로 로그 파일을 출력한다.
- `CrashExport(...)`: 오류 기록과 출력 생성을 함께 수행한다.
- `ForceReset()`: 현재 세션을 새 세대로 교체하고 이전 핸들의 기록을 무효화한다.

## 문서

[0.2.0 세션과 기록 계약](docs/Wiki/05-Sessions.md)에서 수명·기본 세션·무잠금 기록·출력 규칙을 설명한다.

## 테스트

테스트 코드는 `tests/BlackThunder.BlackboxSystem.Tests`에 있으며 NUnit을 사용한다.

테스트 프로젝트는 exporter hook을 위해 Blackbox 런타임 소스를 `BLACKBOX_TESTS`로 컴파일하고, UniTest는 `BlackThunder.UniTest` 안정 버전 `0.2.0` 패키지를 참조한다. `BlackboxTableProject`는 `ExecutionMode.Inline`을 선택하여 전역 Blackbox 상태를 사용하는 기존 실행 방식을 유지한다.

루트 `NuGet.config`는 `packages/` 로컬 피드와 nuget.org를 사용한다. `packages/`에는 고정된 UniTest 배포 패키지와 출처 기록을 보관하며, 자체 패키지 ID는 로컬 피드에만 매핑한다. 일반 복원 캐시와 `bin/obj`는 저장소에 포함하지 않는다.

```powershell
dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj
```

[세션 검증 결과](docs/Tests/04-Sessions.md)와 [성능 측정](benchmarks/README.md)을 참고한다.

## 라이선스

Blackbox는 MIT 라이선스로 배포된다. 자세한 내용은 [LICENSE.md](LICENSE.md)를 참고한다.
