# ShaderGraph MCP Capabilities

## Purpose

This document is the source of truth for the ShaderGraph MCP surface currently exposed by the local private fork.

Related documents:

- `docs/dev/shadergraph-mcp-plan.md`
  - roadmap and historical epic/slice breakdown
- `docs/dev/futureDebt.MD`
  - deferred work, known limitations, validation gaps, and open questions

## Current Checkpoint

The current implementation is ready for renewed first-pass URP Shader Graph recreation trials from a reference image, visual concept, or existing example. The 2026-06-14 reflection-outline reference trial exposed a node-coverage blocker for reflection-driven vertex outline graphs; Epic 7A now implements and validates that core node set through the local Unity project-scoped MCP session.

The exposed MCP surface can create and inspect Shader Graph assets, mutate common URP graph settings, author common node/property/edge workflows, configure common URP stack blocks, assign texture assets, create materials from graphs, and validate graph/material texture behavior.

Agents can now create and configure the source-vector, space-transform, procedural-noise, and trigonometric nodes needed for the reflection-outline trial path. The remaining `View Vector` to `Gradient Noise.UV` gap is supported through an explicit ShaderGraph conversion path: `View Vector.Out -> Split.In`, `Split.R/G -> Combine.R/G`, then `Combine.RG -> Gradient Noise.UV`. Broader reference recreation parity still depends on concrete follow-up trials.

The second outline trial's confirmed `Object` node blocker is also implemented. Agents can create `UnityEditor.ShaderGraph.ObjectNode`, inspect its output slots, and wire `Object.Scale` into dynamic math nodes such as `Divide.B`.

The MinionsArt water investigation's remaining behavior-relevant node blockers are also implemented for URP-first authoring. Agents can now add, inspect, duplicate, move, delete, and configure `Scene Color`, `Comparison`, `Normal From Height`, `Blend`, `Remap`, and `Swizzle` nodes through the MCP surface without falling back to manual `.shadergraph` edits.

## Scope

- Focus: URP-first Shader Graph authoring.
- Implementation location: core `com.ivanmurzak.unity.mcp` package.
- Validation baseline: incremental live checks in the local Unity 6 URP/ShaderGraph workspace project at `Unity-test/TestShadergraph`.
- Reproducibility note: `Unity-test/` is git-ignored, so this validation project is local workspace state rather than a tracked repository fixture.
- Unity validation version used at the checkpoint: `6000.4.1f1`.
- Package baseline: `com.ivanmurzak.unity.mcp` version `0.81.0`.

## Exposure Model

ShaderGraph is exposed in Ivan's Extensions window as a built-in capability group named `ShaderGraph`.

Enabling or disabling that row toggles the ShaderGraph tool set as one group.

## Recommended Authoring Flow

1. Use `assets-shadergraph-find`, `assets-shadergraph-create`, or `assets-shadergraph-get-data` to select or create the graph.
2. Use `assets-shadergraph-get-structure` and `assets-shadergraph-get-settings` before mutation.
3. Configure root graph settings, URP target settings, and block stacks where needed.
4. Create or update blackboard properties for reusable values and project texture assets.
5. Add, duplicate, move, delete, and configure supported nodes.
6. Wire graph edges with explicit compatibility checks and replacement/reconnect guardrails.
7. Create or update materials from the graph shader.
8. Use `assets-shadergraph-validate-texture-workflow` when texture assignment must be verified across graph source and material readback.

## Exposed MCP Tools

### Discovery And Diagnostics

- `assets-shadergraph-find`
  - Finds Shader Graph assets by name or folder.
- `assets-shadergraph-get-data`
  - Inspects compiled shader resolution, diagnostics, optional shader messages, and optional compiled shader properties.
- `assets-shadergraph-get-structure`
  - Reads serialized properties, blackboard categories, nodes, slots, edges, and active targets from the graph source.
  - Property readback includes category object id, category name, and category index when available.
