# Table of Contents

0. Blackbox Call Line-Breaking Rules
1. Things to Check Before Starting
2. Shortest Start Flow
3. Method Selection Guide
4. Tutorial 1. Recording State Changes in One Object
5. Tutorial 2. Tracing Object-to-Object Interactions
6. Reading Output and Results

---

This document does not explain the internal implementation structure of Blackbox. Instead, it takes the recording model organized in the overview and implementation documents and moves it into the flow of using it in real code.

Therefore, the explanation order follows what users need to check first: settings, the shortest recording flow, method selection criteria, and actual recording scenarios.

## 0. Blackbox Call Line-Breaking Rules

A Blackbox call is closer to a marker that adds recording meaning to an existing domain call or error message than to domain logic itself.

When you only want to leave the moment of connection with another object, use `ExertMessage(...)`. Conversely, when you want to read the call range as a connected span, close the `ExertHandle` returned by `Exert(...)` with `using`, so Blackbox can try to merge it with the following receiving-side scope.

### 0-1. `Exert(...)` Can Be Attached Like a using Marker

`Exert(...)` opens a call range with the peer object and returns `ExertHandle`. If this range contains only one statement, `using` can be attached right before the next statement like a Blackbox-only marker.

```csharp
using (_bb.Exert(WorkInternal, "Initialize"))
WorkInternal.Initialize(owner, blackboard);
```

This notation reads like this.

```csharp
[Exert: WorkInternal / Initialize]
WorkInternal.Initialize(owner, blackboard);
```

Even when the call result must be assigned to a variable or property, the domain statement remains its own statement.

```csharp
using (_bb.Exert(WorkInternal, $"Start(Work), input: {input}"))
input = WorkInternal.Start(input);
```

Use this notation only for `Exert(...)` and one-statement call ranges. Do not extend it to general `using`, `Scope`, or blocks that execute multiple statements.

### 0-2. Use ExertMessage When Only a One-Line Interaction Record Is Needed

If you only want to leave the object-to-object interaction itself without opening a call range, use `ExertMessage(...)`. This method leaves only the interaction log without creating an `ExertHandle`, so it is not automatically merged with a following receiving-side scope.

```csharp
public void Attack(Monster monster)
{
    BlackboxHandle.Of(this).ExertMessage(monster, $"Attack Monster (HP={HP}, Power={Power})");
    monster.TakeDamage(Power);
}
```

### 0-3. `WriteError(...)` Is Used Like a Marker Attached to an Error Message

In code that throws exceptions, `WriteError(...)` does not replace the domain statement that creates the error message. It is a wrapper that records the same message into Blackbox.

`WriteError(...)` records the error message and returns the same string. When introducing it first, the most readable form is to receive the message into a local variable and pass it to the exception. This splits error recording and exception throw into two statements, so the recording call is slightly more visible in the domain flow, but the meaning of the return value is clear.

```csharp
if (child == null)
{
    var message = _bb.WriteError("child cannot be null.");
    throw new ArgumentNullException(nameof(child), message);
}
```

With this form, the call reads like this.

```text
[WriteError]
child cannot be null.
```

If the message is short and the flow is clear enough, it can also be shortened directly inside the `throw` argument.

```csharp
if (child == null)
    throw new ArgumentNullException(
        nameof(child),
        _bb.WriteError("child cannot be null."));
```

Use the shortened form only when wrapping a one-line error message with `WriteError(...)`. If the message spans several lines or the expression becomes long, use normal indentation or split the message into a local variable first.

### 0-4. Error Records Can Include Related Targets

When only the error message is left, you may later need to search again for which object caused the problem. If you pass a context name and target object as the second argument to `WriteError(...)`, related targets are displayed inside the error log.

```csharp
if (child == null)
    throw new ArgumentNullException(
        nameof(child),
        _bb.WriteError(
            "child cannot be null.",
            ("parent", this)));
```

If there are several targets, you can explicitly create `ErrorContainer`.

```csharp
_bb.WriteError(
    "route link failed.",
    new BlackboxHandle.ErrorContainer(
        ("from", fromNode),
        ("to", toNode)));
```

