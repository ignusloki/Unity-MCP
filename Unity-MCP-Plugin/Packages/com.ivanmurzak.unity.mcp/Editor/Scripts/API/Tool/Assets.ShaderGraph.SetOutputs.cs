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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderSubGraphSetOutputsToolId = "assets-shadersubgraph-set-outputs";

        const int ParentReimportCap = 50;

        static readonly HashSet<string> SupportedOutputTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Color", "Float", "Vector2", "Vector3", "Vector4", "Boolean"
        };

        static readonly HashSet<string> Phase3OutputTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Texture2D", "Matrix4", "Gradient"
        };

        [AiTool
        (
            AssetsShaderSubGraphSetOutputsToolId,
            Title = "Assets / Sub Graph / Set Outputs"
        )]
        [AiSkillDescription("Set the output port contract of a Sub Graph's SubGraphOutputNode declaratively.")]
        [AiSkillBody("Declare the desired output ports of a '.shadersubgraph' asset. The tool reconciles the live SubGraphOutputNode against the input list:\n\n" +
            "**Reconciliation rules:**\n" +
            "- An output whose `name` already exists as a slot on SubGraphOutputNode and whose `type` matches → the slot is preserved (keeping its slot ID and any incoming edge). Default value is updated if provided.\n" +
            "- An output whose `name` exists but whose `type` differs → the slot is replaced. Any incoming edge to that slot is dropped. This is called out in the per-output result.\n" +
            "- An output that is new → a new slot is added.\n" +
            "- A slot on SubGraphOutputNode whose `name` is not in the input list → removed when `removeMissing` is true (default), kept when false.\n" +
            "- Order in the response matches the order in the input list.\n\n" +
            "After mutation the sub-graph is validated, saved, and re-imported. Every parent '.shadergraph' and '.shadersubgraph' that references this sub-graph is also re-imported so their SubGraphNode ports refresh.\n\n" +
            "## Supported output types (Phase 2)\n\n" +
            "- `Color` — RGBA color (ColorRGBAMaterialSlot). Set default via x/y/z/w (mapped to r/g/b/a).\n" +
            "- `Float` — single float (Vector1MaterialSlot). Set default via `floatValue`.\n" +
            "- `Vector2` — 2D vector (Vector2MaterialSlot). Set default via x/y.\n" +
            "- `Vector3` — 3D vector (Vector3MaterialSlot). Set default via x/y/z.\n" +
            "- `Vector4` — 4D vector (Vector4MaterialSlot). Set default via x/y/z/w.\n" +
            "- `Boolean` — true/false (BooleanMaterialSlot). Set default via `boolValue`.\n\n" +
            "Texture2D, Matrix4, and Gradient are not supported yet — requesting them returns a clear error.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadersubgraph' asset.\n" +
            "- `outputs` — ordered list of `{ name, type, floatValue?, boolValue?, x?, y?, z?, w? }` entries.\n" +
            "- `removeMissing` — when true (default), remove slots not listed in `outputs`.\n\n" +
            "## Example: two-output sub-graph\n\n" +
            "```json\n" +
            "{\n" +
            "  \"outputs\": [\n" +
            "    { \"name\": \"Tint\", \"type\": \"Color\", \"x\": 1, \"y\": 1, \"z\": 1, \"w\": 1 },\n" +
            "    { \"name\": \"Mask\", \"type\": \"Float\", \"floatValue\": 1.0 }\n" +
            "  ],\n" +
            "  \"removeMissing\": true\n" +
            "}\n" +
            "```\n\n" +
            "**Not supported for '.shadergraph' assets.** Use 'assets-shadergraph-set-blocks' for the master block stack.")]
        [Description("Set the output port contract of a Sub Graph's SubGraphOutputNode.")]
        public ShaderGraphSetSubGraphOutputsResultData SetOutputs
        (
            [Description("Reference to the '.shadersubgraph' asset.")]
            AssetObjectRef assetRef,
            [Description("Desired output port contract.")]
            ShaderGraphSetSubGraphOutputsInput input,
            [Description("Include the full read-only Structure block in the returned result. Default: false")]
            bool? includeStructure = false,
            [Description("Include the full post-import Graph block in the returned result. Default: false")]
            bool? includeGraph = false,
            [Description("Include shader compiler messages in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Only meaningful when includeGraph is true. Default: false")]
            bool? includeProperties = false
        )
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (input == null)
                throw new ArgumentNullException(nameof(input));

            return MainThread.Instance.Run(() => SetSubGraphOutputs(
                assetRef,
                input,
                includeStructure: includeStructure ?? false,
                includeGraph: includeGraph ?? false,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false,
                deferImport: false));
        }

        static ShaderGraphSetSubGraphOutputsResultData SetSubGraphOutputs(
            AssetObjectRef assetRef,
            ShaderGraphSetSubGraphOutputsInput input,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties,
            bool deferImport = false)
        {
            var assetPath = ResolveAssetPath(assetRef);

            if (IsShaderGraphAssetPath(assetPath))
                throw new InvalidOperationException(
                    "Sub Graph outputs are only mutable on .shadersubgraph assets. Use assets-shadergraph-set-blocks for the master block stack.");

            if (!IsSubGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var outputs = input.Outputs;
            if (outputs == null || outputs.Count == 0)
                throw new ArgumentException("The 'outputs' list must contain at least one entry.", nameof(input));

            var removeMissing = input.RemoveMissing ?? true;

            ValidateOutputInputs(outputs);

            var document = LoadShaderGraphReflectionDocument(assetPath);
            var bindings = document.Bindings;
            var outputNode = bindings.OutputNodeProperty.GetValue(document.GraphData)
                ?? throw new InvalidOperationException("Sub Graph does not contain a SubGraphOutputNode.");

            var existingSlots = GetOutputNodeInputSlots(bindings, outputNode);

            var outputResults = new List<ShaderGraphSubGraphOutputSlotResult>();
            var changedFields = new List<string>();
            var matchedSlotIds = new HashSet<int>();

            foreach (var desired in outputs)
            {
                var existingSlot = existingSlots.FirstOrDefault(s =>
                    string.Equals(GetSlotRawDisplayName(bindings, s), desired.Name, StringComparison.Ordinal));

                if (existingSlot != null)
                {
                    var slotId = (int)bindings.SlotIdProperty.GetValue(existingSlot)!;
                    var existingSlotTypeName = existingSlot.GetType().FullName!;
                    var desiredSlotTypeName = MapOutputTypeToSlotTypeName(desired.Type!);

                    if (string.Equals(existingSlotTypeName, desiredSlotTypeName, StringComparison.Ordinal))
                    {
                        matchedSlotIds.Add(slotId);
                        var defaultUpdated = TryUpdateSlotDefaultValue(bindings, existingSlot, desired);
                        if (defaultUpdated) changedFields.Add($"output.{desired.Name}.defaultValue");

                        outputResults.Add(new ShaderGraphSubGraphOutputSlotResult
                        {
                            Name = desired.Name,
                            Type = desired.Type,
                            SlotId = slotId,
                            Action = "kept"
                        });
                    }
                    else
                    {
                        var droppedEdges = CountIncomingEdges(document, slotId);
                        InvokeShaderGraphMethod(bindings.RemoveSlotMethod, outputNode, slotId);
                        changedFields.Add($"output.{desired.Name}.replaced");

                        var newSlotId = AddTypedSlot(bindings, outputNode, slotId, desired);
                        matchedSlotIds.Add(newSlotId);

                        outputResults.Add(new ShaderGraphSubGraphOutputSlotResult
                        {
                            Name = desired.Name,
                            Type = desired.Type,
                            SlotId = newSlotId,
                            Action = "replaced",
                            DroppedEdgeCount = droppedEdges
                        });
                    }
                }
                else
                {
                    var nextId = AllocateNextSlotId(bindings, outputNode);
                    var newSlotId = AddTypedSlot(bindings, outputNode, nextId, desired);
                    matchedSlotIds.Add(newSlotId);
                    changedFields.Add($"output.{desired.Name}.added");

                    outputResults.Add(new ShaderGraphSubGraphOutputSlotResult
                    {
                        Name = desired.Name,
                        Type = desired.Type,
                        SlotId = newSlotId,
                        Action = "added"
                    });
                }
            }

            if (removeMissing)
            {
                foreach (var slot in existingSlots)
                {
                    var slotId = (int)bindings.SlotIdProperty.GetValue(slot)!;
                    if (matchedSlotIds.Contains(slotId))
                        continue;

                    var slotName = GetSlotRawDisplayName(bindings, slot);
                    var droppedEdges = CountIncomingEdges(document, slotId);
                    InvokeShaderGraphMethod(bindings.RemoveSlotMethod, outputNode, slotId);
                    changedFields.Add($"output.{slotName}.removed");

                    outputResults.Add(new ShaderGraphSubGraphOutputSlotResult
                    {
                        Name = slotName,
                        Type = bindings.SlotConcreteValueTypeProperty.GetValue(slot)?.ToString(),
                        SlotId = slotId,
                        Action = "removed",
                        DroppedEdgeCount = droppedEdges
                    });
                }
            }

            InvokeShaderGraphMethod(bindings.ValidateGraphMethod, document.GraphData);
            SaveShaderGraphReflectionDocument(document);

            if (deferImport)
            {
                return new ShaderGraphSetSubGraphOutputsResultData
                {
                    Operation = "setOutputs",
                    OutputResults = outputResults,
                    ChangedFields = changedFields
                };
            }

            FinalizeShaderGraphMutation(assetPath);

            var parentResults = ReimportParentGraphs(assetPath);

            var graphRef = new AssetObjectRef(assetPath);
            return new ShaderGraphSetSubGraphOutputsResultData
            {
                Operation = "setOutputs",
                OutputResults = outputResults,
                ChangedFields = changedFields,
                ParentResults = parentResults.Results,
                ParentCapWarning = parentResults.CapWarning,
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

        static void ValidateOutputInputs(List<ShaderGraphSubGraphOutputSlotInput> outputs)
        {
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var output in outputs)
            {
                if (string.IsNullOrWhiteSpace(output.Name))
                    throw new ArgumentException("Each output must have a non-empty 'name'.");

                if (!seenNames.Add(output.Name))
                    throw new ArgumentException($"Duplicate output name '{output.Name}'. Output names must be unique.");

                if (string.IsNullOrWhiteSpace(output.Type))
                    throw new ArgumentException($"Output '{output.Name}' must have a non-empty 'type'.");

                if (Phase3OutputTypes.Contains(output.Type))
                    throw new ArgumentException(
                        $"Output type '{output.Type}' is not supported in Phase 2. " +
                        "Supported types: Color, Float, Vector2, Vector3, Vector4, Boolean. " +
                        "Texture2D, Matrix4, and Gradient support is planned for Phase 3.");

                if (!SupportedOutputTypes.Contains(output.Type))
                    throw new ArgumentException(
                        $"Unknown output type '{output.Type}'. " +
                        "Supported types: Color, Float, Vector2, Vector3, Vector4, Boolean.");
            }
        }

        static string MapOutputTypeToConcreteSlotValueType(string outputType)
        {
            return outputType.ToLowerInvariant() switch
            {
                "color"   => "Vector4",
                "float"   => "Vector1",
                "vector2" => "Vector2",
                "vector3" => "Vector3",
                "vector4" => "Vector4",
                "boolean" => "Boolean",
                _         => throw new ArgumentException($"Unsupported output type '{outputType}'.")
            };
        }

        static string MapOutputTypeToSlotTypeName(string outputType)
        {
            return outputType.ToLowerInvariant() switch
            {
                "color"   => "UnityEditor.ShaderGraph.ColorRGBAMaterialSlot",
                "float"   => "UnityEditor.ShaderGraph.Vector1MaterialSlot",
                "vector2" => "UnityEditor.ShaderGraph.Vector2MaterialSlot",
                "vector3" => "UnityEditor.ShaderGraph.Vector3MaterialSlot",
                "vector4" => "UnityEditor.ShaderGraph.Vector4MaterialSlot",
                "boolean" => "UnityEditor.ShaderGraph.BooleanMaterialSlot",
                _         => throw new ArgumentException($"Unsupported output type '{outputType}'.")
            };
        }

        static List<object> GetOutputNodeInputSlots(ShaderGraphReflectionBindings bindings, object outputNode)
        {
            var slotListType = typeof(List<>).MakeGenericType(bindings.MaterialSlotType);
            var slotList = Activator.CreateInstance(slotListType)!;

            var getInputSlots = outputNode.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(m => m.Name == "GetInputSlots"
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 1)
                .MakeGenericMethod(bindings.MaterialSlotType);

            InvokeShaderGraphMethod(getInputSlots, outputNode, slotList);
            return ((IEnumerable)slotList).Cast<object>().ToList();
        }

        static string GetSlotRawDisplayName(ShaderGraphReflectionBindings bindings, object slot)
        {
            var raw = (string?)bindings.SlotRawDisplayNameField.GetValue(slot);
            return raw ?? string.Empty;
        }

        static int AllocateNextSlotId(ShaderGraphReflectionBindings bindings, object outputNode)
        {
            var slots = GetOutputNodeInputSlots(bindings, outputNode);
            if (slots.Count == 0) return 1;
            return slots.Max(s => (int)bindings.SlotIdProperty.GetValue(s)!) + 1;
        }

        static int AddTypedSlot(
            ShaderGraphReflectionBindings bindings,
            object outputNode,
            int slotId,
            ShaderGraphSubGraphOutputSlotInput desired)
        {
            var slotTypeName = MapOutputTypeToSlotTypeName(desired.Type!);
            var slotType = bindings.ShaderGraphEditorAssembly.GetType(slotTypeName, throwOnError: true)!;
            var inputSlotType = Enum.Parse(bindings.SlotTypeEnum, "Input");
            var allStage = Enum.Parse(bindings.ShaderStageCapabilityEnum, "All");
            var name = desired.Name!;

            object newSlot;
            switch (desired.Type!.ToLowerInvariant())
            {
                case "color":
                    newSlot = Activator.CreateInstance(slotType, new object[]
                    {
                        slotId, name, name, inputSlotType,
                        new Vector4(desired.X ?? 0f, desired.Y ?? 0f, desired.Z ?? 0f, desired.W ?? 1f),
                        allStage, false
                    })!;
                    break;
                case "vector4":
                    newSlot = Activator.CreateInstance(slotType, new object[]
                    {
                        slotId, name, name, inputSlotType,
                        new Vector4(desired.X ?? 0f, desired.Y ?? 0f, desired.Z ?? 0f, desired.W ?? 0f),
                        allStage, "X", "Y", "Z", "W", false
                    })!;
                    break;
                case "vector3":
                    newSlot = Activator.CreateInstance(slotType, new object[]
                    {
                        slotId, name, name, inputSlotType,
                        new Vector3(desired.X ?? 0f, desired.Y ?? 0f, desired.Z ?? 0f),
                        allStage, "X", "Y", "Z", false
                    })!;
                    break;
                case "vector2":
                    newSlot = Activator.CreateInstance(slotType, new object[]
                    {
                        slotId, name, name, inputSlotType,
                        new Vector2(desired.X ?? 0f, desired.Y ?? 0f),
                        allStage, "X", "Y", false, false
                    })!;
                    break;
                case "float":
                    newSlot = Activator.CreateInstance(slotType, new object[]
                    {
                        slotId, name, name, inputSlotType,
                        desired.FloatValue ?? 0f,
                        allStage, "", false, false
                    })!;
                    break;
                case "boolean":
                    newSlot = Activator.CreateInstance(slotType, new object[]
                    {
                        slotId, name, name, inputSlotType,
                        desired.BoolValue ?? false,
                        allStage, false
                    })!;
                    break;
                default:
                    throw new ArgumentException($"Unsupported output type '{desired.Type}'.");
            }

            InvokeShaderGraphMethod(bindings.AddSlotMethod, outputNode, newSlot, false);
            return slotId;
        }

        static bool TryUpdateSlotDefaultValue(
            ShaderGraphReflectionBindings bindings,
            object slot,
            ShaderGraphSubGraphOutputSlotInput desired)
        {
            var slotType = slot.GetType();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var updated = false;

            switch (desired.Type!.ToLowerInvariant())
            {
                case "float":
                    if (desired.FloatValue.HasValue)
                    {
                        var field = slotType.GetField("m_Value", flags);
                        if (field != null) { field.SetValue(slot, desired.FloatValue.Value); updated = true; }
                    }
                    break;
                case "boolean":
                    if (desired.BoolValue.HasValue)
                    {
                        var field = slotType.GetField("m_Value", flags);
                        if (field != null) { field.SetValue(slot, desired.BoolValue.Value); updated = true; }
                    }
                    break;
                case "vector2":
                    if (desired.X.HasValue || desired.Y.HasValue)
                    {
                        var field = slotType.GetField("m_Value", flags);
                        if (field != null)
                        {
                            var cur = (Vector2)(field.GetValue(slot) ?? Vector2.zero);
                            field.SetValue(slot, new Vector2(desired.X ?? cur.x, desired.Y ?? cur.y));
                            updated = true;
                        }
                    }
                    break;
                case "vector3":
                    if (desired.X.HasValue || desired.Y.HasValue || desired.Z.HasValue)
                    {
                        var field = slotType.GetField("m_Value", flags);
                        if (field != null)
                        {
                            var cur = (Vector3)(field.GetValue(slot) ?? Vector3.zero);
                            field.SetValue(slot, new Vector3(desired.X ?? cur.x, desired.Y ?? cur.y, desired.Z ?? cur.z));
                            updated = true;
                        }
                    }
                    break;
                case "color":
                case "vector4":
                    if (desired.X.HasValue || desired.Y.HasValue || desired.Z.HasValue || desired.W.HasValue)
                    {
                        var field = slotType.GetField("m_Value", flags);
                        if (field != null)
                        {
                            var cur = (Vector4)(field.GetValue(slot) ?? Vector4.zero);
                            field.SetValue(slot, new Vector4(
                                desired.X ?? cur.x, desired.Y ?? cur.y,
                                desired.Z ?? cur.z, desired.W ?? cur.w));
                            updated = true;
                        }
                    }
                    break;
            }
            return updated;
        }

        static int CountIncomingEdges(ShaderGraphReflectionDocument document, int slotId)
        {
            var outputNode = document.Bindings.OutputNodeProperty.GetValue(document.GraphData);
            if (outputNode == null) return 0;

            try
            {
                var slotRefType = document.Bindings.ShaderGraphEditorAssembly
                    .GetType("UnityEditor.Graphing.SlotReference");
                if (slotRefType == null) return 0;

                var slotRef = Activator.CreateInstance(slotRefType, new object[] { outputNode, slotId });

                var getEdgesMethod = document.Bindings.GraphDataType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "GetEdges"
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == slotRefType);

                if (getEdgesMethod == null) return 0;

                var edges = getEdgesMethod.Invoke(document.GraphData, new[] { slotRef });
                if (edges is IEnumerable enumerable)
                    return enumerable.Cast<object>().Count();
            }
            catch
            {
                // Edge counting is best-effort for the result
            }
            return 0;
        }

        struct ParentReimportResults
        {
            public List<ShaderGraphParentReimportResult> Results;
            public string? CapWarning;
        }

        static ParentReimportResults ReimportParentGraphs(string subGraphAssetPath)
        {
            var results = new List<ShaderGraphParentReimportResult>();
            string? capWarning = null;

            var allGraphGuids = AssetDatabase.FindAssets("t:Shader t:SubGraphAsset");
            var parentPaths = new List<string>();

            foreach (var guid in allGraphGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsShaderGraphFamilyAssetPath(path))
                    continue;
                if (string.Equals(path, subGraphAssetPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var deps = AssetDatabase.GetDependencies(path, false);
                if (deps.Any(d => string.Equals(d, subGraphAssetPath, StringComparison.OrdinalIgnoreCase)))
                    parentPaths.Add(path);
            }

            if (parentPaths.Count > ParentReimportCap)
            {
                capWarning = $"Sub graph is referenced by {parentPaths.Count} parents. " +
                    $"Only the first {ParentReimportCap} were re-imported.";
                parentPaths = parentPaths.Take(ParentReimportCap).ToList();
            }

            foreach (var parentPath in parentPaths)
            {
                AssetDatabase.ImportAsset(parentPath, ImportAssetOptions.ForceSynchronousImport);
                ReloadOpenShaderGraphWindows(parentPath);

                var parentResult = new ShaderGraphParentReimportResult { AssetPath = parentPath };

                if (IsShaderGraphAssetPath(parentPath))
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(parentPath);
                    parentResult.CompilesOk = shader != null && !ShaderUtil.ShaderHasError(shader);
                }
                else
                {
                    parentResult.CompilesOk = true;
                }

                results.Add(parentResult);
            }

            com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();
            return new ParentReimportResults { Results = results, CapWarning = capWarning };
        }
    }
}
