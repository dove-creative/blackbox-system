# 목차

0. Blackbox 호출 줄바꿈 기준
1. 시작 전에 확인할 것
2. 가장 짧은 시작 흐름
3. 메서드 선택 기준
4. 튜토리얼 1. 한 객체의 상태 변화 기록하기
5. 튜토리얼 2. 객체 간 상호작용 추적하기
6. 출력과 결과 읽기

이 문서는 Blackbox의 내부 구현 구조를 설명하는 문서가 아니라, 앞의 개요와 구현 문서에서 정리한 기록 모델을 실제 코드에서 사용하는 흐름으로 옮기는 문서이다.

따라서 설명 순서는 내부 객체 구조가 아니라, 사용자가 먼저 확인해야 할 설정, 가장 짧은 기록 흐름, 메서드 선택 기준, 실제 기록 시나리오 순서를 따른다.

## 0. Blackbox 호출 줄바꿈 기준

Blackbox 호출은 도메인 로직 자체라기보다, 이미 존재하는 도메인 호출이나 오류 메시지에 기록 의미를 덧붙이는 표식에 가깝다.

다른 객체와 연결되는 순간만 남기고 싶을 때는 `ExertMessage(...)`를 사용한다. 반대로 호출 구간을 이어 읽고 싶을 때는 `Exert(...)`가 반환하는 `ExertHandle`을 `using`과 함께 닫아, 받는 쪽의 이어지는 스코프와 병합을 시도한다.

### 0-1. `Exert(...)`는 using 표식처럼 붙일 수 있다

`Exert(...)`는 상대 객체와의 호출 구간을 열고 `ExertHandle`을 반환한다. 이 구간이 한 문장뿐이라면, `using`을 Blackbox 전용 표식처럼 바로 다음 문장에 붙여 쓸 수 있다.

```csharp
using (_bb.Exert(WorkInternal, "Initialize"))
WorkInternal.Initialize(owner, blackboard);
```

이 표기는 다음처럼 읽힌다.

```csharp
[Exert: WorkInternal / Initialize]
WorkInternal.Initialize(owner, blackboard);
```

호출 결과를 변수나 프로퍼티에 넣어야 할 때도 도메인 문장은 자기 문장으로 남긴다.

```csharp
using (_bb.Exert(WorkInternal, $"Start(Work), input: {input}"))
input = WorkInternal.Start(input);
```

이 표기는 `Exert(...)`와 한 문장 호출 구간에만 사용한다. 일반 `using`, `Scope`, 여러 문장을 실행하는 블록에는 확장하지 않는다.

### 0-2. 한 줄 상호작용 기록만 필요하면 ExertMessage를 쓴다

호출 구간을 열 필요 없이 객체 간 상호작용 자체만 남기고 싶다면 `ExertMessage(...)`를 사용한다. 이 방식은 `ExertHandle`을 만들지 않고 상호작용 로그만 남기므로, 뒤따르는 받는 쪽 스코프와 자동 병합되지 않는다.

```csharp
public void Attack(Monster monster)
{
    BlackboxHandle.Of(this).ExertMessage(monster, $"Attack Monster (HP={HP}, Power={Power})");
    monster.TakeDamage(Power);
}
```

### 0-3. `WriteError(...)`는 오류 메시지에 붙는 표식처럼 쓴다

예외를 던지는 코드에서 `WriteError(...)`는 오류 메시지를 만드는 도메인 문장을 대체하는 것이 아니라, 그 메시지를 Blackbox에 함께 기록하는 래퍼이다.

`WriteError(...)`는 오류 메시지를 기록하고 같은 문자열을 반환한다. 처음 소개할 때는 메시지를 먼저 지역 변수로 받은 뒤 예외에 넘기는 형태가 가장 읽기 쉽다. 이 방식은 오류 기록과 예외 throw를 두 문장으로 나누므로 기록 호출이 도메인 흐름 안에 조금 더 드러나지만, 반환값의 의미를 분명하게 보여준다.

```csharp
if (child == null)
{
    var message = _bb.WriteError("child cannot be null.");
    throw new ArgumentNullException(nameof(child), message);
}
```

이렇게 쓰면 호출은 다음처럼 읽힌다.

```text
[WriteError]
child cannot be null.
```

메시지가 짧고 흐름이 충분히 명확하면 `throw` 인자 안에서 바로 축약할 수도 있다.