This record is not meant to change exception control flow. Its purpose is to return the error message while leaving related object references in the same log, so possible causes can be followed immediately when reading the output.

## 1. Things to Check Before Starting

### 1-1. Check the UseBlackbox Setting

The main recording methods of `BlackboxHandle` decide whether to actually record based on the `UseBlackbox` setting. If this value is off, `BlackboxHandle.Of(subject)` does not create a valid handle, and recording calls either leave no log or return the original message as is.

In short, if you inserted the code but no logs appear, first check whether `BlackboxHandle.UseBlackbox` is turned off.

In Unity, the `BLACKBOX` symbol only selects the startup default for `BlackboxHandle.UseBlackbox`. The same runtime API remains available either way. If the symbol is missing, recording starts off by default. To enable recording, add `BLACKBOX` to Scripting Define Symbols in Player Settings, or set `BlackboxHandle.UseBlackbox = true` during startup.

In native C# environments without Unity symbols, the default value is `true`. If you only want to temporarily turn recording off at runtime, set `BlackboxHandle.UseBlackbox = false` or use `Configure(..., useBlackbox: UseBlackboxOption.DoNotUse)`.

### 1-2. Decide the Log Output Location First

The first thing to do is decide where output results will be saved. The example code directly assigns `LogDirectory`.

```csharp
using BlackThunder.BlackboxSystem;

BlackboxHandle.LogDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
```

If you want to configure settings together, use `Configure(...)`.

```csharp
BlackboxHandle.Configure(
    logDirectory: Path.Combine(AppContext.BaseDirectory, "Logs"),
    logger: Console.WriteLine);
```

Output itself is possible with only `LogDirectory`, but if you also want to see normal logs or warning logs in the console while running, it is convenient to pass a logger too.

### 1-3. The Starting Point Is Always BlackboxHandle.Of(this)

Usage code does not handle `Blackbox` directly. External code obtains a handle for the target object and calls recording methods through that handle.

```csharp
BlackboxHandle.Of(this).Write("State updated");
```

Or:

```csharp
var bb = BlackboxHandle.Of(this);
bb.Write("State updated");
```

Remembering this one entry point is enough to follow most usage flows.

### 1-4. Adjust Related Target Display Policy Only When Needed

At first, you can leave the default settings as they are. Configure `TagTargetTypes` when you frequently use `Write(...).With(...)` or `Scope(...).With(...)`, and want to control how much source information appears in target-side logs.

With the default setting, the source name, interaction id, and message are left together. If you want shorter logs, reduce the policy in the start settings.

```csharp
BlackboxHandle.Configure(
    logDirectory: Path.Combine(AppContext.BaseDirectory, "Logs"),
    logger: Console.WriteLine,
    tagTargetTypes: TargetTypes.Name | TargetTypes.InteractionId);
```

This setting controls the display range of tag logs left on the target side. The source log itself keeps the target references passed to `.With(...)`.

---

## 2. Shortest Start Flow

The simplest usage flow has four steps.

1. Decide the log output path.
2. Call `BlackboxHandle.Of(this)` inside the object to record.
3. Leave state changes with `Write`, and group processing ranges with `Scope` when needed.
4. Produce results with `Export()` when output is needed.

```csharp
BlackboxHandle.LogDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");

var player = new Player();
player.DrinkPotion(potion);

BlackboxHandle.Of(player).Export();
```

This flow alone can immediately trace 'in what order one object changed state'.

---

## 3. Method Selection Guide

Before reading the tutorial, understanding which method is used in which situation makes the code much faster to read.