- `assets-shadergraph-get-settings`
  - Reads graph root settings and supported target settings from the graph source.

### Asset And Material Creation

- `assets-shadergraph-create`
  - Creates a new `.shadergraph` asset by cloning a known-good template.
- `assets-shadergraph-create-material`
  - Creates a `.mat` asset from the compiled shader resolved from a Shader Graph asset.
- `assets-shadergraph-create-from-style-recipe`
  - Validates a declarative style-recipe JSON payload, creates a graph and material, applies the currently supported material fields, and returns warnings for fields outside the current recipe implementation.
- `assets-shadergraph-validate-texture-workflow`
  - Creates or overwrites a `.mat` asset from a Shader Graph.
  - Reports graph texture references, material texture-property readback, graph diagnostics, and optional expectation checks.
  - Can copy Shader Graph blackboard `texture2D` asset defaults into matching material texture properties such as `_BaseMap`.
  - Reports direct unconnected `Sample Texture 2D.Texture` slot assets as graph-embedded references.

### Graph Settings Mutation

- `assets-shadergraph-set-settings`
  - Mutates supported graph root settings:
    - `graph.shaderMenuPath`
    - `graph.graphPrecision`
    - `graph.previewMode`
  - Mutates supported URP target settings:
    - `universalTarget.surfaceType`
    - `universalTarget.alphaMode`
    - `universalTarget.renderFace`
    - `universalTarget.depthWrite`
    - `universalTarget.depthTest`
    - `universalTarget.additionalMotionVectors`
    - `universalTarget.allowMaterialOverride`
    - `universalTarget.alphaClip`
    - `universalTarget.castShadows`
    - `universalTarget.receiveShadows`
    - `universalTarget.disableTint`
    - `universalTarget.alembicMotionVectors`
    - `universalTarget.supportsLodCrossFade`
    - `universalTarget.customEditorGui`
    - `universalTarget.supportVfx`

### Stack And Block Mutation

- `assets-shadergraph-set-blocks`
  - Sets the ordered built-in block stack for one selected context: `vertex` or `fragment`.
  - Replaces the selected context's supported built-in block list.
  - Creates missing requested blocks with Unity-compatible slots.
  - Removes omitted supported blocks only when unconnected unless `allowRemovingConnectedBlocks` is true.
  - Supported vertex blocks:
    - `position`
    - `normal`
    - `tangent`
    - `motionVector`
  - Supported fragment blocks:
    - `baseColor`
    - `normalTS`
    - `normalOS`
    - `normalWS`
    - `bentNormal`
    - `metallic`
    - `specular`
    - `smoothness`
    - `occlusion`
    - `emission`
    - `alpha`
    - `alphaClipThreshold`
    - `coatMask`
    - `coatSmoothness`
    - `normalAlpha`
    - `maosAlpha`

### Blackboard Property Mutation

Blackboard property mutation results include normalized summary fields:

- `operation`
- `propertyObjectId`
- `propertyReferenceName`
- `propertyKind`
- `changedFields`
- `property`
- `structure`
- `graph`
- `removedNodeCount` and `removedEdgeCount` for delete operations

- `assets-shadergraph-add-property`
  - Adds supported blackboard property types:
    - `color`
    - `float`
    - `texture2D`
    - `vector2`
    - `vector3`
    - `vector4`
    - `boolean`
  - Can place properties by `categoryObjectId` or `categoryName`.
  - Can create missing categories with `createCategoryIfMissing`.
  - Can insert at a zero-based `categoryIndex`.
  - Can assign a project default texture for `texture2D` through `textureAssetPath`.
