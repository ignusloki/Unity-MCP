# Shader Graph MCP Roadmap

## Purpose

This document is the stable roadmap for ShaderGraph MCP work in the user's private fork. It defines the goal, sequencing rules, and sorted epics/slices.

Update this file only when the roadmap changes. For the currently exposed tool surface, use `docs/dev/shadergraph-mcp-capabilities.md`. For deferred work and accepted limitations, use `docs/dev/futureDebt.MD`.

## Related Documents

- `docs/dev/shadergraph-mcp-capabilities.md`
  - currently exposed ShaderGraph MCP tools and user-facing capability surface
- `docs/dev/futureDebt.MD`
  - deferred work, accepted limitations, validation gaps, and open questions

## Goal

Add safe, incremental Unity MCP support for Shader Graph discovery, diagnostics, safe creation, and progressively broader graph control in the user's private fork, with user-facing capability gating in Ivan's Extensions UI.

The current priority is URP-first practical authoring parity: enough control for an AI agent to build, edit, and repair typical URP Shader Graphs without falling back to manual Unity work.

## Direction

The original template-expansion track was deferred after Epic 5. The active priority is now direct ShaderGraph control.

Principles:

- Prefer explicit, typed mutation tools over broad raw JSON editing.
- Keep support URP-first until common graph-authoring workflows are stable.
- Validate each mutation slice in Unity before committing it.
- Force graph reimport after mutations and return diagnostics.
- Fail loudly on unsupported or ambiguous operations.
- Keep long-tail node and render-pipeline support behind common URP graph-control gaps.

## Branch And Workflow Rules

- Follow `git.MD`.
- `main` stays clean and fast-forwardable to `upstream/main`.
- `custom/main` is the private integration branch.
- ShaderGraph MCP feature work lives on `custom/shadergraph-mcp`.
- Historical epic-named branches are superseded once their code is integrated into `custom/shadergraph-mcp`.
- Do not push, merge, or open a PR without explicit user approval.
- Commit only after the user validates the current slice, unless the user explicitly asks for a docs-only or planning commit.

## Definition Of Done

This ShaderGraph control track is done when an agent can:

- Inspect a URP Shader Graph structurally and semantically.
- Create, delete, and duplicate common URP authoring nodes.
- Create, delete, duplicate, and configure core source-vector, space-transform, procedural-noise, and unary math nodes needed by reference-driven shader recreation.
- Edit meaningful serialized settings on supported nodes.
- Create, update, delete, reorder, and categorize common blackboard property types.
- Create property nodes for supported property types.
- Connect, disconnect, replace, and reconnect edges across the supported slot compatibility matrix.
- Perform common graph-repair rewiring flows with explicit guardrails.
- Control important URP graph settings, target settings, and stack/block coverage.
- Assign project texture assets in common texture workflows.
- Force reimport after mutations and return diagnostics.
- Fail loudly on unsupported or ambiguous operations.

## Sequencing Rules

- Epic numbers define workstreams, not mandatory execution order.
- The next slice should come from the highest-value unresolved control gap.
- A later epic may be worked before an earlier epic if it unlocks practical ShaderGraph control sooner.
- When an epic is broken into slices, record those slices here before implementation starts.
- Keep deferred work and accepted limitations in `docs/dev/futureDebt.MD`, not in this roadmap.

## Epic 0: Reconnaissance, Workflow Review, And Branch Setup

Purpose:

- Establish the repository workflow.
- Confirm local Unity validation setup.
- Create the initial ShaderGraph MCP roadmap.

Scope:

- Read prompt and repo state.
- Confirm branch rules from `git.MD`.
- Create or select the correct private branch.
- Install and validate local package usage in the Unity test project.
- Keep Unity and Codex MCP configuration project-scoped.

## Epic 1: Read-Only Shader Graph Discovery And Diagnostics

Purpose:

- Let agents discover Shader Graph assets and inspect compiled shader health without mutating graph files.

Slices:

- Slice 1.1: find Shader Graph assets by name and folder.
- Slice 1.2: return compiled shader resolution, shader name, diagnostics, and optional messages.
- Slice 1.3: return optional compiled shader properties.

## Epic 2: Safe Creation, Material Creation, And Style-Recipe Foundation

Purpose:

- Create known-good Shader Graph and Material assets safely.

Slices:

- Slice 2.1: clone a known-good `.shadergraph` template.
- Slice 2.2: create a Material from the compiled shader resolved from a Shader Graph.
- Slice 2.3: create graph and material assets from a style recipe.
- Slice 2.4: return explicit warnings for recipe fields that are intentionally deferred.

## Epic 3: Read-Only Graph Structure Introspection

Purpose:

- Give agents a structural view of Shader Graph source files before they mutate anything.

Slices:

- Slice 3.1: read blackboard properties.
- Slice 3.2: read nodes and slots.
- Slice 3.3: read edges.
- Slice 3.4: read active targets and vertex/fragment contexts.

## Epic 4: Graph Settings Inspection And Mutation Baseline

Purpose:

- Expose safe root graph settings and URP target settings.

Slices:

- Slice 4.1: inspect graph root settings and URP target settings.
- Slice 4.2: mutate root graph settings.
- Slice 4.3: mutate safe URP target settings.
- Slice 4.4: return changed fields, reimport diagnostics, and compiled shader health.

## Epic 5: First-Wave Graph Mutation Proof

Purpose:

- Prove end-to-end graph mutation with a small set of property, node, and edge edits.

Slices:

- Slice 5.1: update existing blackboard properties.
- Slice 5.2: add basic blackboard properties.
- Slice 5.3: create basic PropertyNode entries.
- Slice 5.4: move existing graph nodes.
- Slice 5.5: connect and disconnect baseline graph edges.

## Epic 6: ShaderGraph Extensions Entry And Capability Gating

Purpose:

- Let users enable or disable ShaderGraph MCP control from Ivan's Extensions UI.

Slices:

