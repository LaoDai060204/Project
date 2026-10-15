# Validation — Unity 6000.6.3f1, development 0.2.1

Date: 2026-10-15. Target: 6000.6.3f1, revision 45d8eee7de74.

## Executed

- Downloaded and extracted the official Linux Editor from https://download.unity3d.com/download_unity/45d8eee7de74/LinuxEditorInstaller/Unity-6000.6.3f1.tar.xz . Its startup log confirms the exact version/revision.
- Confirmed bundled `com.unity.ugui` is 2.6.0, matching the project manifest.
- Compiled the bundled uGUI source and all Saga runtime scripts against the actual Editor's modular managed assemblies and .NET Standard 2.1 references using Roslyn. Compiled the Editor smoke runner against those same references. No game/test-script warnings or errors after replacing deprecated object search APIs.
- Executed pure C# tests: 96,756 engine assertions, 200 completed AI matches, longest 80 actions; 14 persistence/validation checks. First 100 use the default deck; the remaining 100 sample decks from all 20 supported cards. Replay commands are serialized/deserialized before playback and compared with the original result and log.
- Reproduced the invalid-schema backup bug against the previous `SagaProfile.cs`: the regression fails with InvalidDataException. The same test passes with the fix.

## Actual Editor launch: BLOCKED

Command: `Unity -batchmode -nographics -projectPath <BattleSpiritsSaga> -executeMethod SagaSmokeTests.Run -logFile <editor.log>`.

Relevant output:

```
Unity Editor version:    6000.6.3f1 (45d8eee7de74)
[Licensing::IpcConnector] Error: Connection to channel: "LicenseClient-root" failed; code: "0x80000000"
[Licensing::Module] Error: Licensing initialization failed after 0.00s
```

The environment blocks the licensing IPC connection. No activation or licensing bypass was attempted. Import, Play Mode smoke tests, rendering, real mouse/keyboard input and a Windows build are **not verified**. Standalone C# compilation is not equivalent to Unity import/build success.

## Fixes

1. Leaving a completed replay clears the playback match before result recording. Previously, opening saved history in a fresh session could record it again and corrupt history/stats.
2. UI callbacks schedule a redraw in LateUpdate. Rendering no longer disables/destroys the active InputField from its own end-edit callback; redraw-triggered end-edit callbacks are ignored. This also preserves a button clicked while leaving a text field.
3. Added one AudioListener when absent; owned camera/event system now belong to the scene component for cleanup.
4. Backup recovery now handles structurally invalid JSON profiles, not only parse/IO errors. Reject missing deck names/null card IDs; normalize non-finite AI delays.
5. Clear transient modals when starting matches/replays and discard stale deck-confirmation callbacks on navigation.
6. Replace deprecated FindFirstObjectByType/FindObjectsSortMode usage with the APIs supplied by this exact Editor.

## Local verification

Use **Saga → Run Play Mode Smoke Tests** in an activated Unity 6000.6.3f1 installation. It uses an isolated temporary profile and writes `TestResults/unity-smoke.txt`. The runner is supplied and compile-checked, but its Play Mode execution is pending. See README for the Windows batch command.