- `assets-shadergraph-update-property`
  - Updates generic fields:
    - `displayName`
    - `overrideReferenceName`
    - `hidden`
    - `generatePropertyBlock`
  - Updates typed default values:
    - `colorHex`
    - `floatValue`
    - `vectorX`
    - `vectorY`
    - `vectorZ`
    - `vectorW`
    - `booleanValue`
    - `textureAssetPath`
    - `textureDefaultType`
    - `textureUseTilingAndOffset`
    - `textureUseTexelSize`
    - `textureIsMainTexture`
    - `textureIsHdr`
    - `textureModifiable`
  - For `texture2D` properties, omitted `textureAssetPath` means no change and an empty string clears the assigned default texture asset.
  - Texture property readback includes `textureAssetGuid` and `textureAssetPath` when a project texture asset is assigned.
- `assets-shadergraph-delete-property`
  - Deletes a blackboard property selected by object id or effective reference name.
  - Removes the property from root and category lists.
  - Removes dependent `PropertyNode` instances.
  - Removes edges connected to removed dependent `PropertyNode` instances.
  - Restores the original graph source if write/import finalization fails.
  - Returns a valid mutation response after the file is written even if post-delete structure or graph diagnostics readback fails; readback failures are reported as warning diagnostics.
- `assets-shadergraph-reorder-property`
  - Reorders a property inside its current category by zero-based `categoryIndex`.
  - Can move the property into a selected category while reordering.
- `assets-shadergraph-create-category`
  - Creates a Shader Graph blackboard category.
  - Category names must be unique.
- `assets-shadergraph-set-property-category`
  - Moves a property into a target category selected by object id or name.
  - Can create a missing category by name when `createCategoryIfMissing` is true.
  - Supports zero-based insertion with `categoryIndex`.
- `assets-shadergraph-add-property-node`
  - Creates a `PropertyNode` for an existing blackboard property.
  - Supports `color`, `float`, `texture2D`, `vector2`, `vector3`, `vector4`, and `boolean`.
  - Does not perform automatic edge wiring.

### Node Lifecycle Mutation

Node lifecycle mutation results include normalized summary fields:

- `operation`
- `nodeObjectId`
- `nodeType`
- `changedFields`
- `node`
- `structure`
- `graph`

- `assets-shadergraph-add-node`
  - Adds allowlisted graph nodes without automatic edge wiring.
  - Current allowlisted node families:
    - `add`
    - `subtract`
    - `multiply`
    - `divide`
    - `lerp`
    - `oneMinus`
    - `split`
    - `combine`
    - `sampleTexture2D`
    - `tilingAndOffset`
    - `branch`
    - `viewDirection` (`View Direction`)
    - `viewVector` (`View Vector`)
    - `normalVector` (`Normal Vector`)
    - `position` (`Position`)
    - `object` (`Object`)
    - `transform` (`Transform`)
    - `gradientNoise` (`Gradient Noise`)
    - `simpleNoise` (`Simple Noise`)
    - `uv` (`UV`)
    - `screenPosition` (`Screen Position`)
    - `sceneDepth` (`Scene Depth`)
    - `sceneColor` (`Scene Color`)
    - `comparison` (`Comparison`)
    - `normalFromHeight` (`Normal From Height`)
    - `blend` (`Blend`)
    - `remap` (`Remap`)
    - `swizzle` (`Swizzle`)
    - `time` (`Time`)
    - `smoothstep` (`Smoothstep`)
    - `saturate` (`Saturate`)
    - `vector2` (`Vector 2`)
    - `sine` (`Sine`)
    - `cosine` (`Cosine`)
    - `negate` (`Negate`)
- `assets-shadergraph-duplicate-node`
  - Duplicates a supported existing node by serialized `nodeObjectId`.
  - Supports `PropertyNode` plus the same allowlisted node families as `assets-shadergraph-add-node`.
  - Copies node settings, slots, and property references with fresh serialized object ids.
  - Does not copy edges.
- `assets-shadergraph-delete-node`
  - Deletes an existing node by serialized `nodeObjectId`.
  - Uses Unity's own node-removal flow and removes connected edges as part of the mutation.
  - Respects Unity's `canDeleteNode` restrictions.