```csharp
if (child == null)
    throw new ArgumentNullException(
        nameof(child),
        _bb.WriteError("child cannot be null."));
```

축약형은 한 줄 오류 메시지를 `WriteError(...)`로 감쌀 때만 사용한다. 메시지가 여러 줄이거나 계산식이 길어지면 일반 들여쓰기를 사용하거나, 메시지를 지역 변수로 먼저 분리한다.

### 0-4. 오류 기록에는 관련 대상을 함께 붙일 수 있다

오류 메시지만 남기면 어떤 객체 때문에 문제가 생겼는지 다시 찾아야 할 때가 있다. `WriteError(...)`의 두 번째 인자에 맥락 이름과 대상 객체를 넘기면, 오류 로그 안에 관련 대상이 함께 표시된다.

```csharp
if (child == null)
    throw new ArgumentNullException(
        nameof(child),
        _bb.WriteError(
            "child cannot be null.",
            ("parent", this)));
```

대상이 여러 개라면 `ErrorContainer`를 명시적으로 만들 수 있다.

```csharp
_bb.WriteError(
    "route link failed.",
    new BlackboxHandle.ErrorContainer(
        ("from", fromNode),
        ("to", toNode)));
```

이 기록은 예외 제어 흐름을 바꾸기 위한 기능이 아니다. 오류 메시지를 반환하면서 같은 로그 안에 관련 객체 참조를 남겨, 출력 결과를 읽을 때 원인 후보를 바로 따라갈 수 있게 하는 용도이다.

## 1. 시작 전에 확인할 것

### 1-1. UseBlackbox 설정을 확인한다

`BlackboxHandle`의 주요 기록 메서드는 `UseBlackbox` 설정을 기준으로 실제 기록 여부를 결정한다. 이 값이 꺼져 있으면 `BlackboxHandle.Of(subject)`는 유효한 핸들을 만들지 않고, 기록 호출은 로그를 남기지 않거나 원래 메시지를 그대로 반환한다.

즉 '코드는 넣었는데 로그가 안 남는다'면, 먼저 `BlackboxHandle.UseBlackbox`가 꺼져 있지 않은지 확인하는 편이 좋다.

Unity에서 `BLACKBOX` 심볼은 `BlackboxHandle.UseBlackbox`의 시작 기본값만 정한다. 심볼 유무와 관계없이 동일한 런타임 API를 사용하며, 이 심볼이 없으면 Unity는 기록이 꺼진 상태에서 시작한다. 기록을 활성화하려면 Player Settings의 Scripting Define Symbols에 `BLACKBOX`를 추가하거나 실행 초기에 `BlackboxHandle.UseBlackbox = true`를 설정한다.

네이티브 C#처럼 Unity 심볼이 없는 환경에서는 기본값이 `true`이다. 런타임으로 기록만 잠시 끄고 싶다면 `BlackboxHandle.UseBlackbox = false`를 설정하거나 `Configure(..., useBlackbox: UseBlackboxOption.DoNotUse)`를 사용한다.

### 1-2. 로그 출력 위치를 먼저 정한다

가장 먼저 할 일은 출력 결과가 저장될 경로를 정하는 것이다. 예시 코드에서는 `LogDirectory`를 직접 지정한다.

```csharp
using BlackThunder.BlackboxSystem;

BlackboxHandle.LogDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
```

설정을 한 번에 묶고 싶다면 `Configure(...)`를 써도 된다.

```csharp
BlackboxHandle.Configure(
    logDirectory: Path.Combine(AppContext.BaseDirectory, "Logs"),
    logger: Console.WriteLine);
```

`LogDirectory`만 지정해도 출력 자체는 가능하지만, 실행 중 일반 로그나 경고 로그를 콘솔로 함께 보고 싶다면 로거도 같이 넘기는 편이 편하다.

### 1-3. 시작점은 항상 BlackboxHandle.Of(this)이다

사용 코드에서 `Blackbox`를 직접 다루지는 않는다. 외부 코드는 대상 객체에 대한 핸들을 얻고, 그 핸들로 기록 메서드를 호출한다.

```csharp
BlackboxHandle.Of(this).Write("상태 갱신");
```

혹은

```csharp
var bb = BlackboxHandle.Of(this);
bb.Write("상태 갱신");
```

이 진입점 하나만 기억해도 대부분의 사용 흐름을 따라갈 수 있다.

### 1-4. 관련 대상 표시 정책은 필요할 때만 조정한다

