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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphUpdateNodePositionToolId = "assets-shadergraph-update-node-position";

        [AiTool
        (
            AssetsShaderGraphUpdateNodePositionToolId,
            Title = "Assets / Shader Graph / Update Node Position"
        )]
        [AiSkillDescription("Move an existing Shader Graph node by serialized node id, then re-import the graph and return the updated node and diagnostics.")]
        [AiSkillBody("Move an existing node inside a '.shadergraph' asset.\n\n" +
            "Current support is intentionally narrow and safe:\n" +
            "- existing nodes only\n" +
            "- selection by `nodeObjectId`\n" +
            "- layout mutation only: `positionX`, `positionY`\n" +
            "- no changes to slots, values, or edge connections\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `node` — node selector plus requested position updates.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data.\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect node object ids and current positions.")]
        [Description("Move an existing Shader Graph node and re-import the graph.")]
        public ShaderGraphNodeMutationResultData UpdateNodePosition(
            AssetObjectRef assetRef,
            ShaderGraphUpdateNodePositionInput node,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (node == null)
                throw new ArgumentNullException(nameof(node));

            return MainThread.Instance.Run(() => UpdateShaderGraphNodePosition(
                assetRef,
                node,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphNodeMutationResultData UpdateShaderGraphNodePosition(
            AssetObjectRef assetRef,
            ShaderGraphUpdateNodePositionInput node,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            if (string.IsNullOrWhiteSpace(node.NodeObjectId))
                throw new ArgumentException("node.nodeObjectId must be provided.", nameof(node));

            if (!node.PositionX.HasValue && !node.PositionY.HasValue)
                throw new ArgumentException("At least one of node.positionX or node.positionY must be provided.", nameof(node));

            var document = LoadMutableDocument(assetPath);
            var nodeIds = GetIdArray(document.Root, "m_Nodes");
            var nodeObjectId = node.NodeObjectId!.Trim();

            if (!nodeIds.Contains(nodeObjectId, StringComparer.Ordinal))
                throw new InvalidOperationException($"Shader Graph node '{nodeObjectId}' was not found in root m_Nodes.");

            if (!document.ObjectsById.TryGetValue(nodeObjectId, out var nodeObject))
                throw new InvalidOperationException($"Shader Graph node object '{nodeObjectId}' could not be resolved.");

            var positionObject = EnsureNodePositionObject(nodeObject);
            var changedFields = new List<string>();

            if (node.PositionX.HasValue)
            {
                SetFloat(
                    positionObject,
                    "x",
                    node.PositionX.Value,
                    "node.positionX",
                    changedFields);
            }

            if (node.PositionY.HasValue)
            {
                SetFloat(
                    positionObject,
                    "y",
                    node.PositionY.Value,
                    "node.positionY",
                    changedFields);
            }

            if (changedFields.Count > 0)
            {
                WriteMutableDocument(document);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();
            }

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var updatedNode = structure.Nodes?
                .FirstOrDefault(n => string.Equals(n.ObjectId, nodeObjectId, StringComparison.Ordinal));

            return new ShaderGraphNodeMutationResultData
            {
                ChangedFields = changedFields,
                Node = updatedNode,
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        static JsonObject EnsureNodePositionObject(JsonObject nodeObject)
        {
            if (nodeObject["m_DrawState"] is not JsonObject drawState)
            {
                drawState = new JsonObject
                {
                    ["m_Expanded"] = true
                };
                nodeObject["m_DrawState"] = drawState;
            }

            if (drawState["m_Position"] is JsonObject position)
                return position;

            position = new JsonObject
            {
                ["serializedVersion"] = "2",
                ["x"] = 0.0,
                ["y"] = 0.0,
                ["width"] = 0.0,
                ["height"] = 0.0
            };
            drawState["m_Position"] = position;
            return position;
        }
    }
}
