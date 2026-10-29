# Changelog

This file records public changes to Blackbox.

The format follows Keep a Changelog, and version numbers follow Semantic Versioning.

## [Unreleased]

## [0.2.0] - 2026-10-29

### Added

- Added synchronous `BlackboxSession.Begin()` with ambient session/generation binding, copied settings, nested restoration and isolated crash/reset state.
- Added `BlackboxHandle.Label(string)` for an explicit, set-once display name without invoking Subject code.
- Added deterministic retirement/publication race tests, weak-reference lifetime checks and a reproducible recording benchmark.

### Changed

- Ordinary exports are repeatable and keep recording active. Crash export freezes only its own generation, with crash metadata retained separately from the ring buffers.
- Handles stay bound to their original generation. Reset invalidates all old recording handles without resetting the global slot pools. Retained handles can still export their old records.
- Replaced ConditionalWeakTable/ThreadLocal registration with CAS-published identity buckets and context lists. Published ring entries are immutable, and updates replace entries atomically.
- Removed automatic Subject `ToString()` calls. Object references now render as `Type #Id` or `Label (Type #Id)`; `OwnerString` returns the label or type name.
- Strong-reference mode now retains Subjects while their Blackboxes remain reachable; it no longer has ConditionalWeakTable ephemeron semantics.
- Export files include session keys and ordinals and use exclusive creation. Files accumulate until the caller removes them.
- Warnings are emitted once per generation and cause, including cross-thread scope/exert disposal.

### Fixed

- Prevented negative scope depth when a ring buffer has overwritten a scope's opening record.
- Prevented stale ring slots and delayed publications from being mistaken for the current logical record.
- Reclaimed abandoned handle slots during session retirement, including registration racing retirement and contexts collected before their session.
- Failed ordinary exports no longer disable recording; failed crash exports can be retried with ordinary export.

## [0.1.2] - 2026-10-15

### Added

- Added NuGet package metadata for `BlackThunder.BlackboxSystem`, including the MIT license, README, and source repository commit.
- Added an explicit local NuGet feed for pinned framework release packages and their provenance records.

### Changed

- Changed Blackbox tests to consume `BlackThunder.UniTest` `0.2.0` as a private, exact-version package dependency instead of linking sibling source files.
- Selected `ExecutionMode.Inline` for the Blackbox table project while preserving `BLACKBOX_TESTS` exporter hooks.
- Excluded generated `bin` and `obj` files from the Blackbox runtime sources compiled by its tests.
- Converted the active library, tests, samples, and documentation to a .NET-only repository layout.
- Changed the library target to `netstandard2.1` and the tests and console sample to `net9.0`.
- Removed the Unity package metadata, assemblies, assets, and editor-only log exporter.

## [0.1.1] - 2026-06-17

### Added

- Added native C# usage samples under `Samples~/NativeCSharp` for running the `Write`, `Exert`, `Tag`, and exception-recording scenarios.
- Added an interactive native sample launcher that keeps accepting commands until `exit` is entered.
- Added the bool `BlackboxHandle.UseBlackbox` property and `Configure(..., useBlackbox: UseBlackboxOption.DoNotUse)` as runtime switches for disabling recording.
- Added contribution branch naming guidance for short-lived branches from `main` using the `<username>/<topic>` format.

### Changed

- Changed the public C# namespace and Unity asmdef names from `com.BlackThunder.BlackboxSystem` to `BlackThunder.BlackboxSystem`, and set the Unity package ID to `com.blackthunder.blackbox-system` so it matches the repository and embedded package folder naming.
- Changed `BLACKBOX` to only select Unity's default recording state; native C# and non-Unity builds record by default unless disabled at runtime.
- Updated package sample metadata so usage samples are registered separately by target environment.

## [0.1.0] - 2026-06-11

### Added

- Added the `BlackboxHandle` runtime for recording per-object activity, execution scopes, tags, and object-to-object interactions.
- Added an export pipeline for writing connected Blackbox logs to text and HTML files.
- Added Unity usage samples that demonstrate `Write`, `Scope`, `Exert`, tags, and error-recording flows.
- Added Korean and English wiki documentation, README, CONTRIBUTING, and MIT license files for the public package.
- Added Unity Test Framework/NUnit coverage for the runtime, handles, export tools, and integration flows.