처음에는 기본 설정을 그대로 두어도 된다. `Write(...).With(...)`나 `Scope(...).With(...)`를 자주 사용하고, 대상 쪽 로그에 원본 정보를 어느 정도 보여줄지 조정하고 싶을 때 `TagTargetTypes`를 설정한다.

기본 설정을 그대로 쓰면 원본 이름, 상호작용 id, 메시지를 함께 남긴다. 더 짧게 남기고 싶다면 시작 설정에서 정책을 줄일 수 있다.

```csharp
BlackboxHandle.Configure(
    logDirectory: Path.Combine(AppContext.BaseDirectory, "Logs"),
    logger: Console.WriteLine,
    tagTargetTypes: TargetTypes.Name | TargetTypes.InteractionId);
```

이 설정은 대상 쪽에 남는 태그 로그의 표시 범위를 정한다. 원본 로그 자체에는 `.With(...)`로 넘긴 대상 참조가 남는다.

## 2. 가장 짧은 시작 흐름

가장 단순한 사용 흐름은 아래 네 단계이다.

1. 로그 출력 경로를 정한다.
2. 기록할 객체 안에서 `BlackboxHandle.Of(this)`를 호출한다.
3. `Write`로 상태 변화를 남기고, 필요한 경우 `Scope`로 처리 구간을 묶는다.
4. 출력할 때 `Export()`로 결과를 뽑는다.

```csharp
BlackboxHandle.LogDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");

var player = new Player();
player.DrinkPotion(potion);

BlackboxHandle.Of(player).Export();
```

이 흐름만으로도 '한 객체가 어떤 순서로 상태를 바꾸었는가'는 바로 추적할 수 있다.

## 3. 메서드 선택 기준

튜토리얼을 읽기 전에, 어떤 메서드를 어떤 상황에 쓰는지 먼저 잡아 두면 코드가 훨씬 빨리 읽힌다.

| 메서드 | 쓰는 상황 | 예시 |
| --- | --- | --- |
| `Construct(...)` | 생성자에서 생성 스코프를 열고, 필요하면 핸들을 보관하고 싶을 때 | 객체 생성 기준점, 반복 호출 축약 |
| `Write(...)` | 한 줄 상태 변화나 메모를 남길 때 | 값 변경, 분기 통과, 예외 상황 기록 |
| `Write(...).With(...)` | 한 줄 기록에 관련 객체를 함께 표시하고 싶을 때 | 오류 원인 객체, 계산에 사용한 입력 객체 |
| `Scope(...)` | `Write(...)`에 스코프 열기를 더해 메서드별 처리 단계를 묶고 싶을 때 | 생성, 계산, 처리 단계 |
| `Scope(...).With(...)` | 처리 구간 자체에 관련 객체를 붙이고 싶을 때 | 특정 대상을 처리하는 스코프 |
| `ExertMessage(...)` | 다른 객체와의 연결점만 한 줄로 남길 때 | 요청 전송, 알림, 단순 연결 기록 |
| `Exert(...)` | 다른 객체와의 호출 구간을 열고, `using` 종료 시 받는 쪽 처리 스코프와 병합을 시도할 때 | 위임, 처리 스코프 병합 |
| `WriteError(...)` | 오류 메시지를 남기고 관련 대상을 함께 묶고 싶을 때 | 예외 메시지, null 대상, `CrashExport(...)` 직전 기록 |
| `Export(...)` | 특정 객체를 중심으로 연결된 로그를 파일로 출력하고 싶을 때 | 실행 종료 후 분석 |

실전에서는 아래처럼 생각하면 편하다.

- 생성 시점을 남기고 싶으면 `Construct`
- 내 객체 안에서 일어난 한 줄 변화는 `Write`
- 한 줄 기록에 관련 객체만 붙이고 싶으면 `Write(...).With(...)`
- 한 줄 기록에 실행 범위까지 붙이고 싶으면 `Scope`
- 처리 구간 자체에 관련 객체를 붙이고 싶으면 `Scope(...).With(...)`
- 다른 객체와 연결되는 순간만 남기면 `ExertMessage`
- 연결 기록에 호출 구간까지 붙이고 싶으면 `using`과 `Exert`
- 로그를 파일로 출력할 때는 `Export`

## 4. 튜토리얼 1. 한 객체의 상태 변화 기록하기

첫 번째 단계는 `Scenario_1`처럼 한 객체 내부 상태 변화를 기록하는 것이다. 이 단계에서는 상호작용보다 '내 객체 안에서 무슨 일이 일어났는가'를 읽는 데 집중한다.

