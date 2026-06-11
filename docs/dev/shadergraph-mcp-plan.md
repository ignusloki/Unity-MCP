# Shader Graph MCP Plan

## Goal

Add safe, incremental Unity MCP support for Shader Graph discovery, diagnostics, safe creation, and progressively broader graph control in the user's private fork, with future user-facing capability gating in Ivan's Extensions UI.

## Pivot

- After Epic 5 validation, the user explicitly chose to defer template expansion work
- Epic 6 is now considered lower priority than increasing raw MCP control over Shader Graph structure
- The next implementation track is control-oriented rather than template-oriented

## Scope Now

- Current ShaderGraph integration branch: `custom/shadergraph-mcp`
- Latest validated slice on that branch: Epic 9 PropertyNode expansion
- Current package baseline in the local validation project: `com.ivanmurzak.unity.mcp` version `0.80.0`
- Base branch: `custom/main`
- Date opened: `2026-06-05`

## Workflow Constraints

- Follow `git.MD`
- Private branches for this work must start from `custom/main`
- Ongoing ShaderGraph MCP feature work now lives on a single private branch: `custom/shadergraph-mcp`
- Historical epic-named branches are treated as slice branches only and are superseded by the integration branch once their code is carried forward
- Do not commit, push, merge, or open a PR without explicit user approval
- User-authorized exception: this tracker file was created during Epic 0 even though the prompt says "Do not modify files yet"

## Sequencing Rule

- Epic numbers identify workstreams, not a mandatory execution order
- The next slice should come from the highest-value unresolved gap, even if that means returning from a later-numbered epic to an earlier-numbered one
- The integration branch does not need to match any epic number
- If temporary slice branches are used, their names should match the slice they contain, not the next epic that will be worked later

## Initial Assessment

- The current implementation is a strong foundation, but it does **not** yet provide normal-user-equivalent Shader Graph control
- The biggest remaining gaps are general node lifecycle control, node-specific parameter editing, broader blackboard coverage, and stronger edge mutation workflows
- Full parity with every internal Shader Graph type across every Unity version is not a realistic short-term goal
- The practical target is now **URP-first, high-value authoring parity**: enough control for an AI agent to build, edit, and repair typical URP graphs without falling back to ad hoc manual Unity work

## Current Repo Snapshot

- Long-lived branches:
  - `main` tracks `upstream/main`
  - `custom/main` carries the private integrated branch
- Remotes: `origin = ignusloki/Unity-MCP`, `upstream = IvanMurzak/Unity-MCP`
- Current workspace uses the single-folder flow in `git.MD`
- ShaderGraph MCP support now exists directly in `Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp`

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

- Keep the existing discovery, creation, limited property/node/edge work, and Extensions gating as the foundation layer
- Reprioritize all remaining work around closing the **actual control gaps** instead of expanding templates or recipe polish
- Stay URP-only until common graph-authoring workflows are stable
- Keep every new mutation slice explicit, validated, and import-checked after each write
- Treat style-recipe texture/reference-image work as a supporting workflow, not the primary next milestone

## Definition Of Done For This Track

- An AI agent can inspect a URP Shader Graph structurally and semantically
- An AI agent can create, delete, and duplicate the common node types needed for practical URP graph authoring
- An AI agent can edit the meaningful serialized settings of those nodes, not just their canvas positions
- An AI agent can create, update, delete, reorder, and categorize the common blackboard property types used in URP graphs
- An AI agent can connect, disconnect, and safely replace edges across the supported slot-compatibility matrix
- An AI agent can control the important URP graph settings, target settings, and stack/block coverage required by normal material-authoring workflows
- Every mutation path forces re-import, returns diagnostics, and fails loudly on unsupported or ambiguous operations

## Current Capability Gaps