| Method | When to use | Example |
| --- | --- | --- |
| `Construct(...)` | When opening a construction scope in a constructor and optionally keeping a handle | Object creation baseline, repeated-call shortcut |
| `Write(...)` | When leaving a one-line state change or note | Value change, branch pass, error case record |
| `Write(...).With(...)` | When displaying related objects together with a one-line record | Error-causing object, input object used for calculation |
| `Scope(...)` | When adding scope opening to `Write(...)` to group method-level processing steps | Construction, calculation, processing step |
| `Scope(...).With(...)` | When attaching related objects to the processing range itself | Scope that processes a specific target |
| `ExertMessage(...)` | When leaving only a connection point with another object as one line | Request dispatch, notification, simple connection record |
| `Exert(...)` | When opening a call range with another object and trying to merge with the receiving-side processing scope at `using` end | Delegation, processing scope merge |
| `WriteError(...)` | When leaving an error message and grouping related targets together | Exception message, null target, record before `CrashExport(...)` |
| `Export(...)` | When outputting connected logs centered on a specific object to a file | Analysis after execution |

In practice, it is convenient to think like this.

- Use `Construct` for creation points.
- Use `Write` for one-line changes inside the current object.
- Use `Write(...).With(...)` when you want to attach only related objects to a one-line record.
- Use `Scope` when you want to add an execution range to a one-line record.
- Use `Scope(...).With(...)` when you want to attach related objects to the processing range itself.
- Use `ExertMessage` when only the moment of connection with another object is needed.
- Use `using` and `Exert` when you want to add a call range to the connection record.
- Use `Export` when writing logs to a file.

---

## 4. Tutorial 1. Recording State Changes in One Object

The first step is to record state changes inside one object, as in `Scenario_1`. At this stage, focus on reading 'what happened inside my object' rather than interactions.

### 4-1. Record the Creation Point

A constructor becomes the baseline for interpreting later state, so leaving initial values makes the logs easier to read. Here, `Construct("message", out handle)` opens a construction scope and also returns the handle to use inside the same construction flow.

```csharp
public Player()
{
    using var _ = BlackboxHandle.Of(this).Construct("Set initial values", out var bb);

    HP = 10;
    Power = 1;

    bb.Write($"HP = {HP}, Power = {Power}");
}
```

`Construct(...)` opens a construction scope in the form `[Ctor: ...]` and returns `ScopeHandle`. The returned scope must be closed with `using` or `Dispose()`, and the handle received through `out` can be used to continue recording the same object without repeated lookup.

The reason the example uses `using var _ = ...;` instead of block-form `using (...) { ... }` is that one function usually corresponds to one Blackbox scope. In that case, the whole function body is already the same recording range, so opening the scope with a using declaration interferes less with domain flow and readability than wrapping the body in one more set of braces. This also matches the overview principle of interfering as little as possible with existing code control flow.

If a class has many recording calls, you can keep the returned handle in a field to reduce repeated calls. This is not a required structure; it is a convenience pattern to choose when the code becomes too long.

```csharp
private readonly BlackboxHandle _bb;

public Player()
{
    using var _ = BlackboxHandle.Of(this).Construct("Set initial values", out var bb);
    _bb = bb;
}
```

### 4-2. Leave State Changes with Write First

If you leave the previous value and next value right before actually changing the value, the output can later be read by following the flow directly.

```csharp
public void DrinkPotion(Potion potion)
{
    BlackboxHandle.Of(this).Write($"HP increased, {HP} -> {HP + potion.HPBonus}");
    HP += potion.HPBonus;

    BlackboxHandle.Of(this).Write($"Power increased, {Power} -> {Power + potion.PowerBonus}");
    Power += potion.PowerBonus;
}
```

This form is the most basic recording style. It adds one recording line before the existing state-change code, so it does not greatly change the original code flow.

### 4-2-1. Attach Related Targets to a One-Line Record

A state change itself is the current object's record, but sometimes you want to also leave the object that caused the change. In that case, attach `.With(...)` immediately after `Write(...)`.

```csharp
public void DrinkPotion(Potion potion)
{
    BlackboxHandle.Of(this)
        .Write($"HP increased, {HP} -> {HP + potion.HPBonus}, potion: %0")
        .With(potion);

    HP += potion.HPBonus;
}
```

`%0` is the place where the first target of `.With(...)` is displayed. If there are two or more targets, use `%1`, `%2`, and so on.

