# Timed Pause and Settings Backup Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development or superpowers:executing-plans task by task. The user approved implementation in the current workspace; retain the upstream-based feature branch.

**Goal:** Add persistent timed pause and validated, reversible same-platform JSON configuration backup on Windows and macOS.

**Architecture:** A testable pause controller uses a supplied clock and existing settings. A separate configuration codec validates an explicit whitelist before mutating settings; import writes a pre-import backup and rolls back on persistence failure. Existing App and Customize components provide menus, dialogs and refresh.

**Tech Stack:** C# 5/.NET Framework, WinForms/WPF, JavaScriptSerializer; Swift/AppKit, Foundation JSONSerialization and UserDefaults.

## Task 1: Windows core and regression harness

- [x] Create `test.ps1` and `tests/SettingsFeaturesTests.cs`, compile all sources and a synthetic BuildInfo using the same WPF references and XAML resource as `build.ps1`; set `Settings.Key` to a unique test registry key.
- [x] Add reflection-based assertions that fail because PauseControl and ConfigFile are missing, then run `./test.ps1` and capture the expected failure.
- [x] Create `src/PauseControl.cs`: `Start(int minutes)`, `SetManual(bool paused)`, `Check()`; constructor accepts Settings and Func<DateTime>, exposes Changed event. Persist UTC round-trip string `PauseUntilUtc` through Settings. No countdown for manual pause, startup reconciliation, invalid-date recovery.
- [x] Create `src/ConfigFile.cs`: `Export(Settings)`, `Parse(string, Settings)`, `Read(path, current)`, `Write(path, settings)`, `Import(candidate,current,backupPath,save)`; explicit fields, strict version/platform/types/ranges, 1 MiB limit, nullable string reset, resource fallback notes, atomic file replacement and rollback.
- [x] Run tests for pause transitions/restarts, clock jumps, preferences round trips, invalid documents, omitted/unknown/null fields, excluded runtime state, pre-import backup and write failure rollback.

## Task 2: Windows integration

- [x] Extend `src/App.cs` with 15/30/60 minute menu actions, resume action and timer; run startup reconciliation; manual toggle and Customize edits cancel countdown; hide automatic cards, cancel fail timer and stop sounds when pausing.
- [x] Add `Card.Dismiss()` and guard `OnFailCheck()` with Paused; preserve explicit clipboard inspection.
- [x] Add import/export controls and dialogs to `src/Customize.cs`, route manual pause via a callback to App. Import candidate is validated before confirmation; preserve Paused/deadline and local startup/permissions; reload after successful import.
- [x] Add Chinese/English strings to `src/Strings.cs`; ensure tray tooltip fits Windows' length limit.
- [x] Build into an alternate output before replacing the running executable; run regression checks.

## Task 3: macOS implementation

- [x] Add Foundation-only pause and config codecs, tests, and UserDefaults integration with the same state/file contract (platform=macos, macOS whitelist).
- [x] Wire AppKit tray actions, timer, startup reconciliation, card dismiss and fail-check guard.
- [x] Wire Customize JSON dialogs and uniform manual pause callback, add bilingual strings and error/results.
- [x] Run portable Foundation tests where Swift exists; report unavailable macOS compiler honestly.

## Task 4: Review and delivery

- [x] Review against every acceptance item in the approved spec, then independently review error paths and state ownership.
- [x] Update README with both features, excluded fields, same-platform restriction, missing resources, pre-import backup location and test command.
- [x] Run `./test.ps1`, `./build.ps1`, `git diff --check`; inspect UI/rendered configuration page and timed menus.
- [x] Replace/restart only this workspace's running CCCC after successful build, preserve prior executable as rollback.
- [x] Commit implementation locally, retain feature branch, report artifact and validation. No new PR without a request.


## Execution evidence

- Windows test-first bootstrap failed with `FAIL: timed pause controller exists`; implementation then passed.
- Integration bootstrap failed with `FAIL: pause can dismiss already-visible cards`; integration then passed.
- Explicit-import resource fallback regression failed before the scoped-field fix, then passed.
- Persisted empty-deadline regression failed before changing null handling, then passed.
- Latest Windows suite: 53 checks passed, including isolated registry persistence, JSON file validation/read/write/rollback, and settings UI/manual pause behavior.
- Chinese dark and English light settings pages rendered and inspected.
- Windows production build produced `dist/CCCC.exe` and `dist/CCCC-portable.zip`; the existing workspace process was backed up, replaced and restarted.
- Independent Windows review approved after the empty-deadline fix.
- Independent macOS static review found language refresh and trailing-newline color validation issues; both fixed, with a Foundation regression added for the latter.
- macOS source and Foundation behavioral tests are delivered. Swift/AppKit compilation and execution are unavailable on this Windows host and are not claimed as passed.
- Local feature branch retained; no remote push or new PR requested by this implementation step.