- No node duplication
- No broad node-parameter editing for common URP nodes such as `Tiling And Offset`, `Multiply`, `Add`, `Lerp`, `Split`, `Combine`, `Branch`, and normal-map helpers
- No typed constant/default-slot editing for the allowlisted math and utility nodes
- No property deletion, reordering, or category management
- No project-asset texture assignment workflow yet for blackboard properties or texture-consuming nodes
- Edge mutation is still narrow: connect/disconnect exists, but replacement flows, reconnect semantics, and broader compatibility handling are not complete
- No graph organization parity for groups, sticky notes, or other cleanup-oriented editing
- No advanced URP authoring support yet for subgraphs, custom-function workflows, keywords/enums, or long-tail node families
- No stack/block mutation parity yet beyond the current URP graph/target settings allowlist

## URP Priority Roadmap

1. **Epic 7: Node Lifecycle Foundation**
   - Add a generalized node-creation framework with an explicit allowlist
   - Add node deletion with safe cleanup of connected edges and root references
   - Add node duplication for supported node families where serialized cloning is safe
   - First target node families:
     - math/utility: `Add`, `Subtract`, `Multiply`, `Divide`, `Lerp`, `One Minus`
     - vector/channel: `Split`, `Combine`
     - texture sampling: `Sample Texture 2D`, `Tiling And Offset`
     - control flow: `Branch`
     - retain existing `PropertyNode` support
   - Current state:
     - allowlisted node creation is implemented for the first target families
     - node deletion is implemented with edge cleanup
     - PropertyNode creation now supports `color`, `float`, `texture2D`, `vector2`, `vector3`, `vector4`, and `boolean`
     - duplication is still missing

2. **Epic 8: Node Parameter Editing**
   - Add structured update tools for supported node families
   - Support high-value node-specific fields and modes rather than broad raw JSON mutation
   - First target coverage:
     - `Sample Texture 2D`: type/space and other serialized modes that matter in URP workflows
     - `Tiling And Offset`, math nodes, branch, split/combine where there are stable serialized settings
     - default slot values for nodes where editing constants is practical and safe
   - Current state:
     - typed updates exist for `Sample Texture 2D`
     - broader node-family coverage is still missing

3. **Epic 9: Blackboard Expansion**
   - Expand property creation/update support beyond `color` and `float`
   - First target types:
     - `texture2D`
     - `vector2`
     - `vector3`
     - `vector4`
     - `boolean`
   - Add property deletion
   - Add property reordering and category placement
   - Add broader typed default-value editing
   - Current state:
     - add/update support exists for `color`, `float`, `texture2D`, `vector2`, `vector3`, `vector4`, and `boolean`
     - PropertyNode creation for those property types is implemented
     - property deletion, reordering, and category management are still missing

4. **Epic 10: Edge System V2**
   - Keep current connect/disconnect behavior as the baseline
   - Add safe edge replacement workflows for already-connected inputs
   - Expand slot compatibility handling for supported URP node families
   - Add explicit semantics for reconnect, replace, and guarded auto-disconnect
   - Current state:
     - connect/disconnect baseline exists
     - replace/reconnect flows are still missing

5. **Epic 11: URP Stack And Target Coverage**
   - Expand URP settings coverage beyond the current allowlist
   - Investigate and implement safe stack/block control where serialized structure permits it
   - Prioritize common URP authoring needs:
     - base color
     - normal
     - emission
     - alpha / alpha clip threshold
     - metallic/specular-adjacent stack coverage as supported by the chosen template/target shape

6. **Epic 12: Texture And Asset-Reference Workflows**
   - Complete texture-property and texture-node workflows
   - Support project texture assignment across the supported property/node surface
   - Keep reference-image interpretation secondary; first stabilize project-asset texture flows
   - The currently uncommitted `texture.referenceTextureAssetPath` style-recipe slice belongs here, not ahead of the node/property control gaps

7. **Epic 13: Graph Organization And Cleanup**
   - Groups
   - Sticky notes
   - Better layout and cleanup operations
   - Safe bulk graph refactors for supported node families

8. **Epic 14: Advanced / Long-Tail Research**
   - Subgraphs
   - Custom function nodes
   - Keyword / enum-driven authoring
   - Broader render-pipeline support later if still desired