- Slice 6.1: add a built-in `ShaderGraph` entry to the Extensions window.
- Slice 6.2: group ShaderGraph tool ids behind that entry.
- Slice 6.3: validate that disabling the group disables ShaderGraph tools without affecting unrelated MCP tools.

## Epic 7: Node Lifecycle Foundation

Purpose:

- Give agents lifecycle control over common URP authoring nodes.

Slices:

- Slice 7.1: add allowlisted node creation.
- Slice 7.2: add node deletion with edge cleanup and Unity `canDeleteNode` guardrails.
- Slice 7.3: add node duplication for supported node families.
- Slice 7.4: normalize lifecycle result payloads across add, delete, duplicate, and move operations.

Initial node families:

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
- `PropertyNode`

## Epic 7A: Core Reference-Driven Node Expansion

Status:

- Implemented in code on `custom/shadergraph-mcp`.
- Compile sanity check passed in the local Unity 6 validation project.
- Live Unity Editor validation through the existing project-scoped MCP session passed on 2026-06-14.

Purpose:

- Close the concrete node-coverage gap found during the 2026-06-14 reflection-outline reference-image trial.
- Promote common source-vector, transform, procedural-noise, and unary math nodes out of long-tail debt because they are required for practical reference-driven ShaderGraph recreation.

Required node families:

- `viewDirection` (`View Direction`)
- `viewVector` (`View Vector`)
- `normalVector` (`Normal Vector`)
- `position` (`Position`)
- `transform` (`Transform`)
- `gradientNoise` (`Gradient Noise`)
- `sine` (`Sine`)
- `cosine` (`Cosine`)
- `negate` (`Negate`)

Implementation plan:

- Slice 7A.1: add source-vector node creation for `viewDirection`, `viewVector`, `normalVector`, and `position`, including typed space settings where Unity exposes them. Implemented.
- Slice 7A.2: add `transform` node creation with explicit input space, output space, and transform type settings; reject unsupported space/type combinations loudly. Implemented.
- Slice 7A.3: add unary math node creation for `sine`, `cosine`, and `negate`, and include them in duplicate/delete/move flows. Implemented.
- Slice 7A.4: add `gradientNoise` node creation with safe default scale handling and typed settings for scale where serialized behavior is stable. Implemented.
- Slice 7A.5: extend `assets-shadergraph-update-node-settings`, node readback, tool descriptions, and docs for the new node families. Implemented.
- Slice 7A.6: expand slot compatibility only where these nodes expose concrete unsupported output/input pairs during validation. Implemented for Vector3/Position slot pairs and cross-family `DynamicValueMaterialSlot`/`DynamicVectorMaterialSlot` pairs. Direct `Vector3 -> UV` remains intentionally rejected; the supported workflow is explicit narrowing through `Split` and `Combine.RG`.
- Slice 7A.7: add editor tests that create, inspect, duplicate, move, wire, and reimport each new node family. Implemented in editor tests; live MCP validation passed; automated Unity test-run execution remains deferred to Epic 15.
- Slice 7A.8: add an end-to-end validation case that recreates the reflection-outline graph path from the reference trial: view/vector source nodes, explicit `View Vector -> Split -> Combine.RG -> Gradient Noise.UV` narrowing, reflection-normal trig chain, gradient-noise displacement modulation, transform, vertex position output, base texture/color output. Implemented in editor tests; live MCP validation passed; automated Unity test-run execution remains deferred to Epic 15.

Validation requirements:

- Each slice must create a graph through MCP, force reimport, return no ShaderGraph diagnostics errors, and verify the created node is discoverable through `assets-shadergraph-get-structure`.
- The final validation scene must prove the material uses the generated ShaderGraph rather than a proxy shell or handwritten shader fallback.
- Epic 7A local validation evidence:
  - `Codex_Epic7A_SettingsSmoke.shadergraph`: all new nodes created, typed settings read back, `Transform` duplicate/move/delete passed, final import reported `ShaderResolved=true` and `HasErrors=false`.
  - `Codex_Epic7A_E2E.shadergraph`: reflection-outline path wired with 22 nodes and 15 edges, including `Transform -> VertexDescription.Position`, preserved base texture/color output, final import reported `ShaderResolved=true` and `HasErrors=false`.
  - `Codex_ViewVectorUV_ReflectionOutline.shadergraph`: exact trial path wired with 24 nodes and 19 edges, including `View Vector -> Split -> Combine.RG -> Gradient Noise.UV`, `Transform -> VertexDescription.Position`, preserved base texture/color output, final import reported `ShaderResolved=true` and `HasErrors=false`.
  - Unity test-runner discovery for package editor tests is still tracked under Epic 15.

## Epic 7B: Object-Scale Outline Trial Gap

Status:

- Implemented in code on `custom/shadergraph-mcp`.
- Compile sanity check passed in the local Unity 6 validation project.
- Live Unity Editor validation through the existing project-scoped MCP session passed on 2026-06-14.

Purpose:

- Close the confirmed node gap found during the second outline trial: ShaderGraph `Object` node support.
- Validate the simple object-scale outline topology needed by that trial.

Required node family:

- `object` (`Object`, `UnityEditor.ShaderGraph.ObjectNode`)

Implementation plan:

- Slice 7B.1: add `object` to the `assets-shadergraph-add-node` allowlist and public input descriptions. Implemented.
- Slice 7B.2: verify structure readback exposes Object output slots, especially `Scale`. Implemented.
- Slice 7B.3: verify `Object.Scale -> Divide.B` can be wired through the existing dynamic slot compatibility path. Implemented.
- Slice 7B.4: add an end-to-end simple outline validation case with `Thickness`, `Object.Scale`, `Position(Object)`, `Divide`, `Multiply`, `Add`, vertex position output, outline color to fragment base color, and Universal target `opaque/back`. Implemented.

Validation evidence:

