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
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphDeleteNodeToolId = "assets-shadergraph-delete-node";

        [AiTool
        (
            AssetsShaderGraphDeleteNodeToolId,
            Title = "Assets / Shader Graph / Delete Node"
        )]
        [AiSkillDescription("Delete an existing Shader Graph node, automatically clean up connected edges, then re-import the graph and return diagnostics.")]
        [AiSkillBody("Delete an existing node from a '.shadergraph' asset.\n\n" +
            "Current Epic 7 support is intentionally explicit:\n" +
            "- selection by serialized `nodeObjectId`\n" +
            "- uses Unity's own Shader Graph node-removal flow through reflection\n" +
            "- automatically removes connected edges as part of the graph mutation\n" +
            "- respects Unity's own `canDeleteNode` restrictions\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `node` — serialized node id to delete.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data.\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect the target node id and current graph wiring.")]
        [Description("Delete an existing Shader Graph node and re-import the graph.")]
        public ShaderGraphNodeMutationResultData DeleteNode(
            AssetObjectRef assetRef,
            ShaderGraphDeleteNodeInput node,
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

            return MainThread.Instance.Run(() => DeleteShaderGraphNode(
                assetRef,
                node,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphNodeMutationResultData DeleteShaderGraphNode(
            AssetObjectRef assetRef,
            ShaderGraphDeleteNodeInput node,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var nodeObjectId = node.NodeObjectId?.Trim();
            if (string.IsNullOrEmpty(nodeObjectId))
                throw new ArgumentException("nodeObjectId must be provided.", nameof(node));

            var graphRef = new AssetObjectRef(assetPath);
            var structureBeforeDelete = BuildShaderGraphStructureData(graphRef);
            var deletedNode = structureBeforeDelete.Nodes?
                .FirstOrDefault(n => string.Equals(n.ObjectId, nodeObjectId, StringComparison.Ordinal));

            if (deletedNode == null)
                throw new InvalidOperationException($"Shader Graph node '{nodeObjectId}' was not found.");

            var removedEdgeCount = CountConnectedEdges(structureBeforeDelete, nodeObjectId);
            var document = LoadShaderGraphReflectionDocument(assetPath);
            var nodeObject = ResolveShaderGraphNodeObject(document, nodeObjectId);

            if (!CanDeleteShaderGraphNode(document.Bindings, nodeObject))
            {
                throw new InvalidOperationException(
                    $"Node '{deletedNode.Name ?? deletedNode.ObjectId}' ({nodeObjectId}) cannot be deleted.");
            }

            InvokeShaderGraphMethod(document.Bindings.RemoveNodeMethod, document.GraphData, nodeObject);
            SaveShaderGraphReflectionDocument(document);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();

            var structureAfterDelete = BuildShaderGraphStructureData(graphRef);
            if (structureAfterDelete.Nodes?.Any(n => string.Equals(n.ObjectId, nodeObjectId, StringComparison.Ordinal)) == true)
            {
                throw new InvalidOperationException(
                    $"Deleted Shader Graph node '{nodeObjectId}' was still present after re-import.");
            }

            var changedFields = new List<string> { "node.deleted" };
            if (removedEdgeCount > 0)
                changedFields.Add("edge.autoRemoved");

            return new ShaderGraphNodeMutationResultData
            {
                Operation = "delete",
                NodeObjectId = deletedNode.ObjectId,
                NodeType = deletedNode.Type,
                ChangedFields = changedFields,
                Node = deletedNode,
                RemovedEdgeCount = removedEdgeCount,
                Structure = structureAfterDelete,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }
    }
}