- `assets-shadergraph-update-node-position`
  - Moves an existing node by serialized `nodeObjectId`.

### Node Settings Mutation

- `assets-shadergraph-update-node-settings`
  - Updates supported serialized settings on existing graph nodes.
  - Supported `Sample Texture 2D` fields:
    - `textureType`
    - `normalMapSpace`
    - `useGlobalMipBias`
    - `mipSamplingMode`
    - `textureSlotAssetPath`
    - `textureSlotDefaultType`
  - Supported `Tiling And Offset` fields:
    - `tiling.x`
    - `tiling.y`
    - `offset.x`
    - `offset.y`
  - Supported `Branch` fields:
    - `predicate`
    - `trueValue.x`
    - `trueValue.y`
    - `trueValue.z`
    - `trueValue.w`
    - `falseValue.x`
    - `falseValue.y`
    - `falseValue.z`
    - `falseValue.w`
  - Supported `Split` fields:
    - `input.x`
    - `input.y`
    - `input.z`
    - `input.w`
  - Supported `Combine` fields:
    - `r`
    - `g`
    - `b`
    - `a`
  - Supported `Add`, `Subtract`, and `Divide` fields:
    - `a.x`
    - `a.y`
    - `a.z`
    - `a.w`
    - `b.x`
    - `b.y`
    - `b.z`
    - `b.w`
  - Supported `Lerp` fields:
    - `a.x`
    - `a.y`
    - `a.z`
    - `a.w`
    - `b.x`
    - `b.y`
    - `b.z`
    - `b.w`
    - `t.x`
    - `t.y`
    - `t.z`
    - `t.w`
  - Supported `One Minus` fields:
    - `input.x`
    - `input.y`
    - `input.z`
    - `input.w`
  - Supported `Multiply` fields:
    - `multiplyType`
    - `a.x`
    - `a.y`
    - `a.z`
    - `a.w`
    - `b.x`
    - `b.y`
    - `b.z`
    - `b.w`
  - Supported `View Direction`, `View Vector`, and `Normal Vector` fields:
    - `space`
  - Supported `Position` fields:
    - `space`
    - `positionSource`
  - Supported `Transform` fields:
    - `inputSpace`
    - `outputSpace`
    - `transformType`
    - `normalize`
  - Supported Transform spaces: `object`, `view`, `world`, `tangent`, `absoluteWorld`, `screen`.
  - Supported Transform types: `position`, `direction`, `normal`.
  - Supported `Gradient Noise` fields:
    - `scale`
    - `hashType`
  - Supported `Simple Noise` fields:
    - `scale`
  - `Simple Noise` does not expose typed serialized enum settings today; the Unity 6 / URP 17 `NoiseNode` is slot-driven and exposes no stable serialized hash/type enum, so it is intentionally left slot-driven.
  - Supported `UV` fields:
    - `channel`
  - Supported UV channels: `UV0`, `UV1`, `UV2`, `UV3`.
  - Supported `Screen Position` fields:
    - `mode`
  - Supported Screen Position modes: `default`, `raw`.
  - Supported `Scene Depth` fields:
    - `samplingMode`
  - Supported Scene Depth sampling modes: `linear01`, `raw`, `eye`.
  - Supported `Comparison` fields:
    - `comparisonType`
  - Supported comparison operators: `equal`, `notEqual`, `less`, `lessOrEqual`, `greater`, `greaterOrEqual`.
  - Supported `Normal From Height` fields:
    - `outputSpace`
  - Supported Normal From Height output spaces: `tangent`, `world`.
  - `Normal From Height` structure readback also reports the current `strength` slot default when it is available from the graph source.
  - Supported `Blend` fields:
    - `blendMode`
  - Supported Blend modes: `burn`, `darken`, `difference`, `dodge`, `divide`, `exclusion`, `hardLight`, `hardMix`, `lighten`, `linearBurn`, `linearDodge`, `linearLight`, `linearLightAddSub`, `multiply`, `negation`, `overlay`, `pinLight`, `screen`, `softLight`, `subtract`, `vividLight`.
  - Supported `Swizzle` fields:
    - `mask`
  - Supported Swizzle masks: 1-4 characters from `xyzw` or `rgba`. Mixed notation such as `xg` is rejected loudly.
  - Supported `Remap` fields:
    - `input.x`
    - `input.y`
    - `input.z`
    - `input.w`
    - `inMinMax.x`
    - `inMinMax.y`
    - `outMinMax.x`
    - `outMinMax.y`
  - `Scene Color` does not expose typed serialized settings today; its meaningful behavior is driven through slot wiring and slot defaults surfaced by `assets-shadergraph-get-structure`.
  - Supported `Vector 2` fields:
    - `x`
    - `y`
  - Supported `Sine`, `Cosine`, and `Negate` fields:
    - `input.x`
    - `input.y`
    - `input.z`
    - `input.w`
  - Dynamic unary input defaults are intended for unconnected literal fallback values. For connected dynamic arithmetic chains, wire the upstream node output and inspect returned diagnostics instead of forcing a mismatched literal vector width first.