- `Codex_ObjectScaleOutline.shadergraph`: live MCP validation passed with 17 nodes and 11 edges, including `Object.Scale -> Divide.B`, `Add -> VertexDescription.Position`, `Outline Color -> SurfaceDescription.BaseColor`, Universal target `surface=opaque`, `renderFace=back`, final import reported `ShaderResolved=true` and `HasErrors=false`.
- Targeted Unity `tests-run` for the new editor test was blocked by an unsaved open scene in the existing editor session; Codex did not save user scene state automatically.

## Epic 7C: MinionsArt Water Shader Trial Gap

Status:

- Implemented in code on `custom/shadergraph-mcp`.
- Compile sanity check passed in the local Unity 6 validation project with existing Unity/API warnings and 0 errors.
- Live Unity Editor validation through the existing project-scoped MCP session passed on 2026-06-14.

Purpose:

- Close the confirmed node and wiring gaps found before attempting MinionsArt's water shader trial.
- Validate that the MCP can author the water graph's core screen-depth, time, smoothing, saturation, vector2 UV, and Lit/PBR output paths without manual Shader Graph JSON edits.

Required node families:

- `screenPosition` (`Screen Position`, `UnityEditor.ShaderGraph.ScreenPositionNode`)
- `sceneDepth` (`Scene Depth`, `UnityEditor.ShaderGraph.SceneDepthNode`)
- `time` (`Time`, `UnityEditor.ShaderGraph.TimeNode`)
- `smoothstep` (`Smoothstep`, `UnityEditor.ShaderGraph.SmoothstepNode`)
- `saturate` (`Saturate`, `UnityEditor.ShaderGraph.SaturateNode`)
- `vector2` (`Vector 2`, `UnityEditor.ShaderGraph.Vector2Node`)

Implementation plan:

- Slice 7C.1: add the water-core node families to `assets-shadergraph-add-node` and public tool descriptions. Implemented.
- Slice 7C.2: expose readback for Screen Position mode, Scene Depth sampling mode, Time outputs, Smoothstep slots, Saturate slots, and Vector 2 slots. Implemented.
- Slice 7C.3: add typed settings for `screenPosition.mode`, `sceneDepth.samplingMode`, and `vector2.x/y`; reject unsupported Screen Position modes loudly. Implemented.
- Slice 7C.4: validate and support `ScreenPosition.Out -> SceneDepth.UV` without broadening unsafe vector conversions. Implemented.
- Slice 7C.5: verify Lit/PBR-style outputs through a Lit template plus `assets-shadergraph-set-blocks`; document that target/subtarget switching from an existing Unlit graph is not exposed. Implemented.
- Slice 7C.6: add editor tests for node creation/readback/settings and a minimal water-core e2e graph. Implemented.
- Slice 7C.7: run live MCP validation in the existing Unity editor and record the generated graph evidence. Implemented.
- Slice 7C.8: close the concrete MinionsArt UV-math blocker by supporting vector2-resolved `DynamicVectorMaterialSlot -> UVMaterialSlot` paths such as `Add.Out -> Tiling And Offset.UV`, and validate connect, reconnect, and reroute on that path. Implemented.

Validation requirements:

- Create a graph through MCP, force reimport, return no ShaderGraph diagnostics errors, and verify every new node is discoverable through `assets-shadergraph-get-structure`.
- The minimal water-core graph must include `Screen Position -> Scene Depth`, `Smoothstep`, `Saturate`, `Time`, real `Vector 2`, Base Color output, and at least one Lit/PBR-style output block such as Smoothness or Alpha.
- Do not fake Lit/PBR support by mutating unsupported target/subtarget internals; use a Lit template until explicit target switching is implemented.

Validation evidence:

- `Codex_WaterCore_MinionsArt.shadergraph`: live MCP validation passed with 109 nodes and 116 edges, including `ScreenPosition.Out -> SceneDepth.UV`, Screen Position `raw`, Scene Depth `eye`, `Vector2(0.25, 0.75) -> Sample Texture 2D.UV`, `Saturate.Out -> Alpha`, sampled texture to Base Color, and time/depth-driven Smoothness. Final graph data reported `ShaderResolved=true` and `HasErrors=false`.
- `Codex_Water_AddUvScriptValidation.shadergraph`: follow-up live validation passed with 117 nodes and 122 edges after authoring `Add.Out -> Tiling And Offset.UV`, retargeting the same UV input through `ReconnectEdge`, and rerouting two legacy UV consumers through `RerouteOutputSlot`. Final graph data reported `ShaderResolved=true` and `HasErrors=false`.
- A targeted Unity `tests-run` attempt for `ShaderGraph_WaterCorePath_CanBeWiredEndToEnd` did not discover the package editor test from the local validation project; automated package test discovery remains tracked under Epic 15.

## Epic 7D: MinionsArt Full-Water Behavior Node Parity

Status:

- Implemented in code on `custom/shadergraph-mcp`.
- Compile sanity check passed in the local Unity 6 validation project on 2026-06-15 with existing Unity/API warnings and 0 errors.
- Structure readback, editor-test validation, and live MCP mutation validation passed on 2026-06-15.

Purpose:

- Close the remaining behavior-relevant node-coverage gap exposed by the original MinionsArt water reference graph.
- Let a follow-up agent recreate the behavior-bearing portions of `StylizedWaterInteractiveUpdate.shadergraph` without manual `.shadergraph` edits or prior knowledge of these missing families.

Required node families:

- `sceneColor` (`Scene Color`, `UnityEditor.ShaderGraph.SceneColorNode`)
- `comparison` (`Comparison`, `UnityEditor.ShaderGraph.ComparisonNode`)
- `normalFromHeight` (`Normal From Height`, `UnityEditor.ShaderGraph.NormalFromHeightNode`)
- `blend` (`Blend`, `UnityEditor.ShaderGraph.BlendNode`)
- `remap` (`Remap`, `UnityEditor.ShaderGraph.RemapNode`)
- `swizzle` (`Swizzle`, `UnityEditor.ShaderGraph.SwizzleNode`)

Implementation plan:

