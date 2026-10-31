# Blackbox System for C#

[English](README.md)

Blackbox는 객체별 활동, 실행 스코프, 객체 간 상호작용을 기록하고 나중에 연결된 로그로 다시 읽기 위한 .NET 추적 프레임워크이다.

일반 로그가 한 시점의 메시지를 남기는 데 집중한다면, Blackbox는 '이 객체가 어떤 과정을 거쳤는가'와 '그 과정에서 어떤 객체와 연결되었는가'를 함께 남긴다.

<img src="../docs/Images/blackbox-html-export.png" alt="Blackbox HTML export preview" width="900">

## 시작하기 전에

Blackbox는 BlackThunder 프로젝트군의 기반 추적 프레임워크이다.

사용 중 불편한 점이나 개선할 부분은 [Discussions](https://github.com/dove-creative/blackbox-system/discussions) 또는 vkdl4062@gmail.com으로 알려 주세요. Blackbox를 사용해 주셔서 감사합니다.

## 주요 기능

- 객체별 로그 저장소: 대상 객체마다 대응되는 `Blackbox`를 만들고, 실행 중 로그를 누적한다.
- 스코프 기록: `Scope(...)`와 `using`으로 처리 구간의 시작과 끝을 함께 남긴다.
- 상호작용 기록: `Exert(...)`와 `ExertMessage(...)`로 객체 간 호출 관계를 양쪽 로그에 기록한다.
- 태그 연결: `Write(...).With(...)`나 `Scope(...).With(...)`로 원본 로그에 관련 대상을 붙인다.
- 출력: 기록된 로그를 텍스트 또는 HTML 파일로 내보낸다.
- 런타임 스위치: `BlackboxHandle.UseBlackbox`로 기록 핸들 생성과 로그 저장 여부를 제어한다.

이 문서는 C# 구현의 시작 안내이다. 언어별 구현은 [저장소 소개](../README.ko.md)에서 확인한다.

## 요구 사항과 설치

소비 프로젝트는 안정 배포본 `BlackThunder.BlackboxSystem`의 버전을 고정하여 참조한다. 배포된 `.nupkg`를 보관한 폴더를 NuGet 피드로 등록한 뒤 사용한다.

```powershell
dotnet add package BlackThunder.BlackboxSystem --version "[0.2.0]"
```

프로젝트 파일에 직접 추가할 수도 있다.

```xml
<ItemGroup>
  <PackageReference Include="BlackThunder.BlackboxSystem" Version="[0.2.0]" />
</ItemGroup>
```

Blackbox 자체를 개발하는 저장소의 샘플은 현재 라이브러리 프로젝트를 `ProjectReference`로 참조한다. 런타임 라이브러리는 UniTest에 의존하지 않는다.

아래 저장소 개발 명령은 C# 프로젝트 루트인 `csharp/`에서 실행한다. 빌드와 테스트·샘플·벤치마크 실행에는 .NET 10 SDK가 필요하다. 저장소 루트의 `global.json`이 사용할 SDK 계열을 선택한다. 라이브러리는 `netstandard2.1`, 실행 프로젝트는 `net10.0`을 대상으로 하며 언어 버전은 C# 9이다. 공통 지침의 서브모듈 초기화와 개발 규칙은 [기여 지침](../CONTRIBUTING.md)을 따른다.

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

### C# 샘플 실행

[samples의 콘솔 샘플](samples/README.md)에서 기록 흐름을 확인할 수 있다.

```powershell
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj
dotnet run --project samples/BlackThunder.BlackboxSystem.Samples.csproj -- write
```

첫 명령은 `sample>` 프롬프트를 열어 `write`, `exert`, `tag`, `exception`을 반복 실행한다. 두 번째 명령은 `write` 시나리오를 한 번 실행하고 종료하며 브라우저를 열지 않는다.

로그는 실행 파일의 출력 디렉터리 아래 시나리오별 폴더에 생성된다. `write`의 출력 위치는 `SampleOutput/Write`이다. 명령별 사용법, 종료 코드, 출력 열기 동작은 [샘플 문서](samples/README.md)를 참고한다.

## 주요 API

- `BlackboxHandle.Of(subject, label: name)`: 현재 세션에서 대상 객체의 기록 진입점을 얻고 선택적으로 이름을 지정한다.
- `Write(message)` / `WriteError(message)`: 활동이나 오류를 기록한다.
- `Scope(message)`: `using` 블록의 실행 범위를 기록한다.
- `Exert(other, message)` / `ExertMessage(other, message)`: 객체 간 상호작용을 양쪽 로그에 남긴다.
- `With(targets)`: 활동 로그나 스코프 시작 로그에 관련 대상을 붙인다.
- `Export(...)` / `CrashExport(...)`: 연결된 로그를 일반 출력하거나 오류 기록과 함께 출력한다.
- `BlackboxHandle.BeginSession()`: 수명을 분리한 테스트·디버그 세션을 시작한다.
- `BlackboxHandle.RestartDefaultSession()`: 설정을 유지하며 공유 기본 세션을 종료·교체한다.

호출 기준은 [사용법](../docs/Wiki/04-Usage.md)을, 세션 종료와 누락 진단은 [세션과 기록 계약](../docs/Wiki/05-Sessions.md)을 참고한다.

## 문서

- [개요](../docs/Wiki/01-Overview.md): 목적과 주요 기록 구조
- [사용법](../docs/Wiki/04-Usage.md): 설정과 대표 사용 흐름
- [구현](../docs/Wiki/02-Implementations.md): 구성 요소의 책임과 기록 파이프라인
- [세션과 기록 계약](../docs/Wiki/05-Sessions.md): 수명·기본 세션·무잠금 기록·출력 규칙

## 테스트

.NET 10 SDK로 두 NUnit 테스트 프로젝트를 실행한다. 기본 프로젝트는 기록·핸들·출력·샘플 동작을, C# 10 프로젝트는 보간 문자열의 `WriteHandler` 연결과 기록 비활성 시 식 평가 생략을 검증한다.

C# 프로젝트 루트의 `NuGet.config`는 저장소에 포함된 `packages/` 로컬 피드의 `BlackThunder.UniTest` `0.4.0`과 nuget.org의 외부 의존성을 복원한다.

```powershell
dotnet test tests/BlackThunder.BlackboxSystem.Tests/BlackThunder.BlackboxSystem.Tests.csproj
dotnet test tests/BlackThunder.BlackboxSystem.Tests.CSharp10/BlackThunder.BlackboxSystem.Tests.CSharp10.csproj
```

검증 범위와 개발 설정은 [기여 지침](../CONTRIBUTING.md)을 따른다. [세션 검증 결과](../docs/Tests/04-Sessions.md)와 [성능 측정](benchmarks/README.md)도 참고할 수 있다.

## 라이선스

Blackbox는 MIT 라이선스로 배포된다. 자세한 내용은 [LICENSE.md](../LICENSE.md)를 참고한다.
