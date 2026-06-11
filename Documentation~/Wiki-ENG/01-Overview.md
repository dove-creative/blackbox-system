# Overview

A framework for recording and tracing per-object activity, execution context, and object-to-object interaction flow.

---

# Table of Contents

1. Introducing Blackbox
2. Basic Recording Structure
3. Interaction Recording Method
4. Log Connection Structure and Output

---

# 1. Introducing Blackbox

Blackbox is a framework that traces object activity logs, execution scopes, and object-to-object interactions inside one recording system.

| Category | Content |
| --- | --- |
| Overview | A tracing framework that records per-object execution context and object-to-object interaction flow |
| Purpose | Understand recorded state changes, processing steps, and object connections in the same context while debugging |
| Implementation | Look up the `Blackbox` for the target object and call recording features through `BlackboxHandle` |

## 1-1. Problem to Solve

General logging and breakpoint-based tracing are excellent at inspecting state at a desired point, and they are effective for understanding exactly what happens at a specific moment. On the other hand, they have limits when you need to read how an object reached its current state, and what connections it made with other objects along the way. Blackbox stores internal activity, scope flow, and interactions with other objects in per-object recording storage, so it can trace both 'what process brought this object to its current state' and 'which objects were connected during that process'.

Tracing through logs/breakpoints and Blackbox-based tracing have the following characteristics.

|  | Log/Tracing | Blackbox |
| --- | --- | --- |
| Method | Inspect execution flow in real time with breakpoints | Store activity and interactions in per-object recording storage |
| Strength | Can inspect a desired point in detail | Easy to reconstruct recorded execution context and interaction flow |
| Weakness | Hard to trace previous state changes and execution context | Requires recording calls to be inserted beforehand |
| Use case | Understand exactly what happens at a specific point | Cases with many interactions or hard-to-find causes |
| Example | Inspect line-level operation, assignment, and call flow | Inspect object-level activity, scope, and interaction flow |

## 1-2. Implementation Principles

Blackbox aims to interfere as little as possible with existing code control flow, and to be easy to disable when needed. A recorded object does not need a separate contract or field. `BlackboxHandle` finds the `Blackbox` corresponding to the target object and forwards recording calls. In environments where the preprocessor symbol is off, the main recording paths should not produce actual log storage.

This principle appears in the following traits.

1. It does not force an interface.
   A recorded object should not have to implement a specific interface or inherit from a specific class.
2. It does not force logging fields into domain objects.
   Recording should be possible without adding a separate logger field or Blackbox field inside the object.
3. It can disable major recording features through a preprocessor symbol.
   Without the `BLACKBOX` symbol, major recording calls should fall back to default or fallback values and not create actual log storage.

---

# 2. Basic Recording Structure

The most basic feature of Blackbox is accumulating one object's execution context in a log storage dedicated to that object.

This chapter introduces Blackbox in the order of basic log storage first, then scope storage, interaction recording, and connected output structure on top of it.

## 2-1. Log Storage Feature

The log storage feature independently keeps each object's activity records. This makes it possible to trace, in occurrence order, what state changes and processing steps a specific object went through.

Blackbox stores 'what the object did' as log objects.

## 2-2. Scope Storage Feature

The scope storage feature is added on top of the existing log storage.

If basic log storage records an object's activity, scope storage records the start and end of an execution range together. This makes it possible to read when one processing step started and ended as a block.

Usually, one scope corresponds to one method or one meaningful processing step.

---

# 3. Interaction Recording Method

In addition to recording one object's logs, Blackbox can also record object-to-object interaction relationships bidirectionally through Exert.

This structure works by letting one object leave a connection point with another object or hand work to it, while the `Blackbox` instances of both participating objects record the interaction in both object logs. The two logs share the same interaction id, so when reading each object's individual log, the log flow can continue through the same interaction point.

Because this method leaves traces of the same flow on both the sending side and the receiving side, execution involving many objects can be followed by asking 'who handed execution to whom'.

---

# 4. Log Connection Structure and Output

Blackbox logs have a structure in which per-object records and object-to-object interaction records are connected.

## 4-1. Connection Structure of Log Objects

The log structure is read as follows.

1. Each `Blackbox` has its own log storage.
2. When an object-to-object interaction occurs, the related logs are connected by an interaction id.
3. If scope records exist, each object's internal execution ranges can also be read.

Because of this structure, Blackbox is not merely a log outputter. It is a tracing device for reading multiple objects' execution flows again while keeping them connected.

## 4-2. Log Output

Log output is not structured as a way to isolate only one target object. Instead, it keeps the relationships among objects connected by Exert interactions around that object, so they can be read together.

In the output result, the target object's log becomes the center first, and the corresponding logs of objects connected by Exert interactions continue inside the same relationship network. Thanks to this structure, users can follow execution flow from one specific object while also checking how the connected objects participated.

This kind of log output is useful in the following situations.

- When storing and reviewing an execution flow with complex interactions at once
- When preserving logs right before a crash in an error case that is hard to reproduce
- When checking logs of objects connected by Exert interactions around a specific object