- Slice 7D.1: add the six behavior-node families to `assets-shadergraph-add-node`, duplicate/delete/move flows, and public tool descriptions. Implemented.
- Slice 7D.2: extend `assets-shadergraph-get-structure` to expose meaningful typed readback for serialized settings and stable slot topology taken from the original reference graph. Implemented.
- Slice 7D.3: add typed `assets-shadergraph-update-node-settings` support for `comparisonType`, `normalFromHeight.outputSpace`, `blendMode`, and `swizzle.mask`; keep `sceneColor` slot-driven and reject unsupported values loudly. Implemented.
- Slice 7D.4: validate safe swizzle-mask normalization and topology changes, including loud rejection of mixed `xyzw`/`rgba` notation. Implemented.
- Slice 7D.5: add only the concrete extra edge compatibility required by the behavior path: `Vector3 -> NormalMaterialSlot` for flows such as `Normal From Height.Out -> Fragment NormalWS`. Implemented.
- Slice 7D.6: add editor tests for create, inspect, duplicate, move, delete, wire, reimport, and diagnostics across the new behavior nodes. Implemented.
- Slice 7D.7: inspect `RedirectNodeData` and keep it deferred as non-essential layout/readability data unless a future trial proves it is behavior-relevant. Implemented as documentation/positioning only.
- Slice 7D.8: add the concrete dynamic-vector screen-position compatibility needed by the original graph: `Branch.Out -> Scene Color.UV`, `Branch.Out -> Scene Depth.UV`, and `Subtract.Out -> Scene Depth.UV`. Implemented.
- Slice 7D.9: add the concrete scalar-to-vector2 compatibility needed by the original graph: `Distort Scale -> Tiling And Offset.Tiling` and `Noise Scale -> Tiling And Offset.Tiling`. Implemented.
- Slice 7D.10: add concrete literal-default editing needed by the original graph: `Multiply.A/B`, including matrix-backed dynamic value slots such as `Multiply.B = 0.1`, and Remap `In`, `In Min Max`, and `Out Min Max`. Implemented.

Validation requirements:

- Read the original reference asset structurally before changing behavior support.
- Verify every new node family is addable and discoverable through `assets-shadergraph-get-structure`.
- Verify typed settings updates reimport cleanly and return explicit diagnostics.
- Verify the supported behavior path can wire into Lit outputs without manual JSON editing.
- Do not broaden slot compatibility beyond the exact validated cases needed by the reference path.

Validation evidence:

- [StylizedWaterInteractiveUpdate.shadergraph](/Users/suporte/Unity-MCP/Unity-test/TestShadergraph/Assets/ShaderGraphValidation/MinionsArtWaterTrial/StylizedWaterInteractiveUpdate.shadergraph): reference-graph readback confirmed `Comparison=less`, `Scene Color` slot topology, `Normal From Height.outputSpace=world`, `Swizzle.mask=xz`, `Remap` vector2 min/max slots, `Blend=screen`, and two `RedirectNodeData` layout nodes.
- `Validation_AddNode_MinionsArtWaterBehavior.shadergraph`: editor test created the six new node families and exercised duplicate, move, and delete on `Blend`.
- `Validation_UpdateNodeSettings_MinionsArtWaterBehavior.shadergraph`: editor test updated `comparisonType`, `normalFromHeight.outputSpace`, `blendMode`, and `swizzle.mask`, confirmed the `xz` mask rewired the node shape to `Vector3 -> Vector2`, and verified unsupported mixed notation fails loudly.
- `Validation_MinionsArtWaterBehaviorPath.shadergraph`: editor test recreated the supported behavior path with `Scene Color`, `Swizzle`, `Remap`, `Blend`, `Normal From Height`, `Comparison`, Lit blocks, and `Normal From Height.Out -> Fragment NormalWS`, with final `ShaderResolved=true` and `HasErrors=false`.
- `Validation_MinionsArtWaterDynamicScreenPositionEdges.shadergraph`: editor test directly covered the original dynamic-vector screen-position UV paths: `Branch.Out -> Scene Color.UV`, `Branch.Out -> Scene Depth.UV`, and `Subtract.Out -> Scene Depth.UV`, with final `ShaderResolved=true` and `HasErrors=false`.
- `Validation_MinionsArtWaterScalarToTilingEdges.shadergraph`: editor test directly covered the original scalar expansion paths: `PropertyNode(Distort Scale).Distort Scale -> Tiling And Offset.Tiling` and `PropertyNode(Noise Scale).Noise Scale -> Tiling And Offset.Tiling`, with final `ShaderResolved=true` and `HasErrors=false`.
- `Validation_MinionsArtWaterLiteralDefaults.shadergraph`: editor test directly covered `Multiply.B = 0.1` literal default readback and `Remap.In Min Max = (0, 1)` typed update/readback, with final `ShaderResolved=true` and `HasErrors=false`.
- `Validation_DeleteMinionsArtWaterProperties.shadergraph`: editor test copied the recreated MinionsArt trial graph and deleted every blackboard property one by one, including `_GlobalEffectRT`, with a non-null mutation response for every delete and final structure readback containing zero properties.
- `Codex_MinionsArt_NodeCoverage.shadergraph`: transient live MCP validation asset created in the local project-scoped Unity session on 2026-06-15; `Scene Color` add-node and `Swizzle.mask=xz` update/readback succeeded without diagnostics regressions.

## Epic 7E: Flame Trial Node Gap

Status:

- Implemented in code on `custom/shadergraph-mcp`.
- Compile sanity check passed in the local Unity 6 validation project on 2026-06-17 with existing Unity/API warnings and 0 errors.
- Live Unity Editor validation through the existing project-scoped MCP session passed on 2026-06-17.

Purpose:

- Close the concrete node-coverage gap exposed by a common Unity flame shader trial that needs an explicit `UV` source node and the `Simple Noise` procedural node.
- Promote both families out of the long-tail backlog because typical reference-driven flame, fire, and dissolve shader recreation needs them on the same path as `Add` and `Sample Texture 2D`.

Required node families:

- `uv` (`UV`, `UnityEditor.ShaderGraph.UVNode`)
- `simpleNoise` (`Simple Noise`, `UnityEditor.ShaderGraph.NoiseNode`)

