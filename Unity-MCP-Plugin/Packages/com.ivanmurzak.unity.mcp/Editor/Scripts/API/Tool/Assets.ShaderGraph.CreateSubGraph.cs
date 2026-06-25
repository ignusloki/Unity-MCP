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

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderSubGraphCreateToolId = "assets-shadersubgraph-create";

        [AiTool
        (
            AssetsShaderSubGraphCreateToolId,
            Title = "Assets / Sub Graph / Create"
        )]
        [AiSkillDescription("Create a new Sub Graph (.shadersubgraph) asset by cloning a known-good template or preset.")]
        [AiSkillBody("Create a new Sub Graph asset by cloning a template '.shadersubgraph' file, creating missing folders, and forcing import.\n\n" +
            "Sub Graphs are reusable shader function graphs that can be referenced from a parent '.shadergraph' via the 'subGraph' node type in 'assets-shadergraph-add-node'. " +
            "They produce a SubGraphAsset (not a Shader), so 'ShaderResolved' is always false in the returned data — check 'IsSubGraph' instead.\n\n" +
            "## Inputs\n\n" +
            "- `assetPath` — destination under `Assets/` ending with '.shadersubgraph'.\n" +
            "- `outputPreset` — optional preset name. Selects a built-in template with a specific output slot layout. " +
            "Supported values: 'single-color' (one Color output named 'Out', default), 'single-float' (one Float output named 'Out'), " +
            "'single-vector3' (one Vector3 output named 'Out'), 'empty' (zero output slots — use with 'assets-shadersubgraph-set-outputs' to define the output contract). " +
            "When omitted, defaults to 'single-color'. Ignored when 'templateAssetPath' is provided.\n" +
            "- `templateAssetPath` — optional source template path. Overrides 'outputPreset' when provided.\n" +
            "- `overwrite` — when true, replace an existing destination file.\n\n" +
            "## Behavior\n\n" +
            "Copies the template source file, imports it synchronously, and returns Shader Graph data for the created asset. " +
            "After creation, use 'assets-shadergraph-add-node', 'assets-shadergraph-add-property', and 'assets-shadergraph-connect-edge' to build the sub-graph's node network. " +
            "Use 'assets-shadersubgraph-set-outputs' to define or change the sub-graph's output port contract. " +
            "Then reference this sub-graph from a parent graph using 'assets-shadergraph-add-node' with nodeType='subGraph' and SubGraphAssetPath pointing at the created file.")]
        [Description("Create a new Sub Graph (.shadersubgraph) asset by cloning a template or preset.")]
        public ShaderGraphData CreateSubGraph
        (
            [Description("Destination asset path. Must start with 'Assets/' and end with '.shadersubgraph'.")]
            string assetPath,
            [Description("Optional preset name selecting a built-in template. Values: 'single-color' (default), 'single-float', 'single-vector3', 'empty'. Ignored when templateAssetPath is provided.")]
            string? outputPreset = null,
            [Description("Optional template asset path. Overrides 'outputPreset' when provided.")]
            string? templateAssetPath = null,
            [Description("When true, replace an existing destination file. Default: false")]
            bool? overwrite = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrEmpty(assetPath))
                    throw new ArgumentException(Tool_Assets.Error.EmptyAssetPath(), nameof(assetPath));

                if (!assetPath.StartsWith("Assets/"))
                    throw new ArgumentException(Tool_Assets.Error.AssetPathMustStartWithAssets(assetPath), nameof(assetPath));

                if (!IsSubGraphAssetPath(assetPath))
                    throw new ArgumentException(Error.AssetPathMustEndWithShaderSubGraph(assetPath), nameof(assetPath));

                var resolvedTemplateAssetPath = !string.IsNullOrEmpty(templateAssetPath)
                    ? templateAssetPath!
                    : ResolveSubGraphPresetPath(outputPreset);

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
                File.SetLastWriteTimeUtc(destinationPhysicalPath, DateTime.UtcNow);
                FinalizeShaderGraphExternalDiskWrite(assetPath);

                EditorUtils.RepaintAllEditorWindows();

                return BuildShaderGraphData(
                    new AssetObjectRef(assetPath),
                    includeMessages: false,
                    includeProperties: false,
                    includeDiagnostics: true);
            });
        }
    }
}
