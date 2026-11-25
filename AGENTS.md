# Agent development notes

- Read `README.md` for the plugin/library projects and `docs/Development.md` for build and validation details.
- Restore/build with `dotnet restore MaxIllusionPlugins.sln` and `dotnet build MaxIllusionPlugins.sln -c Release --no-restore -m:1 -nodeReuse:false`.
- Execute all assertions with `python scripts/test.py` or `pwsh -NoProfile -File scripts/Test-All.ps1`. MaximumUI's original harness is a class library; `dotnet test` alone does not execute it. The .NET 9 runner calls its actual Main, while the three regression hosts link production code with explicit external-boundary test doubles.
- Most code targets .NET Framework 3.5 / Unity 5.6. Preserve compatibility when adding language/runtime features. Package versions are centralized.
- Compile-time game/API references are restored from NuGet; no local game path is required to build the solution.
- Runtime validation requires Koikatsu and the relevant installed plugins. A successful build or headless test is not evidence that game behavior is fixed.
- Before changing MultiTeamH, read `docs/MultiTeamH-Handoff.md` and `docs/MultiTeamH-Dependencies.md`. The latest camera change still awaits user runtime feedback.
- Keep optional-plugin compatibility patches in the project that owns the integration. Preserve per-character/per-team state and existing XML data formats.
- Reference material in `.local-reference/` is intentionally excluded from Git and may not exist in a cloud checkout. Use the documented public version links or obtain the separately supplied reference bundle.
- Preserve existing author notices and the repository LICENSE. Do not add game binaries, decompiled snapshots, user cards/configuration, logs or build output to source commits.
- Do not compute SHA256 unless the user explicitly requests it.
