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
        public const string AssetsShaderGraphGetSettingsToolId = "assets-shadergraph-get-settings";

        [AiTool
        (
            AssetsShaderGraphGetSettingsToolId,
            Title = "Assets / Shader Graph / Get Settings",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [AiSkillDescription("Read graph-level Shader Graph settings and the currently supported target settings without mutating the graph.")]
        [AiSkillBody("Read safe, high-level Shader Graph settings from a '.shadergraph' asset.\n\n" +
            "Returns graph settings such as shader menu path, graph precision, preview mode, and currently supported Universal Render Pipeline target settings.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n\n" +
            "Current setting support includes root graph settings plus scalar Universal target settings. Active subtarget and stack/block controls are separate ShaderGraph capability slices.")]
        [Description("Get graph-level Shader Graph settings from a '.shadergraph' asset.")]
        public ShaderGraphSettingsData GetSettings(AssetObjectRef assetRef)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            return MainThread.Instance.Run(() => BuildShaderGraphSettingsData(assetRef));
        }
    }
}