### 4-1. 생성 시점 기록

생성자는 이후 상태 해석의 기준점이 되므로, 초기값을 남겨 두면 로그를 읽기가 쉬워진다. 이때 `Construct("메시지", out handle)`로 생성 스코프를 열고, 같은 생성 흐름 안에서 사용할 핸들을 함께 받을 수 있다.

```csharp
public Player()
{
    using var _ = BlackboxHandle.Of(this).Construct("초기값 설정", out var bb);

    HP = 10;
    Power = 1;

    bb.Write($"HP = {HP}, Power = {Power}");
}
```

`Construct(...)`는 `[Ctor: ...]` 형태의 생성 스코프를 열고 `ScopeHandle`을 반환한다. 반환된 스코프는 `using` 또는 `Dispose()`로 닫아야 하며, `out`으로 받은 핸들은 같은 객체의 이후 기록을 반복 조회 없이 이어 쓰는 데 사용할 수 있다.

예시에서 블록형 `using (...) { ... }` 대신 `using var _ = ...;`를 쓰는 이유는, 보통 함수 하나가 Blackbox 스코프 하나에 대응되기 때문이다. 이 경우 함수 본문 전체가 이미 같은 기록 범위이므로 별도 중괄호로 한 번 더 감싸기보다, 선언형 `using`으로 스코프를 열어 두는 편이 도메인 흐름과 가독성을 덜 해친다. 이는 개요에서 말한 것처럼 기존 코드의 제어 흐름에 최대한 적게 간섭하려는 원칙과도 맞다.

기록 호출이 많은 클래스라면 반환된 핸들을 필드에 보관해 반복 호출을 줄일 수 있다. 이 방식은 필수 구조가 아니라, 코드가 지나치게 길어질 때 선택하는 편의 패턴이다.

```csharp
private readonly BlackboxHandle _bb;

public Player()
{
    using var _ = BlackboxHandle.Of(this).Construct("초기값 설정", out var bb);
    _bb = bb;
}
```

### 4-2. 상태 변경은 먼저 Write로 남긴다

값을 실제로 바꾸기 직전에 이전 값과 이후 값을 함께 남기면, 나중에 출력 결과를 읽을 때 흐름을 바로 따라갈 수 있다.

```csharp
public void DrinkPotion(Potion potion)
{
    BlackboxHandle.Of(this).Write($"HP 증가, {HP} -> {HP + potion.HPBonus}");
    HP += potion.HPBonus;

    BlackboxHandle.Of(this).Write($"Power 증가, {Power} -> {Power + potion.PowerBonus}");
    Power += potion.PowerBonus;
}
```

이 형태가 가장 기본적인 기록 방식이다. 기존 상태 변경 코드 앞에 기록 한 줄을 추가하므로, 원래 코드의 흐름을 크게 바꾸지 않는다.

### 4-2-1. 한 줄 기록에 관련 대상 붙이기

상태 변화 자체는 내 객체의 기록이지만, 그 변화의 원인이 된 객체를 함께 남기고 싶을 때가 있다. 이때는 `Write(...)` 뒤에 `.With(...)`를 바로 붙인다.

```csharp
public void DrinkPotion(Potion potion)
{
    BlackboxHandle.Of(this)
        .Write($"HP 증가, {HP} -> {HP + potion.HPBonus}, potion: %0")
        .With(potion);

    HP += potion.HPBonus;
}
```

`%0`은 `.With(...)`의 첫 번째 대상을 표시할 자리이다. 대상이 둘 이상이면 `%1`, `%2`처럼 이어서 쓸 수 있다.

```csharp
BlackboxHandle.Of(this)
    .Write("장비 교체, old: %0, next: %1")
    .With(oldWeapon, newWeapon);
```

이 방식은 `ExertMessage(...)`와 다르다. `ExertMessage(...)`는 객체 간 호출 관계를 양쪽 로그에 남기는 기록이고, `.With(...)`는 한 줄 기록 안에 관련 객체를 덧붙이는 보조 연결이다. 실제로 다른 객체에게 실행을 넘기는 흐름이면 `ExertMessage(...)`나 `Exert(...)`를 쓰고, 한 줄 기록의 참고 대상만 남기려면 `.With(...)`를 쓴다.