### Edge Mutation

- `assets-shadergraph-connect-edge`
  - Connects compatible existing graph slots by `nodeObjectId` plus `slotObjectId`.
  - Requires the input slot to be unconnected unless `replaceExistingInputConnection` is true.
  - Supports exact slot-type matches, compatible UV/vector2 pairs, scalar expansion into vector2 inputs such as `Float Property -> Tiling And Offset.Tiling`, vector2-resolved `DynamicVectorMaterialSlot -> UVMaterialSlot` paths such as `Add.Out -> Tiling And Offset.UV`, dynamic vector outputs into Shader Graph screen-position inputs such as `Branch.Out -> Scene Color.UV` and `Subtract.Out -> Scene Depth.UV`, compatible Vector3/Position pairs, `Vector3 -> NormalMaterialSlot` paths such as `Normal From Height.Out -> Fragment NormalWS`, Texture2D property outputs into Texture2D input slots, compatible dynamic numeric/vector/color slot families, and cross-family `DynamicValueMaterialSlot`/`DynamicVectorMaterialSlot` pairs.
  - Supports compatible color/vector slot pairs when one endpoint is a ShaderGraph color slot, such as `Vector4MaterialSlot -> ColorRGBMaterialSlot` for Color properties feeding Fragment Base Color.
  - Supports `ScreenPosition.Out` (`Vector4MaterialSlot`) into Shader Graph screen-position inputs such as `SceneDepth.UV` (`ScreenPositionMaterialSlot`).
  - Supports safe explicit Vector3-to-UV authoring through ShaderGraph narrowing nodes: `Vector3 -> Split -> Combine.RG -> UV`.
  - Direct `Vector3MaterialSlot -> UVMaterialSlot` edges remain intentionally rejected until Unity's serialized graph model is validated to allow that conversion safely without an explicit node.
  - Returns `removedEdge` when an incoming edge is replaced.
- `assets-shadergraph-reconnect-edge`
  - Reconnects an exact existing edge to a new output endpoint, input endpoint, or both.
  - Supports the same slot compatibility matrix as `assets-shadergraph-connect-edge`.
  - Rejects no-op reconnects.
  - Can replace another incoming edge on the new target input when `replaceExistingInputConnection` is true.
- `assets-shadergraph-reroute-output-slot`
  - Moves every outgoing edge from one output slot to another compatible output slot.
  - Preflights every downstream input before any write is persisted.
  - Refuses to overwrite unrelated incoming edges or create duplicate edges.
- `assets-shadergraph-disconnect-edge`
  - Removes an existing edge selected by output node and slot plus input node and slot.

## Current Extensions Window Group

