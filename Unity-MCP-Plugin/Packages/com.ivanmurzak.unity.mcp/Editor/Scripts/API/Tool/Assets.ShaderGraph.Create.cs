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
using System.IO;
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphCreateToolId = "assets-shadergraph-create";

        [AiTool
        (
            AssetsShaderGraphCreateToolId,
            Title = "Assets / Shader Graph / Create"
        )]
        [AiSkillDescription("Create a new Shader Graph asset by cloning a known-good template. This avoids ad hoc raw graph authoring.")]
        [AiSkillBody("Create a new Shader Graph asset by cloning a template '.shadergraph' file, creating missing folders, and forcing import.\n\n" +
            "## Inputs\n\n" +
            "- `assetPath` — destination under `Assets/` ending with '.shadergraph'.\n" +
            "- `templateAssetPath` — optional source template path under `Packages/` or `Assets/`. " +
            "Defaults to a built-in cross-pipeline unlit template (HDTarget + URP target + 2 blackboard properties + 4 demo edges).\n" +
            "- `useCleanUrpTemplate` — shortcut: clone the MCP-owned clean URP-only template instead of the default. " +
            "Equivalent to passing the package-local `Packages/com.ivanmurzak.unity.mcp/Editor/Templates/Unlit URP Clean.shadergraph` path. " +
            "The clean template has only the URP target, the 6 default block-stack nodes, no blackboard properties, no inherited categories, and zero edges — start here for strict URP recreation trials.\n" +
            "- `overwrite` — when true, replace an existing destination file.\n\n" +
            "Pass either `templateAssetPath` or `useCleanUrpTemplate`, not both. When both are provided `templateAssetPath` wins.\n\n" +
            "## Behavior\n\n" +
            "Copies the template source file, imports it synchronously, repaints editor windows, and returns Shader Graph data for the created asset.")]
        [Description("Create a new Shader Graph asset by cloning a template '.shadergraph' file.")]
        public ShaderGraphData Create
        (
            [Description("Destination asset path. Must start with 'Assets/' and end with '.shadergraph'.")]
            string assetPath,
            [Description("Optional template asset path under 'Packages/' or 'Assets/'. Defaults to a built-in unlit Shader Graph template.")]
            string? templateAssetPath = null,
            [Description("When true, replace an existing destination file. Default: false")]
            bool? overwrite = false,
            [Description("When true, use the MCP-owned clean URP-only blank template (Packages/com.ivanmurzak.unity.mcp/Editor/Templates/Unlit URP Clean.shadergraph). Ignored when templateAssetPath is also provided. Default: false")]
            bool? useCleanUrpTemplate = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    throw new ArgumentException(Tool_Assets.Error.EmptyAssetPath(), nameof(assetPath));

                if (!assetPath.StartsWith("Assets/"))
                    throw new ArgumentException(Tool_Assets.Error.AssetPathMustStartWithAssets(assetPath), nameof(assetPath));

                if (!IsShaderGraphAssetPath(assetPath))
                    throw new ArgumentException(Error.AssetPathMustEndWithShaderGraph(assetPath), nameof(assetPath));

                string resolvedTemplateAssetPath;
                if (!string.IsNullOrEmpty(templateAssetPath))
                    resolvedTemplateAssetPath = templateAssetPath!;
                else if (useCleanUrpTemplate == true)
                    resolvedTemplateAssetPath = CleanUrpUnlitTemplateAssetPath;
                else
                    resolvedTemplateAssetPath = DefaultTemplateAssetPath;

                var templatePhysicalPath = ResolvePhysicalAssetPath(resolvedTemplateAssetPath);
                if (!File.Exists(templatePhysicalPath))
                    throw new ArgumentException(Error.TemplateAssetNotFound(resolvedTemplateAssetPath), nameof(templateAssetPath));

                var destinationPhysicalPath = ResolvePhysicalAssetPath(assetPath);
                var shouldOverwrite = overwrite ?? false;
                if (File.Exists(destinationPhysicalPath) && !shouldOverwrite)
                    throw new InvalidOperationException(Error.ShaderGraphAssetAlreadyExists(assetPath));

                var directory = Path.GetDirectoryName(destinationPhysicalPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }

                File.Copy(templatePhysicalPath, destinationPhysicalPath, shouldOverwrite);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                if (shader == null)
                    throw new Exception(Error.FailedToLoadShaderGraphShader(assetPath));

                EditorUtils.RepaintAllEditorWindows();

                return BuildShaderGraphData(
                    new AssetObjectRef(shader),
                    includeMessages: true,
                    includeProperties: false,
                    includeDiagnostics: true);
            });
        }
    }
}