9. **Epic 15: Tests, Validation Harness, And Workflow Docs**
   - Improve package-test discoverability in the Unity test project
   - Add higher-signal end-to-end authoring validation cases
   - Document the supported URP node/property matrix for AI agents and users

## Capability Reference

- The single source of truth for the currently exposed ShaderGraph MCP surface is [shadergraph-mcp-capabilities.md](/Users/suporte/Unity-MCP/docs/dev/shadergraph-mcp-capabilities.md)
- That capability document owns:
  - exposed tool ids
  - supported node, property, settings, and edge coverage
  - the ShaderGraph entry in Ivan's Extensions window
  - explicit "not yet exposed" items
- This plan owns only:
  - epic sequencing
  - validation history
  - remaining gaps and risks
  - branch and checkpoint alignment

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
- Epic 4 first slice validated live in Unity through `script_execute`:
  - Read settings from: `Assets/ShaderGraphValidation/Codex_Validation_Unlit.shadergraph`
  - Root settings resolved: `ShaderMenuPath = Unlit`, `GraphPrecision = graph`, `PreviewMode = preview3d`
  - Universal target resolved: `SurfaceType = opaque`, `AlphaMode = alpha`, `RenderFace = front`
  - Created validation graph: `Assets/ShaderGraphValidation/Codex_Settings_Validation.shadergraph`
  - Created validation material: `Assets/ShaderGraphValidation/Codex_Settings_Validation.mat`
  - Applied root changes: `ShaderMenuPath = Codex/SettingsValidation`, `GraphPrecision = half`, `PreviewMode = preview2d`
  - Applied URP target changes: `AllowMaterialOverride = true`, `SurfaceType = transparent`, `AlphaMode = premultiply`, `RenderFace = both`, `AlphaClip = true`, `CastShadows = false`, `ReceiveShadows = false`, `SupportsLodCrossFade = true`
  - Reimported shader resolved to: `Codex/SettingsValidation/Codex_Settings_Validation`
  - Diagnostics returned no `Error`
- Epic 5 first slice validated live in Unity through `script_execute`:
  - Created validation graph: `Assets/ShaderGraphValidation/Codex_Property_Validation.shadergraph`
  - Created validation material: `Assets/ShaderGraphValidation/Codex_Property_Validation.mat`
  - Updated color property:
    - `DisplayName = Tint`
    - `ReferenceName = _TintColor`
    - `ColorHex = #FF7A00CC`
  - Updated texture property:
    - `DisplayName = Diffuse Map`
    - `ReferenceName = _DiffuseTex`
  - Reimported compiled shader kept resolving without `Error`
  - Compiled shader properties included `_TintColor` and `_DiffuseTex`
- Epic 5 second slice validated live in Unity through `script_execute`:
  - Created validation graph: `Assets/ShaderGraphValidation/Codex_AddProperty_Validation.shadergraph`
  - Created validation material: `Assets/ShaderGraphValidation/Codex_AddProperty_Validation.mat`
  - Added color property:
    - `DisplayName = Accent`
    - `ReferenceName = _AccentColor`
    - `ColorHex = #44CC88FF`
  - Added float property:
    - `DisplayName = Glow Strength`
    - `ReferenceName = _GlowStrength`
    - `FloatValue = 0.75`
  - Reimported compiled shader kept resolving without `Error`
  - Compiled shader properties included `_AccentColor` and `_GlowStrength`
- Epic 7 slices validated live in Unity through `script_execute` and user verification:
  - Created validation graph for node creation: `Assets/ShaderGraphValidation/Codex_Node_Validation.shadergraph`
  - Created validation graph for delete flows: `Assets/ShaderGraphValidation/Codex_NodeLifecycle_Validation.shadergraph`
  - Created validation graph for node movement: `Assets/ShaderGraphValidation/Codex_NodeMove_Validation.shadergraph`
  - Allowlisted node creation validated for math, vector, texture, control-flow, and PropertyNode families
  - Node deletion validated with automatic edge cleanup
