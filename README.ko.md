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

## 요구 사항과 설치

소비 프로젝트는 안정 배포본 `BlackThunder.BlackboxSystem`의 버전을 고정하여 참조한다. 배포된 `.nupkg`를 보관한 폴더를 NuGet 피드로 등록한 뒤 사용한다.

```xml
<ItemGroup>
  <PackageReference Include="BlackThunder.BlackboxSystem" Version="[0.2.0]" />
</ItemGroup>
```

Blackbox 자체를 개발하는 저장소의 샘플은 현재 라이브러리 프로젝트를 `ProjectReference`로 참조한다. 런타임 라이브러리는 UniTest에 의존하지 않는다.

저장소 빌드와 테스트·샘플·벤치마크 실행에는 .NET 10 SDK가 필요하다. 라이브러리는 `netstandard2.1`, 실행 프로젝트는 `net10.0`을 대상으로 하며 언어 버전은 C# 9이다. 공통 지침의 서브모듈 초기화와 개발 규칙은 [기여 지침](CONTRIBUTING.md)을 따른다.

실행 초기에 로그 출력 위치와 로거를 설정한다.

```csharp
BlackboxHandle.Configure(
    logDirectory: Path.Combine(AppContext.BaseDirectory, "BlackboxLogs"),
    logger: Console.WriteLine);
```

기본 기록 상태는 켜져 있다. 실행 중 기록을 멈추고 싶다면 `BlackboxHandle.UseBlackbox = false`를 설정하거나 `Configure(..., useBlackbox: UseBlackboxOption.DoNotUse)`를 사용한다. 이 런타임 스위치가 꺼져 있으면 `BlackboxHandle.Of(subject)`는 유효하지 않은 핸들을 반환하고, 기록 호출은 no-op 또는 기본값 반환으로 빠진다.

C# 9 호출자의 보간 문자열은 일반 문자열 오버로드를 사용하므로, 기록이 꺼져 있어도 문자열 안의 식은 먼저 평가된다. `WriteHandler`를 통한 자동 평가 생략은 C# 10 이상으로 컴파일한 호출자에서 지원한다.

## 빠른 시작

### 기본 사용 코드

```csharp
using BlackThunder.BlackboxSystem;

public class Loader
{
    public Loader()
    {
        using var scope = BlackboxHandle.Of(this, label: "Loader").Construct();
    }

    public void Load(Worker worker)
    {
        using var _ = BlackboxHandle.Of(this).Scope("Load");

        BlackboxHandle.Of(this).Write("Prepare %0").With(worker);

        using (BlackboxHandle.Of(this).Exert(worker, "Worker.Load"))
        worker.Load();
    }

    public void ExportLogs() => BlackboxHandle.Of(this).Export();
}
```

이 예시는 다음 정보를 남긴다.

- `Loader` 객체의 `Load` 스코프
- 스코프 시작 로그
- `worker.Load()` 호출에 대한 양쪽 상호작용 기록
- `ExportLogs()` 호출 시 `Loader`를 중심으로 연결된 로그 출력

### 테스트 세션

```csharp
using var session = BlackboxHandle.BeginSession();
BlackboxHandle.Configure(logDirectory, Console.WriteLine,
    openLogOption: OpenLogOption.Never);
var system = new SystemUnderTest();
await system.RunAsync();
```

세션 안에서 객체를 만들고 모든 자식 작업을 await/join한 뒤 Dispose한다. 핸들은 획득 당시 세션에 고정되며, Dispose하지 않은 명시적 세션은 전역 활성 목록이 계속 보관한다. 세션 격리·누락 진단·기본 세션 재시작·출력 계약은 [세션과 기록 계약](docs/Wiki/05-Sessions.md)을 참고한다.

### C# 샘플 실행

[samples의 콘솔 샘플](samples/README.md)에서 기록 흐름을 확인할 수 있다.

```powershell
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj -- write
```

첫 명령은 `sample>` 프롬프트를 연다. `write`, `exert`, `tag`, `exception`을 반복 실행할 수 있고, 빈 입력은 무시하며 `help`는 도움말을, `exit`는 종료를 수행한다. 두 번째 명령은 시나리오를 한 번 실행하고 종료하며 브라우저를 열지 않는다. 일회 실행의 종료 코드는 성공 0, 실행 오류 1, 잘못된 명령 또는 인자 2이다.

