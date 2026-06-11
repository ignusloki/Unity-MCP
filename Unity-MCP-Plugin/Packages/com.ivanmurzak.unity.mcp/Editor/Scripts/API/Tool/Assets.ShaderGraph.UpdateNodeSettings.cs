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
        public const string AssetsShaderGraphUpdateNodeSettingsToolId = "assets-shadergraph-update-node-settings";

        [AiTool
        (
            AssetsShaderGraphUpdateNodeSettingsToolId,
            Title = "Assets / Shader Graph / Update Node Settings"
        )]
        [AiSkillDescription("Update supported serialized settings on an existing Shader Graph node, then re-import the graph and return the updated node and diagnostics.")]
        [AiSkillBody("Update supported settings on an existing node inside a '.shadergraph' asset.\n\n" +
            "Current Epic 8 support is intentionally typed and allowlisted:\n" +
            "- existing nodes only\n" +
            "- selection by `nodeObjectId`\n" +
            "- `sampleTexture2D`: `textureType`, `normalMapSpace`, `useGlobalMipBias`, `mipSamplingMode`\n" +
            "- `tilingAndOffset`: default `tiling` and `offset` slot values\n" +
            "- `branch`: default `predicate`, `trueValue`, and `falseValue` slot values\n" +
            "- `split`: default `input` slot value\n" +
            "- `combine`: default `r`, `g`, `b`, `a` slot values\n" +
            "- `add`, `subtract`, `divide`: default `a` and `b` slot values\n" +
            "- `lerp`: default `a`, `b`, and `t` slot values\n" +
            "- `oneMinus`: default `input` slot value\n" +
            "- `multiply`: `multiplyType`\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `node` — node selector plus the typed settings payload for the supported node family.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data.\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect node object ids, slot ids, slot display names, and the current supported node settings.")]
        [Description("Update supported serialized settings on an existing Shader Graph node and re-import the graph.")]
        public ShaderGraphNodeMutationResultData UpdateNodeSettings(
            AssetObjectRef assetRef,
            ShaderGraphUpdateNodeSettingsInput node,
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

            return MainThread.Instance.Run(() => UpdateShaderGraphNodeSettings(
                assetRef,
                node,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphNodeMutationResultData UpdateShaderGraphNodeSettings(
            AssetObjectRef assetRef,
            ShaderGraphUpdateNodeSettingsInput node,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var nodeObjectId = node.NodeObjectId?.Trim();
            if (string.IsNullOrEmpty(nodeObjectId))
                throw new ArgumentException("node.nodeObjectId must be provided.", nameof(node));

            var hasSampleTexture2DUpdates = HasSampleTexture2DUpdates(node.SampleTexture2D);
            var hasSerializedNodeUpdates = HasSerializedNodeSettingsUpdates(node);

            if (!hasSampleTexture2DUpdates && !hasSerializedNodeUpdates)
                throw new ArgumentException("At least one supported node settings field must be provided.", nameof(node));

            if (hasSampleTexture2DUpdates && hasSerializedNodeUpdates)
            {
                throw new ArgumentException(
                    "Sample Texture 2D settings updates cannot be combined with other node settings payloads in the same request.",
                    nameof(node));
            }

            return hasSampleTexture2DUpdates
                ? UpdateSampleTexture2DNodeSettings(assetRef, node, includeMessages, includeProperties)
                : UpdateSerializedNodeSettings(assetRef, node, includeMessages, includeProperties);
        }

        static ShaderGraphNodeMutationResultData UpdateSampleTexture2DNodeSettings(
            AssetObjectRef assetRef,
            ShaderGraphUpdateNodeSettingsInput node,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            var nodeObjectId = node.NodeObjectId!.Trim();

            var document = LoadShaderGraphReflectionDocument(assetPath);
            var nodeObject = ResolveShaderGraphNodeObject(document, nodeObjectId);
            var changedFields = new List<string>();

            ApplySampleTexture2DNodeSettings(nodeObject, node.SampleTexture2D!, changedFields);

            if (changedFields.Count == 0)
                throw new InvalidOperationException($"Shader Graph node '{nodeObjectId}' did not change.");

            SaveShaderGraphReflectionDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            return BuildNodeSettingsMutationResult(
                assetPath,
                nodeObjectId,
                changedFields,
                includeMessages,
                includeProperties);
        }

        static ShaderGraphNodeMutationResultData UpdateSerializedNodeSettings(
            AssetObjectRef assetRef,
            ShaderGraphUpdateNodeSettingsInput node,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            var nodeObjectId = node.NodeObjectId!.Trim();

            var document = LoadShaderGraphReflectionDocument(assetPath);
            var nodeObject = ResolveShaderGraphNodeObject(document, nodeObjectId);
            var nodeType = nodeObject.GetType().FullName
                ?? throw new InvalidOperationException($"Shader Graph node '{nodeObjectId}' does not declare a runtime type.");
            var changedFields = new List<string>();

            switch (nodeType)
            {
                case "UnityEditor.ShaderGraph.TilingAndOffsetNode":
                    ApplyTilingAndOffsetNodeSettings(document.Bindings, nodeObject, node.TilingAndOffset, changedFields);
                    break;
                case "UnityEditor.ShaderGraph.BranchNode":
                    ApplyBranchNodeSettings(document.Bindings, nodeObject, node.Branch, changedFields);
                    break;
                case "UnityEditor.ShaderGraph.SplitNode":
                    ApplySplitNodeSettings(document.Bindings, nodeObject, node.Split, changedFields);
                    break;
                case "UnityEditor.ShaderGraph.CombineNode":
                    ApplyCombineNodeSettings(document.Bindings, nodeObject, node.Combine, changedFields);
                    break;
                case "UnityEditor.ShaderGraph.AddNode":
                    ApplyBinaryVectorNodeSettings(document.Bindings, nodeObject, node.Add, "node.add", changedFields);
                    break;
                case "UnityEditor.ShaderGraph.SubtractNode":
                    ApplyBinaryVectorNodeSettings(document.Bindings, nodeObject, node.Subtract, "node.subtract", changedFields);
                    break;
                case "UnityEditor.ShaderGraph.DivideNode":
                    ApplyBinaryVectorNodeSettings(document.Bindings, nodeObject, node.Divide, "node.divide", changedFields);
                    break;
                case "UnityEditor.ShaderGraph.LerpNode":
                    ApplyLerpNodeSettings(document.Bindings, nodeObject, node.Lerp, changedFields);
                    break;
                case "UnityEditor.ShaderGraph.OneMinusNode":
                    ApplyOneMinusNodeSettings(document.Bindings, nodeObject, node.OneMinus, changedFields);
                    break;
                case "UnityEditor.ShaderGraph.MultiplyNode":
                    ApplyMultiplyNodeSettings(nodeObject, node.Multiply, changedFields);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Node '{nodeType}' does not yet support typed node settings updates.");
            }

            if (changedFields.Count == 0)
                throw new InvalidOperationException($"Shader Graph node '{nodeObjectId}' did not change.");

            InvokeShaderGraphMethod(document.Bindings.ValidateGraphMethod, document.GraphData);
            SaveShaderGraphReflectionDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            return BuildNodeSettingsMutationResult(
                assetPath,
                nodeObjectId,
                changedFields,
                includeMessages,
                includeProperties);
        }

        static ShaderGraphNodeMutationResultData BuildNodeSettingsMutationResult(
            string assetPath,
            string nodeObjectId,
            List<string> changedFields,
            bool includeMessages,
            bool includeProperties)
        {
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

        static bool HasAnyNodeSettingsUpdates(ShaderGraphUpdateNodeSettingsInput node)
            => HasSampleTexture2DUpdates(node.SampleTexture2D)
               || HasSerializedNodeSettingsUpdates(node);

        static bool HasSerializedNodeSettingsUpdates(ShaderGraphUpdateNodeSettingsInput node)
            => HasTilingAndOffsetUpdates(node.TilingAndOffset)
               || HasBranchUpdates(node.Branch)
               || HasSplitUpdates(node.Split)
               || HasCombineUpdates(node.Combine)
               || HasBinaryVectorUpdates(node.Add)
               || HasBinaryVectorUpdates(node.Subtract)
               || HasBinaryVectorUpdates(node.Divide)
               || HasLerpUpdates(node.Lerp)
               || HasOneMinusUpdates(node.OneMinus)
               || HasMultiplyUpdates(node.Multiply);

        static bool HasSampleTexture2DUpdates(ShaderGraphSampleTexture2DNodeSettingsUpdateInput? sampleTexture2D)
            => sampleTexture2D != null
               && (!string.IsNullOrWhiteSpace(sampleTexture2D.TextureType)
                   || !string.IsNullOrWhiteSpace(sampleTexture2D.NormalMapSpace)
                   || sampleTexture2D.UseGlobalMipBias.HasValue
                   || !string.IsNullOrWhiteSpace(sampleTexture2D.MipSamplingMode));

        static bool HasTilingAndOffsetUpdates(ShaderGraphTilingAndOffsetNodeSettingsUpdateInput? tilingAndOffset)
            => tilingAndOffset != null
               && (HasVector2Updates(tilingAndOffset.Tiling)
                   || HasVector2Updates(tilingAndOffset.Offset));

        static bool HasBranchUpdates(ShaderGraphBranchNodeSettingsUpdateInput? branch)
            => branch != null
               && (branch.Predicate.HasValue
                   || HasVector4Updates(branch.TrueValue)
                   || HasVector4Updates(branch.FalseValue));

        static bool HasSplitUpdates(ShaderGraphSplitNodeSettingsUpdateInput? split)
            => split != null && HasVector4Updates(split.Input);

        static bool HasCombineUpdates(ShaderGraphCombineNodeSettingsUpdateInput? combine)
            => combine != null
               && (combine.R.HasValue
                   || combine.G.HasValue
                   || combine.B.HasValue
                   || combine.A.HasValue);

        static bool HasBinaryVectorUpdates(ShaderGraphBinaryVectorNodeSettingsUpdateInput? binary)
            => binary != null
               && (HasVector4Updates(binary.A)
                   || HasVector4Updates(binary.B));

        static bool HasLerpUpdates(ShaderGraphLerpNodeSettingsUpdateInput? lerp)
            => lerp != null
               && (HasVector4Updates(lerp.A)
                   || HasVector4Updates(lerp.B)
                   || HasVector4Updates(lerp.T));

        static bool HasOneMinusUpdates(ShaderGraphOneMinusNodeSettingsUpdateInput? oneMinus)
            => oneMinus != null && HasVector4Updates(oneMinus.Input);

        static bool HasMultiplyUpdates(ShaderGraphMultiplyNodeSettingsUpdateInput? multiply)
            => multiply != null && !string.IsNullOrWhiteSpace(multiply.MultiplyType);

        static bool HasVector2Updates(ShaderGraphVector2ValueUpdateInput? value)
            => value != null && (value.X.HasValue || value.Y.HasValue);

        static bool HasVector4Updates(ShaderGraphVector4ValueUpdateInput? value)
            => value != null
               && (value.X.HasValue
                   || value.Y.HasValue
                   || value.Z.HasValue
                   || value.W.HasValue);

        static void ApplySampleTexture2DNodeSettings(
            object nodeObject,
            ShaderGraphSampleTexture2DNodeSettingsUpdateInput sampleTexture2D,
            List<string> changedFields)
        {
            var nodeType = nodeObject.GetType();
            if (!string.Equals(nodeType.FullName, "UnityEditor.ShaderGraph.SampleTexture2DNode", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Node '{nodeType.FullName}' does not support sampleTexture2D settings updates. Expected UnityEditor.ShaderGraph.SampleTexture2DNode.");
            }

            if (!string.IsNullOrWhiteSpace(sampleTexture2D.TextureType))
            {
                SetEnumProperty(
                    nodeObject,
                    nodeType,
                    "textureType",
                    sampleTexture2D.TextureType!,
                    "node.sampleTexture2D.textureType",
                    changedFields);
            }

            if (!string.IsNullOrWhiteSpace(sampleTexture2D.NormalMapSpace))
            {
                SetEnumProperty(
                    nodeObject,
                    nodeType,
                    "normalMapSpace",
                    sampleTexture2D.NormalMapSpace!,
                    "node.sampleTexture2D.normalMapSpace",
                    changedFields);
            }

            if (sampleTexture2D.UseGlobalMipBias.HasValue)
            {
                SetBoolProperty(
                    nodeObject,
                    nodeType,
                    "enableGlobalMipBias",
                    sampleTexture2D.UseGlobalMipBias.Value,
                    "node.sampleTexture2D.useGlobalMipBias",
                    changedFields);
            }

            if (!string.IsNullOrWhiteSpace(sampleTexture2D.MipSamplingMode))
            {
                SetEnumProperty(
                    nodeObject,
                    nodeType,
                    "mipSamplingMode",
                    sampleTexture2D.MipSamplingMode!,
                    "node.sampleTexture2D.mipSamplingMode",
                    changedFields);
            }
        }

        static void ApplyTilingAndOffsetNodeSettings(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            ShaderGraphTilingAndOffsetNodeSettingsUpdateInput? tilingAndOffset,
            List<string> changedFields)
        {
            if (tilingAndOffset == null)
                throw new InvalidOperationException("Tiling And Offset nodes require a `tilingAndOffset` settings payload.");

            SetSlotVector2(bindings, nodeObject, "Tiling", tilingAndOffset.Tiling, "node.tilingAndOffset.tiling", changedFields);
            SetSlotVector2(bindings, nodeObject, "Offset", tilingAndOffset.Offset, "node.tilingAndOffset.offset", changedFields);
        }

        static void ApplyBranchNodeSettings(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            ShaderGraphBranchNodeSettingsUpdateInput? branch,
            List<string> changedFields)
        {
            if (branch == null)
                throw new InvalidOperationException("Branch nodes require a `branch` settings payload.");

            SetSlotBool(bindings, nodeObject, "Predicate", branch.Predicate, "node.branch.predicate", changedFields);
            SetSlotVector4(bindings, nodeObject, "True", branch.TrueValue, "node.branch.trueValue", changedFields);
            SetSlotVector4(bindings, nodeObject, "False", branch.FalseValue, "node.branch.falseValue", changedFields);
        }

        static void ApplySplitNodeSettings(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            ShaderGraphSplitNodeSettingsUpdateInput? split,
            List<string> changedFields)
        {
            if (split == null)
                throw new InvalidOperationException("Split nodes require a `split` settings payload.");

            SetSlotVector4(bindings, nodeObject, "In", split.Input, "node.split.input", changedFields);
        }

        static void ApplyCombineNodeSettings(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            ShaderGraphCombineNodeSettingsUpdateInput? combine,
            List<string> changedFields)
        {
            if (combine == null)
                throw new InvalidOperationException("Combine nodes require a `combine` settings payload.");

            SetSlotFloat(bindings, nodeObject, "R", combine.R, "node.combine.r", changedFields);
            SetSlotFloat(bindings, nodeObject, "G", combine.G, "node.combine.g", changedFields);
            SetSlotFloat(bindings, nodeObject, "B", combine.B, "node.combine.b", changedFields);
            SetSlotFloat(bindings, nodeObject, "A", combine.A, "node.combine.a", changedFields);
        }

        static void ApplyBinaryVectorNodeSettings(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            ShaderGraphBinaryVectorNodeSettingsUpdateInput? binary,
            string changedFieldPrefix,
            List<string> changedFields)
        {
            if (binary == null)
                throw new InvalidOperationException($"This node requires a `{changedFieldPrefix[(changedFieldPrefix.LastIndexOf('.') + 1)..]}` settings payload.");

            SetSlotVector4(bindings, nodeObject, "A", binary.A, $"{changedFieldPrefix}.a", changedFields);
            SetSlotVector4(bindings, nodeObject, "B", binary.B, $"{changedFieldPrefix}.b", changedFields);
        }

        static void ApplyLerpNodeSettings(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            ShaderGraphLerpNodeSettingsUpdateInput? lerp,
            List<string> changedFields)
        {
            if (lerp == null)
                throw new InvalidOperationException("Lerp nodes require a `lerp` settings payload.");

            SetSlotVector4(bindings, nodeObject, "A", lerp.A, "node.lerp.a", changedFields);
            SetSlotVector4(bindings, nodeObject, "B", lerp.B, "node.lerp.b", changedFields);
            SetSlotVector4(bindings, nodeObject, "T", lerp.T, "node.lerp.t", changedFields);
        }

        static void ApplyOneMinusNodeSettings(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            ShaderGraphOneMinusNodeSettingsUpdateInput? oneMinus,
            List<string> changedFields)
        {
            if (oneMinus == null)
                throw new InvalidOperationException("One Minus nodes require a `oneMinus` settings payload.");

            SetSlotVector4(bindings, nodeObject, "In", oneMinus.Input, "node.oneMinus.input", changedFields);
        }

        static void ApplyMultiplyNodeSettings(
            object nodeObject,
            ShaderGraphMultiplyNodeSettingsUpdateInput? multiply,
            List<string> changedFields)
        {
            if (multiply == null)
                throw new InvalidOperationException("Multiply nodes require a `multiply` settings payload.");

            if (!string.IsNullOrWhiteSpace(multiply.MultiplyType))
            {
                var multiplyType = ParseMultiplyType(multiply.MultiplyType!);
                SetIntOrEnumField(nodeObject, "m_MultiplyType", multiplyType, "node.multiply.multiplyType", changedFields);
            }
        }

        static object ResolveRuntimeSlotObject(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            string slotDisplayName)
        {
            var materialSlotType = bindings.ShaderGraphEditorAssembly.GetType("UnityEditor.ShaderGraph.MaterialSlot", throwOnError: true)
                ?? throw new InvalidOperationException("Shader Graph MaterialSlot type could not be resolved.");
            var slotListType = typeof(List<>).MakeGenericType(materialSlotType);
            var slotList = Activator.CreateInstance(slotListType)
                ?? throw new InvalidOperationException("Failed to create a temporary Shader Graph slot list.");

            var getInputSlotsMethod = nodeObject.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == "GetInputSlots"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 1)
                ?.MakeGenericMethod(materialSlotType);

            if (getInputSlotsMethod == null)
            {
                throw new InvalidOperationException(
                    $"Input slots could not be inspected on node '{nodeObject.GetType().FullName ?? "unknown"}'.");
            }

            InvokeShaderGraphMethod(getInputSlotsMethod, nodeObject, slotList);

            var slotObject = ((IEnumerable)slotList).Cast<object>()
                .FirstOrDefault(slot => string.Equals(GetRuntimeSlotDisplayName(slot), slotDisplayName, StringComparison.Ordinal));

            return slotObject
                ?? throw new InvalidOperationException(
                    $"Input slot '{slotDisplayName}' could not be resolved on node '{nodeObject.GetType().FullName ?? "unknown"}'.");
        }

        static void SetSlotVector2(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            string slotDisplayName,
            ShaderGraphVector2ValueUpdateInput? value,
            string changedFieldPrefix,
            List<string> changedFields)
        {
            if (!HasVector2Updates(value))
                return;

            var slotObject = ResolveRuntimeSlotObject(bindings, nodeObject, slotDisplayName);
            var currentValue = TryGetFieldValue(slotObject, "m_Value", out Vector2 existingValue)
                ? existingValue
                : (TryGetFieldValue(slotObject, "m_DefaultValue", out Vector2 existingDefaultValue)
                    ? existingDefaultValue
                    : default);

            var updatedValue = new Vector2(
                value!.X ?? currentValue.x,
                value.Y ?? currentValue.y);

            var changed = !Approximately(currentValue, updatedValue)
                || !TryGetFieldValue(slotObject, "m_DefaultValue", out Vector2 currentDefaultValue)
                || !Approximately(currentDefaultValue, updatedValue);

            if (changed)
            {
                var template = CreateVector2SlotTemplate(slotObject, updatedValue);
                CopySlotValuesFrom(slotObject, template);
                CopySlotDefaultValue(slotObject, template);
            }

            if (changed)
            {
                if (value.X.HasValue)
                    AddChangedField(changedFields, $"{changedFieldPrefix}.x");
                if (value.Y.HasValue)
                    AddChangedField(changedFields, $"{changedFieldPrefix}.y");
            }
        }

        static void SetSlotVector4(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            string slotDisplayName,
            ShaderGraphVector4ValueUpdateInput? value,
            string changedFieldPrefix,
            List<string> changedFields)
        {
            if (!HasVector4Updates(value))
                return;

            var slotObject = ResolveRuntimeSlotObject(bindings, nodeObject, slotDisplayName);
            var currentValue = TryGetFieldValue(slotObject, "m_Value", out Vector4 existingValue)
                ? existingValue
                : (TryGetFieldValue(slotObject, "m_DefaultValue", out Vector4 existingDefaultValue)
                    ? existingDefaultValue
                    : default);

            var updatedValue = new Vector4(
                value!.X ?? currentValue.x,
                value.Y ?? currentValue.y,
                value.Z ?? currentValue.z,
                value.W ?? currentValue.w);

            var changed = !Approximately(currentValue, updatedValue)
                || !TryGetFieldValue(slotObject, "m_DefaultValue", out Vector4 currentDefaultValue)
                || !Approximately(currentDefaultValue, updatedValue);

            if (changed)
            {
                var template = CreateVector4SlotTemplate(slotObject, updatedValue, literalMode: true);
                CopySlotValuesFrom(slotObject, template);
                CopySlotDefaultValue(slotObject, template);
            }

            changed |= SetOptionalBoolField(slotObject, "m_LiteralMode", true)
                | SetOptionalEnumField(slotObject, "m_ConcreteValueType", "Vector4");

            if (changed)
            {
                if (value.X.HasValue)
                    AddChangedField(changedFields, $"{changedFieldPrefix}.x");
                if (value.Y.HasValue)
                    AddChangedField(changedFields, $"{changedFieldPrefix}.y");
                if (value.Z.HasValue)
                    AddChangedField(changedFields, $"{changedFieldPrefix}.z");
                if (value.W.HasValue)
                    AddChangedField(changedFields, $"{changedFieldPrefix}.w");
            }
        }

        static void SetSlotFloat(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            string slotDisplayName,
            float? value,
            string changedFieldName,
            List<string> changedFields)
        {
            if (!value.HasValue)
                return;

            var slotObject = ResolveRuntimeSlotObject(bindings, nodeObject, slotDisplayName);
            var changed = !TryGetFieldValue(slotObject, "m_Value", out float currentValue)
                || Math.Abs(currentValue - value.Value) > 0.0001f
                || !TryGetFieldValue(slotObject, "m_DefaultValue", out float currentDefaultValue)
                || Math.Abs(currentDefaultValue - value.Value) > 0.0001f;

            if (changed)
            {
                var template = CreateFloatSlotTemplate(slotObject, value.Value, literalMode: true);
                CopySlotValuesFrom(slotObject, template);
                CopySlotDefaultValue(slotObject, template);
            }

            changed |= SetOptionalBoolField(slotObject, "m_LiteralMode", true);

            if (changed)
                AddChangedField(changedFields, changedFieldName);
        }

        static void SetSlotBool(
            ShaderGraphReflectionBindings bindings,
            object nodeObject,
            string slotDisplayName,
            bool? value,
            string changedFieldName,
            List<string> changedFields)
        {
            if (!value.HasValue)
                return;

            var slotObject = ResolveRuntimeSlotObject(bindings, nodeObject, slotDisplayName);
            var changed = !TryGetFieldValue(slotObject, "m_Value", out bool currentValue)
                || currentValue != value.Value
                || !TryGetFieldValue(slotObject, "m_DefaultValue", out bool currentDefaultValue)
                || currentDefaultValue != value.Value;

            if (changed)
            {
                var template = CreateBoolSlotTemplate(slotObject, value.Value);
                CopySlotValuesFrom(slotObject, template);
                CopySlotDefaultValue(slotObject, template);
            }

            if (changed)
                AddChangedField(changedFields, changedFieldName);
        }

        static string GetRuntimeSlotDisplayName(object slotObject)
        {
            if (TryGetFieldValue(slotObject, "m_DisplayName", out string displayName) && !string.IsNullOrEmpty(displayName))
                return NormalizeRuntimeSlotDisplayName(displayName);

            var property = slotObject.GetType().GetProperty(
                "displayName",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var runtimeDisplayName = property?.GetValue(slotObject) as string;
            if (!string.IsNullOrEmpty(runtimeDisplayName))
                return NormalizeRuntimeSlotDisplayName(runtimeDisplayName!);

            throw new InvalidOperationException(
                $"Slot '{slotObject.GetType().FullName ?? "unknown"}' does not expose a display name.");
        }

        static void AddChangedField(List<string> changedFields, string changedFieldName)
        {
            if (!changedFields.Contains(changedFieldName, StringComparer.Ordinal))
                changedFields.Add(changedFieldName);
        }

        static bool TryGetFieldValue<T>(object target, string fieldName, out T value)
        {
            var field = TryResolveNodeInstanceField(target.GetType(), fieldName);

            if (field?.GetValue(target) is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default!;
            return false;
        }

        static object CreateVector2SlotTemplate(object slotObject, Vector2 value)
        {
            var slotType = slotObject.GetType();
            var constructor = slotType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(ctor => ctor.GetParameters().Length == 10);
            var labels = GetSlotLabels(slotObject);
            var template = constructor.Invoke(new[]
            {
                (object)ReadRequiredFieldValue<int>(slotObject, "m_Id"),
                GetRuntimeSlotDisplayName(slotObject),
                ReadRequiredFieldValue<string>(slotObject, "m_ShaderOutputName"),
                ReadRequiredFieldObject(slotObject, "m_SlotType"),
                value,
                ReadRequiredFieldObject(slotObject, "m_StageCapability"),
                labels.Length > 0 ? labels[0] : string.Empty,
                labels.Length > 1 ? labels[1] : string.Empty,
                ReadRequiredFieldValue<bool>(slotObject, "m_Hidden"),
                TryGetFieldValue(slotObject, "m_Integer", out bool integerMode) && integerMode
            });

            return template;
        }

        static object CreateVector4SlotTemplate(object slotObject, Vector4 value, bool literalMode)
        {
            var slotType = slotObject.GetType();
            var constructor = slotType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(ctor => ctor.GetParameters().Length == 8);
            var template = constructor.Invoke(new[]
            {
                (object)ReadRequiredFieldValue<int>(slotObject, "m_Id"),
                GetRuntimeSlotDisplayName(slotObject),
                ReadRequiredFieldValue<string>(slotObject, "m_ShaderOutputName"),
                ReadRequiredFieldObject(slotObject, "m_SlotType"),
                value,
                ReadRequiredFieldObject(slotObject, "m_StageCapability"),
                ReadRequiredFieldValue<bool>(slotObject, "m_Hidden"),
                literalMode
            });

            SetVector4Field(template, "m_DefaultValue", value);
            SetOptionalEnumField(template, "m_ConcreteValueType", "Vector4");
            return template;
        }

        static object CreateFloatSlotTemplate(object slotObject, float value, bool literalMode)
        {
            var slotType = slotObject.GetType();
            var constructor = slotType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(ctor => ctor.GetParameters().Length == 9);
            var labels = GetSlotLabels(slotObject);

            return constructor.Invoke(new[]
            {
                (object)ReadRequiredFieldValue<int>(slotObject, "m_Id"),
                GetRuntimeSlotDisplayName(slotObject),
                ReadRequiredFieldValue<string>(slotObject, "m_ShaderOutputName"),
                ReadRequiredFieldObject(slotObject, "m_SlotType"),
                value,
                ReadRequiredFieldObject(slotObject, "m_StageCapability"),
                labels.Length > 0 ? labels[0] : string.Empty,
                ReadRequiredFieldValue<bool>(slotObject, "m_Hidden"),
                literalMode
            });
        }

        static object CreateBoolSlotTemplate(object slotObject, bool value)
        {
            var slotType = slotObject.GetType();
            var constructor = slotType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(ctor => ctor.GetParameters().Length == 7);

            return constructor.Invoke(new[]
            {
                (object)ReadRequiredFieldValue<int>(slotObject, "m_Id"),
                GetRuntimeSlotDisplayName(slotObject),
                ReadRequiredFieldValue<string>(slotObject, "m_ShaderOutputName"),
                ReadRequiredFieldObject(slotObject, "m_SlotType"),
                value,
                ReadRequiredFieldObject(slotObject, "m_StageCapability"),
                ReadRequiredFieldValue<bool>(slotObject, "m_Hidden")
            });
        }

        static string[] GetSlotLabels(object slotObject)
        {
            var field = TryResolveNodeInstanceField(slotObject.GetType(), "m_Labels");
            if (field?.GetValue(slotObject) is string[] labels)
                return labels;

            if (field?.GetValue(slotObject) is IEnumerable enumerable)
            {
                return enumerable.Cast<object?>()
                    .Select(value => value?.ToString() ?? string.Empty)
                    .ToArray();
            }

            return Array.Empty<string>();
        }

        static T ReadRequiredFieldValue<T>(object target, string fieldName)
        {
            if (TryGetFieldValue(target, fieldName, out T value))
                return value;

            throw new InvalidOperationException(
                $"Field '{target.GetType().FullName}.{fieldName}' could not be resolved as '{typeof(T).FullName}'.");
        }

        static object ReadRequiredFieldObject(object target, string fieldName)
        {
            var field = ResolveNodeInstanceField(target.GetType(), fieldName);
            return field.GetValue(target)
                ?? throw new InvalidOperationException(
                    $"Field '{target.GetType().FullName}.{fieldName}' was unexpectedly null.");
        }

        static void CopySlotValuesFrom(object slotObject, object template)
        {
            var method = slotObject.GetType().GetMethod(
                "CopyValuesFrom",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null)
                throw new InvalidOperationException($"Slot '{slotObject.GetType().FullName}' does not expose CopyValuesFrom.");

            InvokeShaderGraphMethod(method, slotObject, template);
        }

        static void CopySlotDefaultValue(object slotObject, object template)
        {
            var method = slotObject.GetType().GetMethod(
                "CopyDefaultValue",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null)
                throw new InvalidOperationException($"Slot '{slotObject.GetType().FullName}' does not expose CopyDefaultValue.");

            InvokeShaderGraphMethod(method, slotObject, template);
        }

        static bool SetVector2Field(object target, string fieldName, Vector2 value)
        {
            var field = ResolveNodeInstanceField(target.GetType(), fieldName);
            var currentValue = field.GetValue(target) is Vector2 vector
                ? vector
                : default;

            if (Approximately(currentValue, value))
                return false;

            field.SetValue(target, value);
            return true;
        }

        static bool SetVector4Field(object target, string fieldName, Vector4 value)
        {
            var field = ResolveNodeInstanceField(target.GetType(), fieldName);
            var currentValue = field.GetValue(target) is Vector4 vector
                ? vector
                : default;

            if (Approximately(currentValue, value))
                return false;

            field.SetValue(target, value);
            return true;
        }

        static bool SetFloatField(object target, string fieldName, float value)
        {
            var field = ResolveNodeInstanceField(target.GetType(), fieldName);
            var currentValue = field.GetValue(target) is float floatValue
                ? floatValue
                : default;

            if (Math.Abs(currentValue - value) <= 0.0001f)
                return false;

            field.SetValue(target, value);
            return true;
        }

        static bool SetBoolField(object target, string fieldName, bool value)
        {
            var field = ResolveNodeInstanceField(target.GetType(), fieldName);
            var currentValue = field.GetValue(target) is bool boolValue
                ? boolValue
                : default;

            if (currentValue == value)
                return false;

            field.SetValue(target, value);
            return true;
        }

        static bool SetOptionalBoolField(object target, string fieldName, bool value)
        {
            var field = TryResolveNodeInstanceField(target.GetType(), fieldName);
            if (field == null)
                return false;

            var currentValue = field.GetValue(target) is bool boolValue
                ? boolValue
                : default;
            if (currentValue == value)
                return false;

            field.SetValue(target, value);
            return true;
        }

        static bool SetOptionalEnumField(object target, string fieldName, string value)
        {
            var field = TryResolveNodeInstanceField(target.GetType(), fieldName);
            if (field == null)
                return false;
            if (!field.FieldType.IsEnum)
                throw new InvalidOperationException($"Field '{target.GetType().FullName}.{fieldName}' is not an enum.");

            var parsedValue = ParseNodeEnumValue(field.FieldType, value, $"{target.GetType().FullName}.{fieldName}");
            if (Equals(field.GetValue(target), parsedValue))
                return false;

            field.SetValue(target, parsedValue);
            return true;
        }

        static void SetIntOrEnumField(
            object target,
            string fieldName,
            int value,
            string changedFieldName,
            List<string> changedFields)
        {
            var field = ResolveNodeInstanceField(target.GetType(), fieldName);
            object fieldValue;

            if (field.FieldType.IsEnum)
                fieldValue = Enum.ToObject(field.FieldType, value);
            else if (field.FieldType == typeof(int))
                fieldValue = value;
            else
                throw new InvalidOperationException($"Field '{target.GetType().FullName}.{fieldName}' is not an int or enum.");

            if (Equals(field.GetValue(target), fieldValue))
                return;

            field.SetValue(target, fieldValue);
            AddChangedField(changedFields, changedFieldName);
        }

        static void SetEnumProperty(
            object target,
            Type targetType,
            string propertyName,
            string value,
            string changedFieldName,
            List<string> changedFields)
        {
            var property = ResolveNodeInstanceProperty(targetType, propertyName);
            var enumType = property.PropertyType;
            if (!enumType.IsEnum)
                throw new InvalidOperationException($"Property '{targetType.FullName}.{propertyName}' is not an enum.");

            var parsedValue = ParseNodeEnumValue(enumType, value, changedFieldName);
            var currentValue = property.GetValue(target);
            if (Equals(currentValue, parsedValue))
                return;

            property.SetValue(target, parsedValue);
            AddChangedField(changedFields, changedFieldName);
        }

        static void SetBoolProperty(
            object target,
            Type targetType,
            string propertyName,
            bool value,
            string changedFieldName,
            List<string> changedFields)
        {
            var property = ResolveNodeInstanceProperty(targetType, propertyName);
            if (property.PropertyType != typeof(bool))
                throw new InvalidOperationException($"Property '{targetType.FullName}.{propertyName}' is not a bool.");

            var currentValue = property.GetValue(target) is bool boolValue
                ? boolValue
                : (bool?)null;
            if (currentValue == value)
                return;

            property.SetValue(target, value);
            AddChangedField(changedFields, changedFieldName);
        }

        static PropertyInfo ResolveNodeInstanceProperty(Type targetType, string propertyName)
        {
            var property = targetType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return property
                ?? throw new InvalidOperationException(
                    $"Property '{targetType.FullName}.{propertyName}' could not be resolved.");
        }

        static FieldInfo ResolveNodeInstanceField(Type targetType, string fieldName)
            => TryResolveNodeInstanceField(targetType, fieldName)
                ?? throw new InvalidOperationException(
                    $"Field '{targetType.FullName}.{fieldName}' could not be resolved.");

        static FieldInfo? TryResolveNodeInstanceField(Type targetType, string fieldName)
        {
            for (var type = targetType; type != null; type = type.BaseType)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
            }

            return null;
        }

        static int ParseMultiplyType(string value)
        {
            return NormalizeEnumValue(value) switch
            {
                "vector" => 0,
                "matrix" => 1,
                "mixed" => 2,
                _ => throw new ArgumentException(
                    $"Unsupported multiplyType '{value}'. Supported values: vector, matrix, mixed.")
            };
        }

        static string NormalizeRuntimeSlotDisplayName(string value)
        {
            var suffixIndex = value.IndexOf('(');
            return suffixIndex > 0
                ? value[..suffixIndex]
                : value;
        }

        static bool Approximately(Vector2 left, Vector2 right)
            => Math.Abs(left.x - right.x) <= 0.0001f
               && Math.Abs(left.y - right.y) <= 0.0001f;

        static bool Approximately(Vector4 left, Vector4 right)
            => Math.Abs(left.x - right.x) <= 0.0001f
               && Math.Abs(left.y - right.y) <= 0.0001f
               && Math.Abs(left.z - right.z) <= 0.0001f
               && Math.Abs(left.w - right.w) <= 0.0001f;

        static object ParseNodeEnumValue(Type enumType, string value, string fieldPath)
        {
            var normalizedValue = NormalizeEnumValue(value);
            foreach (var enumName in Enum.GetNames(enumType))
            {
                if (string.Equals(NormalizeEnumValue(enumName), normalizedValue, StringComparison.Ordinal))
                    return Enum.Parse(enumType, enumName, ignoreCase: false);
            }

            var supportedValues = string.Join(", ", Enum.GetNames(enumType)
                .Select(enumName => NormalizeEnumValue(enumName))
                .OrderBy(enumName => enumName, StringComparer.Ordinal));

            throw new ArgumentException(
                $"Unsupported value '{value}' for {fieldPath}. Supported values: {supportedValues}.");
        }
    }
}
