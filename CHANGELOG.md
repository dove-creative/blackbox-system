# Changelog

This file records public changes to Blackbox.

The format follows Keep a Changelog, and version numbers follow Semantic Versioning.

## [0.1.1] - 2026-06-17

### Added

- Added native C# usage samples under `Samples~/NativeCSharp` for running the `Write`, `Exert`, `Tag`, and exception-recording scenarios.
- Added an interactive native sample launcher that keeps accepting commands until `exit` is entered.
- Added the bool `BlackboxHandle.UseBlackbox` property and `Configure(..., useBlackbox: UseBlackboxOption.DoNotUse)` as runtime switches for disabling recording.
- Added contribution branch naming guidance for short-lived branches from `main` using the `<username>/<topic>` format.

### Changed

- Changed the public C# namespace and Unity asmdef names from `com.BlackThunder.BlackboxSystem` to `BlackThunder.BlackboxSystem` while keeping the Unity package ID as `com.blackthunder.blackboxsystem`.
- Changed `BLACKBOX` to only select Unity's default recording state; native C# and non-Unity builds record by default unless disabled at runtime.
- Updated package sample metadata so usage samples are registered separately by target environment.

## [0.1.0] - 2026-06-11

### Added

- Added the `BlackboxHandle` runtime for recording per-object activity, execution scopes, tags, and object-to-object interactions.
- Added an export pipeline for writing connected Blackbox logs to text and HTML files.
- Added Unity usage samples that demonstrate `Write`, `Scope`, `Exert`, tags, and error-recording flows.
- Added Korean and English wiki documentation, README, CONTRIBUTING, and MIT license files for the public package.
- Added Unity Test Framework/NUnit coverage for the runtime, handles, export tools, and integration flows.
