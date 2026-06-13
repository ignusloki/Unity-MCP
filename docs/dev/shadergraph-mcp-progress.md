# Shader Graph MCP Progress

## Purpose

This document tracks the current work state for the ShaderGraph MCP roadmap.

Update this file during day-to-day implementation. Keep `docs/dev/shadergraph-mcp-plan.md` stable unless the roadmap or slice breakdown changes.

## Current Snapshot

- Integration branch: `custom/shadergraph-mcp`
- Base branch: `custom/main`
- Current local package baseline: `com.ivanmurzak.unity.mcp` version `0.81.0`
- Local Unity validation project: `/Users/suporte/Unity-MCP/Unity-test/TestShadergraph`
- Unity validation version: `6000.4.1f1`
- Active epic: Epic 7, Node Lifecycle Foundation
- Latest user-validated slice: Epic 7.3, node duplication
- Current code state: Epic 7.3 is user-validated and ready to commit
- Next planned slice after commit: Epic 7.4, lifecycle result payload normalization
- Conditional future edge slice: Epic 10.5, additional compatibility cases only if concrete unsupported URP paths are found

Current local environment note:

- The direct MCP HTTP endpoint is reachable at `http://localhost:28002`.
- During Epic 10.3 validation, the running standalone MCP server reported `0.80.1.0` even though package files are updated to `0.81.0`.
- Refresh the running server process if version alignment matters before the next validation pass.

## Active Work

Epic 7.3 validated changes waiting to be committed:

- Added `assets-shadergraph-duplicate-node`.
- The tool duplicates `PropertyNode` plus the same allowlisted node families as `assets-shadergraph-add-node`.
- It copies node settings and slot definitions with fresh serialized object ids.
- It preserves blackboard property references for duplicated `PropertyNode` nodes.
- It intentionally does not copy edges; duplicated nodes start disconnected and must be wired explicitly.
- It supports explicit duplicate positions or a deterministic source-position offset.
- Added editor coverage for successful connected-node duplication without copied edges and unsupported block-node rejection.

Validation completed for Epic 7.3:

- `dotnet build Assembly-CSharp.csproj -v minimal` passed in the local Unity validation project with `0` errors and existing unrelated warnings.
- Live MCP validation graph: `Assets/ShaderGraphValidation/Codex_NodeDuplicate_Validation.shadergraph`.
- Live MCP validation result:
  - duplicated the connected template `Multiply` node
  - created duplicate node `cac81a5a8d3a4c5a848a721af86f1d99`
  - placed the duplicate at `(34, 338)`
  - returned `node.duplicated`, `node.slot.duplicated`, `node.positionX`, and `node.positionY`
  - increased `NodeCount` from `10` to `11`
  - kept `EdgeCount = 4`
  - left the duplicate with no connected edges
  - produced no graph diagnostics errors
  - user verified the graph in Unity

Commit Epic 7.3 before starting Epic 7.4.

Epic 10.5 note:

- No concrete unsupported URP slot compatibility case is currently known.
- Treat Slice 10.5 as deferred until a real graph path fails the current compatibility matrix.

## Progress By Epic

### Epic 0: Reconnaissance, Workflow Review, And Branch Setup

Status: complete

Completed:

- Branch workflow documented in `git.MD`.
- Single-folder workflow confirmed.
- Local Unity test project established.
- Project-scoped Codex MCP config established.

### Epic 1: Read-Only Shader Graph Discovery And Diagnostics

Status: complete

Completed:

- Shader Graph asset discovery.
- Compiled shader resolution and diagnostics.
- Optional shader messages and compiled property reporting.

### Epic 2: Safe Creation, Material Creation, And Style-Recipe Foundation

Status: complete

Completed:

- Template-based graph creation.
- Material creation from graph shader.
- Style recipe foundation with explicit warnings for deferred fields.

### Epic 3: Read-Only Graph Structure Introspection

Status: complete

Completed:

- Read properties, nodes, slots, edges, targets, and graph contexts.

### Epic 4: Graph Settings Inspection And Mutation Baseline

Status: complete

Completed:

- Read graph root and URP target settings.
- Mutate safe graph root settings.
- Mutate safe URP target settings.
- Return changed fields and diagnostics.

### Epic 5: First-Wave Graph Mutation Proof

