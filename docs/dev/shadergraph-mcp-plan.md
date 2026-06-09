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

- The current implementation is a strong foundation, but it does **not** yet provide normal-user-equivalent Shader Graph control
- The biggest remaining gaps are general node lifecycle control, node-specific parameter editing, broader blackboard coverage, and stronger edge mutation workflows
- Full parity with every internal Shader Graph type across every Unity version is not a realistic short-term goal
- The practical target is now **URP-first, high-value authoring parity**: enough control for an AI agent to build, edit, and repair typical URP graphs without falling back to ad hoc manual Unity work

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

- No general node creation: current node creation only supports `PropertyNode`, and only for existing `color` and `float` properties
- No node deletion
- No node duplication
- No general node editing: current node mutation only moves nodes by `positionX` and `positionY`
- No broad node-parameter editing for common URP nodes such as `Sample Texture 2D`, `Tiling And Offset`, `Multiply`, `Add`, `Lerp`, `Split`, `Combine`, `Branch`, and normal-map helpers
- No broad blackboard support: current property creation is limited to `color` and `float`, and current property editing is limited to a few generic fields plus color default value
- No property deletion, reordering, or category management
- No texture-property authoring parity yet for common graph workflows
- Edge mutation is still narrow: connect/disconnect exists, but replacement flows and broader compatibility handling are not complete
- No graph organization parity for groups, sticky notes, or other cleanup-oriented editing
- No advanced URP authoring support yet for subgraphs, custom-function workflows, keywords/enums, or long-tail node families

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

2. **Epic 8: Node Parameter Editing**
   - Add structured update tools for supported node families
   - Support high-value node-specific fields and modes rather than broad raw JSON mutation
   - First target coverage:
     - `Sample Texture 2D`: type/space and other serialized modes that matter in URP workflows
     - `Tiling And Offset`, math nodes, branch, split/combine where there are stable serialized settings
     - default slot values for nodes where editing constants is practical and safe

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

4. **Epic 10: Edge System V2**
   - Keep current connect/disconnect behavior as the baseline
   - Add safe edge replacement workflows for already-connected inputs
   - Expand slot compatibility handling for supported URP node families
   - Add explicit semantics for reconnect, replace, and guarded auto-disconnect

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

## ShaderGraph Extension UI Status

- Add a `ShaderGraph` row to Ivan's Extensions window once the control surface is mature enough to expose as a user-facing capability area
- Let users explicitly allow or deny Shader Graph control rather than always exposing every Shader Graph tool
- Reuse the existing tool-enable persistence path where possible:
  - `tool-set-enabled-state`
  - `ToolToggleInput`
- Important design constraint:
  - the current Extensions UI assumes installable packages
  - ShaderGraph support currently lives in the core package
  - implementation likely needs a built-in toggle-only extension mode or a later package split

## Current Extension UI Slice

- Implement `ShaderGraph` in the Extensions section as a built-in capability group, not as an installable package
- Button behavior:
  - `Disable` when the full ShaderGraph tool group is enabled
  - `Enable` when any ShaderGraph tools in the group are disabled
- Persistence path:
  - batch-toggle the grouped ShaderGraph tool ids through `ToolManager.SetToolEnabled(...)`
  - persist through `UnityMcpPluginEditor.Instance.Save()`
- The row should refresh when tool enabled states change so the button text does not go stale after toggles from other UI surfaces

## Implemented On This Branch

- New tool ids implemented in the package:
  - `assets-shadergraph-find`
  - `assets-shadergraph-get-data`
  - `assets-shadergraph-get-structure`
  - `assets-shadergraph-get-settings`
  - `assets-shadergraph-add-property`
  - `assets-shadergraph-add-property-node`
  - `assets-shadergraph-update-node-position`
  - `assets-shadergraph-connect-edge`
  - `assets-shadergraph-disconnect-edge`
  - `assets-shadergraph-create`
  - `assets-shadergraph-create-material`
  - `assets-shadergraph-create-from-style-recipe`
  - `assets-shadergraph-set-settings`
  - `assets-shadergraph-update-property`
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
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.GetSettings.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.AddProperty.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.AddPropertyNode.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.UpdateNodePosition.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.UpdateEdge.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.Create.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.CreateMaterial.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.CreateFromStyleRecipe.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.SetSettings.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.Settings.Common.cs`
  - `Editor/Scripts/API/Tool/Assets.ShaderGraph.UpdateProperty.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphData.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphDiagnosticData.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphEdgeMutationData.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphNodeMutationData.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphPropertyMutationData.cs`
  - `Editor/Scripts/API/Tool/Data/ShaderGraphSettingsData.cs`
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
- [ ] Epic 8: Node parameter editing
- [ ] Epic 9: Blackboard expansion
- [ ] Epic 10: Edge system V2
- [ ] Epic 11: URP stack and target coverage
- [ ] Epic 12: Texture and asset-reference workflows
- [ ] Epic 13: Graph organization and cleanup
- [ ] Epic 14: Advanced / long-tail research
- [ ] Epic 15: tests, validation harness, and workflow docs

## Current Checkpoint

- The roadmap is now reprioritized around closing the control gaps required for practical URP authoring parity
- The current uncommitted style-recipe texture slice is explicitly **paused**, not the next priority
- The next epic to start is:
  - **Epic 7: Node lifecycle foundation**
- First slice recommendation inside Epic 7:
  - generalized allowlisted node-creation infrastructure
  - node deletion with safe edge cleanup
  - first node families: `Add`, `Multiply`, `Lerp`, `Split`, `Combine`, `Sample Texture 2D`, `Tiling And Offset`, `Branch`
- Existing committed foundation still stands:
  - discovery and diagnostics
  - safe graph/material creation
  - baseline URP settings mutation
  - limited property mutation
  - limited node/edge mutation proof
  - ShaderGraph Extensions gating

## Open Questions

- Which Unity versions in practice must this support first: `2022.3` only, or also `2023.x` and `Unity 6`?
- How broad should the first allowlisted node set be before we stop and validate in-editor?
- Should the broad-control implementation stay fork-only until the mutation model is proven, or should it be kept upstream-shaped from the start?