The built-in `ShaderGraph` entry currently groups these tool ids:

- `assets-shadergraph-find`
- `assets-shadergraph-get-data`
- `assets-shadergraph-get-structure`
- `assets-shadergraph-get-settings`
- `assets-shadergraph-create`
- `assets-shadergraph-create-material`
- `assets-shadergraph-validate-texture-workflow`
- `assets-shadergraph-create-from-style-recipe`
- `assets-shadergraph-set-settings`
- `assets-shadergraph-set-blocks`
- `assets-shadergraph-update-property`
- `assets-shadergraph-add-property`
- `assets-shadergraph-delete-property`
- `assets-shadergraph-reorder-property`
- `assets-shadergraph-create-category`
- `assets-shadergraph-set-property-category`
- `assets-shadergraph-add-property-node`
- `assets-shadergraph-add-node`
- `assets-shadergraph-duplicate-node`
- `assets-shadergraph-delete-node`
- `assets-shadergraph-update-node-settings`
- `assets-shadergraph-update-node-position`
- `assets-shadergraph-connect-edge`
- `assets-shadergraph-reconnect-edge`
- `assets-shadergraph-reroute-output-slot`
- `assets-shadergraph-disconnect-edge`

## Validation State

- The current exposed surface has been validated through incremental live Unity checks in the local Unity 6 workspace validation project at `Unity-test/TestShadergraph`.
- The 2026-06-14 reference-image trial created a valid graph, material, and scene, but exact recreation of a reflection-based outline ShaderGraph was blocked first by missing node support for `View Direction`, `View Vector`, `Normal Vector`, `Position`, `Transform`, `Gradient Noise`, `Sine`, `Cosine`, and `Negate`, then by the need for safe `View Vector` narrowing into `Gradient Noise.UV`.
- Epic 7A now adds code support for those node families, typed settings/readback where stable, Vector3/Position slot compatibility, cross-family dynamic slot compatibility, explicit View Vector UV narrowing through `Split` plus `Combine.RG`, and an editor-test e2e case for the reflection-outline path.
- The second outline trial confirmed one additional node blocker: `Object`. Current support adds `nodeType: "object"` for `UnityEditor.ShaderGraph.ObjectNode`, readback for `Position`, `Scale`, `World Bounds Min`, `World Bounds Max`, and `Bounds Size` output slots, and a simple object-scale outline e2e validation path.
- The MinionsArt water-shader pretrial confirmed another concrete core-node gap. Current support adds `screenPosition`, `sceneDepth`, `time`, `smoothstep`, `saturate`, and real `vector2` node creation/readback, typed settings for Screen Position mode, Scene Depth sampling mode, and Vector 2 X/Y defaults, plus a minimal water-core Lit graph validation path.
- The exact MinionsArt reference graph, [StylizedWaterInteractiveUpdate.shadergraph](/Users/suporte/Unity-MCP/Unity-test/TestShadergraph/Assets/ShaderGraphValidation/MinionsArtWaterTrial/StylizedWaterInteractiveUpdate.shadergraph), exposed a second behavior-node gap. Current support now covers `sceneColor`, `comparison`, `normalFromHeight`, `blend`, `remap`, and `swizzle`, including typed readback for serialized settings, typed updates for `comparisonType`, `normalFromHeight.outputSpace`, `blendMode`, and `swizzle.mask`, plus narrow compatibility rules for `Normal From Height.Out -> Fragment NormalWS` and dynamic vector outputs into screen-position UV slots used by `Scene Color` and `Scene Depth`.
- `RedirectNodeData` remains intentionally unsupported for add/duplicate flows. It is treated as non-essential layout/readability data rather than a behavior blocker unless a future trial proves otherwise.
- Lit/PBR-style output blocks are supported when the graph is created from a Lit template and then managed with `assets-shadergraph-set-blocks`; converting an existing Unlit graph target/subtarget into Lit/PBR is not currently exposed.
- Compile sanity check, `dotnet build Assembly-CSharp.csproj -v minimal`, passed in that local project on 2026-06-15 with existing Unity/API warnings and 0 errors.
- Epic 7A live validation through the already-open project-scoped MCP/editor session passed on 2026-06-14:
  - `Codex_Epic7A_SettingsSmoke.shadergraph`: add/update/readback for all Epic 7A nodes, duplicate/move/delete for `Transform`, final `ShaderResolved=true`, `HasErrors=false`.
  - `Codex_Epic7A_E2E.shadergraph`: reflection-outline path with source vectors, trig chain, gradient-noise displacement modulation, `Transform -> VertexDescription.Position`, preserved base texture/color output, final `ShaderResolved=true`, `HasErrors=false`, 22 nodes and 15 edges.
  - `Codex_ViewVectorUV_ReflectionOutline.shadergraph`: exact trial path with `View Vector -> Split -> Combine.RG -> Gradient Noise.UV`, source-vector nodes, trig chain, gradient-noise displacement modulation, `Transform -> VertexDescription.Position`, preserved base texture/color output, final `ShaderResolved=true`, `HasErrors=false`, 24 nodes and 19 edges.
  - Short readback probe confirmed `Position.space=absoluteWorld`, `Transform` conversion `absoluteWorld -> object`, `GradientNoise.scale=18`, and the final vertex-position edge.
  - Short readback probe confirmed the explicit `View Vector -> Split -> Combine.RG -> Gradient Noise.UV` edge path.
