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
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    [AiToolType]
    public partial class Tool_Assets_ShaderGraph
    {
        internal const string DefaultTemplateAssetPath =
            "Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline/Unlit Simple.shadergraph";

        // MCP-owned URP-only blank Shader Graph: 6 block-stack nodes, no inherited
        // blackboard properties or categories, no HDTarget scaffolding. Use this
        // template when starting a strict URP recreation trial so the agent does
        // not need to clean up cross-pipeline cruft first.
        internal const string CleanUrpUnlitTemplateAssetPath =
            "Packages/com.ivanmurzak.unity.mcp/Editor/Templates/Unlit URP Clean.shadergraph";

        internal const string DefaultSubGraphTemplateAssetPath =
            "Packages/com.ivanmurzak.unity.mcp/Editor/Templates/Blank SubGraph.shadersubgraph";

        const string SubGraphPresetSingleColor =
            "Packages/com.ivanmurzak.unity.mcp/Editor/Templates/SubGraphPresets/SingleColor.shadersubgraph";
        const string SubGraphPresetSingleFloat =
            "Packages/com.ivanmurzak.unity.mcp/Editor/Templates/SubGraphPresets/SingleFloat.shadersubgraph";
        const string SubGraphPresetSingleVector3 =
            "Packages/com.ivanmurzak.unity.mcp/Editor/Templates/SubGraphPresets/SingleVector3.shadersubgraph";
        const string SubGraphPresetEmpty =
            "Packages/com.ivanmurzak.unity.mcp/Editor/Templates/SubGraphPresets/Empty.shadersubgraph";

        static string ResolveSubGraphPresetPath(string? presetName)
        {
            return (presetName ?? "single-color").ToLowerInvariant() switch
            {
                "single-color"   => SubGraphPresetSingleColor,
                "single-float"   => SubGraphPresetSingleFloat,
                "single-vector3" => SubGraphPresetSingleVector3,
                "empty"          => SubGraphPresetEmpty,
                _ => throw new System.ArgumentException(
                    $"Unknown outputPreset '{presetName}'. Supported values: single-color, single-float, single-vector3, empty.")
            };
        }

        public static class Error
        {
            public static string AssetPathMustEndWithShaderGraph(string assetPath)
                => $"Asset path must end with '.shadergraph'. Path: '{assetPath}'.";

            public static string AssetPathMustEndWithShaderSubGraph(string assetPath)
                => $"Asset path must end with '.shadersubgraph'. Path: '{assetPath}'.";

            public static string AssetIsNotShaderGraph(string assetPath)
                => $"Asset is not a Shader Graph source file. Expected '.shadergraph' or '.shadersubgraph'. Path: '{assetPath}'.";

            public static string TemplateAssetNotFound(string templateAssetPath)
                => $"Shader Graph template asset was not found. Path: '{templateAssetPath}'.";

            public static string ShaderGraphAssetAlreadyExists(string assetPath)
                => $"Shader Graph asset already exists at path: '{assetPath}'. Set 'overwrite' to true to replace it.";

            public static string FailedToLoadShaderGraphShader(string assetPath)
                => $"Unity did not resolve a Shader asset from Shader Graph path: '{assetPath}'.";
        }
    }
}
