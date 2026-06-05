/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/

#nullable enable
using System;
using System.ComponentModel;
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphSetSettingsToolId = "assets-shadergraph-set-settings";

        [AiTool
        (
            AssetsShaderGraphSetSettingsToolId,
            Title = "Assets / Shader Graph / Set Settings"
        )]
        [AiSkillDescription("Safely mutate a narrow allowlist of Shader Graph settings, then re-import the graph and return the updated settings and diagnostics.")]
        [AiSkillBody("Safely mutate a narrow allowlist of Shader Graph settings on a '.shadergraph' asset.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `settings` — a structured settings payload. Currently supported:\n" +
            "  - `graph.shaderMenuPath`\n" +
            "  - `graph.graphPrecision` = `single` | `graph` | `half`\n" +
            "  - `graph.previewMode` = `inherit` | `preview2d` | `preview3d`\n" +
            "  - `universalTarget.surfaceType` = `opaque` | `transparent`\n" +
            "  - `universalTarget.alphaMode` = `alpha` | `premultiply` | `additive` | `multiply`\n" +
            "  - `universalTarget.renderFace` = `front` | `back` | `both`\n" +
            "  - `universalTarget.allowMaterialOverride`, `alphaClip`, `castShadows`, `receiveShadows`, `supportsLodCrossFade`\n" +
            "- `includeMessages` — include shader compiler messages in the returned graph data.\n" +
            "- `includeProperties` — include compiled shader properties in the returned graph data.\n\n" +
            "## Behavior\n\n" +
            "Applies only explicitly provided fields, writes the updated '.shadergraph' source, forces re-import, and returns both the updated settings snapshot and post-import Shader Graph diagnostics.")]
        [Description("Set a narrow allowlist of Shader Graph settings and re-import the graph.")]
        public ShaderGraphSettingsMutationResultData SetSettings(
            AssetObjectRef assetRef,
            ShaderGraphSettingsUpdateInput settings,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            return MainThread.Instance.Run(() => UpdateShaderGraphSettings(
                assetRef,
                settings,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }
    }
}
