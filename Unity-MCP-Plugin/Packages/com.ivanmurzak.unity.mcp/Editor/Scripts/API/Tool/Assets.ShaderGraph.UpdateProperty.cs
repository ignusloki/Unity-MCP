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
        public const string AssetsShaderGraphUpdatePropertyToolId = "assets-shadergraph-update-property";

        [AiTool
        (
            AssetsShaderGraphUpdatePropertyToolId,
            Title = "Assets / Shader Graph / Update Property"
        )]
        [AiSkillDescription("Update an existing Shader Graph blackboard property by object id or reference name, then re-import the graph and return the updated property and diagnostics.")]
        [AiSkillBody("Update an existing Shader Graph blackboard property on a '.shadergraph' asset.\n\n" +
            "Current support is intentionally scoped to common URP Blackboard property types:\n" +
            "- generic property fields: display name, override reference name, hidden, generate property block\n" +
            "- color property default value via `colorHex`\n" +
            "- float property default value via `floatValue`\n" +
            "- vector2/vector3/vector4 default components via `vectorX`, `vectorY`, `vectorZ`, `vectorW`\n" +
            "- boolean default value via `booleanValue`\n" +
            "- Texture2D asset reference via `textureAssetPath`, default type, and toggles via `textureDefaultType`, `textureUseTilingAndOffset`, `textureUseTexelSize`, `textureIsMainTexture`, `textureIsHdr`, `textureModifiable`\n\n" +
            "## Response shape\n\n" +
            "By default returns a slim diff: `Operation`, `PropertyObjectId`, `PropertyReferenceName`, `PropertyKind`, `Property`, `ChangedFields`, and `GraphSummary`. " +
            "Set `includeStructure: true` to also receive the full read-only `Structure` block, `includeGraph: true` for the full post-import `Graph` block.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `property` — selector plus requested updates.\n" +
            "- `includeStructure` — include the full read-only Structure block in the response. Default: false.\n" +
            "- `includeGraph` — include the full post-import Graph block in the response. Default: false.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data (only meaningful when includeGraph is true).\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data (only meaningful when includeGraph is true).\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect property object ids, types, and effective reference names.")]
        [Description("Update an existing Shader Graph blackboard property and re-import the graph.")]
        public ShaderGraphPropertyMutationResultData UpdateProperty(
            AssetObjectRef assetRef,
            ShaderGraphPropertyUpdateInput property,
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

            return MainThread.Instance.Run(() => UpdateShaderGraphProperty(
                assetRef,
                property,
                includeStructure: includeStructure ?? false,
                includeGraph: includeGraph ?? false,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphPropertyMutationResultData UpdateShaderGraphProperty(
            AssetObjectRef assetRef,
            ShaderGraphPropertyUpdateInput property,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties,
            bool deferImport = false)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            if (!HasAnyPropertyUpdates(property))
                throw new ArgumentException("At least one property field must be specified.", nameof(property));

            var document = LoadMutableDocument(assetPath);
            var propertyIds = GetIdArray(document.Root, "m_Properties");
            var propertyObjects = propertyIds
                .Where(document.ObjectsById.ContainsKey)
                .Select(id => document.ObjectsById[id])
                .ToList();

            var propertyObject = ResolvePropertyObject(property, propertyObjects);
            var changedFields = new List<string>();

            ApplyGenericPropertyUpdates(propertyObject, property, propertyObjects, changedFields);
            ApplyTypedPropertyUpdates(propertyObject, property, changedFields);

            var updatedPropertyId = GetString(propertyObject, "m_ObjectId");

            if (changedFields.Count > 0)
            {
                WriteMutableDocument(document);
                if (!deferImport)
                    FinalizeShaderGraphMutation(assetPath);
            }

            if (deferImport)
            {
                return new ShaderGraphPropertyMutationResultData
                {
                    Operation = "update",
                    PropertyObjectId = updatedPropertyId,
                    ChangedFields = changedFields
                };
            }

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var updatedProperty = structure.Properties?
                .FirstOrDefault(p => string.Equals(p.ObjectId, updatedPropertyId, StringComparison.Ordinal));

            return new ShaderGraphPropertyMutationResultData
            {
                Operation = "update",
                PropertyObjectId = updatedProperty?.ObjectId ?? updatedPropertyId,
                PropertyReferenceName = updatedProperty?.EffectiveReferenceName,
                PropertyKind = updatedProperty?.PropertyKind,
                ChangedFields = changedFields,
                Property = updatedProperty,
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

        static bool HasAnyPropertyUpdates(ShaderGraphPropertyUpdateInput property)
            => !string.IsNullOrWhiteSpace(property.DisplayName)
               || !string.IsNullOrWhiteSpace(property.OverrideReferenceName)
               || property.Hidden.HasValue
               || property.GeneratePropertyBlock.HasValue
               || !string.IsNullOrWhiteSpace(property.ColorHex)
               || property.FloatValue.HasValue
               || HasAnyVectorUpdates(property)
               || property.BooleanValue.HasValue
               || HasAnyTextureUpdates(property);

        static void ApplyGenericPropertyUpdates(
            JsonObject propertyObject,
            ShaderGraphPropertyUpdateInput property,
            List<JsonObject> allPropertyObjects,
            List<string> changedFields)
        {
            if (!string.IsNullOrWhiteSpace(property.DisplayName))
            {
                var displayName = property.DisplayName!.Trim();
                ValidateUniqueDisplayName(propertyObject, allPropertyObjects, displayName);
                SetString(propertyObject, "m_Name", displayName, "property.displayName", changedFields);
                SetString(propertyObject, "m_RefNameGeneratedByDisplayName", displayName, "property.refNameGeneratedByDisplayName", changedFields);

                var overrideReferenceName = GetString(propertyObject, "m_OverrideReferenceName");
                if (string.IsNullOrEmpty(overrideReferenceName))
                {
                    var generatedDefaultReferenceName = GenerateDefaultReferenceName(displayName);
                    ValidateUniqueReferenceName(propertyObject, allPropertyObjects, generatedDefaultReferenceName);
                    SetString(
                        propertyObject,
                        "m_DefaultReferenceName",
                        generatedDefaultReferenceName,
                        "property.defaultReferenceName",
                        changedFields);
                }
            }

            if (!string.IsNullOrWhiteSpace(property.OverrideReferenceName))
            {
                var overrideReferenceName = property.OverrideReferenceName!.Trim();
                ValidateReferenceNameSyntax(overrideReferenceName);
                ValidateUniqueReferenceName(propertyObject, allPropertyObjects, overrideReferenceName);
                SetString(
                    propertyObject,
                    "m_OverrideReferenceName",
                    overrideReferenceName,
                    "property.overrideReferenceName",
                    changedFields);
            }

            if (property.Hidden.HasValue)
            {
                SetBool(
                    propertyObject,
                    "m_Hidden",
                    property.Hidden.Value,
                    "property.hidden",
                    changedFields);
            }

            if (property.GeneratePropertyBlock.HasValue)
            {
                SetBool(
                    propertyObject,
                    "m_GeneratePropertyBlock",
                    property.GeneratePropertyBlock.Value,
                    "property.generatePropertyBlock",
                    changedFields);
            }
        }

        static void ApplyTypedPropertyUpdates(
            JsonObject propertyObject,
            ShaderGraphPropertyUpdateInput property,
            List<string> changedFields)
        {
            var propertyType = GetString(propertyObject, "m_Type");

            if (!string.IsNullOrWhiteSpace(property.ColorHex))
                ApplyColorPropertyUpdates(propertyObject, propertyType, property, changedFields);

            if (property.FloatValue.HasValue)
                ApplyFloatPropertyUpdates(propertyObject, propertyType, property, changedFields);

            if (HasAnyVectorUpdates(property))
                ApplyVectorPropertyUpdates(propertyObject, propertyType, property, changedFields);

            if (property.BooleanValue.HasValue)
                ApplyBooleanPropertyUpdates(propertyObject, propertyType, property, changedFields);

            if (HasAnyTextureUpdates(property))
                ApplyTexture2DPropertyUpdates(propertyObject, propertyType, property, changedFields);
        }

        static void ApplyColorPropertyUpdates(
            JsonObject propertyObject,
            string? propertyType,
            ShaderGraphPropertyUpdateInput property,
            List<string> changedFields)
        {
            RequirePropertyType(propertyType, "UnityEditor.ShaderGraph.Internal.ColorShaderProperty", "colorHex");

            if (!ColorUtility.TryParseHtmlString(property.ColorHex!.Trim(), out var color))
                throw new ArgumentException($"Invalid colorHex '{property.ColorHex}'. Expected formats like '#RRGGBB' or '#RRGGBBAA'.");

            if (propertyObject["m_Value"] is not JsonObject colorValue)
                throw new InvalidOperationException("Color property is missing its serialized m_Value object.");

            SetFloat(colorValue, "r", color.r, "property.color.r", changedFields);
            SetFloat(colorValue, "g", color.g, "property.color.g", changedFields);
            SetFloat(colorValue, "b", color.b, "property.color.b", changedFields);
            SetFloat(colorValue, "a", color.a, "property.color.a", changedFields);
        }

        static void ApplyFloatPropertyUpdates(
            JsonObject propertyObject,
            string? propertyType,
            ShaderGraphPropertyUpdateInput property,
            List<string> changedFields)
        {
            RequirePropertyType(propertyType, "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty", "floatValue");
            SetFloat(propertyObject, "m_Value", property.FloatValue!.Value, "property.floatValue", changedFields);
        }

        static void ApplyVectorPropertyUpdates(
            JsonObject propertyObject,
            string? propertyType,
            ShaderGraphPropertyUpdateInput property,
            List<string> changedFields)
        {
            var dimension = GetVectorPropertyDimension(propertyType);
            if (!dimension.HasValue)
            {
                throw new InvalidOperationException(
                    $"vectorX/vectorY/vectorZ/vectorW are only supported for vector properties. Property type: '{propertyType ?? "null"}'.");
            }

            if (propertyObject["m_Value"] is not JsonObject vectorValue)
                throw new InvalidOperationException("Vector property is missing its serialized m_Value object.");

            SetVectorComponent(vectorValue, "x", property.VectorX, 1, dimension.Value, "property.vector.x", changedFields);
            SetVectorComponent(vectorValue, "y", property.VectorY, 2, dimension.Value, "property.vector.y", changedFields);
            SetVectorComponent(vectorValue, "z", property.VectorZ, 3, dimension.Value, "property.vector.z", changedFields);
            SetVectorComponent(vectorValue, "w", property.VectorW, 4, dimension.Value, "property.vector.w", changedFields);
        }

        static void ApplyBooleanPropertyUpdates(
            JsonObject propertyObject,
            string? propertyType,
            ShaderGraphPropertyUpdateInput property,
            List<string> changedFields)
        {
            RequirePropertyType(propertyType, "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty", "booleanValue");
            SetBool(propertyObject, "m_Value", property.BooleanValue!.Value, "property.booleanValue", changedFields);
        }

        static void ApplyTexture2DPropertyUpdates(
            JsonObject propertyObject,
            string? propertyType,
            ShaderGraphPropertyUpdateInput property,
            List<string> changedFields)
        {
            RequirePropertyType(propertyType, "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", "texture fields");

            if (!string.IsNullOrWhiteSpace(property.TextureDefaultType))
            {
                SetInt(
                    propertyObject,
                    "m_DefaultType",
                    ParseTexture2DDefaultType(property.TextureDefaultType),
                    "property.texture.defaultType",
                    changedFields);
            }

            if (property.TextureAssetPath != null)
            {
                var textureValue = EnsureTextureValueObject(propertyObject);
                var textureAssetGuid = ResolveTexture2DAssetGuid(property.TextureAssetPath, "property.textureAssetPath");

                SetString(
                    textureValue,
                    "m_Guid",
                    textureAssetGuid ?? string.Empty,
                    "property.texture.assetGuid",
                    changedFields);

                SetString(
                    textureValue,
                    "m_SerializedTexture",
                    string.Empty,
                    "property.texture.serializedTexture",
                    changedFields);
            }

            if (property.TextureUseTilingAndOffset.HasValue)
            {
                SetBool(
                    propertyObject,
                    "useTilingAndOffset",
                    property.TextureUseTilingAndOffset.Value,
                    "property.texture.useTilingAndOffset",
                    changedFields);
            }

            if (property.TextureUseTexelSize.HasValue)
            {
                SetBool(
                    propertyObject,
                    "useTexelSize",
                    property.TextureUseTexelSize.Value,
                    "property.texture.useTexelSize",
                    changedFields);
            }

            if (property.TextureIsMainTexture.HasValue)
            {
                SetBool(
                    propertyObject,
                    "isMainTexture",
                    property.TextureIsMainTexture.Value,
                    "property.texture.isMainTexture",
                    changedFields);
            }

            if (property.TextureIsHdr.HasValue)
            {
                SetBool(
                    propertyObject,
                    "isHDR",
                    property.TextureIsHdr.Value,
                    "property.texture.isHdr",
                    changedFields);
            }

            if (property.TextureModifiable.HasValue)
            {
                SetBool(
                    propertyObject,
                    "m_Modifiable",
                    property.TextureModifiable.Value,
                    "property.texture.modifiable",
                    changedFields);
            }
        }

        static JsonObject EnsureTextureValueObject(JsonObject propertyObject)
        {
            if (propertyObject["m_Value"] is JsonObject textureValue)
                return textureValue;

            textureValue = new JsonObject
            {
                ["m_SerializedTexture"] = string.Empty,
                ["m_Guid"] = string.Empty
            };
            propertyObject["m_Value"] = textureValue;
            return textureValue;
        }

        static string? ResolveTexture2DAssetGuid(string? textureAssetPath, string fieldPath)
        {
            if (textureAssetPath == null)
                return null;

            var trimmedPath = textureAssetPath.Trim();
            if (trimmedPath.Length == 0)
                return null;

            ValidateProjectAssetPath(trimmedPath, fieldPath);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(trimmedPath);
            if (texture == null)
            {
                throw new ArgumentException(
                    $"{fieldPath} points to '{trimmedPath}', but no Texture2D asset could be loaded from that path.");
            }

            var guid = AssetDatabase.AssetPathToGUID(trimmedPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new ArgumentException(
                    $"{fieldPath} points to '{trimmedPath}', but Unity did not return an asset GUID for it.");
            }

            return guid;
        }

        static bool HasAnyVectorUpdates(ShaderGraphPropertyUpdateInput property)
            => property.VectorX.HasValue
               || property.VectorY.HasValue
               || property.VectorZ.HasValue
               || property.VectorW.HasValue;

        static bool HasAnyTextureUpdates(ShaderGraphPropertyUpdateInput property)
            => !string.IsNullOrWhiteSpace(property.TextureDefaultType)
               || property.TextureAssetPath != null
               || property.TextureUseTilingAndOffset.HasValue
               || property.TextureUseTexelSize.HasValue
               || property.TextureIsMainTexture.HasValue
               || property.TextureIsHdr.HasValue
               || property.TextureModifiable.HasValue;

        static void RequirePropertyType(string? actualPropertyType, string expectedPropertyType, string fieldName)
        {
            if (string.Equals(actualPropertyType, expectedPropertyType, StringComparison.Ordinal))
                return;

            throw new InvalidOperationException(
                $"{fieldName} is only supported for {expectedPropertyType}. Property type: '{actualPropertyType ?? "null"}'.");
        }

        static int? GetVectorPropertyDimension(string? propertyType)
        {
            return propertyType switch
            {
                "UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty" => 2,
                "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty" => 3,
                "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty" => 4,
                _ => null
            };
        }

        static void SetVectorComponent(
            JsonObject vectorValue,
            string componentName,
            float? value,
            int requiredDimension,
            int actualDimension,
            string changedFieldName,
            List<string> changedFields)
        {
            if (!value.HasValue)
                return;

            if (actualDimension < requiredDimension)
            {
                throw new ArgumentException(
                    $"{changedFieldName} requires a vector{requiredDimension} or larger property, but the selected property is vector{actualDimension}.");
            }

            SetFloat(vectorValue, componentName, value.Value, changedFieldName, changedFields);
        }

        static void ValidateUniqueDisplayName(
            JsonObject? propertyObject,
            List<JsonObject> allPropertyObjects,
            string displayName)
        {
            var duplicate = allPropertyObjects.Any(other =>
                !ReferenceEquals(other, propertyObject)
                && string.Equals(GetString(other, "m_Name"), displayName, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
                throw new InvalidOperationException($"Another Shader Graph property already uses display name '{displayName}'.");
        }

        static void ValidateUniqueReferenceName(
            JsonObject? propertyObject,
            List<JsonObject> allPropertyObjects,
            string referenceName)
        {
            var duplicate = allPropertyObjects.Any(other =>
                !ReferenceEquals(other, propertyObject)
                && string.Equals(GetEffectivePropertyReferenceName(other), referenceName, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
                throw new InvalidOperationException($"Another Shader Graph property already uses reference name '{referenceName}'.");
        }

        static void ValidateReferenceNameSyntax(string referenceName)
        {
            if (string.IsNullOrWhiteSpace(referenceName))
                throw new ArgumentException("overrideReferenceName must not be empty.");

            if (!(char.IsLetter(referenceName[0]) || referenceName[0] == '_'))
            {
                throw new ArgumentException(
                    $"overrideReferenceName '{referenceName}' must start with a letter or underscore.");
            }

            for (var i = 1; i < referenceName.Length; i++)
            {
                var ch = referenceName[i];
                if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                {
                    throw new ArgumentException(
                        $"overrideReferenceName '{referenceName}' must contain only letters, digits, or underscores.");
                }
            }
        }

        static string GetEffectivePropertyReferenceName(JsonObject propertyObject)
        {
            var overrideReferenceName = GetString(propertyObject, "m_OverrideReferenceName");
            if (!string.IsNullOrWhiteSpace(overrideReferenceName))
                return overrideReferenceName!;

            return GetString(propertyObject, "m_DefaultReferenceName") ?? string.Empty;
        }

        static string GenerateDefaultReferenceName(string displayName)
        {
            var sanitizedChars = displayName
                .Trim()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();

            var sanitized = new string(sanitizedChars).Trim('_');
            if (string.IsNullOrEmpty(sanitized))
                sanitized = "Property";

            if (char.IsDigit(sanitized[0]))
                sanitized = "_" + sanitized;

            return sanitized.StartsWith("_", StringComparison.Ordinal)
                ? sanitized
                : "_" + sanitized;
        }

        static void SetFloat(
            JsonObject root,
            string propertyName,
            float value,
            string changedFieldName,
            List<string> changedFields)
        {
            var currentValue = GetFloat(root, propertyName);
            if (currentValue.HasValue && Mathf.Abs(currentValue.Value - value) <= 0.0001f)
                return;

            root[propertyName] = value;
            changedFields.Add(changedFieldName);
        }

        static float? GetFloat(JsonObject root, string propertyName)
        {
            if (root[propertyName] is not JsonValue propertyValue)
                return null;

            if (propertyValue.TryGetValue<float>(out var floatValue))
                return floatValue;

            if (propertyValue.TryGetValue<double>(out var doubleValue))
                return (float)doubleValue;

            return null;
        }
    }
}