Implementation plan:

- Slice 7E.1: add `uv` and `simpleNoise` to the `assets-shadergraph-add-node` allowlist and public input descriptions. Implemented.
- Slice 7E.2: expose structure readback for the new node families, including the UV `channel` enum and the Simple Noise `Scale` slot default. Implemented.
- Slice 7E.3: add typed `assets-shadergraph-update-node-settings` support for `uv.channel` (`UV0`/`UV1`/`UV2`/`UV3`) and `simpleNoise.scale`; reject unknown UV channels loudly. Implemented.
- Slice 7E.4: confirm duplicate, delete, and move flows resolve both nodes through the existing allowlist-driven path with no extra wiring. Implemented.
- Slice 7E.5: add editor tests mirroring `gradientNoise`/`screenPosition` cases for add, settings update, structure readback, duplicate, delete, move, and an end-to-end flame-shader-style chain `UV -> Add -> Simple Noise -> Sample Texture 2D.UV`. Implemented in editor tests; live MCP validation pending; automated Unity test-run execution remains deferred to Epic 15.
- Slice 7E.6: confirm `NoiseNode` does not expose a stable serialized hash/type enum in current Unity 6 / URP 17 and document that decision in the capabilities doc rather than expose an unsupported typed setting. Implemented.

Validation requirements:

- Each slice must create a graph through MCP, force reimport, return no ShaderGraph diagnostics errors, and verify the created node is discoverable through `assets-shadergraph-get-structure`.
- The flame-trial validation graph must include `UV` (with each channel set through `update-node-settings`) and `Simple Noise` (with `scale` set through settings) wired through `UV -> Add -> Simple Noise -> Sample Texture 2D.UV`, final import reporting `ShaderResolved=true` and `HasErrors=false`.
- Loud-failure check must pass: invalid `uv.channel` and invalid `simpleNoise.scale` payloads must return a clean error, not a stack trace.

Validation evidence:

- `Codex_FlameTrial_NodeProbe.shadergraph`: throwaway live MCP probe under `Assets/Unity-MCP-Test/Trials/Flames/` created `uv` (default `UV0`) and `simpleNoise` (default Scale slot 500), cycled `uv.channel` through `UV1` and `UV3`, set `simpleNoise.scale=250` (slot Value=250 / Default=500), wired `UV.Out -> Add.A` and `Add.Out -> Simple Noise.UV`, and confirmed loud failure on invalid `uv.channel=UV9` (`Supported values: UV0, UV1, UV2, UV3`) and empty `simpleNoise` payload (`At least one supported node settings field must be provided`). Final state reported 13 nodes, 6 edges, `ShaderResolved=true`, `HasErrors=false`. The throwaway asset was deleted after validation.

## Epic 7F: Dissolve Trial Node Gap

Status:

- Implemented in code on `custom/shadergraph-mcp`.
- Compile sanity check passed in the local Unity 6 validation project on 2026-06-18 with existing Unity/API warnings and 0 errors.
- Focused Unity Editor validation through the existing project-scoped MCP session passed on 2026-06-18.

Purpose:

- Close the concrete node-coverage gap reported by the dissolve shader trial in `Assets/Unity-MCP-Test/Trials/Dissolve/TRIAL_REPORT.md`.
- Promote `Fraction`, `Step`, and `Invert Colors` out of deferred status because they are behavior-critical for a looping hard-threshold dissolve mask and edge-glow path.

Required node families:

- `fraction` (`Fraction`, `UnityEditor.ShaderGraph.FractionNode`)
- `step` (`Step`, `UnityEditor.ShaderGraph.StepNode`)
- `invertColors` (`Invert Colors`, `UnityEditor.ShaderGraph.InvertColorsNode`)

Implementation plan:

- Slice 7F.1: add `fraction`, `step`, and `invertColors` to the `assets-shadergraph-add-node` allowlist and public input descriptions. Implemented.
- Slice 7F.2: expose structure readback for the serialized `Invert Colors` channel toggles (`red`, `green`, `blue`); `alpha` readback is unavailable because Unity does not serialize `m_AlphaChannel` safely in the current package. `Fraction` and `Step` remain slot-only nodes with generic slot readback. Implemented.
- Slice 7F.3: add typed `assets-shadergraph-update-node-settings` support for `invertColors.red`, `invertColors.green`, and `invertColors.blue`; reject `invertColors.alpha` loudly. Implemented.
- Slice 7F.4: add editor tests for add, inspect, duplicate, move, delete, typed `Invert Colors` update/readback, loud alpha rejection, and a minimal dissolve-style path `Time -> Fraction -> Add -> Step -> Invert Colors -> Fragment Emission`. Implemented in editor tests.

Validation requirements:

- `dotnet build Assembly-CSharp.csproj -v minimal` must pass with 0 errors.
- Focused editor tests must validate `Validation_AddNode_DissolveTrial.shadergraph`, `Validation_UpdateNodeSettings_DissolveTrial.shadergraph`, and `Validation_DissolveTrialPath.shadergraph`.
- Record the confirmed `Invert Colors.alpha` serialization limitation in `futureDebt.MD` and do not claim full four-channel parity until Unity exposes a safely persisted field.

Validation evidence:

- `Validation_AddNode_DissolveTrial.shadergraph`: editor test created `Fraction`, `Step`, and `Invert Colors`, verified expected slots, and exercised duplicate, move, and delete on `Invert Colors`.
- `Validation_UpdateNodeSettings_DissolveTrial.shadergraph`: editor test set `Invert Colors.red=true`, confirmed green/blue disabled, confirmed alpha readback is unavailable in the current Unity package, and verified `invertColors.alpha` is rejected loudly.
- `Validation_DissolveTrialPath.shadergraph`: editor test wired `Time.Time -> Fraction.In -> Add.A -> Step.Edge -> Invert Colors.In -> Fragment Emission`, with final `ShaderResolved=true` and no ShaderGraph diagnostic errors.