```csharp
BlackboxHandle.Of(this)
    .Write("Equipment changed, old: %0, next: %1")
    .With(oldWeapon, newWeapon);
```

This method differs from `ExertMessage(...)`. `ExertMessage(...)` records an object-to-object call relationship on both logs, while `.With(...)` adds related objects inside one log line as an auxiliary connection. If the flow actually hands execution to another object, use `ExertMessage(...)` or `Exert(...)`. If you only want to leave reference targets for one log line, use `.With(...)`.

When the fact that there is no target itself must be recorded, use `.With(null)`. `.With()` attaches no targets, while `.With(null)` attaches one null target.

```csharp
BlackboxHandle.Of(this)
    .Write("No target, target: %0")
    .With(null);
```

### 4-3. Use Scope When You Need to See the Processing Range

When one-line records are not enough and the whole method should appear as one processing step, use `Scope(...)`. You can understand `Scope(...)` as `Write(...)` with scope opening added.

In general, `Scope(...)` is used to group method-level processing steps. Detailed changes that happen inside the method are added with `Write(...)` or `Exert(...)`.

If a major method was called, open the scope at the start of the method unless there is a special reason not to. This records both that the method was called and whether it returned immediately because of an early condition.

A bad example is leaving a scope only when a condition check passed first.

```csharp
public void Foo()
{
    if (!isAlive) return;
    using var _ = BlackboxHandle.Of(this).Scope("Foo");
}
```

In this case, the bigger problem is not only that `isAlive` was `false`. The fact that `Foo()` was called is not left in the log.

A good example opens the scope first and includes the condition value needed for early decisions in the scope message.

```csharp
public void Foo()
{
    using var _ = BlackboxHandle.Of(this).Scope("Foo, isAlive: " + isAlive);
    if (!isAlive) return;
}
```

This lets you read the method call, condition value, and early return inside the same processing step.

```csharp
public void DrinkPotion(Potion potion)
{
    using var _ = BlackboxHandle.Of(this).Scope("Drink potion");

    BlackboxHandle.Of(this).Write($"HP increased, {HP} -> {HP + potion.HPBonus}");
    HP += potion.HPBonus;

    BlackboxHandle.Of(this).Write($"Power increased, {Power} -> {Power + potion.PowerBonus}");
    Power += potion.PowerBonus;
}
```

This code opens a scope called 'Drink potion' and closes it automatically when the block ends. The reason for using the `using` pattern is to avoid manually matching scope ends.

If the scope itself is a range that processes a specific target, use `Scope(...).With(...)`. Here, `.With(...)` attaches the target to the scope-open log, and the return value remains the same scope handle, so `using var` can keep owning the lifecycle.

```csharp
public void ApplyPotion(Potion potion)
{
    using var _ = BlackboxHandle.Of(this)
        .Scope("Apply potion, potion: %0")
        .With(potion);

    HP += potion.HPBonus;
    Power += potion.PowerBonus;
}
```

### 4-4. Close Scopes in the Reverse Order of Opening

Scopes are generally easiest to read when they close in the reverse order of opening.

```text
open outer
  open inner
  close inner
close outer
```

However, do not change domain-code indentation or block structure just for Blackbox recording. When leaving a whole method as one processing range, put `using var` at the start of the method. This does not create a new block, so it does not change the level of existing domain code.

```csharp
public void Process()
{
    using var _ = BlackboxHandle.Of(this).Scope("Process");

    Validate();
    Execute();
}
```

If you want to keep reading the caller-side scope and receiving-side flow without putting existing call code into a lambda, use `Exert(...)` with `using`. This is not for placing several `Scope(...)` calls inside a method, but for leaving an object-to-object call range at the existing code level.

```csharp
public void Attack(Monster monster)
{
    using var _ = BlackboxHandle.Of(this).Scope("Attack");

    using (BlackboxHandle.Of(this).Exert(monster, $"Attack Monster (Power={Power})"))
    monster.TakeDamage(Power);
}
```

If the call range spans several statements, open a `using` block and put the range inside it. If the whole function reads as one interaction with an object, `using var _ = BlackboxHandle.Of(this).Exert(...)` can be placed at the start of the function.