- Second-trial live validation through the already-open project-scoped MCP/editor session passed on 2026-06-14:
  - `Codex_ObjectScaleOutline.shadergraph`: simple object-scale outline graph with `Thickness -> Divide.A`, `Object.Scale -> Divide.B`, `Position(Object) -> Multiply/Add`, `Add -> VertexDescription.Position`, `Outline Color -> SurfaceDescription.BaseColor`, Universal target `surface=opaque`, `renderFace=back`, final `ShaderResolved=true`, `HasErrors=false`, 17 nodes and 11 edges.
- Water-shader pretrial code support added editor tests for new node creation/readback/settings and a minimal Lit water-core graph covering `Screen Position -> Scene Depth`, depth fade through `Smoothstep`/`Saturate`, `Time`-driven smoothness modulation, real `Vector 2 -> Sample Texture 2D.UV`, and Base Color/Alpha/Smoothness Lit outputs.
- Water-shader pretrial live validation through the already-open project-scoped MCP/editor session passed on 2026-06-14:
  - `Codex_WaterCore_MinionsArt.shadergraph`: Lit-template graph with `ScreenPosition.Out -> SceneDepth.UV`, Scene Depth `eye`, Screen Position `raw`, `Smoothstep`/`Saturate` depth fade, `Time.Sine Time` modulation, real `Vector2(0.25, 0.75) -> Sample Texture 2D.UV`, and Base Color/Alpha/Smoothness Lit outputs. Final `ShaderResolved=true`, `HasErrors=false`, 109 nodes and 116 edges.
- Follow-up live validation through the already-open project-scoped MCP/editor session passed on 2026-06-15:
  - `Codex_Water_AddUvScriptValidation.shadergraph`: cloned from the water-core graph, then authored through the ShaderGraph tool methods with `Add.Out -> Tiling And Offset.UV`, `ReconnectEdge` retargeting the same UV input to `Add.Out`, and `RerouteOutputSlot` moving two legacy UV consumers onto `Add.Out`. Final `ShaderResolved=true`, `HasErrors=false`, 117 nodes and 122 edges.
