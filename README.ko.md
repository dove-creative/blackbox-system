# Blackbox System

[English](README.md)

Blackbox는 객체별 활동, 실행 스코프, 객체 간 상호작용을 기록하고 연결된 로그로 다시 읽기 위한 추적 프레임워크이다.

한 객체가 어떤 과정을 거쳤고 그 과정에서 어떤 객체와 연결되었는지를 함께 남긴다. 이 저장소는 같은 기록 개념을 C#과 TypeScript로 제공하며, 각 구현의 빌드·테스트·패키지를 독립적으로 관리한다.

<img src="docs/Images/blackbox-html-export.png" alt="Blackbox HTML export preview" width="900">

## 시작하기 전에

Blackbox는 BlackThunder 프로젝트군의 기반 추적 프레임워크이다.

사용 중 불편한 점이나 개선할 부분은 [Discussions](https://github.com/dove-creative/blackbox-system/discussions) 또는 vkdl4062@gmail.com으로 알려 주세요. Blackbox를 사용해 주셔서 감사합니다.

## 주요 기능

- 객체별 기록: 객체의 활동과 상태 변화 과정을 개별 기록으로 보관한다.
- 스코프 기록: 처리 구간의 시작과 끝을 연결해 실행 흐름을 읽는다.
- 상호작용과 태그: 객체 간 호출 관계와 관련 대상을 기록에 연결한다.
- 세션과 런타임 스위치: 기록 수명을 분리하고 필요한 때 기록을 비활성화한다.
- TXT·HTML 출력: 연결된 기록과 오류 상황을 읽기 좋은 결과로 만든다.

## 언어별 시작 안내

| 구현 | 실행 환경 | 패키지 | 시작 안내 |
| --- | --- | --- | --- |
| C# | .NET Standard 2.1 호환 런타임 | `BlackThunder.BlackboxSystem` | [설치·사용법](csharp/README.ko.md) |
| TypeScript | Node.js 22+와 브라우저 | `@blackthunder/blackbox-system` | [설치·사용법](typescript/README.ko.md) |

설치, 최소 사용 예제, 샘플 실행과 테스트 명령은 각 구현의 시작 안내에서 확인한다. 런타임별 비동기 문맥과 출력 방식은 해당 구현의 계약을 따른다.

## 저장소 구조

```text
csharp/        C# 소스, 테스트, 샘플, 벤치마크와 NuGet 설정
typescript/    TypeScript 소스, 테스트, 샘플과 npm 설정
docs/          C# 구현 문서, 다이어그램과 기존 검증 기록
guidelines/    공통 코드·문서 지침
```

C#과 TypeScript의 패키지 버전과 배포는 독립적으로 관리한다. 공유 지침 초기화와 개발·검증 방법은 [기여 지침](CONTRIBUTING.md)을 따른다.

## 문서

- [C# 개요](docs/Wiki/01-Overview.md): 목적과 주요 기록 구조
- [C# 사용법](docs/Wiki/04-Usage.md): 설정과 대표 사용 흐름
- [C# 세션과 기록 계약](docs/Wiki/05-Sessions.md): 수명, 비동기 문맥과 출력
- [C# 검증 기록](docs/Tests/03-Results.md): 기존 구현 단계별 검증 결과
- [TypeScript 안내](typescript/README.ko.md): Node.js·브라우저 사용법과 검증
- [TypeScript 초기 검증 기록](typescript/docs/verification.md): 플랫폼별 결과와 확인 범위

## 라이선스

Blackbox는 MIT 라이선스로 배포된다. 자세한 내용은 [LICENSE.md](LICENSE.md)를 참고한다.
