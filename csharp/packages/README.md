# 자사 테스트 패키지

테스트 프로젝트는 저장소에 포함된 UniTest NuGet 패키지를 사용한다. 소스 저장소가 없어도 로컬 피드에서 복원할 수 있다.

## 현재 버전

| 항목 | 값 |
| --- | --- |
| 패키지 | `BlackThunder.UniTest` |
| 버전 | `0.4.3` |
| 파일 | [BlackThunder.UniTest.0.4.3.nupkg](BlackThunder.UniTest.0.4.3.nupkg) |
| 원본 | [dove-creative/unitest](https://github.com/dove-creative/unitest) |
| 원본 커밋 | [9abfb0e1dbe455a22304355a56d5be870a430714](https://github.com/dove-creative/unitest/commit/9abfb0e1dbe455a22304355a56d5be870a430714) |
| SHA-256 | `133EEF305C41763F7CF17AC8B8951F50D215254DFE9CEB79BA28EE45DEC5A4CD` |
| 대상 | `netstandard2.1` |
| 원본 검증 | 2026-11-25 Release 솔루션 빌드 성공, 경고·오류 0개 |
| 소비 검증 | 빈 캐시에서 로컬 패키지 복원 및 본체·테스트 프로젝트 Release 빌드 성공, 경고·오류 0개 |

2026-11-25 갱신은 사용자 요청에 따라 빌드와 정적 검사만 수행한다. 자동 테스트와 공개 NuGet 배포는 수행하지 않는다.

## 갱신

원본 커밋을 확정한 뒤 고유한 버전으로 패키징하고 `RepositoryCommit`을 기록한다. 같은 버전의 파일을 덮어쓰지 않는다. 참조 버전과 출처를 함께 갱신하고 빈 캐시 복원과 Release 빌드를 확인한다. 검증이 끝나면 참조되지 않는 이전 패키지를 제거한다.
