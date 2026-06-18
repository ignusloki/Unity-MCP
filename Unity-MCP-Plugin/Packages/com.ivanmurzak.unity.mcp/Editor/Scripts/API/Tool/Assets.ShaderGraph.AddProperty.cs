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
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphAddPropertyToolId = "assets-shadergraph-add-property";

        [AiTool
        (
            AssetsShaderGraphAddPropertyToolId,
            Title = "Assets / Shader Graph / Add Property"
        )]
        [AiSkillDescription("Add a new Shader Graph blackboard property, optionally place it in a category, then re-import the graph and return the created property and diagnostics.")]
        [AiSkillBody("Add a new Shader Graph blackboard property to a '.shadergraph' asset.\n\n" +
            "Current support is intentionally scoped to common URP Blackboard property types:\n" +
            "- `propertyType = color`\n" +
            "- `propertyType = float`\n" +
            "- `propertyType = texture2D`\n" +
            "- `propertyType = vector2`, `vector3`, or `vector4`\n" +
            "- `propertyType = boolean`\n\n" +
            "## Response shape\n\n" +
            "By default returns a slim diff: `Operation`, `PropertyObjectId`, `PropertyReferenceName`, `PropertyKind`, `Property`, `ChangedFields`, and `GraphSummary`. " +
            "Set `includeStructure: true` to also receive the full read-only `Structure` block, `includeGraph: true` for the full post-import `Graph` block.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `property` — creation payload for the new property.\n" +
            "- `includeStructure` — include the full read-only Structure block in the response. Default: false.\n" +
            "- `includeGraph` — include the full post-import Graph block in the response. Default: false.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data (only meaningful when includeGraph is true).\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data (only meaningful when includeGraph is true).\n\n" +
            "## Behavior\n\n" +
            "Creates a new blackboard property, updates the serialized property lists and category placement, re-imports the graph, and returns the created property snapshot plus post-import diagnostics.")]
        [Description("Add a new Shader Graph blackboard property and re-import the graph.")]
        public ShaderGraphPropertyMutationResultData AddProperty(
            AssetObjectRef assetRef,
            ShaderGraphAddPropertyInput property,
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

            if (property == null)
                throw new ArgumentNullException(nameof(property));

            return MainThread.Instance.Run(() => AddShaderGraphProperty(
                assetRef,
                property,
                includeStructure: includeStructure ?? false,
                includeGraph: includeGraph ?? false,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphPropertyMutationResultData AddShaderGraphProperty(
            AssetObjectRef assetRef,
            ShaderGraphAddPropertyInput property,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            if (string.IsNullOrWhiteSpace(property.PropertyType))
                throw new ArgumentException("property.propertyType must be provided.", nameof(property));

            if (string.IsNullOrWhiteSpace(property.DisplayName))
                throw new ArgumentException("property.displayName must be provided.", nameof(property));

            var document = LoadMutableDocument(assetPath);
            var displayName = property.DisplayName!.Trim();
            var propertyType = NormalizeEnumValue(property.PropertyType!);
            var propertyIds = GetIdArray(document.Root, "m_Properties");
            var propertyObjects = propertyIds
                .Where(document.ObjectsById.ContainsKey)
                .Select(id => document.ObjectsById[id])
                .ToList();

            ValidateUniqueDisplayName(null, propertyObjects, displayName);

            var overrideReferenceName = string.IsNullOrWhiteSpace(property.OverrideReferenceName)
                ? string.Empty
                : property.OverrideReferenceName!.Trim();

            var defaultReferenceName = GenerateDefaultReferenceName(displayName);
            var effectiveReferenceName = string.IsNullOrEmpty(overrideReferenceName)
                ? defaultReferenceName
                : overrideReferenceName;

            ValidateReferenceNameSyntax(effectiveReferenceName);
            ValidateUniqueReferenceName(null, propertyObjects, effectiveReferenceName);

            var propertyObjectId = Guid.NewGuid().ToString("N");
            var propertyGuid = Guid.NewGuid().ToString();
            var propertyObject = CreatePropertyObject(
                propertyType,
                propertyObjectId,
                propertyGuid,
                displayName,
                defaultReferenceName,
                overrideReferenceName,
                property);

            document.Objects.Add(propertyObject);
            document.ObjectsById[propertyObjectId] = propertyObject;

            AddPropertyReferenceToRoot(document.Root, propertyObjectId);
            AddPropertyReferenceToCategory(
                document,
                propertyObjectId,
                property.CategoryObjectId,
                property.CategoryName,
                property.CreateCategoryIfMissing ?? false,
                property.CategoryIndex);

            WriteMutableDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var createdProperty = structure.Properties?
                .FirstOrDefault(p => string.Equals(p.ObjectId, propertyObjectId, StringComparison.Ordinal));

            return new ShaderGraphPropertyMutationResultData
            {
                Operation = "add",
                PropertyObjectId = createdProperty?.ObjectId ?? propertyObjectId,
                PropertyReferenceName = createdProperty?.EffectiveReferenceName ?? effectiveReferenceName,
                PropertyKind = createdProperty?.PropertyKind,
                ChangedFields = new List<string> { "property.added" },
                Property = createdProperty,
                GraphSummary = BuildShaderGraphSummary(graphRef),
                Structure = includeStructure ? structure : null,
                Graph = includeGraph
                    ? BuildShaderGraphData(
                        graphRef,
                        includeMessages: includeMessages,
                        includeProperties: includeProperties,
                        includeDiagnostics: true)
                    : null
            };
        }

        static JsonObject CreatePropertyObject(
            string propertyType,
            string propertyObjectId,
            string propertyGuid,
            string displayName,
            string defaultReferenceName,
            string overrideReferenceName,
            ShaderGraphAddPropertyInput property)
        {
            return propertyType switch
            {
                "color" => CreateColorPropertyObject(
                    propertyObjectId,
                    propertyGuid,
                    displayName,
                    defaultReferenceName,
                    overrideReferenceName,
                    property),
                "float" => CreateFloatPropertyObject(
                    propertyObjectId,
                    propertyGuid,
                    displayName,
                    defaultReferenceName,
                    overrideReferenceName,
                    property),
                "texture2d" => CreateTexture2DPropertyObject(
                    propertyObjectId,
                    propertyGuid,
                    displayName,
                    defaultReferenceName,
                    overrideReferenceName,
                    property),
                "vector2" => CreateVectorPropertyObject(
                    propertyType,
                    propertyObjectId,
                    propertyGuid,
                    displayName,
                    defaultReferenceName,
                    overrideReferenceName,
                    property),
                "vector3" => CreateVectorPropertyObject(
                    propertyType,
                    propertyObjectId,
                    propertyGuid,
                    displayName,
                    defaultReferenceName,
                    overrideReferenceName,
                    property),
                "vector4" => CreateVectorPropertyObject(
                    propertyType,
                    propertyObjectId,
                    propertyGuid,
                    displayName,
                    defaultReferenceName,
                    overrideReferenceName,
                    property),
                "boolean" => CreateBooleanPropertyObject(
                    propertyObjectId,
                    propertyGuid,
                    displayName,
                    defaultReferenceName,
                    overrideReferenceName,
                    property),
                _ => throw new ArgumentException(
                    $"Unsupported propertyType '{property.PropertyType}'. Supported values: color, float, texture2D, vector2, vector3, vector4, boolean.")
            };
        }

        static JsonObject CreateColorPropertyObject(
            string propertyObjectId,
            string propertyGuid,
            string displayName,
            string defaultReferenceName,
            string overrideReferenceName,
            ShaderGraphAddPropertyInput property)
        {
            var colorHex = string.IsNullOrWhiteSpace(property.ColorHex)
                ? "#FFFFFFFF"
                : property.ColorHex!.Trim();

            if (!ColorUtility.TryParseHtmlString(colorHex, out var color))
                throw new ArgumentException($"Invalid colorHex '{property.ColorHex}'. Expected formats like '#RRGGBB' or '#RRGGBBAA'.");

            return new JsonObject
            {
                ["m_SGVersion"] = 3,
                ["m_Type"] = "UnityEditor.ShaderGraph.Internal.ColorShaderProperty",
                ["m_ObjectId"] = propertyObjectId,
                ["m_Guid"] = new JsonObject
                {
                    ["m_GuidSerialized"] = propertyGuid
                },
                ["m_Name"] = displayName,
                ["m_DefaultRefNameVersion"] = 1,
                ["m_RefNameGeneratedByDisplayName"] = displayName,
                ["m_DefaultReferenceName"] = defaultReferenceName,
                ["m_OverrideReferenceName"] = overrideReferenceName,
                ["m_GeneratePropertyBlock"] = property.GeneratePropertyBlock ?? true,
                ["m_UseCustomSlotLabel"] = false,
                ["m_CustomSlotLabel"] = string.Empty,
                ["m_DismissedVersion"] = 0,
                ["m_Precision"] = 0,
                ["overrideHLSLDeclaration"] = false,
                ["hlslDeclarationOverride"] = 0,
                ["m_Hidden"] = property.Hidden ?? false,
                ["m_PerRendererData"] = false,
                ["m_customAttributes"] = new JsonArray(),
                ["m_Value"] = new JsonObject
                {
                    ["r"] = color.r,
                    ["g"] = color.g,
                    ["b"] = color.b,
                    ["a"] = color.a
                },
                ["isMainColor"] = false,
                ["m_ColorMode"] = 0
            };
        }

        static JsonObject CreateFloatPropertyObject(
            string propertyObjectId,
            string propertyGuid,
            string displayName,
            string defaultReferenceName,
            string overrideReferenceName,
            ShaderGraphAddPropertyInput property)
        {
            return new JsonObject
            {
                ["m_SGVersion"] = 1,
                ["m_Type"] = "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty",
                ["m_ObjectId"] = propertyObjectId,
                ["m_Guid"] = new JsonObject
                {
                    ["m_GuidSerialized"] = propertyGuid
                },
                ["m_Name"] = displayName,
                ["m_DefaultRefNameVersion"] = 1,
                ["m_RefNameGeneratedByDisplayName"] = displayName,
                ["m_DefaultReferenceName"] = defaultReferenceName,
                ["m_OverrideReferenceName"] = overrideReferenceName,
                ["m_GeneratePropertyBlock"] = property.GeneratePropertyBlock ?? true,
                ["m_UseCustomSlotLabel"] = false,
                ["m_CustomSlotLabel"] = string.Empty,
                ["m_DismissedVersion"] = 0,
                ["m_Precision"] = 0,
                ["overrideHLSLDeclaration"] = false,
                ["hlslDeclarationOverride"] = 0,
                ["m_Hidden"] = property.Hidden ?? false,
                ["m_Value"] = property.FloatValue ?? 0f,
                ["m_FloatType"] = 0,
                ["m_RangeValues"] = new JsonObject
                {
                    ["x"] = 0.0,
                    ["y"] = 1.0
                }
            };
        }

        static JsonObject CreateTexture2DPropertyObject(
            string propertyObjectId,
            string propertyGuid,
            string displayName,
            string defaultReferenceName,
            string overrideReferenceName,
            ShaderGraphAddPropertyInput property)
        {
            var textureAssetGuid = ResolveTexture2DAssetGuid(property.TextureAssetPath, "property.textureAssetPath");

            return new JsonObject
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty",
                ["m_ObjectId"] = propertyObjectId,
                ["m_Guid"] = new JsonObject
                {
                    ["m_GuidSerialized"] = propertyGuid
                },
                ["m_Name"] = displayName,
                ["m_DefaultRefNameVersion"] = 1,
                ["m_RefNameGeneratedByDisplayName"] = displayName,
                ["m_DefaultReferenceName"] = defaultReferenceName,
                ["m_OverrideReferenceName"] = overrideReferenceName,
                ["m_GeneratePropertyBlock"] = property.GeneratePropertyBlock ?? true,
                ["m_UseCustomSlotLabel"] = false,
                ["m_CustomSlotLabel"] = string.Empty,
                ["m_DismissedVersion"] = 0,
                ["m_Precision"] = 0,
                ["overrideHLSLDeclaration"] = false,
                ["hlslDeclarationOverride"] = 0,
                ["m_Hidden"] = property.Hidden ?? false,
                ["m_PerRendererData"] = false,
                ["m_customAttributes"] = new JsonArray(),
                ["m_Value"] = new JsonObject
                {
                    ["m_SerializedTexture"] = string.Empty,
                    ["m_Guid"] = textureAssetGuid ?? string.Empty
                },
                ["isMainTexture"] = property.TextureIsMainTexture ?? false,
                ["useTilingAndOffset"] = property.TextureUseTilingAndOffset ?? false,
                ["useTexelSize"] = property.TextureUseTexelSize ?? true,
                ["isHDR"] = property.TextureIsHdr ?? false,
                ["m_Modifiable"] = property.TextureModifiable ?? true,
                ["m_DefaultType"] = ParseTexture2DDefaultType(property.TextureDefaultType)
            };
        }

        static JsonObject CreateVectorPropertyObject(
            string propertyType,
            string propertyObjectId,
            string propertyGuid,
            string displayName,
            string defaultReferenceName,
            string overrideReferenceName,
            ShaderGraphAddPropertyInput property)
        {
            var shaderGraphType = propertyType switch
            {
                "vector2" => "UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty",
                "vector3" => "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty",
                "vector4" => "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty",
                _ => throw new ArgumentException($"Unsupported vector property type '{propertyType}'.")
            };

            return new JsonObject
            {
                ["m_SGVersion"] = 1,
                ["m_Type"] = shaderGraphType,
                ["m_ObjectId"] = propertyObjectId,
                ["m_Guid"] = new JsonObject
                {
                    ["m_GuidSerialized"] = propertyGuid
                },
                ["m_Name"] = displayName,
                ["m_DefaultRefNameVersion"] = 1,
                ["m_RefNameGeneratedByDisplayName"] = displayName,
                ["m_DefaultReferenceName"] = defaultReferenceName,
                ["m_OverrideReferenceName"] = overrideReferenceName,
                ["m_GeneratePropertyBlock"] = property.GeneratePropertyBlock ?? true,
                ["m_UseCustomSlotLabel"] = false,
                ["m_CustomSlotLabel"] = string.Empty,
                ["m_DismissedVersion"] = 0,
                ["m_Precision"] = 0,
                ["overrideHLSLDeclaration"] = false,
                ["hlslDeclarationOverride"] = 0,
                ["m_Hidden"] = property.Hidden ?? false,
                ["m_PerRendererData"] = false,
                ["m_customAttributes"] = new JsonArray(),
                ["m_Value"] = new JsonObject
                {
                    ["x"] = property.VectorX ?? 0f,
                    ["y"] = property.VectorY ?? 0f,
                    ["z"] = property.VectorZ ?? 0f,
                    ["w"] = property.VectorW ?? 0f
                }
            };
        }

        static JsonObject CreateBooleanPropertyObject(
            string propertyObjectId,
            string propertyGuid,
            string displayName,
            string defaultReferenceName,
            string overrideReferenceName,
            ShaderGraphAddPropertyInput property)
        {
            return new JsonObject
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty",
                ["m_ObjectId"] = propertyObjectId,
                ["m_Guid"] = new JsonObject
                {
                    ["m_GuidSerialized"] = propertyGuid
                },
                ["m_Name"] = displayName,
                ["m_DefaultRefNameVersion"] = 1,
                ["m_RefNameGeneratedByDisplayName"] = displayName,
                ["m_DefaultReferenceName"] = defaultReferenceName,
                ["m_OverrideReferenceName"] = overrideReferenceName,
                ["m_GeneratePropertyBlock"] = property.GeneratePropertyBlock ?? true,
                ["m_UseCustomSlotLabel"] = false,
                ["m_CustomSlotLabel"] = string.Empty,
                ["m_DismissedVersion"] = 0,
                ["m_Precision"] = 0,
                ["overrideHLSLDeclaration"] = false,
                ["hlslDeclarationOverride"] = 0,
                ["m_Hidden"] = property.Hidden ?? false,
                ["m_PerRendererData"] = false,
                ["m_customAttributes"] = new JsonArray(),
                ["m_Value"] = property.BooleanValue ?? false
            };
        }

        static int ParseTexture2DDefaultType(string? defaultType)
        {
            if (string.IsNullOrWhiteSpace(defaultType))
                return 0;

            return NormalizeEnumValue(defaultType!) switch
            {
                "white" => 0,
                "black" => 1,
                "grey" or "gray" => 2,
                "normalmap" or "normal" or "bump" => 3,
                "lineargrey" or "lineargray" => 4,
                "red" => 5,
                _ => throw new ArgumentException(
                    $"Unsupported textureDefaultType '{defaultType}'. Supported values: white, black, grey, normalMap, bump, linearGrey, red.")
            };
        }

        static void AddPropertyReferenceToRoot(JsonObject root, string propertyObjectId)
        {
            var propertiesArray = EnsureReferenceArray(root, "m_Properties");
            propertiesArray.Add(new JsonObject
            {
                ["m_Id"] = propertyObjectId
            });
        }

        static void AddPropertyReferenceToCategory(ShaderGraphMutableDocument document, string propertyObjectId)
            => AddPropertyReferenceToCategory(
                document,
                propertyObjectId,
                categoryObjectIdValue: null,
                categoryNameValue: null,
                createCategoryIfMissing: false,
                categoryIndex: null);

        static void AddPropertyReferenceToCategory(
            ShaderGraphMutableDocument document,
            string propertyObjectId,
            string? categoryObjectIdValue,
            string? categoryNameValue,
            bool createCategoryIfMissing,
            int? categoryIndex)
        {
            var categoryObject = ResolveCategoryObject(
                document,
                categoryObjectIdValue,
                categoryNameValue,
                createCategoryIfMissing,
                allowDefaultFallback: true);

            var childArray = EnsureReferenceArray(categoryObject, "m_ChildObjectList");
            InsertPropertyReference(childArray, propertyObjectId, categoryIndex);
        }

        static JsonArray EnsureReferenceArray(JsonObject root, string propertyName)
        {
            if (root[propertyName] is JsonArray existingArray)
                return existingArray;

            var createdArray = new JsonArray();
            root[propertyName] = createdArray;
            return createdArray;
        }
    }
}
