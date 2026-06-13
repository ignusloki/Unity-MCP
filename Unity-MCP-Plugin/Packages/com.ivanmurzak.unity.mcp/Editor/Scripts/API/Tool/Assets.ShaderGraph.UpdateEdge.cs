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

        static readonly HashSet<string> DynamicCompatibleSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.Vector1MaterialSlot",
            "UnityEditor.ShaderGraph.Vector2MaterialSlot",
            "UnityEditor.ShaderGraph.Vector3MaterialSlot",
            "UnityEditor.ShaderGraph.Vector4MaterialSlot",
            "UnityEditor.ShaderGraph.ColorRGBMaterialSlot",
            "UnityEditor.ShaderGraph.ColorRGBAMaterialSlot"
        };

        static readonly HashSet<string> Vector2LikeSlotTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.ShaderGraph.Vector2MaterialSlot",
            "UnityEditor.ShaderGraph.UVMaterialSlot"
        };

        [AiTool
        (
            AssetsShaderGraphConnectEdgeToolId,
            Title = "Assets / Shader Graph / Connect Edge"
        )]
        [AiSkillDescription("Connect two existing Shader Graph slots, then re-import the graph and return the connected edge and diagnostics.")]
        [AiSkillBody("Connect an existing output slot to an existing input slot inside a '.shadergraph' asset.\n\n" +
            "Current support is intentionally narrow and safe:\n" +
            "- selection by node object id plus slot object id\n" +
            "- requires the input slot to be currently unconnected unless `replaceExistingInputConnection` is true\n" +
            "- supports exact slot-type matches\n" +
            "- supports compatible UV/vector2 slot pairs\n" +
            "- supports dynamic numeric/vector/color slots via Shader Graph dynamic slot families such as `DynamicValueMaterialSlot` and `DynamicVectorMaterialSlot`\n" +
            "- supports guarded input-edge replacement when `replaceExistingInputConnection` is true\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect node ids, slot ids, and slot types.")]
        [Description("Connect two existing Shader Graph slots and re-import the graph.")]
        public ShaderGraphEdgeMutationResultData ConnectEdge(
            AssetObjectRef assetRef,
            ShaderGraphConnectEdgeInput edge,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
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
            "Use `assets-shadergraph-get-structure` first to inspect node ids and slot ids.")]
        [Description("Disconnect an existing Shader Graph edge and re-import the graph.")]
        public ShaderGraphEdgeMutationResultData DisconnectEdge(
            AssetObjectRef assetRef,
            ShaderGraphDisconnectEdgeInput edge,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
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
            "Use `assets-shadergraph-get-structure` first to inspect node ids, slot ids, and current edges.")]
        [Description("Reconnect an existing Shader Graph edge to a new endpoint and re-import the graph.")]
        public ShaderGraphEdgeMutationResultData ReconnectEdge(
            AssetObjectRef assetRef,
            ShaderGraphReconnectEdgeInput edge,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
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
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphEdgeMutationResultData ConnectShaderGraphEdge(
            AssetObjectRef assetRef,
            ShaderGraphConnectEdgeInput edge,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var document = LoadMutableDocument(assetPath);
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
                Structure = BuildShaderGraphStructureData(graphRef),
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        static ShaderGraphEdgeMutationResultData DisconnectShaderGraphEdge(
            AssetObjectRef assetRef,
            ShaderGraphDisconnectEdgeInput edge,
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
                Structure = BuildShaderGraphStructureData(graphRef),
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        static ShaderGraphEdgeMutationResultData ReconnectShaderGraphEdge(
            AssetObjectRef assetRef,
            ShaderGraphReconnectEdgeInput edge,
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
                Structure = BuildShaderGraphStructureData(graphRef),
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
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

            var outputIsDynamic = IsDynamicSlotType(outputType);
            var inputIsDynamic = IsDynamicSlotType(inputType);

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
    }
}