대상이 없다는 사실 자체를 남겨야 할 때는 `.With(null)`을 사용한다. `.With()`는 대상을 붙이지 않는 호출이고, `.With(null)`은 null 대상 하나를 붙이는 호출이다.

```csharp
BlackboxHandle.Of(this)
    .Write("대상 없음, target: %0")
    .With(null);
```

### 4-3. 처리 구간까지 보고 싶으면 Scope로 묶는다

한 줄 기록만으로 부족하고, 메서드 전체가 하나의 처리 단계로 보였으면 할 때 `Scope(...)`를 사용한다. `Scope(...)`는 `Write(...)`에 스코프 열기 기능이 더해진 형태로 이해하면 된다.

일반적으로 `Scope(...)`는 메서드별 처리 단계를 묶는 데 사용한다. 메서드 안에서 일어나는 세부 변화는 그 안에 `Write(...)`나 `Exert(...)`로 추가한다.

주요 메서드가 호출되었다면, 특별한 이유가 없는 한 메서드 시작 지점에서 먼저 스코프를 여는 것을 원칙으로 한다. 이렇게 해야 메서드가 호출되었다는 사실과, 초반 조건 때문에 바로 종료되었는지도 함께 남는다.

좋지 않은 예는 조건 검사를 먼저 통과한 경우에만 스코프가 남는 형태이다.

```csharp
public void Foo()
{
    if (!isAlive) return;
    using var _ = BlackboxHandle.Of(this).Scope("Foo");
}
```

이 경우 `isAlive`가 `false`였다는 사실보다 더 큰 문제가 생긴다. `Foo()`가 호출되었는지 자체가 로그에 남지 않는다.

좋은 예는 먼저 스코프를 열고, 초반 판단에 필요한 조건값을 스코프 메시지에 함께 넣는 형태이다.

```csharp
public void Foo()
{
    using var _ = BlackboxHandle.Of(this).Scope("Foo, isAlive: " + isAlive);
    if (!isAlive) return;
}
```

이렇게 하면 메서드 호출, 조건값, 초기 반환 여부를 모두 같은 처리 단계 안에서 읽을 수 있다.

```csharp
public void DrinkPotion(Potion potion)
{
    using var _ = BlackboxHandle.Of(this).Scope("포션 마심");

    BlackboxHandle.Of(this).Write($"HP 증가, {HP} -> {HP + potion.HPBonus}");
    HP += potion.HPBonus;

    BlackboxHandle.Of(this).Write($"Power 증가, {Power} -> {Power + potion.PowerBonus}");
    Power += potion.PowerBonus;
}
```

이 코드는 '포션 마심'이라는 스코프를 열고, 블록이 끝날 때 자동으로 닫는다. `using` 패턴을 쓰는 이유는 스코프 종료를 사람이 직접 맞추지 않아도 되게 만들기 위해서이다.

스코프 자체가 특정 대상을 처리하는 구간이라면 `Scope(...).With(...)`를 사용할 수 있다. 이때 `.With(...)`는 스코프 열기 로그에 대상을 붙이고, 반환값은 계속 같은 스코프 핸들이므로 `using var`로 생명주기를 그대로 소유할 수 있다.

```csharp
public void ApplyPotion(Potion potion)
{
    using var _ = BlackboxHandle.Of(this)
        .Scope("포션 적용, potion: %0")
        .With(potion);

    HP += potion.HPBonus;
    Power += potion.PowerBonus;
}
```

### 4-4. 스코프는 열었던 순서의 반대로 닫는다

스코프는 기본적으로 열었던 순서의 반대로 닫히는 것이 가장 읽기 좋다.

```text
open outer
  open inner
  close inner
close outer
```

다만 Blackbox 기록을 위해 도메인 코드의 들여쓰기나 블록 구조를 바꾸지는 않는다. 메서드 전체를 하나의 처리 구간으로 남길 때는 `using var`를 메서드 시작 지점에 두면 된다. 이 방식은 새 블록을 만들지 않으므로 기존 도메인 코드의 레벨을 바꾸지 않는다.

```csharp
public void Process()
{
    using var _ = BlackboxHandle.Of(this).Scope("Process");

    Validate();
    Execute();
}
```

기존 호출 코드를 람다 안에 넣지 않고 호출한 쪽 스코프와 받는 쪽 흐름을 이어 읽고 싶다면 `Exert(...)`를 `using`과 함께 사용한다. 이 방식은 메서드 안에 `Scope(...)`를 여러 개 두기 위한 용도가 아니라, 객체 간 호출 범위를 기존 코드 레벨 그대로 남기기 위한 용도이다.

