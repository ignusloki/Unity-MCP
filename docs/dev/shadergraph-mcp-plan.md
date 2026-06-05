# Shader Graph MCP Plan

## Goal

Add safe, incremental Unity MCP support for Shader Graph discovery, diagnostics, safe creation, and progressively broader graph control in the user's private fork, with future user-facing capability gating in Ivan's Extensions UI.

## Pivot

- After Epic 5 validation, the user explicitly chose to defer template expansion work
- Epic 6 is now considered lower priority than increasing raw MCP control over Shader Graph structure
- The next implementation track is control-oriented rather than template-oriented

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
- Extension UI and tool-gating files relevant to future ShaderGraph capability toggles:
  - `Editor/Scripts/UI/Window/MainWindowEditor.Extensions.cs`
  - `Editor/Scripts/UI/Components/ExtensionPanel.cs`
  - `Editor/Scripts/API/Tool/Tool.SetEnabledState.cs`
  - `Editor/Scripts/API/Tool/Data/ToolToggleInput.cs`

## Key Risks To Track

- No current compile-time Shader Graph dependency pattern exists in the package asmdefs
- Shader Graph APIs may require soft dependency handling, reflection, version gating, or an isolated assembly split
- Generated shader access from a `.shadergraph` asset may vary by Unity version and render pipeline
- Raw `.shadergraph` file authoring risks asset corruption and should not be the default first creation path
- CI and test environments may not always have Shader Graph installed, so tests must fail gracefully or be conditionally scoped
- The current Extensions UI is package-install oriented; ShaderGraph currently lives in the core package, so future UI integration may require a built-in toggle mode or a package split instead of a plain installable extension entry

## Working Recommendation

- Keep the existing discovery, creation, and style-recipe work as the foundation layer
- Prioritize MCP control-surface expansion over additional template families
- Keep future mutation slices explicit, validated, and narrow rather than attempting broad freeform graph editing early

## Post-Epic-5 Direction

- Defer Epic 6 parameterized template families until later
- Prioritize direct Shader Graph control in safe, narrow slices
- Start with read-only graph structure introspection before mutating graphs
- Keep mutation work scoped to explicit, validated operations with graph reimport/validation after every write
- Add a future ShaderGraph entry to Ivan's Extensions window so users can explicitly allow or deny ShaderGraph control once the capability surface is mature enough

## Pivoted Control Track

1. Read-only graph structure introspection
2. Safe graph settings inspection and mutation
3. Blackboard property operations
4. Limited allowlisted node operations
5. Limited allowlisted edge operations
6. ShaderGraph Extensions entry and capability gating

## Future ShaderGraph Extension UI

- Add a `ShaderGraph` row to Ivan's Extensions window once the control surface is mature enough to expose as a user-facing capability area
- Let users explicitly allow or deny Shader Graph control rather than always exposing every Shader Graph tool
- Reuse the existing tool-enable persistence path where possible:
  - `tool-set-enabled-state`
  - `ToolToggleInput`
- Important design constraint:
  - the current Extensions UI assumes installable packages
  - ShaderGraph support currently lives in the core package
  - implementation likely needs a built-in toggle-only extension mode or a later package split

## Implemented On This Branch

- New tool ids implemented in the package:
  - `assets-shadergraph-find`
  - `assets-shadergraph-get-data`
  - `assets-shadergraph-get-structure`
  - `assets-shadergraph-create`
  - `assets-shadergraph-create-material`
  - `assets-shadergraph-create-from-style-recipe`
- Implementation strategy:
  - No compile-time Shader Graph asmdef dependency was added
  - Source `.shadergraph` files are parsed directly as concatenated JSON objects
  - Compiled shader state is read through Unity's normal imported `Shader` asset path
  - Creation uses safe template cloning from package templates rather than ad hoc graph synthesis
  - Style recipes are validated as declarative JSON and then mapped onto safe template creation plus material property application
- New package files added:
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.Common.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.Find.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.GetData.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.GetStructure.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.Create.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.CreateMaterial.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.CreateFromStyleRecipe.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphData.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphDiagnosticData.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphStructureData.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderStyleRecipeData.cs`
  - `Tests/Editor/Tool/Assets/AssetsShaderGraphTests.cs`

## Live Validation

- Unity plugin registry confirmed the new tool ids through `unity_tool_list`
- Creation validated live in Unity through `script_execute`:
  - Created graph asset: `Assets/ShaderGraphValidation/Codex_Validation_Unlit.shadergraph`
  - Created material asset: `Assets/ShaderGraphValidation/Codex_Validation_Unlit.mat`
  - Created scene object: `SampleScene/Codex_Validation_Cube`
- Epic 4 validated live in Unity through `script_execute`:
  - Source graph asset: `Assets/ShaderGraphValidation/Codex_Validation_Unlit.shadergraph`
  - Created material from graph: `Assets/ShaderGraphValidation/Codex_Validation_FromGraph.mat`
  - Material shader resolved to: `Unlit/Codex_Validation_Unlit`
- Epic 5 validated live in Unity through `script_execute`:
  - Created graph from recipe: `Assets/ShaderGraphValidation/Codex_Recipe_Toon.shadergraph`
  - Created material from recipe: `Assets/ShaderGraphValidation/Codex_Recipe_Toon.mat`
  - Resolved template id: `unlit-simple`
  - Applied material property: `_BaseColor`
  - Deferred recipe fields returned as explicit warnings instead of being silently ignored
- Pivot slice validated live in Unity through `script_execute`:
  - Structure tool asset path: `Assets/ShaderGraphValidation/Codex_Validation_Unlit.shadergraph`
  - Resolved property count: `2`
  - Resolved node count: `10`
  - Resolved edge count: `4`
  - Active targets included: `HDTarget`, `UniversalTarget`
  - Resolved `Sample Texture 2D` slots included `Texture`, `UV`, and `Sampler`
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

- [x] Epic 0: reconnaissance, workflow review, and branch setup
- [x] Epic 1: read-only Shader Graph discovery and diagnostics
- [x] Epic 2: safe creation, material creation, and style-recipe foundation
- [x] Epic 3: read-only graph structure introspection
- [ ] Epic 4: safe graph settings inspection and mutation
- [ ] Epic 5: blackboard property operations
- [ ] Epic 6: limited allowlisted node operations
- [ ] Epic 7: limited allowlisted edge operations
- [ ] Epic 8: ShaderGraph Extensions entry and capability gating
- [ ] Epic 9: optional texture and reference-image handling
- [ ] Epic 10: parameterized style templates (deferred)
- [ ] Epic 11: full graph-editing feasibility research
- [ ] Epic 12: docs and AI workflow guide

## Current Checkpoint

- Read-only graph structure introspection is complete
- Scope delivered:
  - blackboard property listing
  - node listing
  - edge and port-connection listing
  - active target discovery
- Constraints held:
  - read-only only
  - no direct mutation of `.shadergraph` source in this slice
  - safe source parsing and imported-asset inspection only
- Status:
  - implemented
  - validated live in Unity
  - ready to commit
- Next planned slice:
  - safe graph settings inspection and mutation

## Open Questions

- Which Unity versions in practice must this support first: `2022.3` only, or also `2023.x` and `Unity 6`?
- Is `URP` the only intended first render pipeline target?
- Should the first implementation stay fork-only until the dependency story is proven, or should it be shaped for upstream from the start?