If a parent scope handle is disposed before a child scope, Blackbox first automatically closes the open child scope and then closes the parent scope. It keeps the log structure from hanging open, but leaves a warning that the close order was unnatural.

```csharp
var outer = BlackboxHandle.Of(this).Scope("Outer");
var inner = BlackboxHandle.Of(this).Scope("Inner");

outer.Dispose(); // Inner is automatically closed first, then Outer is closed. A warning is also left.
inner.Dispose(); // Already auto-closed, so no duplicate close log is left.
```

This behavior is a correction to keep log files clean. Instead of intentionally relying on it, if you must call `Dispose()` directly, close child scopes first when possible.

### 4-5. Choose the Center Object When Exporting

`Scenario_1` is an example that observes state changes in one `player`, so output also calls `Export(...)` centered on that object.

```csharp
var player = new Player();

foreach (var potion in GetPotions())
    player.DrinkPotion(potion);

BlackboxHandle.Of(player).Export();
```

At this stage, what you get is 'the history of one object'. There is not yet much connection with other objects, but the order and context of state changes can be read clearly enough.

---

## 5. Tutorial 2. Tracing Object-to-Object Interactions

The second step is to read a flow involving several objects, as in `Scenario_2`. This is where Blackbox becomes especially useful.

The key questions in this scenario look like this.

- Why did the monster's final attack power look different from expected?
- Which object called which other object, and how did state continue as a result?

These questions are hard to answer with only single-object logs, and they require interaction records.

### 5-1. Use ExertMessage When You Only Need the Moment of Connection with Another Object

`Player.Attack(Monster monster)` is a call where the player affects the monster. The lightest recording method is to leave only the connection point with `ExertMessage(...)` right before the call.

```csharp
public void Attack(Monster monster)
{
    BlackboxHandle.Of(this).ExertMessage(monster, $"Attack Monster (HP={HP}, Power={Power})");
    monster.TakeDamage(Power);
}
```

This record leaves the call relationship itself.

- The player-side log records whom the attack was sent to.
- The monster-side log also receives a corresponding record with the same interaction id.
- The existing call code does not enter a separate debug scope.
- Since no `ExertHandle` is created, it does not merge with a following monster processing scope.

### 5-2. Attach a One-Statement Call Range with an Exert using Marker

When you want to read not only the call relationship but the whole call range as one processing range, use `Exert(...)` with `using`. `Exert(...)` writes the interaction log first and returns `ExertHandle`.

If the receiving side opens `Scope(...)` inside this `using` range, Blackbox can merge that processing scope with the immediately preceding interaction when the handle closes. Conversely, when only a connection point is needed, use `ExertMessage(...)` and end with a one-line interaction record.

```csharp
public void Attack(Monster monster)
{
    using (BlackboxHandle.Of(this).Exert(monster, $"Attack Monster (HP={HP}, Power={Power})"))
    monster.TakeDamage(Power);
}
```

This method makes the call range an explicit recording range. Therefore, use `ExertMessage(...)` when you only want to leave a connection point, and close `Exert(...)` with `using` when you want to read the following receiving-side processing together.

### 5-3. Open Long Call Ranges Directly with Exert

If the call range spans several statements and needs local variables or intermediate `return`, use `Exert(...)`.

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

If the main flow inside the function is narrow enough to be read as one interaction with one peer object, you can use `using var _ = Exert(...)` instead of a separate `using` block. This keeps the call range alive until the function ends without indenting the domain flow one more level.

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

`Exert(...)` returns `ExertHandle`. Therefore, short one-line work can be left as a `using (_bb.Exert(...))` marker; when the block itself must read as the call range, use a `using` block. When the whole function reads as one interaction range, `using var _` is more natural.

### 5-4. Merge Behavior Is Decided by When the Exert Handle Closes

If you want the sending-side interaction and the receiving-side first processing scope to read more strongly connected in the output, place the receiving-side call inside the `using` range of `Exert(...)`.