```csharp
public void Attack(Monster monster)
{
    using var _ = BlackboxHandle.Of(this).Scope("Attack");

    using (BlackboxHandle.Of(this).Exert(monster, $"Attack Monster (Power={Power})"))
    monster.TakeDamage(Power);
}
```

호출 구간이 여러 문장으로 길어지면 `using` 블록을 열고 그 안에 구간을 둔다. 함수 전체가 한 객체와의 상호작용으로 읽힌다면 `using var _ = BlackboxHandle.Of(this).Exert(...)`를 함수 시작 지점에 둘 수도 있다.

만약 부모 스코프 핸들이 자식 스코프보다 먼저 `Dispose()`되면, Blackbox는 열린 자식 스코프를 먼저 자동으로 닫고 그 다음 부모 스코프를 닫는다. 이때 로그 구조가 열린 채 매달리지 않도록 정리되지만, 종료 순서가 자연스럽지 않았다는 경고는 남긴다.

```csharp
var outer = BlackboxHandle.Of(this).Scope("Outer");
var inner = BlackboxHandle.Of(this).Scope("Inner");

outer.Dispose(); // Inner가 먼저 자동으로 닫히고, 그 다음 Outer가 닫힌다. 경고도 남는다.
inner.Dispose(); // 이미 자동으로 닫혔으므로 중복 닫기 로그는 남지 않는다.
```

이 동작은 로그 파일을 깨끗하게 유지하기 위한 보정이다. 의도적으로 이 흐름에 의존하기보다, 직접 `Dispose()`를 호출해야 하는 경우에는 가능하면 자식 스코프부터 닫는 편이 좋다.

### 4-5. 출력할 때 중심 객체를 선택한다

`Scenario_1`은 `player` 하나의 상태 변화를 보는 예제이므로, 출력할 때도 그 객체를 중심으로 `Export(...)`를 호출한다.

```csharp
var player = new Player();

foreach (var potion in GetPotions())
    player.DrinkPotion(potion);

BlackboxHandle.Of(player).Export();
```

이 단계에서 얻는 것은 '한 객체의 이력'이다. 아직 다른 객체와의 연결은 크지 않지만, 상태 변화의 순서와 문맥은 충분히 읽을 수 있다.

## 5. 튜토리얼 2. 객체 간 상호작용 추적하기

두 번째 단계는 `Scenario_2`처럼 여러 객체가 얽힌 흐름을 읽는 것이다. Blackbox가 특히 유용해지는 지점도 여기이다.

이 시나리오의 핵심 질문은 이런 형태이다.

- 왜 몬스터의 최종 공격력이 예상과 다르게 보였는가
- 어떤 객체가 누구를 호출했고, 그 결과 상태가 어떻게 이어졌는가

이런 질문은 단일 객체 로그만으로는 답하기 어렵고, 상호작용 기록이 필요하다.

### 5-1. 다른 객체와 연결되는 순간만 남기려면 ExertMessage를 쓴다

`Player.Attack(Monster monster)`는 플레이어가 몬스터에게 영향을 주는 호출이다. 가장 가벼운 기록 방식은 호출 직전에 `ExertMessage(...)`로 연결점만 남기는 것이다.

```csharp
public void Attack(Monster monster)
{
    BlackboxHandle.Of(this).ExertMessage(monster, $"Attack Monster (HP={HP}, Power={Power})");
    monster.TakeDamage(Power);
}
```

이 기록은 호출 관계 자체를 남긴다.

- 플레이어 쪽 로그에는 '누구에게 공격을 보냈는가'가 남는다.
- 몬스터 쪽 로그에도 같은 상호작용 번호를 가진 대응 기록이 남는다.
- 기존 호출 코드는 별도 디버그 스코프 안으로 들어가지 않는다.
- `ExertHandle`을 만들지 않으므로 뒤따르는 몬스터 처리 스코프와 병합하지 않는다.

### 5-2. 한 문장 호출 구간은 Exert using 표식으로 붙인다

호출 관계뿐 아니라 '이 호출 구간 전체'를 하나의 처리 범위로 보고 싶을 때 `Exert(...)`를 `using`과 함께 사용한다. `Exert(...)`는 상호작용 로그를 먼저 남기고 `ExertHandle`을 반환한다.