## Epic 8A: Slim Default Mutation Responses

Status:

- Slice 8A.1 in flight on `custom/shadergraph-mcp`.
- Compile sanity check pending for slice 8A.1.
- Live token-budget regression check pending (slice 8A.4).

Purpose:

- Close the per-call response-bloat blocker confirmed during the flame trial reconnaissance, where every mutation echoed the full graph snapshot (`Structure` + `Graph`) and pushed cumulative authoring cost to multi-million tokens for 25-node graphs.
- Make slim, diff-shaped responses the default on every mutation tool while keeping the existing full snapshot one flag-flip away.
- Leave read-only inspection tools (`assets-shadergraph-get-structure`, `assets-shadergraph-get-data`) unchanged.

Required surface change:

- New common `ShaderGraphSummaryData` payload with only `ShaderResolved`, `HasErrors`, `NodeCount`, `EdgeCount`, and a filtered diagnostics list (errors and warnings only, no `Severity:Info` "imported successfully" entries).
- New optional `includeStructure` and `includeGraph` flags (default `false`) on every mutation tool, mirroring the existing `includeMessages` / `includeProperties` pattern.
- New mutation result always returns: `Operation`, target ids (`NodeObjectId` / `PropertyObjectId` / `Edge` etc.), the existing diff (`Node` / `Property` / `ChangedFields` / `RemovedNodeCount` / `RemovedEdgeCount` / `RemovedEdge` / `RemovedEdges`), and the new `GraphSummary`. The legacy `Structure` and `Graph` blocks are populated only when the matching flag is set.
- Read-only tools (`assets-shadergraph-get-structure`, `assets-shadergraph-get-data`, `assets-shadergraph-create`, `assets-shadergraph-validate-texture-workflow`) keep their current shape.

Implementation plan:

- Slice 8A.1: foundation — add `ShaderGraphSummaryData`, shared `BuildShaderGraphSummary` helper in `Assets.ShaderGraph.Common.cs`, the new `GraphSummary` slot on `ShaderGraphNodeMutationResultData`, and the new flag/shape pattern proven on `assets-shadergraph-add-node` plus one editor test. Establish the shape user-verifiably before propagation.
- Slice 8A.2: propagate the pattern to every remaining mutation tool: `assets-shadergraph-add-property`, `assets-shadergraph-add-property-node`, `assets-shadergraph-update-property`, `assets-shadergraph-delete-property`, `assets-shadergraph-reorder-property`, `assets-shadergraph-create-category`, `assets-shadergraph-set-property-category`, `assets-shadergraph-update-node-settings`, `assets-shadergraph-update-node-position`, `assets-shadergraph-delete-node`, `assets-shadergraph-duplicate-node`, `assets-shadergraph-connect-edge`, `assets-shadergraph-disconnect-edge`, `assets-shadergraph-reconnect-edge`, `assets-shadergraph-reroute-output-slot`, `assets-shadergraph-set-settings`, `assets-shadergraph-set-blocks`. Update `AiSkillBody` documentation on each tool to describe the new flag and the slim default.
- Slice 8A.3: add slim-shape editor tests for at least one tool per mutation family (node add, property add, edge connect, settings update, delete), retrofit existing tests that assert on `Structure` so they pass `includeStructure: true`, and add a `### Response Shape` subsection to the relevant groups in `docs/dev/shadergraph-mcp-capabilities.md`.
- Slice 8A.4: live token-budget regression check against `Assets/Unity-MCP-Test/Trials/Flames/Flame.shadergraph` by authoring a 25-node flame graph in two passes (slim default, then `includeStructure:true` to mirror today). Capture cumulative response bytes for both passes and verify >80% reduction on the slim path. Record evidence here and file the follow-up batch mutation tool (`assets-shadergraph-batch`) plus optional server-side affected-node parsing under `docs/dev/futureDebt.MD`.

Validation requirements:

- Each slice must keep `dotnet build` of the local Unity 6 validation project at 0 errors and only pre-existing warnings.
- Existing editor tests that previously asserted on full `Structure` content must keep their coverage, either by passing `includeStructure: true` or by switching to `GraphSummary` assertions where structure inspection was not the test's intent.
- The slim-default path on a deliberately invalid input (e.g., `add-node` with an unknown node type) must still surface a clean, actionable error rather than a stack trace.
- The full `get-structure` call must continue to return the complete snapshot unchanged.
- Slice 8A.4 must demonstrate at least an 80% reduction in cumulative response bytes against the baseline path for the flame-trial sequence.

Validation evidence:

- Pending live MCP validation after slice 8A.4 completes. Update each row once the slice lands.

## Epic 8B: Batch Mutation Tool

Status:

