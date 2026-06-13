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
- Active epic: Epic 12, Texture And Asset-Reference Workflows
- Latest user-validated slice: Epic 11.4, common URP authoring target coverage
- Current code state: Epic 11 is complete; Epic 12.1 has not started yet
- Next planned slice: Epic 12.1, assign project texture assets to supported blackboard texture properties
- Conditional future edge slice: Epic 10.5, additional compatibility cases only if concrete unsupported URP paths are found

Current local environment note:

- The direct MCP HTTP endpoint is reachable at `http://localhost:28002`.
- During Epic 10.3 validation, the running standalone MCP server reported `0.80.1.0` even though package files are updated to `0.81.0`.
- Refresh the running server process if version alignment matters before the next validation pass.

## Active Work

Epic 12.1 starting point:

- Existing blackboard texture property tools can create and update texture metadata such as default type, tiling/offset flags, texel size, HDR, and modifiable state.
- Missing capability: assigning a concrete project `Texture2D` asset reference to a Shader Graph blackboard texture property.
- First validation target: create or update a graph texture property so its default/reference texture points at a project texture asset, then verify graph import and material behavior where Unity exposes that reference.

## Recently Completed

Epic 11.1 audit result:

- Universal target scalar fields are stable in the local URP package cache.
- Safe Epic 11.2 target fields: `depthWrite`, `depthTest`, `disableTint`, `additionalMotionVectors`, `alembicMotionVectors`, `customEditorGui`, and `supportVfx`.
- Existing fields remain supported: `allowMaterialOverride`, `surfaceType`, `alphaMode`, `renderFace`, `alphaClip`, `castShadows`, `receiveShadows`, and `supportsLodCrossFade`.
- Active subtarget fields such as Default Decal Blending and Default SSAO are not on the Universal target object; handle them in a later subtarget/stack slice after the `m_Datas` serialized path is validated.
- Stack/block mutation remains separate work for Epic 11.3.

Epic 11.2 validation completed:

- Expand `assets-shadergraph-get-settings` readback for the safe Universal target scalar fields.
- Expand `assets-shadergraph-set-settings` mutation for the same fields.
- Add editor assertions for readback, mutation, changed-field reporting, and post-import diagnostics.
- Unity validation graph prepared at `Assets/ShaderGraphValidation/Codex_URPTargetSettings_Validation.shadergraph`.
- User validated the graph in Unity.

Epic 11.3 implementation target:

- Add `assets-shadergraph-set-blocks`.
- Support full replacement of one ordered built-in block stack at a time: `vertex` or `fragment`.
- Create missing built-in blocks with Unity-compatible default slots.
- Refuse to remove connected blocks unless `allowRemovingConnectedBlocks` is true.
- Register the tool under Ivan's built-in `ShaderGraph` Extensions group.
- Prepare Unity validation graph at `Assets/ShaderGraphValidation/Codex_BlockStack_Validation.shadergraph`.
- Subtarget-only fields such as Default Decal Blending and Default SSAO still require separate serialized-path validation before mutation support.

Epic 11.3 validation completed:

- Validation graph: `Assets/ShaderGraphValidation/Codex_BlockStack_Validation.shadergraph`.
- MCP result reported `SourceParsed=true`, `ShaderResolved=true`, and no error diagnostics.
- Fragment stack order reported by MCP:
  - `SurfaceDescription.BaseColor`
  - `SurfaceDescription.Emission`
  - `SurfaceDescription.Alpha`
  - `SurfaceDescription.AlphaClipThreshold`
- User validated that the fragment/master stack includes `Alpha Clip Threshold` after `Alpha`, with no console/import errors.

Epic 11.4 implementation target:

- Validate a Lit graph stack that includes base color, tangent-space normal, metallic, specular, smoothness, occlusion, emission, alpha, alpha clip threshold, and bent normal.
- Add stable built-in block descriptors discovered during validation if a stock URP Lit template blocks stack mutation.
- Prepare Unity validation graph at `Assets/ShaderGraphValidation/Codex_URPAuthoringTargets_Validation.shadergraph`.

Epic 11.4 validation completed:

- Validation graph: `Assets/ShaderGraphValidation/Codex_URPAuthoringTargets_Validation.shadergraph`.
- Created from `Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline/1_Lit Full.shadergraph`.
- MCP result reported `SourceParsed=true`, `ShaderResolved=true`, and no error diagnostics.
- Fragment stack order reported by MCP:
  - `SurfaceDescription.BaseColor`
  - `SurfaceDescription.NormalTS`
  - `SurfaceDescription.Metallic`
  - `SurfaceDescription.Specular`
  - `SurfaceDescription.Smoothness`
  - `SurfaceDescription.Occlusion`
  - `SurfaceDescription.Emission`
  - `SurfaceDescription.Alpha`
  - `SurfaceDescription.AlphaClipThreshold`
  - `SurfaceDescription.BentNormal`