Status: complete

Completed:

- Existing property updates.
- Basic property creation.
- Initial PropertyNode creation.
- Node position mutation.
- Baseline edge connect/disconnect.

### Epic 6: ShaderGraph Extensions Entry And Capability Gating

Status: complete

Completed:

- ShaderGraph built-in group added to Ivan's Extensions UI.
- ShaderGraph tool ids grouped under that entry.

### Epic 7: Node Lifecycle Foundation

Status: partial, active

Completed:

- Slice 7.1: allowlisted node creation.
- Slice 7.2: node deletion with edge cleanup and Unity `canDeleteNode` guardrails.
- Slice 7.3: node duplication.
- Node position updates exist from the earlier mutation foundation.

Remaining:

- Slice 7.4: lifecycle result payload normalization.

### Epic 8: Node Parameter Editing

Status: complete for the current URP-first track

Completed:

- Slice 8.1: `Sample Texture 2D` typed settings.
- Slice 8.2: `Tiling And Offset` typed direct settings.
- Slice 8.3: `Branch` typed direct settings.
- Slice 8.4: `Split` and `Combine` typed direct settings.
- Slice 8.5: `Add`, `Subtract`, `Divide`, `Lerp`, and `One Minus` typed direct settings.
- Slice 8.6: `Multiply.multiplyType`.
- Slice 8.7: property-backed workaround for dynamic-vector-driven inputs.

Remaining future work:

- Slice 8.8: improve direct literal/default-slot mutation so the workaround is no longer needed.

Accepted limitation:

- Some dynamic-vector-driven default-slot edits are not surfaced reliably enough in the Shader Graph UI.
- Current stable workflow is blackboard property, PropertyNode, then edge wiring.

### Epic 9: Blackboard Expansion

Status: partial

Completed:

- Add/update support for `color`, `float`, `texture2D`, `vector2`, `vector3`, `vector4`, and `boolean`.
- PropertyNode creation for the same property set.
- Typed structure readback for the same property set.

Remaining:

- Slice 9.1: property deletion.
- Slice 9.2: property reordering in the default blackboard category.
- Slice 9.3: category-aware placement.
- Slice 9.4: safe category creation if serialized behavior is stable.
- Slice 9.5: normalized blackboard workflow validation.

Note:

- These tasks are not blocked by the Epic 8 direct-slot limitation.
- They were deferred while Epic 10 edge control was prioritized.

### Epic 10: Edge System V2

Status: complete for the current known URP-first edge-control matrix

Completed:

- Slice 10.1: explicit single-input edge replacement through `replaceExistingInputConnection`.
- Slice 10.2: explicit reconnect tool for moving an existing edge to a new output endpoint, input endpoint, or both.
- Slice 10.3: Texture2D slot compatibility for texture property outputs into Texture2D input slots.
- Slice 10.4: higher-level guarded output-slot reroute workflow.

Deferred conditional work:

- Slice 10.5: additional slot compatibility cases when concrete unsupported URP paths are found.

### Epic 11: URP Stack And Target Coverage

Status: not started

Remaining:

- Audit common URP target and stack/block fields.
- Expand safe URP target settings.
- Add stack/block control where serialized structure is stable.
- Validate common URP authoring targets.

### Epic 12: Texture And Asset-Reference Workflows

Status: not started

Remaining:

- Assign project texture assets to supported blackboard texture properties.
- Support project texture assignment for texture-consuming node workflows where the graph model permits it.
- Validate material and graph behavior after texture assignment.

### Epic 13: Graph Organization And Cleanup

Status: not started

Remaining:

- Groups.
- Sticky notes.
- Layout cleanup.
- Safe bulk graph refactors.

### Epic 14: Advanced And Long-Tail Research

Status: not started

Remaining:

- Subgraphs.
- Custom function nodes.
- Keyword and enum-driven authoring.
- Broader render-pipeline support if still desired.
- Long-tail node families.

### Epic 15: Tests, Validation Harness, And Workflow Docs

Status: ongoing

Completed:

- Current roadmap split into stable plan plus progress tracker.
- Capability reference exists for exposed tools.

Remaining:

- Make package editor tests discoverable from the local Unity validation project.
- Add higher-signal end-to-end ShaderGraph authoring validation cases.
- Keep roadmap, progress, and capability docs aligned without duplicating ownership.

