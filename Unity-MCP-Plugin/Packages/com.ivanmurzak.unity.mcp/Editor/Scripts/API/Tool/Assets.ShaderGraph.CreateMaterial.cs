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
using UnityEditor;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphCreateMaterialToolId = "assets-shadergraph-create-material";

        [AiTool
        (
            AssetsShaderGraphCreateMaterialToolId,
            Title = "Assets / Shader Graph / Create Material"
        )]
        [AiSkillDescription("Create a Material asset directly from the compiled Shader resolved from a Shader Graph asset.")]
        [AiSkillBody("Create a Material asset directly from a '.shadergraph' asset. " +
            "This avoids a separate shader-name lookup step and guarantees the material targets the imported graph shader.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `materialAssetPath` — destination under `Assets/` ending with `.mat`.\n" +
            "- `overwrite` — when true, replace an existing material asset.\n\n" +
            "Use '" + AssetsShaderGraphGetDataToolId + "' first if you need to inspect the graph before creating a material.")]
        [Description("Create a Material asset directly from the compiled Shader resolved from a Shader Graph asset.")]
        public AssetObjectRef CreateMaterial
        (
            [Description("Reference to a '.shadergraph' asset.")]
            AssetObjectRef assetRef,
            [Description("Destination material asset path. Must start with 'Assets/' and end with '.mat'.")]
            string materialAssetPath,
            [Description("When true, replace an existing destination material. Default: false")]
            bool? overwrite = false
        )
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            return MainThread.Instance.Run(() =>
            {
                var graphAssetPath = ResolveAssetPath(assetRef);
                if (!IsShaderGraphAssetPath(graphAssetPath))
                    throw new ArgumentException(Error.AssetIsNotShaderGraph(graphAssetPath), nameof(assetRef));

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(graphAssetPath);
                if (shader == null)
                    throw new Exception(Error.FailedToLoadShaderGraphShader(graphAssetPath));

                return Tool_Assets.CreateMaterialAsset(
                    assetPath: materialAssetPath,
                    shader: shader,
                    overwrite: overwrite ?? false);
            });
        }
    }
}