기록은 기본값인 `UseBlackbox = true`로 실행된다. 로그는 실행 파일의 출력 디렉터리 아래 `SampleOutput/Write`, `SampleOutput/Exert`, `SampleOutput/Tag`, `SampleOutput/Exception`에 생성된다. 대화형 실행의 출력 열기 동작과 각 명령의 목적은 샘플 README를 참고한다.

## 주요 API

- `BlackboxHandle.Of(subject, label: name)`: 현재 세션에서 대상 객체의 기록 진입점을 얻는다. 이름은 선택 사항이며 최초의 공백 아닌 이름만 저장한다. 생략하면 기존 이름을 유지한다.
- `BlackboxHandle.BeginSession()`: 격리된 테스트·디버그 세션을 시작한다.
- `BlackboxHandle.ActiveSessionCount` / `GetActiveSessionKeys()`: 프로세스 전체의 미종료 명시적 세션을 확인한다.
- `BlackboxHandle.DisposeActiveSessions()`: 해당 세션의 스냅샷을 일괄 종료한다.
- `Write(message)`: 한 줄 활동 로그를 남긴다.
- `Write(message).With(targets)`: 활동 로그에 관련 대상을 붙인다.
- `Scope(message)`: 스코프 시작 로그를 남기고 `ScopeHandle`을 반환한다.
- `Scope(message).With(targets)`: 스코프 시작 로그에 관련 대상을 붙인다.
- `Exert(other, message)`: 다른 객체와의 상호작용을 남기고, 받는 쪽 스코프 병합을 시도할 `ExertHandle`을 반환한다.
- `ExertMessage(other, message)`: 병합 없이 한 줄 상호작용 기록만 남긴다.
- `WriteError(message)`: 오류 메시지를 로그로 남기고 같은 문자열을 반환한다.
- `Export(...)`: 현재 객체를 중심으로 로그 파일을 출력한다.
- `CrashExport(...)`: 오류 기록과 출력 생성을 함께 수행한다.
- `RestartDefaultSession()`: 설정을 유지하며 공유 기본 세션을 종료·교체한다. 명시적 세션은 변경하지 않는다.

## 문서

- [개요](docs/Wiki/01-Overview.md): 목적과 주요 기록 구조
- [사용법](docs/Wiki/04-Usage.md): 설정과 대표 사용 흐름
- [구현](docs/Wiki/02-Implementations.md): 구성 요소의 책임과 기록 파이프라인
- [세션과 기록 계약](docs/Wiki/05-Sessions.md): 수명·기본 세션·무잠금 기록·출력 규칙

## 테스트

테스트 코드는 `tests/BlackThunder.BlackboxSystem.Tests`와 `tests/BlackThunder.BlackboxSystem.Tests.CSharp10`에 있으며, 두 프로젝트 모두 NUnit을 사용한다.

기본 테스트 프로젝트는 exporter hook을 위해 Blackbox 런타임 소스를 `BLACKBOX_TESTS`로 컴파일하고, UniTest는 `BlackThunder.UniTest` 안정 버전 `0.4.0` 패키지를 참조한다. `BlackboxTableProject`는 `ExecutionMode.Inline`을 선택하여 전역 Blackbox 상태를 사용하는 기존 실행 방식을 유지한다.

`BlackThunder.BlackboxSystem.Tests.CSharp10`은 라이브러리 프로젝트를 참조하고, 저장소 공통 C# 9 설정의 유일한 예외로 C# 10을 사용한다. C# 10 이상으로 컴파일한 호출자의 보간 문자열이 `WriteHandler`로 연결되고, 기록이 꺼져 있을 때 문자열 안의 식을 평가하지 않는지 확인한다.

루트 `NuGet.config`는 `packages/` 로컬 피드와 nuget.org를 사용한다. `packages/`에는 고정된 UniTest 배포 패키지와 출처 기록을 보관하며, 자체 패키지 ID는 로컬 피드에만 매핑한다. 일반 복원 캐시와 `bin/obj`는 저장소에 포함하지 않는다.

```powershell
dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj
dotnet test tests/BlackThunder.BlackboxSystem.Tests.CSharp10/BlackThunder.BlackboxSystem.Tests.CSharp10.csproj
```

[세션 검증 결과](docs/Tests/04-Sessions.md)와 [성능 측정](benchmarks/README.md)을 참고한다.

## 라이선스

Blackbox는 MIT 라이선스로 배포된다. 자세한 내용은 [LICENSE.md](LICENSE.md)를 참고한다.