- User validated that the Lit fragment/master stack exposes those common URP authoring blocks with no console/import errors.
- No additional stable URP stack/block gaps were found in the current validation path, so Epic 11 is closed.

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

Status: complete

Completed:

- Slice 7.1: allowlisted node creation.
- Slice 7.2: node deletion with edge cleanup and Unity `canDeleteNode` guardrails.
- Slice 7.3: node duplication.
- Slice 7.4: lifecycle result payload normalization.
- Node position updates exist from the earlier mutation foundation.

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

Status: complete

Completed:

- Add/update support for `color`, `float`, `texture2D`, `vector2`, `vector3`, `vector4`, and `boolean`.
- PropertyNode creation for the same property set.
- Typed structure readback for the same property set.
- Slice 9.1: property deletion with dependent PropertyNode and edge cleanup.
- Slice 9.2: property reordering in the default blackboard category.
- Slice 9.3: category-aware property placement.
- Slice 9.4: safe category creation.
- Slice 9.5: normalized blackboard workflow validation.
- ShaderGraph Extensions entry includes the new blackboard tools.
- Editor tests cover delete cleanup, reorder, category creation, category placement, and category moves.

Note:

- These tasks are not blocked by the Epic 8 direct-slot limitation.
- The final Epic 9 implementation now supports the common URP blackboard workflows needed before Epic 11 and Epic 12.

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

Status: complete

Completed:

- Slice 11.1: audited common URP target and stack/block fields in the local Unity package cache.
- Slice 11.2: expand safe URP target settings.
- Slice 11.3: added stable built-in stack/block control and user-validated `Alpha Clip Threshold` creation.
- Slice 11.4: validated common Lit URP authoring targets and added stable descriptors for `BentNormal`, `CoatMask`, and `CoatSmoothness`.

### Epic 12: Texture And Asset-Reference Workflows

Status: in progress

Remaining:

- Slice 12.1: assign project texture assets to supported blackboard texture properties.
- Slice 12.2: support project texture assignment for texture-consuming node workflows where the graph model permits it.
- Slice 12.3: validate material and graph behavior after texture assignment.
- Slice 12.4: revisit reference-image interpretation only after project asset texture flows are stable.

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
- Editor assertions cover the current ShaderGraph blackboard, node lifecycle, node settings, and edge-control surfaces.

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
- Lifecycle payload normalization validated by compile and editor assertions:
  - add, duplicate, delete, and move results expose `operation`, `nodeObjectId`, and `nodeType`
  - existing result fields remain available

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
- Delete validation graph: `Assets/ShaderGraphValidation/Codex_BlackboardDelete_Validation.shadergraph`.
- Delete validation result:
  - temporary `_CodexDeleteTexture` property was wired into `Sample Texture 2D.Texture`
  - deleting `_CodexDeleteTexture` removed `1` dependent `PropertyNode`
  - deleting `_CodexDeleteTexture` removed `1` dependent edge
  - final readback contains only `_BaseMap` and `_BaseColor` blackboard properties
  - final graph has `EdgeCount = 3`
- Reorder validation graph: `Assets/ShaderGraphValidation/Codex_BlackboardReorder_Validation.shadergraph`.
- Reorder validation result:
  - default category order is `_CodexReorderThird`, `_BaseColor`, `_BaseMap`, `_CodexReorderFirst`, `_CodexReorderSecond`
  - final graph imported without shader errors
- Category validation graph: `Assets/ShaderGraphValidation/Codex_BlackboardCategories_Validation.shadergraph`.
- Category validation result:
  - default category order remains `_BaseColor`, `_BaseMap`
  - `Codex Surface Controls` contains `_CodexCategoryTint`
  - `Codex Auto Created` contains `_CodexCategoryDetail`, `_CodexCategoryStrength`
  - final graph imported without shader errors

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

- `dotnet build Assembly-CSharp.csproj -v minimal` passes in the local Unity test project with `0` errors and existing warnings.
- Unity Test Runner command discovery still needs cleanup: earlier `tests-run` attempts did not discover the package editor tests from `TestShadergraph`.
- Until the test runner setup is fixed, each slice should continue to use:
  - compile sanity check
  - live MCP mutation validation
  - Unity Editor visual/user verification before commit

## Open Questions

- Which Unity versions must be treated as first-class validation targets beyond `2022.3` and the current Unity 6 local project?
- Should the broad-control implementation remain private-fork only until the mutation model is fully proven, or should future slices be shaped for upstream contribution from the start?