이때 받는 쪽이 `using` 구간 안에서 `Scope(...)`를 열면, 핸들이 닫히는 시점에 Blackbox는 그 처리 스코프를 직전 상호작용과 병합할 수 있다. 반대로 연결점만 남길 때는 `ExertMessage(...)`를 사용하여 한 줄 상호작용 기록으로 끝낸다.

```csharp
public void Attack(Monster monster)
{
    using (BlackboxHandle.Of(this).Exert(monster, $"Attack Monster (HP={HP}, Power={Power})"))
    monster.TakeDamage(Power);
}
```

이 방식은 호출 구간을 명시적인 기록 범위로 만든다. 따라서 단순히 연결점만 남기고 싶을 때는 `ExertMessage(...)`를 쓰고, 호출 구간 안에서 이어지는 받는 쪽 처리까지 함께 읽고 싶을 때 `Exert(...)`를 `using`과 함께 닫는다.

### 5-3. 긴 호출 구간은 Exert로 직접 연다

호출 구간이 여러 문장이고 지역 변수나 중간 `return`이 필요하다면 `Exert(...)`를 사용한다.

```csharp
public SegmentView Connect(SegmentView view)
{
    using (BlackboxHandle.Of(this).Exert(view.SegmentSource, "Connect"))
    {
        var length = view.Length;

        view.SegmentSource.EventHub.Connect(this)
            .OnUpdated((int)GeoEvents.Shape, OnShapeUpdated)
            .OnDisposed(Dispose);

        Length += view.Length;
        return view;
    }
}
```

함수 안의 주요 흐름이 하나의 상대 객체와 상호작용하는 일로 충분히 좁혀져 있다면, 별도 `using` 블록 대신 `using var _ = Exert(...)`을 사용할 수 있다. 이 방식은 함수의 도메인 흐름을 한 단계 더 들여쓰지 않으면서도, 함수가 끝날 때까지 호출 구간을 유지한다.

```csharp
public SegmentView Connect(SegmentView view)
{
    using var _ = BlackboxHandle.Of(this).Exert(view.SegmentSource, "Connect");

    var length = view.Length;

    view.SegmentSource.EventHub.Connect(this)
        .OnUpdated((int)GeoEvents.Shape, OnShapeUpdated)
        .OnDisposed(Dispose);

    Length += view.Length;
    return view;
}
```

`Exert(...)`은 `ExertHandle`을 반환한다. 따라서 짧은 한 줄 작업은 `using (_bb.Exert(...))` 표식으로 남기고, 블록 자체가 호출 구간으로 읽혀야 할 때는 `using` 블록을 사용한다. 함수 전체가 하나의 상호작용 구간으로 읽힐 때는 `using var _`가 더 자연스럽다.

### 5-4. 병합 여부는 Exert 핸들을 닫는 시점으로 정한다

보내는 쪽 상호작용과 받는 쪽의 첫 처리 스코프를 출력 결과에서 더 강하게 이어 읽고 싶다면, 받는 쪽 호출을 `Exert(...)`의 `using` 구간 안에 둔다.

```csharp
public void Attack(Monster monster)
{
    using (BlackboxHandle.Of(this).Exert(monster, $"Attack Monster (HP={HP}, Power={Power})"))
    monster.TakeDamage(Power);
}
```

이 방식에서는 기존 호출 코드를 델리게이트 안에 넣지 않는다. `using`은 Blackbox 기록 표식처럼 붙고, 실제 도메인 호출은 자기 문장으로 남는다.

`Exert(...)`은 양쪽 로그에 같은 `InteractionId`를 남긴다. 그리고 반환된 핸들이 닫힐 때, 받는 쪽 `LogContext`에서 같은 상호작용 직후 열린 스코프가 있으면 그 스코프에 상호작용 정보를 병합한다. 그래서 받는 쪽이 `Exert(...)` 구간 실행 중 바로 `Scope(...)`를 열면, 출력 결과에서 두 흐름을 더 가까운 하나의 처리 흐름처럼 읽을 수 있다.

단, 이 병합은 받는 쪽 상호작용 로그가 기록된 대상 문맥 안에서 스코프 열기가 이어질 때의 동작이다. `Task.Run` 같은 비동기 실행으로 받는 쪽 처리 스코프가 다른 스레드 문맥에 기록되면 두 문맥의 로그를 조합해서 병합하지 않는 것이 정책이다.

