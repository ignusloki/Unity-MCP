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
        public const string AssetsShaderGraphGetStructureToolId = "assets-shadergraph-get-structure";

        [AiTool
        (
            AssetsShaderGraphGetStructureToolId,
            Title = "Assets / Shader Graph / Get Structure",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [AiSkillDescription("Get a read-only structural view of a Shader Graph source file, including blackboard properties, nodes, slots, edges, contexts, and active targets.")]
        [AiSkillBody("Get a read-only structural view of a '.shadergraph' asset. " +
            "Returns blackboard properties, node definitions, slot definitions, edge connections, active targets, and vertex/fragment block contexts.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n\n" +
            "Use '" + AssetsShaderGraphFindToolId + "' to locate a valid graph asset first.")]
        [Description("Get a read-only structural view of a Shader Graph source file.")]
        public ShaderGraphStructureData GetStructure(AssetObjectRef assetRef)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            return MainThread.Instance.Run(() => BuildShaderGraphStructureData(assetRef));
        }
    }
}
