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
        public const string AssetsShaderGraphConnectEdgeToolId = "assets-shadergraph-connect-edge";
        public const string AssetsShaderGraphDisconnectEdgeToolId = "assets-shadergraph-disconnect-edge";
        public const string AssetsShaderGraphReconnectEdgeToolId = "assets-shadergraph-reconnect-edge";
        public const string AssetsShaderGraphRerouteOutputSlotToolId = "assets-shadergraph-reroute-output-slot";

        static readonly HashSet<string> DynamicCompatibleSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.Vector1MaterialSlot",
            "UnityEditor.ShaderGraph.Vector2MaterialSlot",
            "UnityEditor.ShaderGraph.Vector3MaterialSlot",
            "UnityEditor.ShaderGraph.Vector4MaterialSlot",
            "UnityEditor.ShaderGraph.PositionMaterialSlot",
            "UnityEditor.ShaderGraph.ColorRGBMaterialSlot",
            "UnityEditor.ShaderGraph.ColorRGBAMaterialSlot"
        };

        static readonly HashSet<string> Vector2LikeSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.Vector2MaterialSlot",
            "UnityEditor.ShaderGraph.UVMaterialSlot"
        };

        static readonly HashSet<string> Vector2ExpansionInputSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.Vector2MaterialSlot"
        };

        static readonly HashSet<string> ScreenPositionInputSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.ScreenPositionMaterialSlot"
        };

        static readonly HashSet<string> NormalInputSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.NormalMaterialSlot"
        };

        static readonly HashSet<string> Vector3LikeSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.Vector3MaterialSlot",
            "UnityEditor.ShaderGraph.PositionMaterialSlot"
        };

        static readonly HashSet<string> ColorSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.ColorRGBMaterialSlot",
            "UnityEditor.ShaderGraph.ColorRGBAMaterialSlot"
        };

        static readonly HashSet<string> ColorCompatibleValueSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.Vector3MaterialSlot",
            "UnityEditor.ShaderGraph.Vector4MaterialSlot",
            "UnityEditor.ShaderGraph.ColorRGBMaterialSlot",
            "UnityEditor.ShaderGraph.ColorRGBAMaterialSlot"
        };

        static readonly HashSet<string> Texture2DLikeSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.Texture2DMaterialSlot",
            "UnityEditor.ShaderGraph.Texture2DInputMaterialSlot"
        };

        [AiTool
        (
            AssetsShaderGraphConnectEdgeToolId,
            Title = "Assets / Shader Graph / Connect Edge"
        )]
        [AiSkillDescription("Connect two existing Shader Graph slots, then re-import the graph and return the connected edge and diagnostics.")]
        [AiSkillBody("Connect an existing output slot to an existing input slot inside a '.shadergraph' asset.\n\n" +
            "Current support is intentionally narrow and safe:\n" +
            "- selection by node object id plus slot object id, or by reference: pass `OutputSlot`/`InputSlot` carrying `Node` (Alias/DisplayName/ObjectId) + `DisplayName` (the slot name) — the resolver looks up the serialized ids for you, removing the need to round-trip through `get-structure`\n" +
            "- requires the input slot to be currently unconnected unless `replaceExistingInputConnection` is true\n" +
            "- supports exact slot-type matches\n" +
            "- supports compatible UV/vector2 slot pairs\n" +
            "- supports scalar outputs into Shader Graph vector2 inputs such as `Float Property -> Tiling And Offset.Tiling`\n" +
            "- supports scalar `Vector1MaterialSlot` outputs broadcasting into Shader Graph UV inputs such as `Time.Time -> Simple Noise.UV`\n" +
            "- supports vector2-resolved `DynamicVectorMaterialSlot` outputs into Shader Graph UV inputs such as `Add.Out -> Tiling And Offset.UV`\n" +
            "- supports Screen Position vector4 output and dynamic vector outputs into Shader Graph screen-position UV inputs such as Scene Color UV and Scene Depth UV\n" +
            "- supports compatible Vector3/Position slot pairs\n" +
            "- supports Vector3 outputs into Shader Graph normal inputs such as `Normal From Height.Out -> Fragment NormalWS`\n" +
            "- supports compatible color/vector slot pairs such as Color property outputs into Base Color\n" +
            "- supports compatible Texture2D property outputs and Texture2D input slots\n" +
            "- supports dynamic numeric/vector/color slots via Shader Graph dynamic slot families such as `DynamicValueMaterialSlot` and `DynamicVectorMaterialSlot`\n" +
            "- supports direct `Vector4 -> UV` edges via Unity's documented `.xy` truncation (no narrowing node needed)\n" +
            "- supports explicit vector narrowing workflows such as `Vector3 -> Split -> Combine(Vector2) -> UV`; direct Vector3-to-UV remains rejected unless Unity exposes a validated direct conversion\n" +
            "- supports guarded input-edge replacement when `replaceExistingInputConnection` is true\n\n" +
            "## Response shape\n\n" +
            "By default returns a slim diff: `Edge`, `RemovedEdge` / `RemovedEdges` when applicable, `ChangedFields`, and `GraphSummary`. " +
            "Set `includeStructure: true` to also receive the full read-only `Structure` block, `includeGraph: true` for the full post-import `Graph` block.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect node ids, slot ids, and slot types.")]
        [Description("Connect two existing Shader Graph slots and re-import the graph.")]
        public ShaderGraphEdgeMutationResultData ConnectEdge(
            AssetObjectRef assetRef,
            ShaderGraphConnectEdgeInput edge,
            [Description("Include the full read-only Structure block in the returned mutation result. Default: false")]
            bool? includeStructure = false,
            [Description("Include the full post-import Graph block in the returned mutation result. Default: false")]
            bool? includeGraph = false,
            [Description("Include shader compiler messages in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (edge == null)
                throw new ArgumentNullException(nameof(edge));

            return MainThread.Instance.Run(() => ConnectShaderGraphEdge(
                assetRef,
                edge,
                includeStructure: includeStructure ?? false,
                includeGraph: includeGraph ?? false,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        [AiTool
        (
            AssetsShaderGraphDisconnectEdgeToolId,
            Title = "Assets / Shader Graph / Disconnect Edge"
        )]
        [AiSkillDescription("Disconnect an existing Shader Graph edge, then re-import the graph and return the removed edge and diagnostics.")]
        [AiSkillBody("Disconnect an existing edge inside a '.shadergraph' asset.\n\n" +
            "Current support is intentionally narrow and safe:\n" +
            "- selection by node object id plus slot object id\n" +
            "- requires the exact edge to exist before removal\n\n" +
            "## Response shape\n\n" +
            "By default returns a slim diff: `Edge`, `RemovedEdge`, `RemovedEdges`, `ChangedFields`, and `GraphSummary`. " +
            "Set `includeStructure: true` to also receive the full read-only `Structure` block, `includeGraph: true` for the full post-import `Graph` block.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect node ids and slot ids.")]
        [Description("Disconnect an existing Shader Graph edge and re-import the graph.")]
        public ShaderGraphEdgeMutationResultData DisconnectEdge(
            AssetObjectRef assetRef,
            ShaderGraphDisconnectEdgeInput edge,
            [Description("Include the full read-only Structure block in the returned mutation result. Default: false")]
            bool? includeStructure = false,
            [Description("Include the full post-import Graph block in the returned mutation result. Default: false")]
            bool? includeGraph = false,
            [Description("Include shader compiler messages in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (edge == null)
                throw new ArgumentNullException(nameof(edge));

            return MainThread.Instance.Run(() => DisconnectShaderGraphEdge(
                assetRef,
                edge,
                includeStructure: includeStructure ?? false,
                includeGraph: includeGraph ?? false,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        [AiTool
        (
            AssetsShaderGraphReconnectEdgeToolId,
            Title = "Assets / Shader Graph / Reconnect Edge"
        )]
        [AiSkillDescription("Reconnect an existing Shader Graph edge to a new output or input endpoint, then re-import the graph and return the mutation details and diagnostics.")]
        [AiSkillBody("Reconnect an existing edge inside a '.shadergraph' asset.\n\n" +
            "Current support is intentionally explicit and safe:\n" +
            "- selection starts from an exact existing edge\n" +
            "- you may move the output side, the input side, or both\n" +
            "- at least one side must change\n" +
            "- compatibility is validated before the new edge is written\n" +
            "- if the new target input is already occupied, set `replaceExistingInputConnection` to true to replace it explicitly\n\n" +
            "## Response shape\n\n" +
            "By default returns a slim diff: `Edge`, `RemovedEdge` / `RemovedEdges` when applicable, `ChangedFields`, and `GraphSummary`. " +
            "Set `includeStructure: true` to also receive the full read-only `Structure` block, `includeGraph: true` for the full post-import `Graph` block.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect node ids, slot ids, and current edges.")]
        [Description("Reconnect an existing Shader Graph edge to a new endpoint and re-import the graph.")]
        public ShaderGraphEdgeMutationResultData ReconnectEdge(
            AssetObjectRef assetRef,
            ShaderGraphReconnectEdgeInput edge,
            [Description("Include the full read-only Structure block in the returned mutation result. Default: false")]
            bool? includeStructure = false,
            [Description("Include the full post-import Graph block in the returned mutation result. Default: false")]
            bool? includeGraph = false,
            [Description("Include shader compiler messages in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (edge == null)
                throw new ArgumentNullException(nameof(edge));

            return MainThread.Instance.Run(() => ReconnectShaderGraphEdge(
                assetRef,
                edge,
                includeStructure: includeStructure ?? false,
                includeGraph: includeGraph ?? false,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        [AiTool
        (
            AssetsShaderGraphRerouteOutputSlotToolId,
            Title = "Assets / Shader Graph / Reroute Output Slot"
        )]
        [AiSkillDescription("Move every outgoing Shader Graph edge from one output slot to another compatible output slot, then re-import the graph and return the mutation details and diagnostics.")]
        [AiSkillBody("Reroute every outgoing edge from one output slot to another output slot inside a '.shadergraph' asset.\n\n" +
            "This is a guarded graph-repair workflow for replacing a source node or property everywhere it is currently used:\n" +
            "- selection starts from an exact existing output node and slot\n" +
            "- all outgoing edges from that output slot are moved to the new output slot\n" +
            "- at least one outgoing edge must exist\n" +
            "- every downstream input is compatibility-checked before any write is persisted\n" +
            "- unrelated incoming edges on downstream inputs are never overwritten\n\n" +
            "## Response shape\n\n" +
            "By default returns a slim diff: `Edges`, `RemovedEdges`, `ChangedFields`, and `GraphSummary`. " +
            "Set `includeStructure: true` to also receive the full read-only `Structure` block, `includeGraph: true` for the full post-import `Graph` block.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect node ids, slot ids, and current edges.")]
        [Description("Reroute every outgoing Shader Graph edge from one output slot to another compatible output slot and re-import the graph.")]
        public ShaderGraphEdgeMutationResultData RerouteOutputSlot(
            AssetObjectRef assetRef,
            ShaderGraphRerouteOutputSlotInput edge,
            [Description("Include the full read-only Structure block in the returned mutation result. Default: false")]
            bool? includeStructure = false,
            [Description("Include the full post-import Graph block in the returned mutation result. Default: false")]
            bool? includeGraph = false,
            [Description("Include shader compiler messages in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (edge == null)
                throw new ArgumentNullException(nameof(edge));

            return MainThread.Instance.Run(() => RerouteShaderGraphOutputSlot(
                assetRef,
                edge,
                includeStructure: includeStructure ?? false,
                includeGraph: includeGraph ?? false,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphEdgeMutationResultData ConnectShaderGraphEdge(
            AssetObjectRef assetRef,
            ShaderGraphConnectEdgeInput edge,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var document = LoadMutableDocument(assetPath);
            ApplyConnectEdgeSlotRefs(edge, new AssetObjectRef(assetPath));
            var outputSlot = ResolveNodeSlot(document, edge.OutputNodeObjectId, edge.OutputSlotObjectId, expectedSlotType: 1);
            var inputSlot = ResolveNodeSlot(document, edge.InputNodeObjectId, edge.InputSlotObjectId, expectedSlotType: 0);

            ValidateEdgeCompatibility(outputSlot, inputSlot);

            var edgesArray = EnsureEdgeArray(document.Root);
            if (FindEdgeIndex(edgesArray, outputSlot.NodeObjectId, outputSlot.SlotId, inputSlot.NodeObjectId, inputSlot.SlotId) >= 0)
            {
                throw new InvalidOperationException(
                    $"The requested edge already exists: {outputSlot.NodeObjectId}:{outputSlot.SlotId} -> {inputSlot.NodeObjectId}:{inputSlot.SlotId}.");
            }

            ShaderGraphEdgeDefinitionData? removedEdge = null;
            var incomingEdgeIndex = FindIncomingEdgeIndex(edgesArray, inputSlot.NodeObjectId, inputSlot.SlotId);
            if (incomingEdgeIndex >= 0)
            {
                if (edge.ReplaceExistingInputConnection != true)
                {
                    throw new InvalidOperationException(
                        $"Input slot '{inputSlot.SlotObjectId}' on node '{inputSlot.NodeObjectId}' is already connected. Disconnect it first or set `replaceExistingInputConnection` to true.");
                }

                if (edgesArray[incomingEdgeIndex] is not JsonObject existingEdgeObject)
                    throw new InvalidOperationException("The existing incoming Shader Graph edge could not be resolved.");

                removedEdge = ReadEdgeDefinition(existingEdgeObject);
                edgesArray.RemoveAt(incomingEdgeIndex);
            }

            edgesArray.Add(CreateEdgeObject(outputSlot.NodeObjectId, outputSlot.SlotId, inputSlot.NodeObjectId, inputSlot.SlotId));

            WriteMutableDocument(document);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();

            var graphRef = new AssetObjectRef(assetPath);
            var changedFields = new List<string>();
            var removedEdges = new List<ShaderGraphEdgeDefinitionData>();
            if (removedEdge != null)
            {
                changedFields.Add("edge.disconnected");
                changedFields.Add("edge.replaced");
                removedEdges.Add(removedEdge);
            }

            changedFields.Add("edge.connected");

            return new ShaderGraphEdgeMutationResultData
            {
                ChangedFields = changedFields,
                Edge = CreateEdgeDefinition(outputSlot.NodeObjectId, outputSlot.SlotId, inputSlot.NodeObjectId, inputSlot.SlotId),
                RemovedEdge = removedEdge,
                RemovedEdges = removedEdges.Count == 0 ? null : removedEdges,
                GraphSummary = BuildShaderGraphSummary(graphRef),
                Structure = includeStructure ? BuildShaderGraphStructureData(graphRef) : null,
                Graph = includeGraph
                    ? BuildShaderGraphData(
                        graphRef,
                        includeMessages: includeMessages,
                        includeProperties: includeProperties,
                        includeDiagnostics: true)
                    : null
            };
        }

        static ShaderGraphEdgeMutationResultData DisconnectShaderGraphEdge(
            AssetObjectRef assetRef,
            ShaderGraphDisconnectEdgeInput edge,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var document = LoadMutableDocument(assetPath);
            var outputSlot = ResolveNodeSlot(document, edge.OutputNodeObjectId, edge.OutputSlotObjectId, expectedSlotType: 1);
            var inputSlot = ResolveNodeSlot(document, edge.InputNodeObjectId, edge.InputSlotObjectId, expectedSlotType: 0);
            var edgesArray = EnsureEdgeArray(document.Root);
            var edgeIndex = FindEdgeIndex(edgesArray, outputSlot.NodeObjectId, outputSlot.SlotId, inputSlot.NodeObjectId, inputSlot.SlotId);

            if (edgeIndex < 0)
            {
                throw new InvalidOperationException(
                    $"The requested edge was not found: {outputSlot.NodeObjectId}:{outputSlot.SlotId} -> {inputSlot.NodeObjectId}:{inputSlot.SlotId}.");
            }

            edgesArray.RemoveAt(edgeIndex);

            WriteMutableDocument(document);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();

            var graphRef = new AssetObjectRef(assetPath);
            var removedEdge = CreateEdgeDefinition(outputSlot.NodeObjectId, outputSlot.SlotId, inputSlot.NodeObjectId, inputSlot.SlotId);
            return new ShaderGraphEdgeMutationResultData
            {
                ChangedFields = new List<string> { "edge.disconnected" },
                Edge = removedEdge,
                RemovedEdge = removedEdge,
                RemovedEdges = new List<ShaderGraphEdgeDefinitionData> { removedEdge },
                GraphSummary = BuildShaderGraphSummary(graphRef),
                Structure = includeStructure ? BuildShaderGraphStructureData(graphRef) : null,
                Graph = includeGraph
                    ? BuildShaderGraphData(
                        graphRef,
                        includeMessages: includeMessages,
                        includeProperties: includeProperties,
                        includeDiagnostics: true)
                    : null
            };
        }

        static ShaderGraphEdgeMutationResultData ReconnectShaderGraphEdge(
            AssetObjectRef assetRef,
            ShaderGraphReconnectEdgeInput edge,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var document = LoadMutableDocument(assetPath);
            var existingOutputSlot = ResolveNodeSlot(document, edge.ExistingOutputNodeObjectId, edge.ExistingOutputSlotObjectId, expectedSlotType: 1);
            var existingInputSlot = ResolveNodeSlot(document, edge.ExistingInputNodeObjectId, edge.ExistingInputSlotObjectId, expectedSlotType: 0);
            var newOutputSlot = ResolveReconnectOutputSlot(document, edge, existingOutputSlot);
            var newInputSlot = ResolveReconnectInputSlot(document, edge, existingInputSlot);

            if (existingOutputSlot.NodeObjectId == newOutputSlot.NodeObjectId
                && existingOutputSlot.SlotId == newOutputSlot.SlotId
                && existingInputSlot.NodeObjectId == newInputSlot.NodeObjectId
                && existingInputSlot.SlotId == newInputSlot.SlotId)
            {
                throw new InvalidOperationException("Reconnect requested no effective edge change.");
            }

            ValidateEdgeCompatibility(newOutputSlot, newInputSlot);

            var edgesArray = EnsureEdgeArray(document.Root);
            var existingEdgeIndex = FindEdgeIndex(
                edgesArray,
                existingOutputSlot.NodeObjectId,
                existingOutputSlot.SlotId,
                existingInputSlot.NodeObjectId,
                existingInputSlot.SlotId);

            if (existingEdgeIndex < 0)
            {
                throw new InvalidOperationException(
                    $"The requested edge was not found: {existingOutputSlot.NodeObjectId}:{existingOutputSlot.SlotId} -> {existingInputSlot.NodeObjectId}:{existingInputSlot.SlotId}.");
            }

            var removedEdges = new List<ShaderGraphEdgeDefinitionData>();
            var removedEdge = CreateEdgeDefinition(
                existingOutputSlot.NodeObjectId,
                existingOutputSlot.SlotId,
                existingInputSlot.NodeObjectId,
                existingInputSlot.SlotId);
            removedEdges.Add(removedEdge);
            edgesArray.RemoveAt(existingEdgeIndex);

            if (FindEdgeIndex(edgesArray, newOutputSlot.NodeObjectId, newOutputSlot.SlotId, newInputSlot.NodeObjectId, newInputSlot.SlotId) >= 0)
            {
                throw new InvalidOperationException(
                    $"The requested reconnected edge already exists: {newOutputSlot.NodeObjectId}:{newOutputSlot.SlotId} -> {newInputSlot.NodeObjectId}:{newInputSlot.SlotId}.");
            }

            var incomingEdgeIndex = FindIncomingEdgeIndex(edgesArray, newInputSlot.NodeObjectId, newInputSlot.SlotId);
            if (incomingEdgeIndex >= 0)
            {
                if (edge.ReplaceExistingInputConnection != true)
                {
                    throw new InvalidOperationException(
                        $"Input slot '{newInputSlot.SlotObjectId}' on node '{newInputSlot.NodeObjectId}' is already connected. Disconnect it first or set `replaceExistingInputConnection` to true.");
                }

                if (edgesArray[incomingEdgeIndex] is not JsonObject existingIncomingEdgeObject)
                    throw new InvalidOperationException("The existing incoming Shader Graph edge could not be resolved.");

                removedEdges.Add(ReadEdgeDefinition(existingIncomingEdgeObject));
                edgesArray.RemoveAt(incomingEdgeIndex);
            }

            edgesArray.Add(CreateEdgeObject(newOutputSlot.NodeObjectId, newOutputSlot.SlotId, newInputSlot.NodeObjectId, newInputSlot.SlotId));

            WriteMutableDocument(document);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();

            var graphRef = new AssetObjectRef(assetPath);
            var changedFields = new List<string>
            {
                "edge.disconnected",
                "edge.reconnected"
            };

            if (removedEdges.Count > 1)
                changedFields.Add("edge.replaced");

            changedFields.Add("edge.connected");

            return new ShaderGraphEdgeMutationResultData
            {
                ChangedFields = changedFields,
                Edge = CreateEdgeDefinition(newOutputSlot.NodeObjectId, newOutputSlot.SlotId, newInputSlot.NodeObjectId, newInputSlot.SlotId),
                RemovedEdge = removedEdge,
                RemovedEdges = removedEdges,
                GraphSummary = BuildShaderGraphSummary(graphRef),
                Structure = includeStructure ? BuildShaderGraphStructureData(graphRef) : null,
                Graph = includeGraph
                    ? BuildShaderGraphData(
                        graphRef,
                        includeMessages: includeMessages,
                        includeProperties: includeProperties,
                        includeDiagnostics: true)
                    : null
            };
        }

        static ShaderGraphEdgeMutationResultData RerouteShaderGraphOutputSlot(
            AssetObjectRef assetRef,
            ShaderGraphRerouteOutputSlotInput edge,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var document = LoadMutableDocument(assetPath);
            var existingOutputSlot = ResolveNodeSlot(document, edge.ExistingOutputNodeObjectId, edge.ExistingOutputSlotObjectId, expectedSlotType: 1);
            var newOutputSlot = ResolveNodeSlot(document, edge.NewOutputNodeObjectId, edge.NewOutputSlotObjectId, expectedSlotType: 1);

            if (existingOutputSlot.NodeObjectId == newOutputSlot.NodeObjectId
                && existingOutputSlot.SlotId == newOutputSlot.SlotId)
            {
                throw new InvalidOperationException("Reroute requested no effective output slot change.");
            }

            var edgesArray = EnsureEdgeArray(document.Root);
            var outgoingEdgeIndexes = FindOutgoingEdgeIndexes(edgesArray, existingOutputSlot.NodeObjectId, existingOutputSlot.SlotId);
            if (outgoingEdgeIndexes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Output slot '{existingOutputSlot.SlotObjectId}' on node '{existingOutputSlot.NodeObjectId}' has no outgoing edges to reroute.");
            }

            var removedEdges = new List<ShaderGraphEdgeDefinitionData>();
            var connectedEdges = new List<ShaderGraphEdgeDefinitionData>();
            var targetInputSlots = new List<NodeSlotContext>();
            foreach (var edgeIndex in outgoingEdgeIndexes)
            {
                if (edgesArray[edgeIndex] is not JsonObject existingEdgeObject)
                    throw new InvalidOperationException("An existing outgoing Shader Graph edge could not be resolved.");

                var removedEdge = ReadEdgeDefinition(existingEdgeObject);
                if (string.IsNullOrEmpty(removedEdge.InputNodeId) || !removedEdge.InputSlotId.HasValue)
                    throw new InvalidOperationException("The existing outgoing Shader Graph edge is missing input identifiers.");

                var targetInputSlot = ResolveNodeSlotBySlotId(
                    document,
                    removedEdge.InputNodeId!,
                    removedEdge.InputSlotId.Value,
                    expectedSlotType: 0);

                ValidateEdgeCompatibility(newOutputSlot, targetInputSlot);

                removedEdges.Add(removedEdge);
                targetInputSlots.Add(targetInputSlot);
                connectedEdges.Add(CreateEdgeDefinition(
                    newOutputSlot.NodeObjectId,
                    newOutputSlot.SlotId,
                    targetInputSlot.NodeObjectId,
                    targetInputSlot.SlotId));
            }

            var reroutedInputKeys = targetInputSlots
                .Select(slot => CreateEdgeEndpointKey(slot.NodeObjectId, slot.SlotId))
                .ToHashSet(StringComparer.Ordinal);

            for (var i = 0; i < edgesArray.Count; i++)
            {
                if (outgoingEdgeIndexes.Contains(i))
                    continue;

                if (edgesArray[i] is not JsonObject existingEdgeObject)
                    continue;

                var inputNodeId = GetStringAt(existingEdgeObject, "m_InputSlot", "m_Node", "m_Id");
                var inputSlotId = GetIntAt(existingEdgeObject, "m_InputSlot", "m_SlotId");
                if (!string.IsNullOrEmpty(inputNodeId)
                    && inputSlotId.HasValue
                    && reroutedInputKeys.Contains(CreateEdgeEndpointKey(inputNodeId!, inputSlotId.Value)))
                {
                    throw new InvalidOperationException(
                        $"Input slot '{inputNodeId}:{inputSlotId.Value}' already has an unrelated incoming edge and cannot be safely rerouted.");
                }

                if (connectedEdges.Any(connectedEdge =>
                        string.Equals(GetStringAt(existingEdgeObject, "m_OutputSlot", "m_Node", "m_Id"), connectedEdge.OutputNodeId, StringComparison.Ordinal)
                        && GetIntAt(existingEdgeObject, "m_OutputSlot", "m_SlotId") == connectedEdge.OutputSlotId
                        && string.Equals(inputNodeId, connectedEdge.InputNodeId, StringComparison.Ordinal)
                        && inputSlotId == connectedEdge.InputSlotId))
                {
                    throw new InvalidOperationException("Reroute would create a duplicate Shader Graph edge.");
                }
            }

            foreach (var edgeIndex in outgoingEdgeIndexes.OrderByDescending(index => index))
                edgesArray.RemoveAt(edgeIndex);

            foreach (var connectedEdge in connectedEdges)
            {
                edgesArray.Add(CreateEdgeObject(
                    connectedEdge.OutputNodeId!,
                    connectedEdge.OutputSlotId!.Value,
                    connectedEdge.InputNodeId!,
                    connectedEdge.InputSlotId!.Value));
            }

            WriteMutableDocument(document);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();

            var graphRef = new AssetObjectRef(assetPath);
            return new ShaderGraphEdgeMutationResultData
            {
                ChangedFields = new List<string>
                {
                    "edge.disconnected",
                    "edge.rerouted",
                    "edge.connected"
                },
                Edge = connectedEdges[0],
                Edges = connectedEdges,
                RemovedEdge = removedEdges[0],
                RemovedEdges = removedEdges,
                GraphSummary = BuildShaderGraphSummary(graphRef),
                Structure = includeStructure ? BuildShaderGraphStructureData(graphRef) : null,
                Graph = includeGraph
                    ? BuildShaderGraphData(
                        graphRef,
                        includeMessages: includeMessages,
                        includeProperties: includeProperties,
                        includeDiagnostics: true)
                    : null
            };
        }

        sealed class NodeSlotContext
        {
            public string NodeObjectId { get; set; } = string.Empty;
            public JsonObject NodeObject { get; set; } = null!;
            public string SlotObjectId { get; set; } = string.Empty;
            public JsonObject SlotObject { get; set; } = null!;
            public int SlotId { get; set; }
            public int SlotType { get; set; }
            public string SlotTypeName { get; set; } = string.Empty;
        }

        static NodeSlotContext ResolveNodeSlot(
            ShaderGraphMutableDocument document,
            string? nodeObjectIdValue,
            string? slotObjectIdValue,
            int expectedSlotType)
        {
            var nodeObjectId = nodeObjectIdValue?.Trim();
            var slotObjectId = slotObjectIdValue?.Trim();

            if (string.IsNullOrEmpty(nodeObjectId))
                throw new ArgumentException("node object id must be provided.");

            if (string.IsNullOrEmpty(slotObjectId))
                throw new ArgumentException("slot object id must be provided.");

            var resolvedNodeObjectId = nodeObjectId!;
            var resolvedSlotObjectId = slotObjectId!;

            if (!document.ObjectsById.TryGetValue(resolvedNodeObjectId, out var nodeObject))
                throw new InvalidOperationException($"Shader Graph node object '{resolvedNodeObjectId}' was not found.");

            var nodeSlotIds = GetIdArray(nodeObject, "m_Slots");
            if (!nodeSlotIds.Contains(resolvedSlotObjectId, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Slot '{resolvedSlotObjectId}' does not belong to node '{resolvedNodeObjectId}'.");
            }

            if (!document.ObjectsById.TryGetValue(resolvedSlotObjectId, out var slotObject))
                throw new InvalidOperationException($"Shader Graph slot object '{resolvedSlotObjectId}' was not found.");

            var slotType = GetInt(slotObject, "m_SlotType");
            if (!slotType.HasValue)
                throw new InvalidOperationException($"Shader Graph slot '{resolvedSlotObjectId}' is missing m_SlotType.");

            if (slotType.Value != expectedSlotType)
            {
                var expectedLabel = expectedSlotType == 1 ? "output" : "input";
                throw new InvalidOperationException(
                    $"Shader Graph slot '{resolvedSlotObjectId}' is not an {expectedLabel} slot.");
            }

            var slotId = GetInt(slotObject, "m_Id");
            if (!slotId.HasValue)
                throw new InvalidOperationException($"Shader Graph slot '{resolvedSlotObjectId}' is missing m_Id.");

            return new NodeSlotContext
            {
                NodeObjectId = resolvedNodeObjectId,
                NodeObject = nodeObject,
                SlotObjectId = resolvedSlotObjectId,
                SlotObject = slotObject,
                SlotId = slotId.Value,
                SlotType = slotType.Value,
                SlotTypeName = GetString(slotObject, "m_Type") ?? string.Empty
            };
        }

        static NodeSlotContext ResolveNodeSlotBySlotId(
            ShaderGraphMutableDocument document,
            string nodeObjectIdValue,
            int slotId,
            int expectedSlotType)
        {
            if (!document.ObjectsById.TryGetValue(nodeObjectIdValue, out var nodeObject))
                throw new InvalidOperationException($"Shader Graph node object '{nodeObjectIdValue}' was not found.");

            foreach (var slotObjectId in GetIdArray(nodeObject, "m_Slots"))
            {
                if (!document.ObjectsById.TryGetValue(slotObjectId, out var slotObject))
                    continue;

                if (GetInt(slotObject, "m_Id") == slotId)
                    return ResolveNodeSlot(document, nodeObjectIdValue, slotObjectId, expectedSlotType);
            }

            throw new InvalidOperationException(
                $"Shader Graph slot id '{slotId}' was not found on node '{nodeObjectIdValue}'.");
        }

        static void ValidateEdgeCompatibility(NodeSlotContext outputSlot, NodeSlotContext inputSlot)
        {
            var outputType = outputSlot.SlotTypeName;
            var inputType = inputSlot.SlotTypeName;

            if (string.IsNullOrEmpty(outputType) || string.IsNullOrEmpty(inputType))
                throw new InvalidOperationException("Both slots must expose a serialized m_Type.");

            if (string.Equals(outputType, inputType, StringComparison.Ordinal))
                return;

            if (Vector2LikeSlotTypes.Contains(outputType) && Vector2LikeSlotTypes.Contains(inputType))
                return;

            if (string.Equals(outputType, "UnityEditor.ShaderGraph.Vector1MaterialSlot", StringComparison.Ordinal)
                && Vector2ExpansionInputSlotTypes.Contains(inputType))
                return;

            // Scalar broadcast into UV inputs: Time.Time -> Simple Noise.UV uses (value, value) per
            // Unity's documented scalar-to-vector promotion. Validated against the DistortionTV trial.
            if (string.Equals(outputType, "UnityEditor.ShaderGraph.Vector1MaterialSlot", StringComparison.Ordinal)
                && string.Equals(inputType, "UnityEditor.ShaderGraph.UVMaterialSlot", StringComparison.Ordinal))
                return;

            if (IsDynamicVectorSlotType(outputType) && Vector2LikeSlotTypes.Contains(inputType))
                return;

            // Vector4 -> UV is supported directly via Unity's documented .xy truncation.
            // Validated against the Flame reference shader trial.
            if (string.Equals(outputType, "UnityEditor.ShaderGraph.Vector4MaterialSlot", StringComparison.Ordinal)
                && string.Equals(inputType, "UnityEditor.ShaderGraph.UVMaterialSlot", StringComparison.Ordinal))
                return;

            if (string.Equals(GetString(outputSlot.NodeObject, "m_Type"), "UnityEditor.ShaderGraph.ScreenPositionNode", StringComparison.Ordinal)
                && string.Equals(outputType, "UnityEditor.ShaderGraph.Vector4MaterialSlot", StringComparison.Ordinal)
                && ScreenPositionInputSlotTypes.Contains(inputType))
            {
                return;
            }

            if (IsDynamicVectorSlotType(outputType) && ScreenPositionInputSlotTypes.Contains(inputType))
                return;

            if (Vector3LikeSlotTypes.Contains(outputType) && Vector3LikeSlotTypes.Contains(inputType))
                return;

            if (Vector3LikeSlotTypes.Contains(outputType) && NormalInputSlotTypes.Contains(inputType))
                return;

            if ((ColorSlotTypes.Contains(outputType) || ColorSlotTypes.Contains(inputType))
                && ColorCompatibleValueSlotTypes.Contains(outputType)
                && ColorCompatibleValueSlotTypes.Contains(inputType))
                return;

            if (Texture2DLikeSlotTypes.Contains(outputType) && Texture2DLikeSlotTypes.Contains(inputType))
                return;

            var outputIsDynamic = IsDynamicSlotType(outputType);
            var inputIsDynamic = IsDynamicSlotType(inputType);

            if (outputIsDynamic && inputIsDynamic)
                return;

            if (outputIsDynamic && DynamicCompatibleSlotTypes.Contains(inputType))
                return;

            if (inputIsDynamic && DynamicCompatibleSlotTypes.Contains(outputType))
                return;

            throw new InvalidOperationException(
                $"Unsupported slot compatibility: '{outputType}' -> '{inputType}'.");
        }

        static bool IsDynamicSlotType(string slotType)
            => string.Equals(slotType, "UnityEditor.ShaderGraph.DynamicValueMaterialSlot", StringComparison.Ordinal)
               || string.Equals(slotType, "UnityEditor.ShaderGraph.DynamicVectorMaterialSlot", StringComparison.Ordinal);

        static bool IsDynamicVectorSlotType(string slotType)
            => string.Equals(slotType, "UnityEditor.ShaderGraph.DynamicVectorMaterialSlot", StringComparison.Ordinal);

        static JsonArray EnsureEdgeArray(JsonObject root)
        {
            if (root["m_Edges"] is JsonArray existingArray)
                return existingArray;

            var createdArray = new JsonArray();
            root["m_Edges"] = createdArray;
            return createdArray;
        }

        static JsonObject CreateEdgeObject(string outputNodeObjectId, int outputSlotId, string inputNodeObjectId, int inputSlotId)
        {
            return new JsonObject
            {
                ["m_OutputSlot"] = new JsonObject
                {
                    ["m_Node"] = new JsonObject
                    {
                        ["m_Id"] = outputNodeObjectId
                    },
                    ["m_SlotId"] = outputSlotId
                },
                ["m_InputSlot"] = new JsonObject
                {
                    ["m_Node"] = new JsonObject
                    {
                        ["m_Id"] = inputNodeObjectId
                    },
                    ["m_SlotId"] = inputSlotId
                }
            };
        }

        static ShaderGraphEdgeDefinitionData CreateEdgeDefinition(string outputNodeObjectId, int outputSlotId, string inputNodeObjectId, int inputSlotId)
        {
            return new ShaderGraphEdgeDefinitionData
            {
                OutputNodeId = outputNodeObjectId,
                OutputSlotId = outputSlotId,
                InputNodeId = inputNodeObjectId,
                InputSlotId = inputSlotId
            };
        }

        static ShaderGraphEdgeDefinitionData ReadEdgeDefinition(JsonObject edgeObject)
        {
            var outputNodeId = GetStringAt(edgeObject, "m_OutputSlot", "m_Node", "m_Id");
            var outputSlotId = GetIntAt(edgeObject, "m_OutputSlot", "m_SlotId");
            var inputNodeId = GetStringAt(edgeObject, "m_InputSlot", "m_Node", "m_Id");
            var inputSlotId = GetIntAt(edgeObject, "m_InputSlot", "m_SlotId");

            if (string.IsNullOrEmpty(outputNodeId)
                || string.IsNullOrEmpty(inputNodeId)
                || !outputSlotId.HasValue
                || !inputSlotId.HasValue)
            {
                throw new InvalidOperationException("The existing Shader Graph edge is missing serialized slot identifiers.");
            }

            return CreateEdgeDefinition(outputNodeId!, outputSlotId.Value, inputNodeId!, inputSlotId.Value);
        }

        static NodeSlotContext ResolveReconnectOutputSlot(
            ShaderGraphMutableDocument document,
            ShaderGraphReconnectEdgeInput edge,
            NodeSlotContext existingOutputSlot)
        {
            var hasNewOutputNode = !string.IsNullOrWhiteSpace(edge.NewOutputNodeObjectId);
            var hasNewOutputSlot = !string.IsNullOrWhiteSpace(edge.NewOutputSlotObjectId);
            if (hasNewOutputNode != hasNewOutputSlot)
                throw new ArgumentException("Reconnect output updates require both new output node and slot ids.");

            return hasNewOutputNode
                ? ResolveNodeSlot(document, edge.NewOutputNodeObjectId, edge.NewOutputSlotObjectId, expectedSlotType: 1)
                : existingOutputSlot;
        }

        static NodeSlotContext ResolveReconnectInputSlot(
            ShaderGraphMutableDocument document,
            ShaderGraphReconnectEdgeInput edge,
            NodeSlotContext existingInputSlot)
        {
            var hasNewInputNode = !string.IsNullOrWhiteSpace(edge.NewInputNodeObjectId);
            var hasNewInputSlot = !string.IsNullOrWhiteSpace(edge.NewInputSlotObjectId);
            if (hasNewInputNode != hasNewInputSlot)
                throw new ArgumentException("Reconnect input updates require both new input node and slot ids.");

            return hasNewInputNode
                ? ResolveNodeSlot(document, edge.NewInputNodeObjectId, edge.NewInputSlotObjectId, expectedSlotType: 0)
                : existingInputSlot;
        }

        static int FindIncomingEdgeIndex(JsonArray edgesArray, string inputNodeObjectId, int inputSlotId)
        {
            for (var i = 0; i < edgesArray.Count; i++)
            {
                if (edgesArray[i] is not JsonObject edgeObject)
                    continue;

                if (string.Equals(GetStringAt(edgeObject, "m_InputSlot", "m_Node", "m_Id"), inputNodeObjectId, StringComparison.Ordinal)
                    && GetIntAt(edgeObject, "m_InputSlot", "m_SlotId") == inputSlotId)
                {
                    return i;
                }
            }

            return -1;
        }

        static List<int> FindOutgoingEdgeIndexes(JsonArray edgesArray, string outputNodeObjectId, int outputSlotId)
        {
            var indexes = new List<int>();
            for (var i = 0; i < edgesArray.Count; i++)
            {
                if (edgesArray[i] is not JsonObject edgeObject)
                    continue;

                if (string.Equals(GetStringAt(edgeObject, "m_OutputSlot", "m_Node", "m_Id"), outputNodeObjectId, StringComparison.Ordinal)
                    && GetIntAt(edgeObject, "m_OutputSlot", "m_SlotId") == outputSlotId)
                {
                    indexes.Add(i);
                }
            }

            return indexes;
        }

        static string CreateEdgeEndpointKey(string nodeObjectId, int slotId)
            => $"{nodeObjectId}:{slotId}";

        static int FindEdgeIndex(JsonArray edgesArray, string outputNodeObjectId, int outputSlotId, string inputNodeObjectId, int inputSlotId)
        {
            for (var i = 0; i < edgesArray.Count; i++)
            {
                if (edgesArray[i] is not JsonObject edgeObject)
                    continue;

                if (string.Equals(GetStringAt(edgeObject, "m_OutputSlot", "m_Node", "m_Id"), outputNodeObjectId, StringComparison.Ordinal)
                    && GetIntAt(edgeObject, "m_OutputSlot", "m_SlotId") == outputSlotId
                    && string.Equals(GetStringAt(edgeObject, "m_InputSlot", "m_Node", "m_Id"), inputNodeObjectId, StringComparison.Ordinal)
                    && GetIntAt(edgeObject, "m_InputSlot", "m_SlotId") == inputSlotId)
                {
                    return i;
                }
            }

            return -1;
        }

        static string? GetStringAt(JsonObject root, params string[] propertyPath)
        {
            JsonNode? current = root;
            foreach (var propertyName in propertyPath)
            {
                if (current is not JsonObject currentObject)
                    return null;

                current = currentObject[propertyName];
                if (current == null)
                    return null;
            }

            return current is JsonValue value && value.TryGetValue<string>(out var result)
                ? result
                : null;
        }

        static int? GetIntAt(JsonObject root, params string[] propertyPath)
        {
            JsonNode? current = root;
            foreach (var propertyName in propertyPath)
            {
                if (current is not JsonObject currentObject)
                    return null;

                current = currentObject[propertyName];
                if (current == null)
                    return null;
            }

            if (current is not JsonValue value)
                return null;

            if (value.TryGetValue<int>(out var intValue))
                return intValue;

            if (value.TryGetValue<double>(out var doubleValue))
                return (int)doubleValue;

            return null;
        }

        // Slice 1 helper: when the caller supplied SlotRefs (Node alias/display + slot display name)
        // instead of the legacy *NodeObjectId / *SlotObjectId pair, resolve them against the current
        // structure and write the resolved object ids back into the input DTO. The downstream JSON
        // mutator then continues unchanged.
        static void ApplyConnectEdgeSlotRefs(
            ShaderGraphConnectEdgeInput edge,
            AssetObjectRef graphRef,
            ShaderGraphAliasBag? aliases = null)
        {
            if (edge.OutputSlot == null && edge.InputSlot == null)
                return;

            var structure = BuildShaderGraphStructureData(graphRef);

            if (edge.OutputSlot != null)
            {
                var resolved = ResolveSlotRef(edge.OutputSlot, structure, aliases, "edge.outputSlot");
                edge.OutputNodeObjectId = resolved.NodeObjectId;
                edge.OutputSlotObjectId = resolved.SlotObjectId;
            }

            if (edge.InputSlot != null)
            {
                var resolved = ResolveSlotRef(edge.InputSlot, structure, aliases, "edge.inputSlot");
                edge.InputNodeObjectId = resolved.NodeObjectId;
                edge.InputSlotObjectId = resolved.SlotObjectId;
            }
        }
    }
}
