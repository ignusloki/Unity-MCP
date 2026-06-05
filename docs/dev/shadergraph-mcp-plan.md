# Shader Graph MCP Plan

## Goal

Add safe, incremental Unity MCP support for Shader Graph discovery, diagnostics, template-based creation, and later style-recipe-driven material generation in the user's private fork.

## Scope Now

- Current authorized implementation scope at branch start: Epic 0 reconnaissance and planning
- Working branch: `custom/shadergraph-epic0-plan`
- Base branch: `custom/main`
- Date opened: `2026-06-05`

## Workflow Constraints

- Follow `git.MD`
- Private branches for this work must start from `custom/main`
- Do not commit, push, merge, or open a PR without explicit user approval
- User-authorized exception: this tracker file was created during Epic 0 even though the prompt says "Do not modify files yet"

## Initial Assessment

- The overall roadmap is sound if kept incremental
- Epic 1 and Epic 2 are the safest first implementation slices
- Epic 3 is only safe if template generation avoids raw ad hoc `.shadergraph` editing and proves package/version handling first
- Full node and edge editing should remain deferred research, not part of the first implementation track

## Current Repo Snapshot

- Active starting branch before branching: `custom/main`
- Working tree at review time: clean
- Remotes: `origin = ignusloki/Unity-MCP`, `upstream = IvanMurzak/Unity-MCP`
- Local ref status at review time: `custom/main` is `14` commits ahead of local `upstream/main`
- Local ref status at review time: `custom/main` is in sync with local `origin/custom/main`
- Worktrees:
  - `/Users/suporte/Unity-MCP` -> `custom/main` at review time
  - `/Users/suporte/Unity-MCP-upstream` -> `main`
- No existing `ShaderGraph` or `com.unity.shadergraph` references were found inside `Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp`

## Local Test Project

- Local Unity test project: `/Users/suporte/Unity-MCP/Unity-test/TestShadergraph`
- Parent repo ignore now excludes:
  - `Unity-test/`
  - `.codex/config.toml`
- Important: the Unity test project is also its own nested git repo, so parent ignore does not stop changes from appearing inside that child repo
- Project Unity version: `6000.4.1f1`
- Package baseline still declares `unity: 2022.3`, so Unity 6 validation is useful but not the baseline compatibility proof
- Project already resolves `com.unity.shadergraph` transitively through URP in `packages-lock.json`
- Local package reference added in the test project manifest:
  - `com.ivanmurzak.unity.mcp -> file:../../../Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp`
- Unity plugin generated project-local Codex config:
  - `/Users/suporte/Unity-MCP/Unity-test/TestShadergraph/.codex/config.toml`
- Repo workspace project-local Codex config added:
  - `/Users/suporte/Unity-MCP/.codex/config.toml`
- Active local MCP endpoint for Codex:
  - `ai-game-developer -> http://localhost:28002`
- Unity-side MCP auth mode in `UserSettings/AI-Game-Developer-Config.json` is currently `none`
- Local `unity-mcp` server process is listening on TCP `28002`, which confirms the local MCP endpoint is up

## Relevant Code Map

- Tool declarations: `Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/`
- Current shader tools:
  - `Assets.Shader.cs`
  - `Assets.Shader.ListAll.cs`
  - `Assets.Shader.GetData.cs`
- Current material tool:
  - `Assets.Material.Create.cs`
- Tests:
  - `Tests/Editor/Tool/Assets/AssetsShaderTests.cs`
  - `Tests/Editor/Tool/Assets/AssetsMaterialTests.cs`
  - `Tests/Editor/BaseTest.cs`
- Package and assembly metadata:
  - `package.json`
  - `Editor/Scripts/com.IvanMurzak.Unity.MCP.Editor.asmdef`
  - `Tests/Editor/com.IvanMurzak.Unity.MCP.Editor.Tests.asmdef`

## Key Risks To Track

- No current compile-time Shader Graph dependency pattern exists in the package asmdefs
- Shader Graph APIs may require soft dependency handling, reflection, version gating, or an isolated assembly split
- Generated shader access from a `.shadergraph` asset may vary by Unity version and render pipeline
- Raw `.shadergraph` file authoring risks asset corruption and should not be the default first creation path
- CI and test environments may not always have Shader Graph installed, so tests must fail gracefully or be conditionally scoped

## Working Recommendation

- Treat `shadergraph-find`, `shadergraph-get-summary`, and diagnostics as the first code slice
- Delay creation tools until Epic 0 confirms a safe package and dependency story
- Keep all early operations read-only unless the user explicitly asks for the creation slice

## Implemented On This Branch

- New tool ids implemented in the package:
  - `assets-shadergraph-find`
  - `assets-shadergraph-get-data`
  - `assets-shadergraph-create`
- Implementation strategy:
  - No compile-time Shader Graph asmdef dependency was added
  - Source `.shadergraph` files are parsed directly as concatenated JSON objects
  - Compiled shader state is read through Unity's normal imported `Shader` asset path
  - Creation uses safe template cloning from package templates rather than ad hoc graph synthesis
- New package files added:
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.Common.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.Find.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.GetData.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.Create.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphData.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphDiagnosticData.cs`
  - `Tests/Editor/Tool/Assets/AssetsShaderGraphTests.cs`

## Live Validation

- Unity plugin registry confirmed the new tool ids through `unity_tool_list`
- Creation validated live in Unity through `script_execute`:
  - Created graph asset: `Assets/ShaderGraphValidation/Codex_Validation_Unlit.shadergraph`
  - Created material asset: `Assets/ShaderGraphValidation/Codex_Validation_Unlit.mat`
  - Created scene object: `SampleScene/Codex_Validation_Cube`
- Shader Graph live result after creation:
  - `SourceParsed = true`
  - `ShaderResolved = true`
  - `ShaderName = Unlit/Codex_Validation_Unlit`
  - `ActiveTargetTypes = 2` (`HDTarget`, `UniversalTarget`)
  - Diagnostics returned only `OK`
- `SampleScene` in the ignored Unity test project was saved after assigning the validation material to the cube

## Validation Gaps

- `tests-run` did not discover the new package tests in the Unity test project
- Likely cause: the local Unity test project does not currently expose this package as a testable package for the Unity Test Runner
- The test file exists in the package and compiled cleanly, but automatic execution in this project still needs a test-runner configuration pass if we want green test automation inside `TestShadergraph`

## Epic Tracker

- [x] Review prompt and detect workflow conflicts
- [x] Read `git.MD`
- [x] Create private planning branch from `custom/main`
- [x] Create tracking document
- [x] Epic 0: repo reconnaissance report
- [x] Epic 0: smallest first implementation slice proposal
- [x] Epic 1: read-only Shader Graph discovery
- [x] Epic 2: Shader Graph diagnostics
- [x] Epic 3: template-based Shader Graph creation
- [ ] Epic 4: material creation from generated graph
- [ ] Epic 5: style recipe schema
- [ ] Epic 6: parameterized style templates
- [ ] Epic 7: optional texture and reference-image handling
- [ ] Epic 8: limited safe graph edits
- [ ] Epic 9: full graph-editing feasibility research
- [ ] Epic 10: docs and AI workflow guide

## Open Questions

- Which Unity versions in practice must this support first: `2022.3` only, or also `2023.x` and `Unity 6`?
- Is `URP` the only intended first render pipeline target?
- Should the first implementation stay fork-only until the dependency story is proven, or should it be shaped for upstream from the start?
