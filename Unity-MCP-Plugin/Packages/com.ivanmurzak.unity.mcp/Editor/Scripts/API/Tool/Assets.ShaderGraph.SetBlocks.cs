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

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphSetBlocksToolId = "assets-shadergraph-set-blocks";

        sealed class ShaderGraphBlockDefinition
        {
            public string ApiName { get; set; } = string.Empty;
            public string Descriptor { get; set; } = string.Empty;
            public string Context { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string ShaderOutputName { get; set; } = string.Empty;
            public string SlotTypeName { get; set; } = string.Empty;
            public float DefaultFloat { get; set; }
            public float DefaultX { get; set; }
            public float DefaultY { get; set; }
            public float DefaultZ { get; set; }
            public float DefaultW { get; set; } = 1f;
            public int ColorMode { get; set; }
            public int? Space { get; set; }
            public string[] Aliases { get; set; } = Array.Empty<string>();
            public int StageCapability => string.Equals(Context, "vertex", StringComparison.Ordinal) ? 1 : 2;
        }

        static readonly List<ShaderGraphBlockDefinition> AllowlistedBlockDefinitions =
            CreateAllowlistedBlockDefinitions();

        [AiTool
        (
            AssetsShaderGraphSetBlocksToolId,
            Title = "Assets / Shader Graph / Set Blocks"
        )]
        [AiSkillDescription("Set the ordered built-in Shader Graph vertex or fragment block stack, then re-import the graph and return diagnostics.")]
        [AiSkillBody("Set the ordered built-in block stack for one Shader Graph context.\n\n" +
            "This is a full replacement list for the selected context's supported built-in blocks. Missing requested blocks are created with Unity-compatible default slots. Existing supported blocks not in the requested list are removed only when unconnected, unless `allowRemovingConnectedBlocks` is true.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `blocks.context` = `vertex` | `fragment`.\n" +
            "- `blocks.blocks` — ordered descriptors or aliases.\n" +
            "- `blocks.allowRemovingConnectedBlocks` — when true, remove connected block edges while removing blocks.\n\n" +
            "Supported vertex blocks: `position`, `normal`, `tangent`, `motionVector`.\n" +
            "Supported fragment blocks: `baseColor`, `normalTS`, `normalOS`, `normalWS`, `bentNormal`, `metallic`, `specular`, `smoothness`, `occlusion`, `emission`, `alpha`, `alphaClipThreshold`, `coatMask`, `coatSmoothness`, `normalAlpha`, `maosAlpha`.\n\n" +
            "Use `assets-shadergraph-get-structure` after mutation to inspect the created block node ids and slots before wiring edges.")]
        [Description("Set the ordered built-in Shader Graph block stack for one context and re-import the graph.")]
        public ShaderGraphBlockMutationResultData SetBlocks(
            AssetObjectRef assetRef,
            ShaderGraphSetBlocksInput blocks,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (blocks == null)
                throw new ArgumentNullException(nameof(blocks));

            return MainThread.Instance.Run(() => SetShaderGraphBlocks(
                assetRef,
                blocks,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphBlockMutationResultData SetShaderGraphBlocks(
            AssetObjectRef assetRef,
            ShaderGraphSetBlocksInput blocks,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var contextName = NormalizeBlockContext(blocks.Context);
            if (blocks.Blocks == null)
                throw new ArgumentException("blocks.blocks must be provided.", nameof(blocks));

            var requestedDefinitions = blocks.Blocks
                .Select(block => ResolveBlockDefinition(contextName, block))
                .ToList();

            var duplicateDescriptor = requestedDefinitions
                .GroupBy(definition => definition.Descriptor, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;
            if (!string.IsNullOrEmpty(duplicateDescriptor))
                throw new ArgumentException($"Block descriptor '{duplicateDescriptor}' was provided more than once.", nameof(blocks));

            var document = LoadMutableDocument(assetPath);
            var contextObject = GetContextObject(document.Root, contextName);
            var existingBlockIds = GetIdArray(contextObject, "m_Blocks");
            var blockByDescriptor = ResolveExistingBlockNodes(document, existingBlockIds, contextName);
            var requestedDescriptors = requestedDefinitions
                .Select(definition => definition.Descriptor)
                .ToHashSet(StringComparer.Ordinal);
            var removedBlockNodeIds = new List<string>();
            var createdBlockNodeIds = new List<string>();
            var changedFields = new List<string>();
            var removedEdgeCount = 0;
            var structureBeforeMutation = BuildShaderGraphStructureData(new AssetObjectRef(assetPath));

            foreach (var existingBlock in blockByDescriptor.Values.ToList())
            {
                if (requestedDescriptors.Contains(existingBlock.Definition.Descriptor))
                    continue;

                var connectedEdgeCount = CountConnectedEdges(structureBeforeMutation, existingBlock.BlockNodeId);
                if (connectedEdgeCount > 0 && blocks.AllowRemovingConnectedBlocks != true)
                {
                    throw new InvalidOperationException(
                        $"Block '{existingBlock.Definition.Descriptor}' has {connectedEdgeCount} connected edge(s). " +
                        "Disconnect it first or set allowRemovingConnectedBlocks to true.");
                }

                if (connectedEdgeCount > 0)
                    removedEdgeCount += RemoveEdgesConnectedToNodes(document.Root, new HashSet<string> { existingBlock.BlockNodeId });

                RemoveReferenceFromArray(document.Root, "m_Nodes", existingBlock.BlockNodeId);
                RemoveObjectById(document, existingBlock.BlockNodeId);
                RemoveObjectById(document, existingBlock.SlotObjectId);
                removedBlockNodeIds.Add(existingBlock.BlockNodeId);
            }

            foreach (var definition in requestedDefinitions)
            {
                if (blockByDescriptor.ContainsKey(definition.Descriptor))
                    continue;

                var blockNodeId = CreateUniqueShaderGraphObjectId(document);
                var slotObjectId = CreateUniqueShaderGraphObjectId(document);
                var blockObject = CreateBlockNodeObject(blockNodeId, slotObjectId, definition);
                var slotObject = CreateBlockSlotObject(slotObjectId, definition);

                document.Objects.Add(blockObject);
                document.Objects.Add(slotObject);
                document.ObjectsById[blockNodeId] = blockObject;
                document.ObjectsById[slotObjectId] = slotObject;
                AddNodeReferenceToRoot(document.Root, blockNodeId);
                createdBlockNodeIds.Add(blockNodeId);
                blockByDescriptor[definition.Descriptor] = new ExistingBlockNode(definition, blockNodeId, slotObjectId);
            }

            var desiredBlockIds = requestedDefinitions
                .Select(definition => blockByDescriptor[definition.Descriptor].BlockNodeId)
                .ToList();

            if (!existingBlockIds.SequenceEqual(desiredBlockIds))
                changedFields.Add($"{contextName}Context.blocks");
            if (createdBlockNodeIds.Count > 0)
                changedFields.Add("block.created");
            if (removedBlockNodeIds.Count > 0)
                changedFields.Add("block.removed");
            if (removedEdgeCount > 0)
                changedFields.Add("edge.autoRemoved");

            if (changedFields.Count > 0)
            {
                contextObject["m_Blocks"] = CreateReferenceArray(desiredBlockIds);
                WriteMutableDocument(document);
                FinalizeShaderGraphMutation(assetPath);
            }

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var context = string.Equals(contextName, "vertex", StringComparison.Ordinal)
                ? structure.VertexContext
                : structure.FragmentContext;
            var blockDescriptors = context?.BlockNodeIds?
                .Select(blockNodeId => structure.Nodes?.FirstOrDefault(node =>
                    string.Equals(node.ObjectId, blockNodeId, StringComparison.Ordinal))?.SerializedDescriptor)
                .Where(descriptor => !string.IsNullOrEmpty(descriptor))
                .Cast<string>()
                .ToList();

            return new ShaderGraphBlockMutationResultData
            {
                Operation = "setBlocks",
                Context = contextName,
                BlockDescriptors = blockDescriptors,
                CreatedBlockNodeIds = createdBlockNodeIds,
                RemovedBlockNodeIds = removedBlockNodeIds,
                RemovedEdgeCount = removedEdgeCount,
                ChangedFields = changedFields,
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        sealed class ExistingBlockNode
        {
            public ExistingBlockNode(ShaderGraphBlockDefinition definition, string blockNodeId, string slotObjectId)
            {
                Definition = definition;
                BlockNodeId = blockNodeId;
                SlotObjectId = slotObjectId;
            }

            public ShaderGraphBlockDefinition Definition { get; }
            public string BlockNodeId { get; }
            public string SlotObjectId { get; }
        }

        static string NormalizeBlockContext(string? context)
        {
            return NormalizeEnumValue(context ?? string.Empty) switch
            {
                "vertex" => "vertex",
                "fragment" => "fragment",
                _ => throw new ArgumentException("blocks.context must be 'vertex' or 'fragment'.")
            };
        }

        static ShaderGraphBlockDefinition ResolveBlockDefinition(string context, string? block)
        {
            if (string.IsNullOrWhiteSpace(block))
                throw new ArgumentException("Block names must not be empty.");

            var normalized = NormalizeEnumValue(block!);
            var definition = AllowlistedBlockDefinitions
                .Where(def => string.Equals(def.Context, context, StringComparison.Ordinal))
                .FirstOrDefault(def =>
                    string.Equals(NormalizeEnumValue(def.ApiName), normalized, StringComparison.Ordinal)
                    || string.Equals(NormalizeEnumValue(def.Descriptor), normalized, StringComparison.Ordinal)
                    || def.Aliases.Any(alias => string.Equals(NormalizeEnumValue(alias), normalized, StringComparison.Ordinal)));

            if (definition != null)
                return definition;

            var supportedValues = string.Join(", ", AllowlistedBlockDefinitions
                .Where(def => string.Equals(def.Context, context, StringComparison.Ordinal))
                .Select(def => def.ApiName)
                .OrderBy(name => name, StringComparer.Ordinal));

            throw new ArgumentException(
                $"Unsupported {context} block '{block}'. Supported values: {supportedValues}.");
        }

        static JsonObject GetContextObject(JsonObject root, string contextName)
        {
            var propertyName = string.Equals(contextName, "vertex", StringComparison.Ordinal)
                ? "m_VertexContext"
                : "m_FragmentContext";

            if (root[propertyName] is JsonObject contextObject)
                return contextObject;

            throw new InvalidOperationException($"Shader Graph root is missing '{propertyName}'.");
        }

        static Dictionary<string, ExistingBlockNode> ResolveExistingBlockNodes(
            ShaderGraphMutableDocument document,
            List<string> blockNodeIds,
            string contextName)
        {
            var result = new Dictionary<string, ExistingBlockNode>(StringComparer.Ordinal);

            foreach (var blockNodeId in blockNodeIds)
            {
                if (!document.ObjectsById.TryGetValue(blockNodeId, out var blockObject))
                    throw new InvalidOperationException($"Context references missing block node '{blockNodeId}'.");

                var descriptor = GetString(blockObject, "m_SerializedDescriptor");
                if (string.IsNullOrWhiteSpace(descriptor))
                    throw new InvalidOperationException($"Block node '{blockNodeId}' is missing m_SerializedDescriptor.");

                var definition = AllowlistedBlockDefinitions.FirstOrDefault(def =>
                    string.Equals(def.Context, contextName, StringComparison.Ordinal)
                    && string.Equals(def.Descriptor, descriptor, StringComparison.Ordinal));
                if (definition == null)
                {
                    throw new InvalidOperationException(
                        $"Block '{descriptor}' in the {contextName} context is not supported by this tool. " +
                        "Use Unity for custom or unsupported blocks.");
                }

                var slotObjectId = GetIdArray(blockObject, "m_Slots").FirstOrDefault();
                if (string.IsNullOrEmpty(slotObjectId))
                    throw new InvalidOperationException($"Block node '{blockNodeId}' does not reference a slot.");

                result[definition.Descriptor] = new ExistingBlockNode(definition, blockNodeId, slotObjectId);
            }

            return result;
        }

        static JsonObject CreateBlockNodeObject(
            string blockNodeId,
            string slotObjectId,
            ShaderGraphBlockDefinition definition)
        {
            return new JsonObject
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = "UnityEditor.ShaderGraph.BlockNode",
                ["m_ObjectId"] = blockNodeId,
                ["m_Group"] = new JsonObject
                {
                    ["m_Id"] = string.Empty
                },
                ["m_Name"] = definition.Descriptor,
                ["m_DrawState"] = new JsonObject
                {
                    ["m_Expanded"] = true,
                    ["m_Position"] = new JsonObject
                    {
                        ["serializedVersion"] = "2",
                        ["x"] = 0.0,
                        ["y"] = 0.0,
                        ["width"] = 0.0,
                        ["height"] = 0.0
                    }
                },
                ["m_Slots"] = CreateReferenceArray(new[] { slotObjectId }),
                ["synonyms"] = new JsonArray(),
                ["m_Precision"] = 0,
                ["m_PreviewExpanded"] = true,
                ["m_DismissedVersion"] = 0,
                ["m_PreviewMode"] = 0,
                ["m_CustomColors"] = new JsonObject
                {
                    ["m_SerializableColors"] = new JsonArray()
                },
                ["m_SerializedDescriptor"] = definition.Descriptor
            };
        }

        static JsonObject CreateBlockSlotObject(string slotObjectId, ShaderGraphBlockDefinition definition)
        {
            return definition.SlotTypeName switch
            {
                "UnityEditor.ShaderGraph.Vector1MaterialSlot" => CreateFloatBlockSlotObject(slotObjectId, definition),
                "UnityEditor.ShaderGraph.Vector3MaterialSlot" => CreateVector3BlockSlotObject(slotObjectId, definition),
                "UnityEditor.ShaderGraph.ColorRGBMaterialSlot" => CreateColorRgbBlockSlotObject(slotObjectId, definition),
                "UnityEditor.ShaderGraph.NormalMaterialSlot" => CreateVector3BlockSlotObject(slotObjectId, definition),
                "UnityEditor.ShaderGraph.PositionMaterialSlot" => CreateVector3BlockSlotObject(slotObjectId, definition),
                "UnityEditor.ShaderGraph.TangentMaterialSlot" => CreateVector3BlockSlotObject(slotObjectId, definition),
                _ => throw new InvalidOperationException($"Unsupported block slot type '{definition.SlotTypeName}'.")
            };
        }

        static JsonObject CreateFloatBlockSlotObject(string slotObjectId, ShaderGraphBlockDefinition definition)
        {
            return CreateBlockSlotBase(slotObjectId, definition, definition.DefaultFloat);
        }

        static JsonObject CreateVector3BlockSlotObject(string slotObjectId, ShaderGraphBlockDefinition definition)
        {
            var slotObject = CreateBlockSlotBase(
                slotObjectId,
                definition,
                new JsonObject
                {
                    ["x"] = definition.DefaultX,
                    ["y"] = definition.DefaultY,
                    ["z"] = definition.DefaultZ
                });

            if (definition.Space.HasValue)
                slotObject["m_Space"] = definition.Space.Value;

            return slotObject;
        }

        static JsonObject CreateColorRgbBlockSlotObject(string slotObjectId, ShaderGraphBlockDefinition definition)
        {
            var slotObject = CreateVector3BlockSlotObject(slotObjectId, definition);
            slotObject["m_ColorMode"] = definition.ColorMode;
            slotObject["m_DefaultColor"] = new JsonObject
            {
                ["r"] = definition.DefaultX,
                ["g"] = definition.DefaultY,
                ["b"] = definition.DefaultZ,
                ["a"] = definition.DefaultW
            };
            return slotObject;
        }

        static JsonObject CreateBlockSlotBase(string slotObjectId, ShaderGraphBlockDefinition definition, JsonNode? defaultValue)
        {
            return new JsonObject
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = definition.SlotTypeName,
                ["m_ObjectId"] = slotObjectId,
                ["m_Id"] = 0,
                ["m_DisplayName"] = definition.DisplayName,
                ["m_SlotType"] = 0,
                ["m_Hidden"] = false,
                ["m_ShaderOutputName"] = definition.ShaderOutputName,
                ["m_StageCapability"] = definition.StageCapability,
                ["m_Value"] = CloneJsonNode(defaultValue),
                ["m_DefaultValue"] = CloneJsonNode(defaultValue),
                ["m_Labels"] = new JsonArray()
            };
        }

        static JsonNode? CloneJsonNode(JsonNode? source)
            => source == null ? null : JsonNode.Parse(source.ToJsonString());

        static JsonArray CreateReferenceArray(IEnumerable<string> objectIds)
        {
            var array = new JsonArray();
            foreach (var objectId in objectIds)
            {
                array.Add(new JsonObject
                {
                    ["m_Id"] = objectId
                });
            }

            return array;
        }

        static List<ShaderGraphBlockDefinition> CreateAllowlistedBlockDefinitions()
        {
            return new List<ShaderGraphBlockDefinition>
            {
                new()
                {
                    ApiName = "position",
                    Descriptor = "VertexDescription.Position",
                    Context = "vertex",
                    DisplayName = "Position",
                    ShaderOutputName = "Position",
                    SlotTypeName = "UnityEditor.ShaderGraph.PositionMaterialSlot",
                    Space = 0
                },
                new()
                {
                    ApiName = "normal",
                    Descriptor = "VertexDescription.Normal",
                    Context = "vertex",
                    DisplayName = "Normal",
                    ShaderOutputName = "Normal",
                    SlotTypeName = "UnityEditor.ShaderGraph.NormalMaterialSlot",
                    Space = 0
                },
                new()
                {
                    ApiName = "tangent",
                    Descriptor = "VertexDescription.Tangent",
                    Context = "vertex",
                    DisplayName = "Tangent",
                    ShaderOutputName = "Tangent",
                    SlotTypeName = "UnityEditor.ShaderGraph.TangentMaterialSlot",
                    Space = 0
                },
                new()
                {
                    ApiName = "motionVector",
                    Descriptor = "VertexDescription.MotionVector",
                    Context = "vertex",
                    DisplayName = "Motion Vector",
                    ShaderOutputName = "MotionVector",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector3MaterialSlot"
                },
                new()
                {
                    ApiName = "baseColor",
                    Descriptor = "SurfaceDescription.BaseColor",
                    Context = "fragment",
                    DisplayName = "Base Color",
                    ShaderOutputName = "BaseColor",
                    SlotTypeName = "UnityEditor.ShaderGraph.ColorRGBMaterialSlot",
                    DefaultX = 0.5f,
                    DefaultY = 0.5f,
                    DefaultZ = 0.5f
                },
                new()
                {
                    ApiName = "normalTS",
                    Descriptor = "SurfaceDescription.NormalTS",
                    Context = "fragment",
                    DisplayName = "Normal (Tangent Space)",
                    ShaderOutputName = "NormalTS",
                    SlotTypeName = "UnityEditor.ShaderGraph.NormalMaterialSlot",
                    Space = 3,
                    Aliases = new[] { "normal" }
                },
                new()
                {
                    ApiName = "normalOS",
                    Descriptor = "SurfaceDescription.NormalOS",
                    Context = "fragment",
                    DisplayName = "Normal (Object Space)",
                    ShaderOutputName = "NormalOS",
                    SlotTypeName = "UnityEditor.ShaderGraph.NormalMaterialSlot",
                    Space = 0
                },
                new()
                {
                    ApiName = "normalWS",
                    Descriptor = "SurfaceDescription.NormalWS",
                    Context = "fragment",
                    DisplayName = "Normal (World Space)",
                    ShaderOutputName = "NormalWS",
                    SlotTypeName = "UnityEditor.ShaderGraph.NormalMaterialSlot",
                    Space = 1
                },
                new()
                {
                    ApiName = "bentNormal",
                    Descriptor = "SurfaceDescription.BentNormal",
                    Context = "fragment",
                    DisplayName = "Bent Normal",
                    ShaderOutputName = "BentNormal",
                    SlotTypeName = "UnityEditor.ShaderGraph.NormalMaterialSlot",
                    Space = 3
                },
                new()
                {
                    ApiName = "metallic",
                    Descriptor = "SurfaceDescription.Metallic",
                    Context = "fragment",
                    DisplayName = "Metallic",
                    ShaderOutputName = "Metallic",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector1MaterialSlot"
                },
                new()
                {
                    ApiName = "specular",
                    Descriptor = "SurfaceDescription.Specular",
                    Context = "fragment",
                    DisplayName = "Specular Color",
                    ShaderOutputName = "Specular",
                    SlotTypeName = "UnityEditor.ShaderGraph.ColorRGBMaterialSlot",
                    DefaultX = 0.5f,
                    DefaultY = 0.5f,
                    DefaultZ = 0.5f
                },
                new()
                {
                    ApiName = "smoothness",
                    Descriptor = "SurfaceDescription.Smoothness",
                    Context = "fragment",
                    DisplayName = "Smoothness",
                    ShaderOutputName = "Smoothness",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector1MaterialSlot",
                    DefaultFloat = 0.5f
                },
                new()
                {
                    ApiName = "occlusion",
                    Descriptor = "SurfaceDescription.Occlusion",
                    Context = "fragment",
                    DisplayName = "Ambient Occlusion",
                    ShaderOutputName = "Occlusion",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector1MaterialSlot",
                    DefaultFloat = 1f
                },
                new()
                {
                    ApiName = "emission",
                    Descriptor = "SurfaceDescription.Emission",
                    Context = "fragment",
                    DisplayName = "Emission",
                    ShaderOutputName = "Emission",
                    SlotTypeName = "UnityEditor.ShaderGraph.ColorRGBMaterialSlot",
                    ColorMode = 1
                },
                new()
                {
                    ApiName = "alpha",
                    Descriptor = "SurfaceDescription.Alpha",
                    Context = "fragment",
                    DisplayName = "Alpha",
                    ShaderOutputName = "Alpha",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector1MaterialSlot",
                    DefaultFloat = 1f
                },
                new()
                {
                    ApiName = "alphaClipThreshold",
                    Descriptor = "SurfaceDescription.AlphaClipThreshold",
                    Context = "fragment",
                    DisplayName = "Alpha Clip Threshold",
                    ShaderOutputName = "AlphaClipThreshold",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector1MaterialSlot",
                    DefaultFloat = 0.5f,
                    Aliases = new[] { "alphaClip", "clipThreshold" }
                },
                new()
                {
                    ApiName = "coatMask",
                    Descriptor = "SurfaceDescription.CoatMask",
                    Context = "fragment",
                    DisplayName = "Coat Mask",
                    ShaderOutputName = "CoatMask",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector1MaterialSlot"
                },
                new()
                {
                    ApiName = "coatSmoothness",
                    Descriptor = "SurfaceDescription.CoatSmoothness",
                    Context = "fragment",
                    DisplayName = "Coat Smoothness",
                    ShaderOutputName = "CoatSmoothness",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector1MaterialSlot",
                    DefaultFloat = 1f
                },
                new()
                {
                    ApiName = "normalAlpha",
                    Descriptor = "SurfaceDescription.NormalAlpha",
                    Context = "fragment",
                    DisplayName = "Normal Alpha",
                    ShaderOutputName = "NormalAlpha",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector1MaterialSlot",
                    DefaultFloat = 1f
                },
                new()
                {
                    ApiName = "maosAlpha",
                    Descriptor = "SurfaceDescription.MAOSAlpha",
                    Context = "fragment",
                    DisplayName = "MAOS Alpha",
                    ShaderOutputName = "MAOSAlpha",
                    SlotTypeName = "UnityEditor.ShaderGraph.Vector1MaterialSlot",
                    DefaultFloat = 1f
                }
            };
        }
    }
}