- Full-water behavior-node parity validation for the new MinionsArt blockers passed through readback, tests, and live MCP mutation on 2026-06-15:
  - [StylizedWaterInteractiveUpdate.shadergraph](/Users/suporte/Unity-MCP/Unity-test/TestShadergraph/Assets/ShaderGraphValidation/MinionsArtWaterTrial/StylizedWaterInteractiveUpdate.shadergraph): readback confirmed `Comparison=less`, `Scene Color` slot topology, `Normal From Height.outputSpace=world`, `Swizzle.mask=xz`, `Remap` vector2 min/max slots, `Blend=screen`, and two `RedirectNodeData` layout nodes.
  - `Validation_AddNode_MinionsArtWaterBehavior.shadergraph`: editor test created all six new node families and exercised duplicate, move, and delete on `Blend`.
  - `Validation_UpdateNodeSettings_MinionsArtWaterBehavior.shadergraph`: editor test updated `comparisonType`, `normalFromHeight.outputSpace`, `blendMode`, and `swizzle.mask`, verified `swizzle.mask=xz` changed slot topology to `Vector3 -> Vector2`, and confirmed invalid mixed notation is rejected loudly.
  - `Validation_MinionsArtWaterBehaviorPath.shadergraph`: editor test recreated the supported behavior path with `Scene Color`, `Swizzle`, `Remap`, `Blend`, `Normal From Height`, `Comparison`, Lit blocks, and `Normal From Height.Out -> Fragment NormalWS`, with final `ShaderResolved=true` and `HasErrors=false`.
  - `Validation_MinionsArtWaterDynamicScreenPositionEdges.shadergraph`: editor test covers the original dynamic-vector screen-position edges directly: `Branch.Out -> Scene Color.UV`, `Branch.Out -> Scene Depth.UV`, and `Subtract.Out -> Scene Depth.UV`, with final `ShaderResolved=true` and `HasErrors=false`.
  - `Validation_MinionsArtWaterScalarToTilingEdges.shadergraph`: editor test covers the original scalar expansion edges directly: `PropertyNode(Distort Scale).Distort Scale -> Tiling And Offset.Tiling` and `PropertyNode(Noise Scale).Noise Scale -> Tiling And Offset.Tiling`, with final `ShaderResolved=true` and `HasErrors=false`.
  - `Validation_MinionsArtWaterLiteralDefaults.shadergraph`: editor test covers `Multiply.B = 0.1` literal default readback through Unity's matrix-backed dynamic value slot and `Remap.In Min Max = (0, 1)` typed update/readback, with final `ShaderResolved=true` and `HasErrors=false`.
- Epic 7E flame-trial node-gap validation through the already-open project-scoped MCP/editor session passed on 2026-06-17:
  - Throwaway `Codex_FlameTrial_NodeProbe.shadergraph` under `Assets/Unity-MCP-Test/Trials/Flames/`: created `uv` with default channel `UV0` and `simpleNoise` with default `Scale=500`, cycled `uv.channel` through `UV1` and `UV3`, applied `simpleNoise.scale=250`, wired `UV.Out -> Add.A` and `Add.Out -> Simple Noise.UV`, and verified loud failure on `uv.channel=UV9` and on an empty `simpleNoise` payload. Final state reported 13 nodes, 6 edges, `ShaderResolved=true`, `HasErrors=false`; the asset was deleted after validation.
- A Unity batch-mode editor test run was intentionally not performed while the project was already open in a Unity Editor instance.
- A targeted MCP `tests-run` attempt for the new editor test was blocked by an unsaved open scene; Codex did not save editor scene state automatically.
- A targeted MCP `tests-run` attempt for `ShaderGraph_WaterCorePath_CanBeWiredEndToEnd` did not discover the package editor test from the local validation project.
- The local `Unity-test/TestShadergraph` project is available in this workspace, but it is git-ignored and should not be treated as a reproducible repository fixture until explicitly promoted or documented as setup-required state.
- Unity package editor test discovery for the ShaderGraph tests still needs to be fixed before this can be treated as a complete automated validation baseline.
- Future gaps and non-blocking limitations are tracked in `docs/dev/futureDebt.MD`.
