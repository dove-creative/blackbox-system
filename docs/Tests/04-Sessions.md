# 0.2.0 세션 구현 검증

검증 환경: Windows x64, .NET SDK 9.0.320, net9.0 NUnit 테스트, netstandard2.1 라이브러리. 기존 테스트 159개를 세션별 동결·반복 출력·명시적 Label 계약에 맞게 이관하고 동시성/수명 테스트 35개를 추가했다.

## 검증 범위

- 병렬 세션의 설정·기록·크래시 격리, Task/Thread 실행 문맥 상속, 중첩 및 이미 종료된 부모 복원.
- 기본 세션 공유 세대와 명시적 세션의 호출자만 바뀌는 ForceReset, 다른 async 메서드에서 Dispose한 문맥의 한계.
- 버킷 1개에서 동시 등록/조회/정리, Subject 및 스레드별 컨텍스트 유일성.
- 핸들 등록을 중간에 정지한 상태에서 Dispose/ForceReset을 실행하는 강제 경합, 이전 세대 게시 지연, 다른 세션 핸들 보존과 슬롯 반환.
- 논리 인덱스 확인, 이전 회전의 미게시 슬롯, 늦은 게시에 의한 덮어쓰기 방지, 비단조 시퀀스, 스코프 갱신 후 상호작용 무효화 순서.
- 태그 배열 복사와 동시 출력, 링 버퍼 순환 후 음수 깊이 방지, 크래시 출력 1회와 일반 반복 출력, 출력 실패 후 재시도.
- 사용자 ToString 자동 호출 0회, 최초 Label 지정 경쟁, 타입명/HTML 처리, 출력 내 이름 일관성.
- NoInlining 헬퍼로 분리한 약한/강한 참조 수명, Subject만 살아 있어도 과거 세션을 붙잡지 않는지, 수집된 컨텍스트의 남은 슬롯 반환.
- 원인별 경고 1회, await로 스레드가 바뀐 Scope/Exert 종료, 경고 로거 재진입, 등록된 Subject 조회 및 추적 슬롯 재사용의 무할당.

## 실행

핵심 구현 커밋 전 결과: Release 194/194, Debug 194/194, 잠금 핸들 풀 비교 구성 194/194 통과, 건너뜀 0개. 라이브러리와 콘솔 샘플 Release 빌드는 경고·오류 0개였다.

```powershell
dotnet build src/BlackThunder.BlackboxSystem -c Release
dotnet build samples/Blackbox.NativeCSharp.Samples -c Release
dotnet test tests/BlackThunder.BlackboxSystem.Tests -c Release
dotnet test tests/BlackThunder.BlackboxSystem.Tests -c Debug
dotnet test tests/BlackThunder.BlackboxSystem.Tests -c Release -p:BlackboxLockHandles=true
```

`BlackboxLockHandles`는 기존 잠금 핸들 풀을 비교하는 테스트 구성이다. 기본 라이브러리에는 활성화하지 않는다. 테스트는 실제 testhost 실행 결과로 판단하며 빌드 성공이나 호스트 연결 시간 초과를 통과로 처리하지 않는다.

초기 샌드박스 실행은 testhost 연결 시간 초과로 중단되었다. 샌드박스 밖의 기존 코드 실행은 159개 중 158개 통과, 약한 소유자의 자동 이름 변경에 의존하던 테이블 테스트 1개 실패였다. 변경 후에는 해당 검증을 명시적 이름 계약으로 이관했다. 결과 TRX는 무시되는 `tests/BlackThunder.BlackboxSystem.Tests/TestResults/`에 저장한다.

측정 가능한 처리량·할당 비용은 [벤치마크 보고서](../../benchmarks/README.md)를 참고한다. 실행 중 출력은 근사 스냅샷이므로, 모든 동시 기록의 포함이나 여러 컨텍스트를 한 순간에 읽는 원자성을 테스트 계약으로 삼지 않는다.

## 후속 세션 키 진단 단계

핵심 구현을 `6d08696`으로 커밋한 뒤 `CurrentKey`, `DefaultKey`, 핸들의 `SessionKey`를 별도 단계로 추가했다. 기존 API 구현은 변경하지 않고 읽기 전용 속성만 추가했다.

`SessionKeyTests`의 15개 검증은 다음을 포함한다.

- 세션 밖의 기본 키, Begin 후 인스턴스 키, 중첩 Dispose 후 바깥 키 복원.
- Task.Run·새 Thread·await에서 키 상속, Begin 전에 시작한 Task와 Thread의 기본 키 유지.
- 생성자에서 보관한 기본 세션 핸들이 다른 세션에서도 원래 저장소에 기록하는 상황.
- 기본값 및 리셋·종료·비활성화·동결로 무효화된 핸들의 Guid.Empty, 경고 없는 조회.
- 리셋에도 유지되는 세션 키, 공개 세션 객체 속성 부재, 10,000회 조회에서 할당 0바이트.

후속 변경을 포함한 Release 전체 테스트는 **209/209** 통과했다. 새 진단 테스트는 Debug와 잠금 핸들 풀 비교 구성에서 각각 **15/15** 통과했고 건너뜀은 없었다. 라이브러리와 콘솔 샘플 Release 빌드도 경고·오류 0개로 통과했다.

## 2026-10-30: 트래커 누적 수정

컨텍스트별 트래커 등록 목록을 세대별 고정 트래커 배열(Scope/Exert 각각 16개)로 교체했다. 기존 전체 ID 반환 및 CAS 종료 처리는 유지한다. `TrackerScalabilityTests` 9개 사례는 기본 세션의 2만 컨텍스트·48스레드에서의 트래커 재사용, 6만 객체 Write-only churn 후 힙 잔류, 공유 트래커의 동시 등록·재사용, 등록을 정지시킨 상태의 Dispose/ForceReset과 새 세대 슬롯 보호를 검증한다.

Release 전체 **218/218**, 잠금 핸들 풀 비교 구성 전체 **218/218**, Debug의 새 회귀 테스트 **9/9**가 통과했다. 힙 테스트는 NoInlining 헬퍼와 GC → Subject 정리 → GC로 저장소 잔류와 트래커 누적을 구분하며, 2만/4만/6만 객체 시점의 기준 대비 증가량은 각각 0바이트였다. 솔루션(샘플 포함) Release 빌드는 `-m:1 -p:UseSharedCompilation=false`에서 경고·오류 0개로 통과했다.

별도 프로세스 벤치마크는 변경 전후를 번갈아 실행했다. 30만 객체 후 트래커는 60만 개에서 32개로 줄었고, Subject 정리 후 힙 증가량은 220,804,472바이트에서 1,400바이트로 줄었다. GC만 수행한 수치와 스코프 처리량은 [벤치마크 보고서](../../benchmarks/README.md)에 함께 기록했다.
