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
        const float DefaultDuplicateNodeOffset = 40f;
        public const string AssetsShaderGraphDuplicateNodeToolId = "assets-shadergraph-duplicate-node";

        [AiTool
        (
            AssetsShaderGraphDuplicateNodeToolId,
            Title = "Assets / Shader Graph / Duplicate Node"
        )]
        [AiSkillDescription("Duplicate a supported Shader Graph node without copying edges, then re-import the graph and return the duplicate node and diagnostics.")]
        [AiSkillBody("Duplicate a supported node inside a '.shadergraph' asset.\n\n" +
            "Current Epic 7 support is intentionally explicit:\n" +
            "- selection by serialized `nodeObjectId`\n" +
            "- supported nodes: PropertyNode plus the same allowlisted node families as `assets-shadergraph-add-node`\n" +
            "- duplicates the node and its serialized slots with fresh object ids\n" +
            "- preserves node settings and blackboard property references\n" +
            "- does not copy any edges; use edge tools separately to wire the duplicate\n" +
            "- defaults to placing the duplicate 40 units down/right from the source unless explicit position values are supplied\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect the target node id and current graph layout.")]
        [Description("Duplicate a supported Shader Graph node without copying edges and re-import the graph.")]
        public ShaderGraphNodeMutationResultData DuplicateNode(
            AssetObjectRef assetRef,
            ShaderGraphDuplicateNodeInput node,
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

            return MainThread.Instance.Run(() => DuplicateShaderGraphNode(
                assetRef,
                node,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphNodeMutationResultData DuplicateShaderGraphNode(
            AssetObjectRef assetRef,
            ShaderGraphDuplicateNodeInput node,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var sourceNodeObjectId = node.NodeObjectId?.Trim();
            if (string.IsNullOrEmpty(sourceNodeObjectId))
                throw new ArgumentException("nodeObjectId must be provided.", nameof(node));

            var document = LoadMutableDocument(assetPath);
            var nodeIds = GetIdArray(document.Root, "m_Nodes");
            if (!nodeIds.Contains(sourceNodeObjectId, StringComparer.Ordinal))
                throw new InvalidOperationException($"Shader Graph node '{sourceNodeObjectId}' was not found in root m_Nodes.");

            if (!document.ObjectsById.TryGetValue(sourceNodeObjectId!, out var sourceNodeObject))
                throw new InvalidOperationException($"Shader Graph node object '{sourceNodeObjectId}' could not be resolved.");

            var sourceNodeType = GetString(sourceNodeObject, "m_Type") ?? string.Empty;
            if (!IsDuplicatableShaderGraphNodeType(sourceNodeType))
            {
                throw new InvalidOperationException(
                    $"Shader Graph node type '{sourceNodeType}' is not currently supported for duplication.");
            }

            var sourceSlotObjectIds = GetIdArray(sourceNodeObject, "m_Slots");
            var duplicatedNodeObjectId = CreateUniqueShaderGraphObjectId(document);
            var duplicatedNodeObject = CloneJsonObject(sourceNodeObject);
            duplicatedNodeObject["m_ObjectId"] = duplicatedNodeObjectId;

            var duplicatedSlotReferences = new JsonArray();
            foreach (var sourceSlotObjectId in sourceSlotObjectIds)
            {
                if (!document.ObjectsById.TryGetValue(sourceSlotObjectId, out var sourceSlotObject))
                    throw new InvalidOperationException($"Shader Graph slot object '{sourceSlotObjectId}' could not be resolved.");

                var duplicatedSlotObjectId = CreateUniqueShaderGraphObjectId(document);
                var duplicatedSlotObject = CloneJsonObject(sourceSlotObject);
                duplicatedSlotObject["m_ObjectId"] = duplicatedSlotObjectId;
                duplicatedSlotReferences.Add(new JsonObject
                {
                    ["m_Id"] = duplicatedSlotObjectId
                });

                document.Objects.Add(duplicatedSlotObject);
                document.ObjectsById[duplicatedSlotObjectId] = duplicatedSlotObject;
            }

            duplicatedNodeObject["m_Slots"] = duplicatedSlotReferences;

            var positionObject = EnsureNodePositionObject(duplicatedNodeObject);
            var sourcePositionX = GetFloat(positionObject, "x") ?? 0f;
            var sourcePositionY = GetFloat(positionObject, "y") ?? 0f;
            var duplicatedPositionX = node.PositionX ?? sourcePositionX + (node.PositionOffsetX ?? DefaultDuplicateNodeOffset);
            var duplicatedPositionY = node.PositionY ?? sourcePositionY + (node.PositionOffsetY ?? DefaultDuplicateNodeOffset);
            var changedFields = new List<string>
            {
                "node.duplicated"
            };

            if (sourceSlotObjectIds.Count > 0)
                changedFields.Add("node.slot.duplicated");

            SetFloat(positionObject, "x", duplicatedPositionX, "node.positionX", changedFields);
            SetFloat(positionObject, "y", duplicatedPositionY, "node.positionY", changedFields);

            document.Objects.Add(duplicatedNodeObject);
            document.ObjectsById[duplicatedNodeObjectId] = duplicatedNodeObject;
            AddNodeReferenceToRoot(document.Root, duplicatedNodeObjectId);

            WriteMutableDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var duplicatedNode = structure.Nodes?
                .FirstOrDefault(n => string.Equals(n.ObjectId, duplicatedNodeObjectId, StringComparison.Ordinal));

            if (duplicatedNode == null)
            {
                throw new InvalidOperationException(
                    $"Duplicated Shader Graph node '{duplicatedNodeObjectId}' could not be resolved after re-import.");
            }

            return new ShaderGraphNodeMutationResultData
            {
                ChangedFields = changedFields,
                Node = duplicatedNode,
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        static bool IsDuplicatableShaderGraphNodeType(string nodeType)
            => string.Equals(nodeType, "UnityEditor.ShaderGraph.PropertyNode", StringComparison.Ordinal)
               || AllowlistedNodeDefinitions.Values.Any(definition =>
                   string.Equals(definition.TypeName, nodeType, StringComparison.Ordinal));

        static string CreateUniqueShaderGraphObjectId(ShaderGraphMutableDocument document)
        {
            string objectId;
            do
            {
                objectId = Guid.NewGuid().ToString("N");
            }
            while (document.ObjectsById.ContainsKey(objectId));

            return objectId;
        }

        static JsonObject CloneJsonObject(JsonObject source)
            => JsonNode.Parse(source.ToJsonString())?.AsObject()
               ?? throw new InvalidOperationException("Failed to clone Shader Graph JSON object.");
    }
}