- Slice 8B.1 (alias foundation + batch tool, v1) landed in code on `custom/shadergraph-mcp`. `ShaderGraphNodeRef`, `ShaderGraphSlotRef`, `ShaderGraphPropertyRef` DTOs and a resolver are in place. `assets-shadergraph-connect-edge` now accepts the reference form so callers can identify slots by `Node` (Alias / DisplayName / ObjectId) + slot `DisplayName` instead of two serialized object ids. The reference form is additive — object-ID callers continue working unchanged.
- `assets-shadergraph-batch` accepts an ordered list of operations (`addNode`, `updateNodeSettings`, `deleteNode`, `addProperty`, `updateProperty`, `deleteProperty`, `addPropertyNode`, `connectEdge`, `updateNodePosition`), executes them in order against the live asset, registers per-op aliases for downstream use, and returns one consolidated result with per-op summaries and a single post-batch `GraphSummary`. `stopOnError=true` (default) snapshot-rolls the file back on any failure.
- Slice 8B.2 (filtered read) landed in code on `custom/shadergraph-mcp`. `assets-shadergraph-query-structure` returns a `Stats` summary on every call and projects a subset of the existing `ShaderGraphStructureData` shape (Properties, Categories, Nodes, Edges, Targets, contexts) using filters: `statsOnly`, `propertiesOnly`, `nodeObjectIds`, `nodeTypeSubstrings`, `nodeDisplayNames`, `includeSlots`, `includeNodeSettings`, `includeEdges`, `edgesTouchingNodeIds`, `includeTargets`. `get-structure` semantics are unchanged. v2 follow-up: collapse to a single in-memory parse instead of rebuilding the full structure per query.
- Slice 8B.3 (response modes) landed in code on `custom/shadergraph-mcp`. New `ShaderGraphResponseMode` enum (`Summary` / `Diff` / `Selection` / `Full`) wired into `ShaderGraphBatchInput`. The batch tool now switches its post-batch payload by mode: `Diff` strips `GraphSummary`, `Selection` reuses `ProjectQueryStructure` to scope the view to the ids touched by the batch (and edges touching them), `Full` echoes the entire `ShaderGraphStructureData`. Single-op mutation tools still expose the legacy `includeStructure` / `includeGraph` / `includeMessages` / `includeProperties` flags; folding them into the new enum is tracked as v2 debt because it touches every single-op tool surface.
- Slice 8B.4 (idempotent connect) landed in code on `custom/shadergraph-mcp`. `ShaderGraphConnectEdgeInput.AllowExisting` flag and `ShaderGraphEdgeMutationResultData.AlreadyExisted` mirror the same contract through the single-op `assets-shadergraph-connect-edge` and through the batch `connectEdge` op. When the requested edge already exists and `AllowExisting=true`, the call short-circuits before the JSON write + AssetDatabase import (cheap no-op) and returns `ChangedFields=["edge.alreadyExists"]`. Incompatible pairings and conflicting input connections still throw loudly.
- Slice 8B.5 (category cleanup) landed in code on `custom/shadergraph-mcp`. Two new tools — `assets-shadergraph-delete-category` and `assets-shadergraph-prune-empty-categories` — let agents trim stale blackboard scaffolding without a full graph rewrite. Delete refuses the default category and refuses non-empty categories unless `ReassignPropertiesToDefault=true`; reassignment moves children to the default category before strip. Prune skips the default category and skips re-import when nothing is removed. `ShaderGraphCategoryMutationResultData` grew three fields (`RemovedCategoryCount`, `RemovedCategoryNames`, `ReassignedPropertyCount`) shared by both tools.
- Slice 8B.6 (clean URP template) landed in code on `custom/shadergraph-mcp`. The plugin now ships a vendored fixture at `Packages/com.ivanmurzak.unity.mcp/Editor/Templates/Unlit URP Clean.shadergraph` (URP target only, 6 block-stack nodes, no inherited blackboard properties or categories, no HDTarget objects). The fixture was authored once via a Unity round-trip by stripping the default cross-pipeline template and was validated to import with `ShaderResolved=true`, `HasErrors=false`. `assets-shadergraph-create` gains a `useCleanUrpTemplate` shortcut that points at the vendored fixture; `templateAssetPath` still overrides when both are passed.
- Slice 8B.7 (Vector3 → UV auto-narrow) landed in code on `custom/shadergraph-mcp`. `assets-shadergraph-connect-edge` gains `AutoNarrowVector3ToUV`. When true and the resolved slot pair is exactly `Vector3MaterialSlot -> UVMaterialSlot`, the plugin authors the chain `source -> Split.In, Split.R -> Combine.R, Split.G -> Combine.G, Combine.RG -> target.UV` in one call. `ShaderGraphEdgeMutationResultData` grew `AutoCreatedNodeObjectIds` and `AutoCreatedEdges`; the final `Edge` field reports the Combine.RG -> target connection. Direct Vector3 -> UV without the flag still fails loudly. The narrow path costs 6 imports (2 add-node + 4 connect-edge) under the current single-op delegation model; collapsing to one is part of the existing Epic 8B v2 shared-document follow-up.
- v1 caveat: the batch still delegates each op to its single-op helper, so the v1 import count equals the op count. The main token win comes from collapsing N MCP round-trips to 1 plus removing per-op `get-structure` lookups through aliases. v2 (shared in-memory `GraphData`) will cut the import count to 1.
- Identified during the 2026-06-17 Flame trial after Epic 8A slimmed per-call payloads but per-call **count** still dominated the authoring cost for non-trivial graphs.

Purpose:

- Add a single `assets-shadergraph-batch` tool that executes an ordered list of mutation operations against one Shader Graph asset in a single MCP round-trip.
- Slash per-call overhead for medium-to-large graphs (Flame at 27 nodes already required ~35 calls; 50+ node graphs scale linearly under the current shape).
- Make multi-step authoring expressible in one schema-checked envelope rather than chained round-trips, which also enables agents to reference newly created `ObjectId`s from later operations in the same batch without round-tripping the ids back.

Required surface:

- New tool `assets-shadergraph-batch` with inputs:
  - `assetRef` — the target `.shadergraph`.
  - `operations` — an ordered list of typed operation envelopes, one per existing mutation tool. The envelope is a discriminated union; each variant carries the same input DTO already used by the single-op tool: `addNode`, `addProperty`, `addPropertyNode`, `updateNodeSettings`, `updateProperty`, `connectEdge`, `disconnectEdge`, `reconnectEdge`, `deleteNode`, `deleteProperty`, `updateNodePosition`, `setBlocks`, `setSettings`, `setPropertyCategory`, `reorderProperty`, `createCategory`, `duplicateNode`.
  - `stopOnError` — boolean, default `true`. When `false` the remaining ops still run after one fails, and each op carries its own per-op error in the response.
  - `includeStructure` / `includeGraph` / `includeMessages` / `includeProperties` — evaluated **once after the batch**, not per operation. The slim per-op response stays the default.
  - `$ref` resolver — a way for op N to reference an id materialised by op M (M < N). Suggested shape: `"$ref": "ops[2].NodeObjectId"` or `"$ref": "ops[2].Property.ObjectId"`. The resolver inspects the live in-batch response array.