## Ordered Validation History

### Epic 1 Validation

- ShaderGraph discovery and diagnostics validated against `Assets/ShaderGraphValidation/Codex_Validation_Unlit.shadergraph`.
- Result highlights:
  - `SourceParsed = true`
  - `ShaderResolved = true`
  - `ShaderName = Unlit/Codex_Validation_Unlit`
  - active targets included `HDTarget` and `UniversalTarget`
  - diagnostics returned no errors

### Epic 2 Validation

- Created graph asset: `Assets/ShaderGraphValidation/Codex_Validation_Unlit.shadergraph`.
- Created material asset: `Assets/ShaderGraphValidation/Codex_Validation_Unlit.mat`.
- Created scene object: `SampleScene/Codex_Validation_Cube`.
- Created material from graph: `Assets/ShaderGraphValidation/Codex_Validation_FromGraph.mat`.
- Material shader resolved to `Unlit/Codex_Validation_Unlit`.
- Style recipe validation created:
  - `Assets/ShaderGraphValidation/Codex_Recipe_Toon.shadergraph`
  - `Assets/ShaderGraphValidation/Codex_Recipe_Toon.mat`
- Deferred recipe fields returned explicit warnings instead of being silently ignored.

### Epic 3 Validation

- Structure introspection validated against `Assets/ShaderGraphValidation/Codex_Validation_Unlit.shadergraph`.
- Readback included:
  - property count: `2`
  - node count: `10`
  - edge count: `4`
  - active targets: `HDTarget`, `UniversalTarget`
  - `Sample Texture 2D` slots: `Texture`, `UV`, `Sampler`

### Epic 4 Validation

- Read settings from `Assets/ShaderGraphValidation/Codex_Validation_Unlit.shadergraph`.
- Created validation graph: `Assets/ShaderGraphValidation/Codex_Settings_Validation.shadergraph`.
- Created validation material: `Assets/ShaderGraphValidation/Codex_Settings_Validation.mat`.
- Applied root changes:
  - `ShaderMenuPath = Codex/SettingsValidation`
  - `GraphPrecision = half`
  - `PreviewMode = preview2d`
- Applied URP target changes:
  - `AllowMaterialOverride = true`
  - `SurfaceType = transparent`
  - `AlphaMode = premultiply`
  - `RenderFace = both`
  - `AlphaClip = true`
  - `CastShadows = false`
  - `ReceiveShadows = false`
  - `SupportsLodCrossFade = true`
- Reimported shader resolved to `Codex/SettingsValidation/Codex_Settings_Validation`.
- Diagnostics returned no errors.

### Epic 5 Validation

- Property update validation graph: `Assets/ShaderGraphValidation/Codex_Property_Validation.shadergraph`.
- Property add validation graph: `Assets/ShaderGraphValidation/Codex_AddProperty_Validation.shadergraph`.
- Updated color and texture properties, including `_TintColor` and `_DiffuseTex`.
- Added `_AccentColor` and `_GlowStrength`.
- Edge baseline validation graph: `Assets/ShaderGraphValidation/Codex_Edge_Validation.shadergraph`.
- Connect/disconnect flows rerouted graph wiring without import errors.

### Epic 6 Validation

- Unity plugin registry confirmed the ShaderGraph tool group through `unity_tool_list`.
- ShaderGraph appears as a built-in entry in Ivan's Extensions window.

### Epic 7 Validation

- Node creation graph: `Assets/ShaderGraphValidation/Codex_Node_Validation.shadergraph`.
- Node deletion graph: `Assets/ShaderGraphValidation/Codex_NodeLifecycle_Validation.shadergraph`.
- Node movement graph: `Assets/ShaderGraphValidation/Codex_NodeMove_Validation.shadergraph`.
- Allowlisted node creation validated for math, vector, texture, control-flow, and PropertyNode families.
- Node deletion validated with automatic edge cleanup.
- Node duplication graph: `Assets/ShaderGraphValidation/Codex_NodeDuplicate_Validation.shadergraph`.
- Node duplication validated:
  - duplicated the connected template `Multiply` node
  - created duplicate node `cac81a5a8d3a4c5a848a721af86f1d99`
  - placed the duplicate at `(34, 338)`
  - increased `NodeCount` from `10` to `11`
  - kept `EdgeCount = 4`
  - left the duplicate with no connected edges
  - produced no Unity console errors
  - user verified the graph in Unity