중요한 점은 이 병합이 무조건 일어나지 않는다는 것이다. `Exert(...)` 핸들이 닫힌 뒤 받는 쪽이 새 스코프를 열면, Blackbox는 두 기록을 뒤늦게 합치지 않는다. 이미 끝난 호출 범위를 나중 작업과 섞지 않기 위한 규칙이다.

### 5-5. 받는 쪽은 자기 내부 변화를 계속 Scope로 남긴다

상호작용을 받았다고 해서 받는 쪽 코드가 특별한 형식으로만 작성되는 것은 아니다. 실제 상태 변경은 여전히 자기 객체의 스코프와 `Write(...)`로 남긴다.

보내는 쪽에서 `Exert(...)`를 사용했고 그 핸들이 아직 닫히기 전에 받는 쪽에서 `Scope(...)`를 열면, Blackbox는 받는 쪽의 첫 처리 스코프를 직전 상호작용 기록과 병합할 수 있다. 따라서 받는 쪽 코드는 자기 처리 단계를 그대로 `Scope(...)`로 표현하면 된다.

```csharp
public void TakeDamage(int damage)
{
    using var _ = BlackboxHandle.Of(this).Scope($"Take Damage: HP={HP}, Damage={damage}");
    var changedHP = Math.Max(HP - damage, 0);

    BlackboxHandle.Of(this).Write($"HP 감소, {HP} -> {changedHP}");
    HP = changedHP;
    if (HP <= 0)
    {
        BlackboxHandle.Of(this).Write($"Die (HP={HP})");
        IsAlive = false;
    }
}
```

즉 상호작용 로그와 내부 상태 로그는 경쟁 관계가 아니라, 서로 다른 층위의 기록이다.

- 객체 사이 연결점은 `ExertMessage...`
- 객체 사이 호출 구간은 `Exert...`
- 객체 내부 변화는 `Write...`

둘을 같이 써야 나중에 흐름이 끊기지 않는다.

### 5-6. 버그를 따라갈 때는 중심 객체 하나를 정해서 출력한다

`Scenario_2`는 여러 몬스터가 등장하지만, 출력할 때는 `player`를 기준으로 `Export(...)`를 시작한다.

```csharp
BlackboxHandle.Of(player).Export();
```

이렇게 하면 플레이어를 중심으로 연결된 몬스터 로그까지 따라갈 수 있다. 즉 출력의 기준 객체는 '문제를 가장 잘 드러내는 출발점'으로 잡는 편이 좋다.

## 6. 출력과 결과 읽기

### 6-1. 기본 출력

가장 기본적인 호출은 아래 한 줄이다.

```csharp
BlackboxHandle.Of(player).Export();
```

기본 설정에서는 `Html` 출력이 사용되며, 연결된 객체 흐름을 따라가며 읽기에 적합하다. 텍스트 파일이 더 편하면 `Txt`로 바꿀 수 있다.

```csharp
BlackboxHandle.Of(player).Export(format: ExportFormat.Txt);
```

### 6-2. 출력은 실행이 끝난 뒤 읽는 도구이다

Blackbox는 실시간 콘솔 디버거라기보다, 실행이 끝난 후 맥락을 다시 읽는 도구에 더 가깝다. 따라서 모든 메서드 안에서 즉시 화면에 보려 하기보다, 문제 구간을 충분히 기록한 뒤 필요한 시점에 출력해서 읽는 흐름이 잘 맞는다.


정리하면 Blackbox 사용 흐름은 복잡하지 않다.

1. 로그 경로를 정한다.
2. 객체 안에서 `BlackboxHandle.Of(this)`를 얻는다.
3. 생성 시점은 `Construct`, 내부 변화는 `Write`, 객체 간 연결점은 `ExertMessage`로 먼저 남긴다.
4. 한 줄 기록이나 스코프 열기 로그에 관련 객체를 붙이고 싶으면 `.With(...)`를 사용한다.
5. 자기 객체의 처리 구간은 `Scope`로 묶는다.
6. 객체 간 호출 구간을 이어 읽고 싶으면 `Exert(...)`를 `using`과 함께 사용한다.
7. 출력할 때 분석 기준이 되는 객체 하나를 골라 `Export()`한다.

구조를 먼저 이해했다면, 실제 사용 감각은 `Scenario_1`과 `Scenario_2`를 직접 따라 입력해 보는 것이 가장 빠르다. 앞의 문서가 '왜 이렇게 설계되었는가'를 설명했다면, 이 문서는 '그래서 코드에서는 어떻게 쓰는가'를 바로 연결하는 역할을 한다.