- Result shape:
  - Per-op summary: `ChangedFields`, created `ObjectId`s, per-op error message (when `stopOnError=false`).
  - One final `GraphSummary` after the whole batch has been imported.
  - Optional `Structure` / `Graph` blocks once after the batch when their flag is set.

Implementation plan:

- Slice 8B.1: introduce the operation envelope DTO and discriminated union; implement a small dispatcher that invokes the existing internal helpers per op without touching the existing single-op tools.
- Slice 8B.2: add `$ref` resolution against accumulated per-op results; restrict to fields actually exposed by the slim per-op response shape.
- Slice 8B.3: add `stopOnError=false` partial-failure semantics: rollback rules for the failing op only, the rest persist, per-op errors flow into the consolidated response.
- Slice 8B.4: collapse the post-batch Finalize/Reimport to a single re-import after the whole batch instead of per-op, and emit one consolidated `GraphSummary`.
- Slice 8B.5: editor tests, capabilities doc `assets-shadergraph-batch` section, validation evidence by re-authoring the Flame trial in a single batch call and confirming the final scene still renders.

Validation requirements:

- Compile sanity check.
- Editor tests for: ordered execution, `$ref` resolution across mixed op types, `stopOnError=true` aborts after the first failure, `stopOnError=false` runs the remainder, consolidated `GraphSummary` reflects the post-batch state.
- Live MCP regression: re-author the Flame trial in a single batch call against `Assets/Unity-MCP-Test/Trials/Flames/Flame.shadergraph` and confirm round-trip count ≤ 3 (open, batch, validate).

Validation evidence:

- Pending; record once Slice 8B.5 lands.

## Epic 8: Node Parameter Editing

Purpose:

- Let agents update meaningful serialized settings on supported nodes.

Slices:

- Slice 8.1: typed settings for `Sample Texture 2D`.
- Slice 8.2: typed direct settings for `Tiling And Offset`.
- Slice 8.3: typed direct settings for `Branch`.
- Slice 8.4: typed direct settings for `Split` and `Combine`.
- Slice 8.5: typed direct settings for `Add`, `Subtract`, `Divide`, `Lerp`, and `One Minus`.
- Slice 8.6: typed settings for `Multiply.multiplyType` and Multiply input-slot literals.
- Slice 8.7: document and validate the property-backed workaround for dynamic-vector-driven inputs.
- Slice 8.8: future direct literal/default-slot mutation research.

Accepted authoring workaround:

- When direct default-slot editing is unreliable in the Shader Graph UI, create or update a blackboard property, create a `PropertyNode`, and connect it into the target input.

## Epic 9: Blackboard Expansion

Purpose:

- Give agents broader control over common URP blackboard property workflows.

Slices:

- Slice 9.1: delete supported blackboard property types.
- Slice 9.2: reorder properties in the default blackboard category.
- Slice 9.3: place properties into explicit categories.
- Slice 9.4: safely create categories if the serialized behavior is stable in the Unity baseline.
- Slice 9.5: normalize add, update, delete, reorder, and category placement workflows.

Supported property types for the first track:

- `color`
- `float`
- `texture2D`
- `vector2`
- `vector3`
- `vector4`
- `boolean`

## Epic 10: Edge System V2

Purpose:

- Give agents robust, explicit graph rewiring control.

Slices:

- Slice 10.1: add explicit single-input edge replacement through `replaceExistingInputConnection`.
- Slice 10.2: add explicit reconnect semantics for moving an existing edge to a new output endpoint, input endpoint, or both.
- Slice 10.3: expand slot compatibility for high-value URP paths, starting with Texture2D property outputs into Texture2D input slots.
- Slice 10.4: add higher-level guarded rewiring workflows for common graph-repair operations.
- Slice 10.5: add additional compatibility cases only when concrete unsupported URP paths are found.

Compatibility families in scope:

- exact slot-type matches
- UV/vector2 pairs
- Texture2D property output to Texture2D input
- dynamic numeric/vector/color slots

## Epic 11: URP Stack And Target Coverage

Purpose:

- Expand URP graph, target, and stack/block control beyond the current settings allowlist.

Slices:

- Slice 11.1: audit common URP target and stack/block fields.
- Slice 11.2: expand safe URP target settings.
- Slice 11.3: add stack/block control where serialized structure is stable.
- Slice 11.4: validate common authoring targets such as base color, normal, emission, alpha, alpha clip threshold, and metallic/specular-adjacent coverage.

## Epic 12: Texture And Asset-Reference Workflows

Purpose:

- Let agents assign project texture assets across common ShaderGraph texture workflows.

Slices:

- Slice 12.1: assign project texture assets to supported blackboard texture properties.
- Slice 12.2: support texture assignment for texture-consuming node workflows where the graph model permits it.
- Slice 12.3: validate material and graph behavior after texture assignment.
- Slice 12.4: revisit reference-image interpretation only after project asset texture flows are stable.

## Epic 13: Graph Organization And Cleanup

Purpose:

- Add graph cleanup and organization tools used by normal Shader Graph authors.

Slices:

- Slice 13.1: groups.
- Slice 13.2: sticky notes.
- Slice 13.3: layout cleanup.
- Slice 13.4: safe bulk graph refactors for supported node families.

## Epic 14: Advanced And Long-Tail Research

Purpose:

- Research advanced authoring workflows after common URP control is stable.

Slices:

- Slice 14.1: subgraphs.
- Slice 14.2: custom function nodes.
- Slice 14.3: keyword and enum-driven authoring.
- Slice 14.4: broader render-pipeline support if still desired.
- Slice 14.5: long-tail node families.

## Epic 15: Tests, Validation Harness, And Workflow Docs

Purpose:

- Improve validation automation and keep user/agent docs coherent.

Slices:

- Slice 15.1: make package editor tests discoverable from the local Unity validation project.
- Slice 15.2: add higher-signal end-to-end ShaderGraph authoring validation cases.
- Slice 15.3: document the supported URP node, property, settings, and edge matrix.
- Slice 15.4: keep roadmap, capability, and future-debt docs aligned without duplicating ownership.