### Epic 8 Validation

- Node settings graph: `Assets/ShaderGraphValidation/Codex_NodeSettings_Validation.shadergraph`.
- `Sample Texture 2D` typed settings mutation validated.
- Workaround graph: `Assets/ShaderGraphValidation/Codex_Epic8_NodeSlots.shadergraph`.
- Final workaround graph summary:
  - `GraphPropertyCount = 20`
  - `NodeCount = 32`
  - `EdgeCount = 18`
  - `HasErrors = false`
- Property-backed workflow validated for `Tiling And Offset`, `Branch`, `Split`, `Combine`, `Add`, `Subtract`, `Divide`, `Lerp`, and `One Minus`.

### Epic 9 Validation

- Blackboard validation graph: `Assets/ShaderGraphValidation/Codex_Blackboard_Validation.shadergraph`.
- Expanded add/update support validated for `texture2D`, `float`, `vector2`, `vector3`, `vector4`, and `boolean`.
- PropertyNode validation graph: `Assets/ShaderGraphValidation/Codex_PropertyNode_Validation.shadergraph`.
- PropertyNode slot generation validated for `_BaseColor`, `_BaseMap`, `_CodexGlowStrength`, `_CodexUVScale`, `_CodexFlowDirection`, `_CodexPackedControls`, and `_CodexUseDetail`.

### Epic 10 Validation

- Slice 10.1 graph: `Assets/ShaderGraphValidation/Codex_EdgeReplace_Validation.shadergraph`.
- Slice 10.1 result:
  - replaced `_BaseColor -> Multiply.B` with `_AccentColor -> Multiply.B`
  - returned `edge.disconnected`, `edge.replaced`, and `edge.connected`
  - kept `EdgeCount = 4`
  - produced no Unity console errors
- Slice 10.2 graph: `Assets/ShaderGraphValidation/Codex_EdgeReconnect_Validation.shadergraph`.
- Slice 10.2 result:
  - reconnected `_BaseColor -> Multiply.B` to `_AccentColor -> Multiply.B`
  - reconnected `Multiply.Out -> SurfaceDescription.BaseColor` to `Multiply.Out -> SurfaceDescription.Emission`
  - returned `edge.disconnected`, `edge.reconnected`, and `edge.connected`
  - kept `EdgeCount = 4`
- Slice 10.3 graph: `Assets/ShaderGraphValidation/Codex_EdgeTexture_Validation.shadergraph`.
- Slice 10.3 result:
  - reconnected `Sample Texture 2D.Texture` from `_BaseMap` to `_DetailMap`
  - replaced `Sample Texture 2D.Texture` from `_DetailMap` to `_MaskMap`
  - validated `Texture2DMaterialSlot -> Texture2DInputMaterialSlot`
  - returned expected reconnect and replacement mutation summaries
  - kept `EdgeCount = 4`
  - produced no Unity console errors
- Slice 10.4 graph: `Assets/ShaderGraphValidation/Codex_EdgeReroute_Validation.shadergraph`.
- Slice 10.4 result:
  - rerouted all outgoing `_BaseColor` edges to `_RerouteAccent`
  - moved three downstream consumers in one guarded operation
  - returned `edge.disconnected`, `edge.rerouted`, and `edge.connected`
  - returned `removedEdges.Count = 3` and `edges.Count = 3`
  - kept `EdgeCount = 6`
  - produced no Unity console errors
  - user verified the graph in Unity

## Validation Gaps

- `dotnet build Assembly-CSharp.csproj -v minimal` passes in the local Unity test project with existing unrelated warnings.
- Unity Test Runner command discovery still needs cleanup: earlier `tests-run` attempts did not discover the package editor tests from `TestShadergraph`.
- Until the test runner setup is fixed, each slice should continue to use:
  - compile sanity check
  - live MCP mutation validation
  - Unity Editor visual/user verification before commit

## Open Questions

- Which Unity versions must be treated as first-class validation targets beyond `2022.3` and the current Unity 6 local project?
- Should the broad-control implementation remain private-fork only until the mutation model is fully proven, or should future slices be shaped for upstream contribution from the start?
