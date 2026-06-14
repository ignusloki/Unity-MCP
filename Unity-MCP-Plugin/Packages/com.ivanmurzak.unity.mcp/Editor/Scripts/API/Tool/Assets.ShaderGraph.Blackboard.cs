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
        public const string AssetsShaderGraphDeletePropertyToolId = "assets-shadergraph-delete-property";
        public const string AssetsShaderGraphReorderPropertyToolId = "assets-shadergraph-reorder-property";
        public const string AssetsShaderGraphCreateCategoryToolId = "assets-shadergraph-create-category";
        public const string AssetsShaderGraphSetPropertyCategoryToolId = "assets-shadergraph-set-property-category";

        [AiTool
        (
            AssetsShaderGraphDeletePropertyToolId,
            Title = "Assets / Shader Graph / Delete Property"
        )]
        [AiSkillDescription("Delete an existing Shader Graph blackboard property, remove dependent PropertyNodes and edges, then re-import the graph and return diagnostics.")]
        [AiSkillBody("Delete an existing blackboard property from a '.shadergraph' asset.\n\n" +
            "The deletion is guarded:\n" +
            "- selection by `propertyObjectId` or `propertyReferenceName`\n" +
            "- removes the property from root and category lists\n" +
            "- removes PropertyNode instances that reference the deleted property\n" +
            "- removes edges connected to those removed PropertyNodes\n" +
            "- does not remove unrelated nodes or properties\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect property ids and references.")]
        [Description("Delete an existing Shader Graph blackboard property and clean up dependent PropertyNodes.")]
        public ShaderGraphPropertyMutationResultData DeleteProperty(
            AssetObjectRef assetRef,
            ShaderGraphDeletePropertyInput property,
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

            return MainThread.Instance.Run(() => DeleteShaderGraphProperty(
                assetRef,
                property,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        [AiTool
        (
            AssetsShaderGraphReorderPropertyToolId,
            Title = "Assets / Shader Graph / Reorder Property"
        )]
        [AiSkillDescription("Reorder an existing Shader Graph blackboard property inside a category, then re-import the graph and return diagnostics.")]
        [AiSkillBody("Reorder a blackboard property inside a Shader Graph category.\n\n" +
            "If no category selector is supplied, the current category containing the property is used. Supplying a category selector can also move the property into that category at the requested index.")]
        [Description("Reorder an existing Shader Graph blackboard property inside a category.")]
        public ShaderGraphPropertyMutationResultData ReorderProperty(
            AssetObjectRef assetRef,
            ShaderGraphReorderPropertyInput property,
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

            return MainThread.Instance.Run(() => ReorderShaderGraphProperty(
                assetRef,
                property,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        [AiTool
        (
            AssetsShaderGraphCreateCategoryToolId,
            Title = "Assets / Shader Graph / Create Category"
        )]
        [AiSkillDescription("Create a Shader Graph blackboard category, then re-import the graph and return diagnostics.")]
        [AiSkillBody("Create a blackboard category in a '.shadergraph' asset.\n\n" +
            "Category names must be unique. The default category is represented by an empty name and is created automatically when needed by property tools.")]
        [Description("Create a Shader Graph blackboard category.")]
        public ShaderGraphCategoryMutationResultData CreateCategory(
            AssetObjectRef assetRef,
            ShaderGraphCreateCategoryInput category,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (category == null)
                throw new ArgumentNullException(nameof(category));

            return MainThread.Instance.Run(() => CreateShaderGraphCategory(
                assetRef,
                category,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        [AiTool
        (
            AssetsShaderGraphSetPropertyCategoryToolId,
            Title = "Assets / Shader Graph / Set Property Category"
        )]
        [AiSkillDescription("Move a Shader Graph blackboard property into a target category, optionally creating that category, then re-import the graph and return diagnostics.")]
        [AiSkillBody("Move a property into a Shader Graph blackboard category.\n\n" +
            "The target category can be selected by object id or display name. When `createCategoryIfMissing` is true, a missing category name is created before the move.")]
        [Description("Move a Shader Graph blackboard property into a category.")]
        public ShaderGraphPropertyMutationResultData SetPropertyCategory(
            AssetObjectRef assetRef,
            ShaderGraphSetPropertyCategoryInput property,
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

            return MainThread.Instance.Run(() => SetShaderGraphPropertyCategory(
                assetRef,
                property,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphPropertyMutationResultData DeleteShaderGraphProperty(
            AssetObjectRef assetRef,
            ShaderGraphDeletePropertyInput property,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveShaderGraphAssetPath(assetRef);
            var document = LoadMutableDocument(assetPath);
            var propertyObjects = GetRootPropertyObjects(document);
            var propertyObject = ResolvePropertyObject(property.PropertyObjectId, property.PropertyReferenceName, propertyObjects);
            var propertyObjectId = GetRequiredObjectId(propertyObject, "property");
            var graphRef = new AssetObjectRef(assetPath);
            var structureBeforeDelete = BuildShaderGraphStructureData(graphRef);
            var deletedProperty = structureBeforeDelete.Properties?
                .FirstOrDefault(p => string.Equals(p.ObjectId, propertyObjectId, StringComparison.Ordinal));

            var removedNodeIds = new HashSet<string>(StringComparer.Ordinal);
            var removedSlotIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var nodeId in GetIdArray(document.Root, "m_Nodes"))
            {
                if (!document.ObjectsById.TryGetValue(nodeId, out var nodeObject))
                    continue;

                if (!string.Equals(GetString(nodeObject, "m_Type"), "UnityEditor.ShaderGraph.PropertyNode", StringComparison.Ordinal))
                    continue;

                if (!string.Equals(GetStringAt(nodeObject, "m_Property", "m_Id"), propertyObjectId, StringComparison.Ordinal))
                    continue;

                removedNodeIds.Add(nodeId);
                foreach (var slotId in GetIdArray(nodeObject, "m_Slots"))
                    removedSlotIds.Add(slotId);
            }

            var removedEdgeCount = RemoveEdgesConnectedToNodes(document.Root, removedNodeIds);
            RemoveReferenceFromArray(document.Root, "m_Properties", propertyObjectId);
            RemovePropertyReferenceFromAllCategories(document, propertyObjectId);

            foreach (var removedNodeId in removedNodeIds)
            {
                RemoveReferenceFromArray(document.Root, "m_Nodes", removedNodeId);
                RemoveObjectById(document, removedNodeId);
            }

            foreach (var removedSlotId in removedSlotIds)
                RemoveObjectById(document, removedSlotId);

            RemoveObjectById(document, propertyObjectId);

            WriteMutableDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            var structure = BuildShaderGraphStructureData(graphRef);
            return new ShaderGraphPropertyMutationResultData
            {
                Operation = "delete",
                PropertyObjectId = deletedProperty?.ObjectId ?? propertyObjectId,
                PropertyReferenceName = deletedProperty?.EffectiveReferenceName,
                PropertyKind = deletedProperty?.PropertyKind,
                Property = deletedProperty,
                RemovedNodeCount = removedNodeIds.Count,
                RemovedEdgeCount = removedEdgeCount,
                ChangedFields = BuildDeletePropertyChangedFields(removedNodeIds.Count, removedEdgeCount),
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        static ShaderGraphPropertyMutationResultData ReorderShaderGraphProperty(
            AssetObjectRef assetRef,
            ShaderGraphReorderPropertyInput property,
            bool includeMessages,
            bool includeProperties)
        {
            if (!property.CategoryIndex.HasValue)
                throw new ArgumentException("categoryIndex must be provided.", nameof(property));

            var assetPath = ResolveShaderGraphAssetPath(assetRef);
            var document = LoadMutableDocument(assetPath);
            var propertyObjects = GetRootPropertyObjects(document);
            var propertyObject = ResolvePropertyObject(property.PropertyObjectId, property.PropertyReferenceName, propertyObjects);
            var propertyObjectId = GetRequiredObjectId(propertyObject, "property");
            var currentCategory = FindCategoryContainingProperty(document, propertyObjectId);
            var targetCategory = HasCategorySelector(property.CategoryObjectId, property.CategoryName)
                ? ResolveCategoryObject(
                    document,
                    property.CategoryObjectId,
                    property.CategoryName,
                    createCategoryIfMissing: false,
                    allowDefaultFallback: false)
                : currentCategory ?? throw new InvalidOperationException(
                    $"Property '{propertyObjectId}' is not currently assigned to any blackboard category.");

            MovePropertyReferenceToCategory(document, propertyObjectId, targetCategory, property.CategoryIndex);

            WriteMutableDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            return BuildPropertyMutationResult(
                assetPath,
                propertyObjectId,
                operation: "reorder",
                changedFields: new List<string> { "property.reordered", "property.categoryIndex" },
                includeMessages,
                includeProperties);
        }

        static ShaderGraphCategoryMutationResultData CreateShaderGraphCategory(
            AssetObjectRef assetRef,
            ShaderGraphCreateCategoryInput category,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveShaderGraphAssetPath(assetRef);
            var document = LoadMutableDocument(assetPath);
            var categoryName = category.CategoryName?.Trim() ?? string.Empty;

            if (FindCategoryByName(document, categoryName) != null)
                throw new InvalidOperationException($"A Shader Graph blackboard category named '{categoryName}' already exists.");

            var categoryObject = CreateCategoryObject(document, categoryName);
            WriteMutableDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var categoryObjectId = GetRequiredObjectId(categoryObject, "category");
            var createdCategory = structure.Categories?
                .FirstOrDefault(c => string.Equals(c.ObjectId, categoryObjectId, StringComparison.Ordinal));

            return new ShaderGraphCategoryMutationResultData
            {
                Operation = "createCategory",
                CategoryObjectId = createdCategory?.ObjectId ?? categoryObjectId,
                CategoryName = createdCategory?.Name ?? categoryName,
                Category = createdCategory,
                ChangedFields = new List<string> { "category.added" },
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        static ShaderGraphPropertyMutationResultData SetShaderGraphPropertyCategory(
            AssetObjectRef assetRef,
            ShaderGraphSetPropertyCategoryInput property,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveShaderGraphAssetPath(assetRef);
            var document = LoadMutableDocument(assetPath);
            var propertyObjects = GetRootPropertyObjects(document);
            var propertyObject = ResolvePropertyObject(property.PropertyObjectId, property.PropertyReferenceName, propertyObjects);
            var propertyObjectId = GetRequiredObjectId(propertyObject, "property");
            var targetCategory = ResolveCategoryObject(
                document,
                property.CategoryObjectId,
                property.CategoryName,
                property.CreateCategoryIfMissing ?? false,
                allowDefaultFallback: false);

            MovePropertyReferenceToCategory(document, propertyObjectId, targetCategory, property.CategoryIndex);

            WriteMutableDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            return BuildPropertyMutationResult(
                assetPath,
                propertyObjectId,
                operation: "setCategory",
                changedFields: new List<string> { "property.category", "property.categoryIndex" },
                includeMessages,
                includeProperties);
        }

        static string ResolveShaderGraphAssetPath(AssetObjectRef assetRef)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            return assetPath;
        }

        static List<JsonObject> GetRootPropertyObjects(ShaderGraphMutableDocument document)
            => GetIdArray(document.Root, "m_Properties")
                .Where(document.ObjectsById.ContainsKey)
                .Select(id => document.ObjectsById[id])
                .ToList();

        static JsonObject ResolvePropertyObject(
            string? propertyObjectIdValue,
            string? propertyReferenceNameValue,
            List<JsonObject> propertyObjects)
        {
            var objectId = propertyObjectIdValue?.Trim();
            var referenceName = propertyReferenceNameValue?.Trim();

            if (string.IsNullOrEmpty(objectId) && string.IsNullOrEmpty(referenceName))
                throw new ArgumentException("Either propertyObjectId or propertyReferenceName must be provided.");

            JsonObject? resolved;
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

            return resolved ?? throw new InvalidOperationException(
                $"Shader Graph property was not found. objectId='{objectId ?? string.Empty}', referenceName='{referenceName ?? string.Empty}'.");
        }

        static JsonObject ResolvePropertyObject(
            ShaderGraphPropertyUpdateInput property,
            List<JsonObject> propertyObjects)
            => ResolvePropertyObject(property.PropertyObjectId, property.PropertyReferenceName, propertyObjects);

        static JsonObject ResolveCategoryObject(
            ShaderGraphMutableDocument document,
            string? categoryObjectIdValue,
            string? categoryNameValue,
            bool createCategoryIfMissing,
            bool allowDefaultFallback)
        {
            var categoryObjectId = categoryObjectIdValue?.Trim();
            var categoryName = categoryNameValue?.Trim();

            if (!string.IsNullOrEmpty(categoryObjectId))
            {
                var resolvedCategoryObjectId = categoryObjectId!;
                if (document.ObjectsById.TryGetValue(resolvedCategoryObjectId, out var categoryObject)
                    && IsCategoryObject(categoryObject))
                {
                    return categoryObject;
                }

                throw new InvalidOperationException($"Shader Graph blackboard category '{resolvedCategoryObjectId}' was not found.");
            }

            if (!string.IsNullOrEmpty(categoryName))
            {
                var resolvedCategoryName = categoryName!;
                var categoryObject = FindCategoryByName(document, resolvedCategoryName);
                if (categoryObject != null)
                    return categoryObject;

                if (createCategoryIfMissing)
                    return CreateCategoryObject(document, resolvedCategoryName);

                throw new InvalidOperationException($"Shader Graph blackboard category named '{resolvedCategoryName}' was not found.");
            }

            if (allowDefaultFallback)
                return GetOrCreateDefaultCategoryObject(document);

            throw new ArgumentException("Either categoryObjectId or categoryName must be provided.");
        }

        static JsonObject GetOrCreateDefaultCategoryObject(ShaderGraphMutableDocument document)
        {
            var defaultCategory = FindCategoryByName(document, string.Empty);
            if (defaultCategory != null)
                return defaultCategory;

            return CreateCategoryObject(document, string.Empty);
        }

        static JsonObject? FindCategoryContainingProperty(ShaderGraphMutableDocument document, string propertyObjectId)
        {
            foreach (var categoryObject in GetCategoryObjects(document))
            {
                if (GetIdArray(categoryObject, "m_ChildObjectList").Contains(propertyObjectId, StringComparer.Ordinal))
                    return categoryObject;
            }

            return null;
        }

        static JsonObject? FindCategoryByName(ShaderGraphMutableDocument document, string categoryName)
            => GetCategoryObjects(document)
                .FirstOrDefault(category =>
                    string.Equals(GetString(category, "m_Name") ?? string.Empty, categoryName, StringComparison.Ordinal));

        static List<JsonObject> GetCategoryObjects(ShaderGraphMutableDocument document)
            => GetIdArray(document.Root, "m_CategoryData")
                .Where(document.ObjectsById.ContainsKey)
                .Select(id => document.ObjectsById[id])
                .Where(IsCategoryObject)
                .ToList();

        static bool IsCategoryObject(JsonObject obj)
            => string.Equals(GetString(obj, "m_Type"), "UnityEditor.ShaderGraph.CategoryData", StringComparison.Ordinal);

        static JsonObject CreateCategoryObject(ShaderGraphMutableDocument document, string categoryName)
        {
            var categoryObjectId = CreateUniqueShaderGraphObjectId(document);
            var categoryObject = new JsonObject
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = "UnityEditor.ShaderGraph.CategoryData",
                ["m_ObjectId"] = categoryObjectId,
                ["m_Name"] = categoryName,
                ["m_ChildObjectList"] = new JsonArray()
            };

            document.Objects.Add(categoryObject);
            document.ObjectsById[categoryObjectId] = categoryObject;

            var categoryArray = EnsureReferenceArray(document.Root, "m_CategoryData");
            categoryArray.Add(new JsonObject
            {
                ["m_Id"] = categoryObjectId
            });

            return categoryObject;
        }

        static void MovePropertyReferenceToCategory(
            ShaderGraphMutableDocument document,
            string propertyObjectId,
            JsonObject targetCategory,
            int? categoryIndex)
        {
            RemovePropertyReferenceFromAllCategories(document, propertyObjectId);
            var childArray = EnsureReferenceArray(targetCategory, "m_ChildObjectList");
            InsertPropertyReference(childArray, propertyObjectId, categoryIndex);
        }

        static int RemovePropertyReferenceFromAllCategories(ShaderGraphMutableDocument document, string propertyObjectId)
        {
            var removedCount = 0;
            foreach (var categoryObject in GetCategoryObjects(document))
                removedCount += RemoveReferenceFromArray(categoryObject, "m_ChildObjectList", propertyObjectId);

            return removedCount;
        }

        static void InsertPropertyReference(JsonArray childArray, string propertyObjectId, int? categoryIndex)
        {
            if (categoryIndex.HasValue && categoryIndex.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(categoryIndex), "categoryIndex must be zero or greater.");

            var reference = new JsonObject
            {
                ["m_Id"] = propertyObjectId
            };

            if (!categoryIndex.HasValue || categoryIndex.Value >= childArray.Count)
            {
                childArray.Add(reference);
                return;
            }

            childArray.Insert(categoryIndex.Value, reference);
        }

        static int RemoveReferenceFromArray(JsonObject root, string propertyName, string objectId)
        {
            if (root[propertyName] is not JsonArray array)
                return 0;

            var removedCount = 0;
            for (var i = array.Count - 1; i >= 0; i--)
            {
                if (string.Equals(array[i]?["m_Id"]?.GetValue<string>(), objectId, StringComparison.Ordinal))
                {
                    array.RemoveAt(i);
                    removedCount++;
                }
            }

            return removedCount;
        }

        static int RemoveEdgesConnectedToNodes(JsonObject root, HashSet<string> nodeObjectIds)
        {
            if (nodeObjectIds.Count == 0 || root["m_Edges"] is not JsonArray edgesArray)
                return 0;

            var removedCount = 0;
            for (var i = edgesArray.Count - 1; i >= 0; i--)
            {
                if (edgesArray[i] is not JsonObject edgeObject)
                    continue;

                var outputNodeId = GetStringAt(edgeObject, "m_OutputSlot", "m_Node", "m_Id");
                var inputNodeId = GetStringAt(edgeObject, "m_InputSlot", "m_Node", "m_Id");
                if ((outputNodeId != null && nodeObjectIds.Contains(outputNodeId))
                    || (inputNodeId != null && nodeObjectIds.Contains(inputNodeId)))
                {
                    edgesArray.RemoveAt(i);
                    removedCount++;
                }
            }

            return removedCount;
        }

        static void RemoveObjectById(ShaderGraphMutableDocument document, string objectId)
        {
            if (!document.ObjectsById.TryGetValue(objectId, out var obj))
                return;

            document.Objects.Remove(obj);
            document.ObjectsById.Remove(objectId);
        }

        static bool HasCategorySelector(string? categoryObjectId, string? categoryName)
            => !string.IsNullOrWhiteSpace(categoryObjectId) || !string.IsNullOrWhiteSpace(categoryName);

        static string GetRequiredObjectId(JsonObject obj, string label)
            => GetString(obj, "m_ObjectId")
               ?? throw new InvalidOperationException($"Resolved Shader Graph {label} object is missing m_ObjectId.");

        static List<string> BuildDeletePropertyChangedFields(int removedNodeCount, int removedEdgeCount)
        {
            var changedFields = new List<string>
            {
                "property.deleted",
                "property.categoryReferenceRemoved"
            };

            if (removedNodeCount > 0)
                changedFields.Add("node.autoRemoved");

            if (removedEdgeCount > 0)
                changedFields.Add("edge.autoRemoved");

            return changedFields;
        }

        static ShaderGraphPropertyMutationResultData BuildPropertyMutationResult(
            string assetPath,
            string propertyObjectId,
            string operation,
            List<string> changedFields,
            bool includeMessages,
            bool includeProperties)
        {
            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var updatedProperty = structure.Properties?
                .FirstOrDefault(p => string.Equals(p.ObjectId, propertyObjectId, StringComparison.Ordinal));

            return new ShaderGraphPropertyMutationResultData
            {
                Operation = operation,
                PropertyObjectId = updatedProperty?.ObjectId ?? propertyObjectId,
                PropertyReferenceName = updatedProperty?.EffectiveReferenceName,
                PropertyKind = updatedProperty?.PropertyKind,
                Property = updatedProperty,
                ChangedFields = changedFields,
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
