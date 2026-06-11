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
        public const string AssetsShaderGraphAddPropertyNodeToolId = "assets-shadergraph-add-property-node";

        [AiTool
        (
            AssetsShaderGraphAddPropertyNodeToolId,
            Title = "Assets / Shader Graph / Add Property Node"
        )]
        [AiSkillDescription("Add a Shader Graph Property node for an existing blackboard property, then re-import the graph and return the created node and diagnostics.")]
        [AiSkillBody("Add a safe allowlisted Property node to a '.shadergraph' asset.\n\n" +
            "Current support is intentionally scoped to common URP Blackboard property types:\n" +
            "- existing blackboard properties only\n" +
            "- property types: `color`, `float`, `texture2D`, `vector2`, `vector3`, `vector4`, `boolean`\n" +
            "- no edge wiring yet\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `node` — selector for an existing blackboard property plus the requested node position.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data.\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect property object ids and effective reference names.")]
        [Description("Add a Shader Graph Property node for an existing blackboard property and re-import the graph.")]
        public ShaderGraphNodeMutationResultData AddPropertyNode(
            AssetObjectRef assetRef,
            ShaderGraphAddPropertyNodeInput node,
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

            return MainThread.Instance.Run(() => AddShaderGraphPropertyNode(
                assetRef,
                node,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphNodeMutationResultData AddShaderGraphPropertyNode(
            AssetObjectRef assetRef,
            ShaderGraphAddPropertyNodeInput node,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var document = LoadMutableDocument(assetPath);
            var propertyIds = GetIdArray(document.Root, "m_Properties");
            var propertyObjects = propertyIds
                .Where(document.ObjectsById.ContainsKey)
                .Select(id => document.ObjectsById[id])
                .ToList();

            var propertyObject = ResolvePropertyObject(node, propertyObjects);
            var propertyObjectId = GetString(propertyObject, "m_ObjectId")
                ?? throw new InvalidOperationException("Resolved Shader Graph property is missing m_ObjectId.");
            var propertyType = GetString(propertyObject, "m_Type")
                ?? throw new InvalidOperationException("Resolved Shader Graph property is missing m_Type.");
            var propertyDisplayName = GetString(propertyObject, "m_Name");

            var nodeObjectId = Guid.NewGuid().ToString("N");
            var slotObjectId = Guid.NewGuid().ToString("N");
            var createdNode = CreatePropertyNodeObject(
                nodeObjectId,
                slotObjectId,
                propertyObjectId,
                node.PositionX ?? 0f,
                node.PositionY ?? 0f);
            var createdSlot = CreatePropertyNodeSlotObject(
                slotObjectId,
                propertyType,
                propertyDisplayName);

            document.Objects.Add(createdNode);
            document.Objects.Add(createdSlot);
            document.ObjectsById[nodeObjectId] = createdNode;
            document.ObjectsById[slotObjectId] = createdSlot;

            AddNodeReferenceToRoot(document.Root, nodeObjectId);

            WriteMutableDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var addedNode = structure.Nodes?
                .FirstOrDefault(n => string.Equals(n.ObjectId, nodeObjectId, StringComparison.Ordinal));

            return new ShaderGraphNodeMutationResultData
            {
                ChangedFields = new List<string>
                {
                    "node.added",
                    "node.slot.added"
                },
                Node = addedNode,
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        static JsonObject ResolvePropertyObject(
            ShaderGraphAddPropertyNodeInput node,
            List<JsonObject> propertyObjects)
        {
            var objectId = node.PropertyObjectId?.Trim();
            var referenceName = node.PropertyReferenceName?.Trim();

            if (string.IsNullOrEmpty(objectId) && string.IsNullOrEmpty(referenceName))
            {
                throw new ArgumentException(
                    "Either propertyObjectId or propertyReferenceName must be provided.");
            }

            JsonObject? resolved = null;
            if (!string.IsNullOrEmpty(objectId))
            {
                resolved = propertyObjects.FirstOrDefault(obj =>
                    string.Equals(GetString(obj, "m_ObjectId"), objectId, StringComparison.Ordinal));
            }
            else
            {
                resolved = propertyObjects.FirstOrDefault(obj =>
                    string.Equals(GetEffectivePropertyReferenceName(obj), referenceName, StringComparison.Ordinal));
            }

            if (resolved == null)
            {
                throw new InvalidOperationException(
                    $"Shader Graph property was not found. objectId='{objectId ?? string.Empty}', referenceName='{referenceName ?? string.Empty}'.");
            }

            return resolved;
        }

        static JsonObject CreatePropertyNodeObject(
            string nodeObjectId,
            string slotObjectId,
            string propertyObjectId,
            float positionX,
            float positionY)
        {
            return new JsonObject
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = "UnityEditor.ShaderGraph.PropertyNode",
                ["m_ObjectId"] = nodeObjectId,
                ["m_Group"] = new JsonObject
                {
                    ["m_Id"] = string.Empty
                },
                ["m_Name"] = "Property",
                ["m_DrawState"] = new JsonObject
                {
                    ["m_Expanded"] = true,
                    ["m_Position"] = new JsonObject
                    {
                        ["serializedVersion"] = "2",
                        ["x"] = positionX,
                        ["y"] = positionY,
                        ["width"] = 140.0,
                        ["height"] = 36.0
                    }
                },
                ["m_Slots"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["m_Id"] = slotObjectId
                    }
                },
                ["synonyms"] = new JsonArray(),
                ["m_Precision"] = 0,
                ["m_PreviewExpanded"] = true,
                ["m_DismissedVersion"] = 0,
                ["m_PreviewMode"] = 0,
                ["m_CustomColors"] = new JsonObject
                {
                    ["m_SerializableColors"] = new JsonArray()
                },
                ["m_Property"] = new JsonObject
                {
                    ["m_Id"] = propertyObjectId
                }
            };
        }

        static JsonObject CreatePropertyNodeSlotObject(
            string slotObjectId,
            string propertyType,
            string? propertyDisplayName)
        {
            var displayName = string.IsNullOrWhiteSpace(propertyDisplayName)
                ? "Property"
                : propertyDisplayName!.Trim();

            return propertyType switch
            {
                "UnityEditor.ShaderGraph.Internal.ColorShaderProperty" => CreateVectorSlotObject(
                    slotObjectId,
                    "UnityEditor.ShaderGraph.Vector4MaterialSlot",
                    displayName,
                    dimension: 4),
                "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty" => CreateFloatSlotObject(
                    slotObjectId,
                    displayName),
                "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty" => CreateTexture2DSlotObject(
                    slotObjectId,
                    displayName),
                "UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty" => CreateVectorSlotObject(
                    slotObjectId,
                    "UnityEditor.ShaderGraph.Vector2MaterialSlot",
                    displayName,
                    dimension: 2),
                "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty" => CreateVectorSlotObject(
                    slotObjectId,
                    "UnityEditor.ShaderGraph.Vector3MaterialSlot",
                    displayName,
                    dimension: 3),
                "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty" => CreateVectorSlotObject(
                    slotObjectId,
                    "UnityEditor.ShaderGraph.Vector4MaterialSlot",
                    displayName,
                    dimension: 4),
                "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty" => CreateBooleanSlotObject(
                    slotObjectId,
                    displayName),
                _ => throw new InvalidOperationException(
                    $"Property nodes currently support color, float, texture2D, vector2, vector3, vector4, and boolean properties. Property type: '{propertyType}'.")
            };
        }

        static JsonObject CreateFloatSlotObject(string slotObjectId, string displayName)
            => new()
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = "UnityEditor.ShaderGraph.Vector1MaterialSlot",
                ["m_ObjectId"] = slotObjectId,
                ["m_Id"] = 0,
                ["m_DisplayName"] = displayName,
                ["m_SlotType"] = 1,
                ["m_Hidden"] = false,
                ["m_ShaderOutputName"] = "Out",
                ["m_StageCapability"] = 3,
                ["m_Value"] = 0.0,
                ["m_DefaultValue"] = 0.0,
                ["m_Labels"] = new JsonArray()
            };

        static JsonObject CreateVectorSlotObject(
            string slotObjectId,
            string slotType,
            string displayName,
            int dimension)
        {
            var value = CreateVectorValueObject(dimension);

            return new JsonObject
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = slotType,
                ["m_ObjectId"] = slotObjectId,
                ["m_Id"] = 0,
                ["m_DisplayName"] = displayName,
                ["m_SlotType"] = 1,
                ["m_Hidden"] = false,
                ["m_ShaderOutputName"] = "Out",
                ["m_StageCapability"] = 3,
                ["m_Value"] = value.DeepClone(),
                ["m_DefaultValue"] = value,
                ["m_Labels"] = new JsonArray()
            };
        }

        static JsonObject CreateVectorValueObject(int dimension)
        {
            var value = new JsonObject
            {
                ["x"] = 0.0,
                ["y"] = 0.0
            };

            if (dimension >= 3)
                value["z"] = 0.0;

            if (dimension >= 4)
                value["w"] = 0.0;

            return value;
        }

        static JsonObject CreateBooleanSlotObject(string slotObjectId, string displayName)
            => new()
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = "UnityEditor.ShaderGraph.BooleanMaterialSlot",
                ["m_ObjectId"] = slotObjectId,
                ["m_Id"] = 0,
                ["m_DisplayName"] = displayName,
                ["m_SlotType"] = 1,
                ["m_Hidden"] = false,
                ["m_ShaderOutputName"] = "Out",
                ["m_StageCapability"] = 3,
                ["m_Value"] = false,
                ["m_DefaultValue"] = false
            };

        static JsonObject CreateTexture2DSlotObject(string slotObjectId, string displayName)
            => new()
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = "UnityEditor.ShaderGraph.Texture2DMaterialSlot",
                ["m_ObjectId"] = slotObjectId,
                ["m_Id"] = 0,
                ["m_DisplayName"] = displayName,
                ["m_SlotType"] = 1,
                ["m_Hidden"] = false,
                ["m_ShaderOutputName"] = "Out",
                ["m_StageCapability"] = 3,
                ["m_BareResource"] = false
            };

        static void AddNodeReferenceToRoot(JsonObject root, string nodeObjectId)
        {
            var nodesArray = EnsureReferenceArray(root, "m_Nodes");
            nodesArray.Add(new JsonObject
            {
                ["m_Id"] = nodeObjectId
            });
        }
    }
}