- Epic 8 first slice validated live in Unity through `script_execute` and user verification:
  - Created validation graph: `Assets/ShaderGraphValidation/Codex_NodeSettings_Validation.shadergraph`
  - `Sample Texture 2D` typed settings mutation validated
- Epic 9 expanded slices validated live in Unity through `script_execute` and user verification:
  - Created validation graph: `Assets/ShaderGraphValidation/Codex_Blackboard_Validation.shadergraph`
  - Expanded add/update support validated for `texture2D`, `float`, `vector2`, `vector3`, `vector4`, and `boolean`
  - Typed structure readback validated for the same property set
- PropertyNode expansion slice validated live in Unity through `script_execute` and user verification:
  - Created validation graph: `Assets/ShaderGraphValidation/Codex_PropertyNode_Validation.shadergraph`
  - PropertyNode slot generation validated for `_BaseColor`, `_BaseMap`, `_CodexGlowStrength`, `_CodexUVScale`, `_CodexFlowDirection`, `_CodexPackedControls`, and `_CodexUseDetail`
- Edge baseline validated live in Unity through `script_execute` and user verification:
  - Created validation graph: `Assets/ShaderGraphValidation/Codex_Edge_Validation.shadergraph`
  - Connect/disconnect flows re-routed graph wiring without import errors
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
- [x] Epic 4: safe graph settings inspection and mutation baseline
- [x] Epic 5: first-wave ShaderGraph mutation proof
  - blackboard property operations
  - limited `PropertyNode` creation
  - node position mutation
  - limited edge connect/disconnect
- [x] Epic 6: ShaderGraph Extensions entry and capability gating
- [ ] Epic 7: Node lifecycle foundation
  - current state: allowlisted node creation and node deletion are done; duplication is still missing
- [ ] Epic 8: Node parameter editing
  - current state: `Sample Texture 2D` typed updates are done; broader node-family editing is still missing
- [ ] Epic 9: Blackboard expansion
  - current state: typed add/update and PropertyNode support are done for the high-value URP property types; delete/reorder/category work is still missing
- [ ] Epic 10: Edge system V2
  - current state: connect/disconnect baseline is done; replace/reconnect flows are still missing
- [ ] Epic 11: URP stack and target coverage
- [ ] Epic 12: Texture and asset-reference workflows
- [ ] Epic 13: Graph organization and cleanup
- [ ] Epic 14: Advanced / long-tail research
- [ ] Epic 15: tests, validation harness, and workflow docs

## Current Checkpoint

- The roadmap is now reprioritized around closing the control gaps required for practical URP authoring parity
- The current integrated branch already carries the validated Epic 7, Epic 8 first-slice, and Epic 9 work
- The latest validated slice belongs to **Epic 9: Blackboard expansion**
- Texture asset-reference workflows remain deferred behind the higher-value graph-control gaps
- Epic numbering is not the execution order; the next slice is chosen by priority, not by the largest epic number already touched
- This is **not** the last epic required for broad ShaderGraph parity in MCP
- The next epic to focus is:
  - **Epic 8: Node parameter editing**
- First slice recommendation inside Epic 8:
  - add typed settings coverage for `Tiling And Offset`, `Branch`, `Split`, `Combine`, and the allowlisted math nodes where serialized constant/default-value editing is stable
  - keep the API typed and explicit rather than exposing raw node JSON mutation
- Existing committed foundation still stands:
  - discovery and diagnostics
  - safe graph/material creation
  - baseline URP settings mutation
  - expanded blackboard property mutation
  - allowlisted node creation/deletion
  - PropertyNode creation across the common URP property types
  - baseline node/edge mutation proof
  - ShaderGraph Extensions gating

## Open Questions

- Which Unity versions in practice must this support first: `2022.3` only, or also `2023.x` and `Unity 6`?
- How broad should the first allowlisted node set be before we stop and validate in-editor?
- Should the broad-control implementation stay fork-only until the mutation model is proven, or should it be kept upstream-shaped from the start?
