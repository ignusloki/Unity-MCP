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
        [AiSkillDescription("Add a new Shader Graph blackboard property to the default category, then re-import the graph and return the created property and diagnostics.")]
        [AiSkillBody("Add a new Shader Graph blackboard property to a '.shadergraph' asset.\n\n" +
            "Current support is intentionally narrow and safe:\n" +
            "- `propertyType = color`\n" +
            "- `propertyType = float`\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `property` — creation payload for the new property.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data.\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data.\n\n" +
            "## Behavior\n\n" +
            "Creates a new blackboard property in the graph's default category, updates the serialized property lists, re-imports the graph, and returns the created property snapshot plus post-import diagnostics.")]
        [Description("Add a new Shader Graph blackboard property and re-import the graph.")]
        public ShaderGraphPropertyMutationResultData AddProperty(
            AssetObjectRef assetRef,
            ShaderGraphAddPropertyInput property,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
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
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphPropertyMutationResultData AddShaderGraphProperty(
            AssetObjectRef assetRef,
            ShaderGraphAddPropertyInput property,
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
            AddPropertyReferenceToCategory(document, propertyObjectId);

            WriteMutableDocument(document);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var createdProperty = structure.Properties?
                .FirstOrDefault(p => string.Equals(p.ObjectId, propertyObjectId, StringComparison.Ordinal));

            return new ShaderGraphPropertyMutationResultData
            {
                ChangedFields = new List<string> { "property.added" },
                Property = createdProperty,
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
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
                _ => throw new ArgumentException(
                    $"Unsupported propertyType '{property.PropertyType}'. Supported values: color, float.")
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

        static void AddPropertyReferenceToRoot(JsonObject root, string propertyObjectId)
        {
            var propertiesArray = EnsureReferenceArray(root, "m_Properties");
            propertiesArray.Add(new JsonObject
            {
                ["m_Id"] = propertyObjectId
            });
        }

        static void AddPropertyReferenceToCategory(ShaderGraphMutableDocument document, string propertyObjectId)
        {
            JsonObject categoryObject;
            var categoryIds = GetIdArray(document.Root, "m_CategoryData");
            if (categoryIds.Count > 0 && document.ObjectsById.TryGetValue(categoryIds[0], out var existingCategory))
            {
                categoryObject = existingCategory;
            }
            else
            {
                var categoryObjectId = Guid.NewGuid().ToString("N");
                categoryObject = new JsonObject
                {
                    ["m_SGVersion"] = 0,
                    ["m_Type"] = "UnityEditor.ShaderGraph.CategoryData",
                    ["m_ObjectId"] = categoryObjectId,
                    ["m_Name"] = string.Empty,
                    ["m_ChildObjectList"] = new JsonArray()
                };

                document.Objects.Add(categoryObject);
                document.ObjectsById[categoryObjectId] = categoryObject;

                var categoryArray = EnsureReferenceArray(document.Root, "m_CategoryData");
                categoryArray.Add(new JsonObject
                {
                    ["m_Id"] = categoryObjectId
                });
            }

            var childArray = EnsureReferenceArray(categoryObject, "m_ChildObjectList");
            childArray.Add(new JsonObject
            {
                ["m_Id"] = propertyObjectId
            });
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
