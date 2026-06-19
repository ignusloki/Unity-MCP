/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
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
        public const string AssetsShaderGraphDeleteCategoryToolId = "assets-shadergraph-delete-category";
        public const string AssetsShaderGraphPruneEmptyCategoriesToolId = "assets-shadergraph-prune-empty-categories";

        [AiTool
        (
            AssetsShaderGraphDeleteCategoryToolId,
            Title = "Assets / Shader Graph / Delete Category"
        )]
        [AiSkillDescription("Delete a Shader Graph blackboard category. Optionally reassigns properties currently inside it to the default category before deletion.")]
        [AiSkillBody("Delete a blackboard category in a '.shadergraph' asset.\n\n" +
            "Selection: either `CategoryObjectId` or `CategoryName` must be provided. The default category (empty name) cannot be deleted.\n\n" +
            "Non-empty categories: when `ReassignPropertiesToDefault=true`, every property inside the category is appended to the default category (created if missing) before deletion. When `ReassignPropertiesToDefault=false` (default), deleting a non-empty category fails loudly.\n\n" +
            "## Response shape\n\n" +
            "Returns: `Operation='deleteCategory'`, `CategoryObjectId`, `CategoryName`, `RemovedCategoryCount=1`, `RemovedCategoryNames`, `ReassignedPropertyCount`, `ChangedFields`, `GraphSummary`.")]
        [Description("Delete a Shader Graph blackboard category.")]
        public ShaderGraphCategoryMutationResultData DeleteCategory(
            AssetObjectRef assetRef,
            ShaderGraphDeleteCategoryInput category,
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

            if (category == null)
                throw new ArgumentNullException(nameof(category));

            return MainThread.Instance.Run(() => DeleteShaderGraphCategory(
                assetRef,
                category,
                includeStructure: includeStructure ?? false,
                includeGraph: includeGraph ?? false,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        [AiTool
        (
            AssetsShaderGraphPruneEmptyCategoriesToolId,
            Title = "Assets / Shader Graph / Prune Empty Categories"
        )]
        [AiSkillDescription("Delete every Shader Graph blackboard category whose property list is empty. The default category (empty name) is always preserved.")]
        [AiSkillBody("Sweep a '.shadergraph' asset and delete every empty blackboard category.\n\n" +
            "Skipped:\n" +
            "- The default category (empty name) is always preserved even when empty — it auto-recreates on the next property add anyway.\n\n" +
            "## Response shape\n\n" +
            "Returns: `Operation='pruneEmptyCategories'`, `RemovedCategoryCount`, `RemovedCategoryNames`, `ChangedFields`, `GraphSummary`. " +
            "`CategoryObjectId` / `CategoryName` / `Category` are null because the operation is multi-category. No re-import is performed when `RemovedCategoryCount=0`.")]
        [Description("Delete every empty blackboard category from a Shader Graph asset.")]
        public ShaderGraphCategoryMutationResultData PruneEmptyCategories(
            AssetObjectRef assetRef,
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

            return MainThread.Instance.Run(() => PruneShaderGraphEmptyCategories(
                assetRef,
                includeStructure: includeStructure ?? false,
                includeGraph: includeGraph ?? false,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphCategoryMutationResultData DeleteShaderGraphCategory(
            AssetObjectRef assetRef,
            ShaderGraphDeleteCategoryInput input,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveShaderGraphAssetPath(assetRef);
            var document = LoadMutableDocument(assetPath);
            var categoryObject = ResolveCategoryObject(
                document,
                input.CategoryObjectId,
                input.CategoryName,
                createCategoryIfMissing: false,
                allowDefaultFallback: false);

            var categoryObjectId = GetRequiredObjectId(categoryObject, "category");
            var categoryName = GetString(categoryObject, "m_Name") ?? string.Empty;

            if (string.IsNullOrEmpty(categoryName))
                throw new InvalidOperationException(
                    "The default Shader Graph blackboard category (empty name) cannot be deleted. It auto-recreates whenever a property is added.");

            var childPropertyIds = GetIdArray(categoryObject, "m_ChildObjectList").ToList();
            var reassignedPropertyCount = 0;

            if (childPropertyIds.Count > 0)
            {
                if (input.ReassignPropertiesToDefault != true)
                {
                    throw new InvalidOperationException(
                        $"Shader Graph blackboard category '{categoryName}' is not empty ({childPropertyIds.Count} properties). " +
                        $"Set ReassignPropertiesToDefault=true to move them to the default category before deletion, or move them out manually first.");
                }

                var defaultCategory = GetOrCreateDefaultCategoryObject(document);
                foreach (var propertyId in childPropertyIds)
                {
                    MovePropertyReferenceToCategory(document, propertyId, defaultCategory, categoryIndex: null);
                    reassignedPropertyCount++;
                }
            }

            RemoveCategoryFromGraph(document, categoryObjectId);

            WriteMutableDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            var graphRef = new AssetObjectRef(assetPath);
            var changedFields = new List<string> { "category.deleted" };
            if (reassignedPropertyCount > 0)
                changedFields.Add("property.categoryReassigned");

            return BuildCategoryDeletionResult(
                graphRef,
                operation: "deleteCategory",
                categoryObjectId: categoryObjectId,
                categoryName: categoryName,
                removedNames: new List<string> { categoryName },
                reassignedPropertyCount: reassignedPropertyCount,
                changedFields: changedFields,
                includeStructure,
                includeGraph,
                includeMessages,
                includeProperties);
        }

        static ShaderGraphCategoryMutationResultData PruneShaderGraphEmptyCategories(
            AssetObjectRef assetRef,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveShaderGraphAssetPath(assetRef);
            var document = LoadMutableDocument(assetPath);

            var emptyCategories = GetCategoryObjects(document)
                .Where(cat =>
                {
                    var name = GetString(cat, "m_Name") ?? string.Empty;
                    if (string.IsNullOrEmpty(name))
                        return false;
                    return GetIdArray(cat, "m_ChildObjectList").Count == 0;
                })
                .ToList();

            var removedNames = new List<string>();
            foreach (var categoryObject in emptyCategories)
            {
                var categoryObjectId = GetRequiredObjectId(categoryObject, "category");
                var categoryName = GetString(categoryObject, "m_Name") ?? string.Empty;
                RemoveCategoryFromGraph(document, categoryObjectId);
                removedNames.Add(categoryName);
            }

            var graphRef = new AssetObjectRef(assetPath);

            if (removedNames.Count == 0)
            {
                return new ShaderGraphCategoryMutationResultData
                {
                    Operation = "pruneEmptyCategories",
                    RemovedCategoryCount = 0,
                    RemovedCategoryNames = removedNames,
                    ChangedFields = new List<string>(),
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

            WriteMutableDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            return BuildCategoryDeletionResult(
                graphRef,
                operation: "pruneEmptyCategories",
                categoryObjectId: null,
                categoryName: null,
                removedNames: removedNames,
                reassignedPropertyCount: 0,
                changedFields: new List<string> { "category.pruned" },
                includeStructure,
                includeGraph,
                includeMessages,
                includeProperties);
        }

        static void RemoveCategoryFromGraph(ShaderGraphMutableDocument document, string categoryObjectId)
        {
            RemoveReferenceFromArray(document.Root, "m_CategoryData", categoryObjectId);
            RemoveObjectById(document, categoryObjectId);
        }

        static ShaderGraphCategoryMutationResultData BuildCategoryDeletionResult(
            AssetObjectRef graphRef,
            string operation,
            string? categoryObjectId,
            string? categoryName,
            List<string> removedNames,
            int reassignedPropertyCount,
            List<string> changedFields,
            bool includeStructure,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties)
        {
            return new ShaderGraphCategoryMutationResultData
            {
                Operation = operation,
                CategoryObjectId = categoryObjectId,
                CategoryName = categoryName,
                Category = null,
                ChangedFields = changedFields,
                RemovedCategoryCount = removedNames.Count,
                RemovedCategoryNames = removedNames,
                ReassignedPropertyCount = reassignedPropertyCount > 0 ? reassignedPropertyCount : (int?)null,
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
    }
}
