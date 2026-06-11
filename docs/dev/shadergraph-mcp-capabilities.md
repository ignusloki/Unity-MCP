# ShaderGraph MCP Capabilities

## Purpose

This document is the single source of truth for what the local ShaderGraph MCP implementation currently exposes to agents and users.

- Roadmap, sequencing, and epic status live only in `docs/dev/shadergraph-mcp-plan.md`
- This file describes the actual MCP surface that exists today

## Scope

- Focus: URP-first Shader Graph authoring
- Current implementation lives in the core `com.ivanmurzak.unity.mcp` package
- Validation has been performed in the local Unity test project used by this private workflow

## Exposure Model

- ShaderGraph is exposed in Ivan's Extensions window as a built-in capability group named `ShaderGraph`
- Enabling or disabling that row toggles the current ShaderGraph tool set as one group

## Exposed MCP Tools

### Discovery And Diagnostics

- `assets-shadergraph-find`
  - Find Shader Graph assets by name or folder.
- `assets-shadergraph-get-data`
  - Inspect compiled shader resolution, diagnostics, and optional shader messages and properties.
- `assets-shadergraph-get-structure`
  - Read serialized properties, nodes, slots, edges, and active targets from the graph source.
- `assets-shadergraph-get-settings`
  - Read graph root settings and supported target settings from the graph source.

### Asset Creation

- `assets-shadergraph-create`
  - Create a new `.shadergraph` asset by cloning a known-good template.
- `assets-shadergraph-create-material`
  - Create a `.mat` asset from the compiled shader resolved from a Shader Graph asset.
- `assets-shadergraph-create-from-style-recipe`
  - Validate a declarative style-recipe JSON payload, create a graph and material, apply the currently supported material fields, and return warnings for deferred recipe fields.

### Graph Settings Mutation

- `assets-shadergraph-set-settings`
  - Supported graph root fields:
    - `graph.shaderMenuPath`
    - `graph.graphPrecision`
    - `graph.previewMode`
  - Supported URP target fields:
    - `universalTarget.surfaceType`
    - `universalTarget.alphaMode`
    - `universalTarget.renderFace`
    - `universalTarget.allowMaterialOverride`
    - `universalTarget.alphaClip`
    - `universalTarget.castShadows`
    - `universalTarget.receiveShadows`
    - `universalTarget.supportsLodCrossFade`

### Blackboard Property Mutation

- `assets-shadergraph-add-property`
  - Supported property types:
    - `color`
    - `float`
    - `texture2D`
    - `vector2`
    - `vector3`
    - `vector4`
    - `boolean`
  - New properties are added to the default blackboard category.
- `assets-shadergraph-update-property`
  - Supported generic fields:
    - `displayName`
    - `overrideReferenceName`
    - `hidden`
    - `generatePropertyBlock`
  - Supported typed default-value editing:
    - `colorHex`
    - `floatValue`
    - `vectorX`
    - `vectorY`
    - `vectorZ`
    - `vectorW`
    - `booleanValue`
    - `textureDefaultType`
    - `textureUseTilingAndOffset`
    - `textureUseTexelSize`
    - `textureIsMainTexture`
    - `textureIsHdr`
    - `textureModifiable`
- `assets-shadergraph-add-property-node`
  - Creates a `PropertyNode` for an existing blackboard property.
  - Supported property-backed node types:
    - `color`
    - `float`
    - `texture2D`
    - `vector2`
    - `vector3`
    - `vector4`
    - `boolean`
  - No automatic edge wiring is performed.

### Node Lifecycle Mutation

- `assets-shadergraph-add-node`
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
  - Nodes are created without automatic edge wiring.
- `assets-shadergraph-delete-node`
  - Deletes an existing node by serialized `nodeObjectId`.
  - Uses Unity's own node-removal flow and removes connected edges as part of the mutation.
  - Respects Unity's `canDeleteNode` restrictions.
- `assets-shadergraph-update-node-position`
  - Moves an existing node by serialized `nodeObjectId`.

### Node Settings Mutation

- `assets-shadergraph-update-node-settings`
  - Current supported node family:
    - `Sample Texture 2D`
  - Supported typed fields:
    - `textureType`
    - `normalMapSpace`
    - `useGlobalMipBias`
    - `mipSamplingMode`

### Edge Mutation

- `assets-shadergraph-connect-edge`
  - Selects slots by `nodeObjectId` plus `slotObjectId`.
  - Requires the input slot to be currently unconnected.
  - Supports exact slot-type matches.
  - Supports `DynamicValueMaterialSlot` connections for compatible numeric, vector, and color slot families.
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
- `assets-shadergraph-create-from-style-recipe`
- `assets-shadergraph-set-settings`
- `assets-shadergraph-update-property`
- `assets-shadergraph-add-property`
- `assets-shadergraph-add-property-node`
- `assets-shadergraph-add-node`
- `assets-shadergraph-delete-node`
- `assets-shadergraph-update-node-settings`
- `assets-shadergraph-update-node-position`
- `assets-shadergraph-connect-edge`
- `assets-shadergraph-disconnect-edge`

## Not Yet Exposed

- Node duplication.
- Broad typed settings coverage for `Tiling And Offset`, `Branch`, `Split`, `Combine`, and the allowlisted math nodes.
- Typed constant and default-slot editing for the common math and utility nodes.
- Property deletion, reordering, and category placement.
- Project texture assignment workflows across blackboard properties and texture-consuming nodes.
- Edge replacement and reconnect semantics for already-connected inputs.
- Groups, sticky notes, and other graph cleanup and organization tools.
- Subgraphs, custom function nodes, keywords, enums, and other long-tail Shader Graph authoring flows.
- Broader URP stack and block mutation parity beyond the current root and target settings allowlist.
