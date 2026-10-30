# 안정 패키지

Blackbox 테스트가 소비하는 검증된 자사 NuGet 배포본을 보관한다. 복원 캐시와 일반 빌드 산출물은 이 폴더에 추가하지 않는다.

## 현재 버전

| 항목 | 값 |
| --- | --- |
| 패키지 | `BlackThunder.UniTest` |
| 버전 | `0.4.0` |
| 파일 | `BlackThunder.UniTest.0.4.0.nupkg` |
| 원본 저장소 | [dove-creative/unitest](https://github.com/dove-creative/unitest) |
| 원본 커밋 | [5b5e593f1a1018d0b359e55073426cb83be80590](https://github.com/dove-creative/unitest/commit/5b5e593f1a1018d0b359e55073426cb83be80590) |
| SHA-256 | `F978A9E221098D9F6FAA25A619FB30D710EEEA5499ED604BAF8891EFD8E79F8E` |
| 검증 | Release 솔루션 빌드, 단위 테스트 590개 및 재귀 테스트 8개 통과, Windows·Ubuntu CI 통과 |
| 소비 검증 | 빈 패키지 캐시에서 로컬 피드 복원, Blackbox Release 솔루션 빌드, 기본 테스트 278개 및 C# 10 테스트 2개 통과 |

패키지는 `netstandard2.1`의 `BlackThunder.UniTest.dll`을 제공하며, 네임스페이스도 `BlackThunder.UniTest`이다. Blackbox 테스트는 `[0.4.0]`으로 버전을 고정하고 `ExecutionMode.Inline`을 명시한다. 일반 생성자의 기본값은 `ThreadPool`이다. `Inline`도 비동기로 실행되며 같은 OS 스레드를 보장하지 않는다.

`0.2.0`에서 올리면서 `0.3.0`의 JSON 보고서 API(`GetReport().ToJson()`, `GetFailedReports().ToJson()`)와 `0.4.0`의 네임스페이스 변경을 테스트에 반영했다. 이전 배포본 `BlackThunder.UniTest.0.2.0.nupkg`(원본 커밋 `76ab64a`, SHA-256 `CD31ABF0…5135A8`)는 덮어쓰지 않고 그대로 보관한다.

## 갱신 절차

1. 독립 UniTest 저장소에서 변경과 버전·문서를 갱신하고 솔루션 빌드 및 기존 두 NUnit 프로젝트를 검증한다.
2. 통과한 변경을 커밋하고 해당 커밋에서 Release 패키지를 만든다. `dotnet pack`에 `-p:RepositoryCommit=<원본-SHA>`를 명시한다.
3. 새 버전의 `.nupkg`만 이 폴더로 복사한다. 같은 버전의 파일을 덮어쓰지 않는다.
4. 패키지의 ID·버전·대상 프레임워크·원본 커밋과 SHA-256을 확인하고 이 문서에 기록한다.
5. 테스트 프로젝트의 정확한 버전을 갱신하고 Blackbox 솔루션 빌드와 기존 테스트 전체를 검증한다.
6. 비어 있는 임시 패키지 캐시에서도 로컬 피드로 복원·빌드되는지 확인한 뒤 패키지·참조·출처 문서를 함께 커밋한다.

루트 `NuGet.config`가 자사 패키지를 이 로컬 피드에 연결하므로 형제 UniTest 개발 저장소가 없어도 복원할 수 있다. 패키지는 공개 NuGet 피드에 게시하지 않는다.
