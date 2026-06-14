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
        public const string AssetsShaderGraphAddNodeToolId = "assets-shadergraph-add-node";

        [AiTool
        (
            AssetsShaderGraphAddNodeToolId,
            Title = "Assets / Shader Graph / Add Node"
        )]
        [AiSkillDescription("Add a safe allowlisted Shader Graph node, then re-import the graph and return the created node and diagnostics.")]
        [AiSkillBody("Add a safe allowlisted node to a '.shadergraph' asset.\n\n" +
            "Current Epic 7A support is intentionally explicit:\n" +
            "- node types: `add`, `subtract`, `multiply`, `divide`, `lerp`, `oneMinus`, `split`, `combine`, `sampleTexture2D`, `tilingAndOffset`, `branch`, `viewDirection`, `viewVector`, `normalVector`, `position`, `transform`, `gradientNoise`, `sine`, `cosine`, `negate`\n" +
            "- node creation only, no automatic edge wiring\n" +
            "- uses Unity's own Shader Graph graph APIs through reflection, then re-imports the asset\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `node` — allowlisted node type plus the requested node position.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data.\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect the current graph and choose placement. Use `assets-shadergraph-connect-edge` separately to wire the new node into the graph.")]
        [Description("Add a safe allowlisted Shader Graph node and re-import the graph.")]
        public ShaderGraphNodeMutationResultData AddNode(
            AssetObjectRef assetRef,
            ShaderGraphAddNodeInput node,
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

            return MainThread.Instance.Run(() => AddShaderGraphNode(
                assetRef,
                node,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphNodeMutationResultData AddShaderGraphNode(
            AssetObjectRef assetRef,
            ShaderGraphAddNodeInput node,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var definition = ResolveAllowlistedNodeDefinition(node.NodeType);
            var document = LoadShaderGraphReflectionDocument(assetPath);
            var createdNodeObject = CreateShaderGraphNodeInstance(document.Bindings, definition);

            InvokeShaderGraphMethod(document.Bindings.AddNodeMethod, document.GraphData, createdNodeObject, false);
            SetShaderGraphNodePosition(
                document.Bindings,
                createdNodeObject,
                node.PositionX ?? 0f,
                node.PositionY ?? 0f,
                definition);
            InvokeShaderGraphMethod(document.Bindings.ValidateGraphMethod, document.GraphData);

            SaveShaderGraphReflectionDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            var createdNodeObjectId = GetShaderGraphNodeObjectId(document.Bindings, createdNodeObject);
            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var addedNode = structure.Nodes?
                .FirstOrDefault(n => string.Equals(n.ObjectId, createdNodeObjectId, StringComparison.Ordinal));

            if (addedNode == null)
            {
                throw new InvalidOperationException(
                    $"Added Shader Graph node '{createdNodeObjectId}' could not be resolved after re-import.");
            }

            return new ShaderGraphNodeMutationResultData
            {
                Operation = "add",
                NodeObjectId = addedNode.ObjectId,
                NodeType = addedNode.Type,
                ChangedFields = new List<string> { "node.added" },
                Node = addedNode,
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }
    }
}