```csharp
public void Attack(Monster monster)
{
    using (BlackboxHandle.Of(this).Exert(monster, $"Attack Monster (HP={HP}, Power={Power})"))
    monster.TakeDamage(Power);
}
```

This method does not put existing call code into a delegate. `using` is attached like a Blackbox recording marker, and the actual domain call remains its own statement.

`Exert(...)` leaves the same `InteractionId` on both logs. When the returned handle closes, if an open scope exists immediately after the same interaction in the receiving-side `LogContext`, Blackbox merges the interaction information into that scope. Therefore, if the receiving side opens `Scope(...)` immediately while the `Exert(...)` range is executing, the two flows can be read in the output as a closer single processing flow.

However, this merge happens only when scope opening continues inside the target context where the receiving-side interaction log was recorded. If the receiving-side processing scope is recorded in another thread context through asynchronous execution such as `Task.Run`, the policy is not to combine logs from the two contexts and merge them.

The important point is that this merge does not always happen. If the receiving side opens a new scope after the `Exert(...)` handle closes, Blackbox does not merge the two records later. This rule prevents an already-finished call range from being mixed with later work.

### 5-5. The Receiving Side Continues to Leave Internal Changes with Scope

Receiving an interaction does not force the receiving-side code into a special form. Actual state changes are still left through that object's own scopes and `Write(...)`.

If the sending side used `Exert(...)` and the receiving side opens `Scope(...)` before that handle closes, Blackbox can merge the receiving side's first processing scope with the immediately preceding interaction record. Therefore, receiving-side code can express its own processing step as `Scope(...)` as usual.

```csharp
public void TakeDamage(int damage)
{
    using var _ = BlackboxHandle.Of(this).Scope($"Take Damage: HP={HP}, Damage={damage}");
    var changedHP = Math.Max(HP - damage, 0);

    BlackboxHandle.Of(this).Write($"HP decreased, {HP} -> {changedHP}");
    HP = changedHP;
    if (HP <= 0)
    {
        BlackboxHandle.Of(this).Write($"Die (HP={HP})");
        IsAlive = false;
    }
}
```

In short, interaction logs and internal state logs are not competitors. They are records at different layers.

- Connection points between objects are `ExertMessage...`
- Call ranges between objects are `Exert...`
- Internal changes inside an object are `Write...`

Using both keeps the flow from being cut later.

### 5-6. Choose One Center Object When Following a Bug

`Scenario_2` includes several monsters, but output starts with `Export(...)` centered on `player`.

```csharp
BlackboxHandle.Of(player).Export();
```

This allows you to follow monster logs connected around the player. In other words, it is best to choose the output root object as the starting point that best reveals the problem.

---

## 6. Reading Output and Results

### 6-1. Basic Output

The most basic call is one line.

```csharp
BlackboxHandle.Of(player).Export();
```

By default, `Html` output is used, and it is suitable for following connected object flow. If a text file is more convenient, switch to `Txt`.

```csharp
BlackboxHandle.Of(player).Export(format: ExportFormat.Txt);
```

### 6-2. Output Is a Tool for Reading After Execution

Blackbox is closer to a tool for reading context again after execution than to a real-time console debugger. Therefore, instead of trying to display something immediately inside every method, it fits better to record the problem range sufficiently and then output and read it at the necessary point.


---

To summarize, the Blackbox usage flow is not complicated.

1. Decide the log path.
2. Obtain `BlackboxHandle.Of(this)` inside the object.
3. First leave creation points with `Construct`, internal changes with `Write`, and object-to-object connection points with `ExertMessage`.
4. Use `.With(...)` when you want to attach related objects to a one-line record or scope-open log.
5. Group the current object's processing range with `Scope`.
6. Use `Exert(...)` with `using` when you want to read object-to-object call ranges continuously.
7. Choose one object as the analysis root and call `Export()` when output is needed.

Once you understand the structure, the fastest way to gain a practical feel is to type through `Scenario_1` and `Scenario_2` yourself. If the previous documents explain 'why it was designed this way', this document directly connects that to 'how to use it in code'.
