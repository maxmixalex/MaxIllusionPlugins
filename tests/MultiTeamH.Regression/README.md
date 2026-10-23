# MultiTeamH compatibility regression tests

Run with .NET SDK 9:

```sh
dotnet run --project tests/MultiTeamH.Regression/MultiTeamH.Regression.csproj -c Release
```

This is an executable assertion harness, not a `dotnet test` test-adapter project.
A failed assertion exits nonzero. No game installation or external NuGet packages
are required. `IsPluginArtifact=false` excludes its executable assembly from the
plugin aggregation directory.

## What executes

The project links the actual production `ExpressionControlCompat.cs`,
`RuntimeModCompat.cs`, `BreastmilkCompat.cs`, `HSceneOptionsCompat.cs`,
`NativeUiHooks.cs`, `MultiTeamSelection.cs` and `CoroutineGuard.cs`; it does not maintain test copies
of their implementations. `BoundaryStubs.cs` replaces only the external Unity,
game, Harmony, logging, and `TeamSession` boundaries. Fault injection covers:

- original expression processing failures, a missing character list, repeated
  cleanup, duplicate processing of unmanaged characters, and per-session cache
  cleanup;
- HCtrl binding failures in the prefix, original method exceptions, UI cleanup
  failures, cursor restoration, independently saved plugin state, and idempotent
  postfix/finalizer cleanup;
- AutoSpeed exception restoration;
- Cyu iterator exception restoration of both the camera and the bound team,
  targeted kiss-tail cancellation that preserves Waitfor/GetMouse, and natural
  completion releasing the tracked iterator;
- nested calls restoring their own caller's context;
- Breastmilk coroutine failure falling back to native processing, cleanup even
  when state restoration fails, and HSceneOptions resetting scene-owned values;
- native special-action menu and portrait exception recovery, including
  idempotent menu postfix/finalizer cleanup;
- a delayed scene shutdown preserving a newer Free H selection, completing the
  owning launch, and rejecting an incomplete pair;
- coroutine failures after yielding, explicit iterator disposal, and failures
  inside an error handler.

The prefix-failure case uses a compiled direct call because `MethodInfo.Invoke`
does not write `ref` arguments back to its argument array when the target throws.
This preserves the reference semantics used by Harmony-generated calls.

## Boundaries

These tests exercise compatibility logic and exception recovery, not Unity's
rendering, player loop, scene lifetime or Harmony's detour generation. The plugin
itself must also be compiled against its existing net35 game references.

`CoroutineGuard` protects the supplied iterator's `MoveNext` and `Current` only.
It does not drive nested iterators or a custom yield instruction's callbacks.
Both production callers (`InitializeAfterFrame` and `SwitchRoutine`) yield only
`null`; loader polling runs inside the guarded parent iterator. The cancellation
test verifies explicit `IDisposable.Dispose`, not Unity's coroutine scheduler.

Game regression should include starting two teams, repeated Tab switching,
scene exit/reentry while another selection is pending, a failed second-character
load, closing/reopening Free H, and running the installed CyuNoVR 0.3.3,
KK_HCtrl 0.1.1, ExpressionControl and HSceneOptions integrations. Confirm camera,
UI and character bindings in game; a headless pass does not certify those.
