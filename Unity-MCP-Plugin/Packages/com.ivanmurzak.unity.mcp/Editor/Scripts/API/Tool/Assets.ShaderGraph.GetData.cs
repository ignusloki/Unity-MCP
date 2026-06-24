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
        public const string AssetsShaderGraphGetDataToolId = "assets-shadergraph-get-data";

        [AiTool
        (
            AssetsShaderGraphGetDataToolId,
            Title = "Assets / Shader Graph / Get Data",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [AiSkillDescription("Get Shader Graph source summary, compiled shader state, and import diagnostics.")]
        [AiSkillBody("Get Shader Graph data from a '.shadergraph' or '.shadersubgraph' asset. " +
            "Returns source-file summary information, compiled shader state, optional shader properties and messages, and optional diagnostics.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `includeMessages` — include compiled shader compiler messages.\n" +
            "- `includeProperties` — include compiled shader properties.\n" +
            "- `includeDiagnostics` — include graph/import diagnostics.\n\n" +
            "Use '" + AssetsShaderGraphFindToolId + "' to locate a valid graph asset first.")]
        [Description("Get Shader Graph source summary, compiled shader state, and import diagnostics.")]
        public ShaderGraphData GetData
        (
            AssetObjectRef assetRef,
            [Description("Include compiled shader compiler messages. Default: true")]
            bool? includeMessages = true,
            [Description("Include compiled shader properties. Default: false")]
            bool? includeProperties = false,
            [Description("Include graph/import diagnostics. Default: true")]
            bool? includeDiagnostics = true
        )
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            return MainThread.Instance.Run(() => BuildShaderGraphData(
                assetRef,
                includeMessages ?? true,
                includeProperties ?? false,
                includeDiagnostics ?? true));
        }
    }
}
