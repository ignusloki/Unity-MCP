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
using System.IO;
using System.Linq;
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphBatchToolId = "assets-shadergraph-batch";

        [AiTool
        (
            AssetsShaderGraphBatchToolId,
            Title = "Assets / Shader Graph / Batch"
        )]
        [AiSkillDescription("Apply an ordered list of Shader Graph mutation operations against one '.shadergraph' asset in a single MCP call. Reduces per-op round-trips and supports batch-local aliases.")]
        [AiSkillBody("Apply an ordered list of Shader Graph mutation operations to a '.shadergraph' or '.shadersubgraph' asset in one MCP round-trip.\n\n" +
            "Supported operation kinds:\n" +
            "- `addNode`\n" +
            "- `updateNodeSettings` (accepts a `Node` selector: Alias / DisplayName / ObjectId)\n" +
            "- `deleteNode`\n" +
            "- `addProperty`\n" +
            "- `updateProperty` (accepts a `Property` selector: Alias / ReferenceName / DisplayName / ObjectId)\n" +
            "- `deleteProperty`\n" +
            "- `addPropertyNode` (accepts a `Property` selector)\n" +
            "- `connectEdge`\n" +
            "- `updateNodePosition`\n" +
            "- `setSettings`\n" +
            "- `setBlocks`\n" +
            "- `setOutputs` (Sub Graph only — declares the output port contract of SubGraphOutputNode)\n\n" +
            "## Aliases\n\n" +
            "Each `addNode`, `addProperty`, and `addPropertyNode` envelope accepts an optional `Alias`. Aliases are batch-local and let later ops reference the newly created object without serialized ids:\n\n" +
            "- `ConnectEdge.OutputSlot.Node.Alias = \"noise\"` matches an earlier `addNode` envelope with `Alias=\"noise\"`.\n" +
            "- `AddPropertyNode.PropertyReferenceName` still works; `AddPropertyNode.Node` may resolve a property by alias once the linked PropertyRef field is wired in a later slice.\n\n" +
            "## Slot references\n\n" +
            "`ConnectEdge` supports the same `OutputSlot` / `InputSlot` reference shape as the single-op `assets-shadergraph-connect-edge`: `Node` (Alias / DisplayName / ObjectId) + slot `DisplayName`. The resolver looks up the serialized ids against the current graph plus the batch alias bag.\n\n" +
            "## Atomicity\n\n" +
            "When `stopOnError=true` (default) the batch snapshots the `.shadergraph` file before running. If any op throws, the snapshot is restored, the asset is re-imported, and the failure is surfaced with the failing op's index. `stopOnError=false` persists whatever succeeded and surfaces per-op errors for the rest of the batch.\n\n" +
            "## Response\n\n" +
            "Returns one consolidated `ShaderGraphBatchResultData` carrying per-op summaries (operation tag, ObjectId, ChangedFields, error), the alias map, and a post-batch view scoped by `ResponseMode`:\n\n" +
            "- `Summary` (default) — per-op summaries plus one post-batch `GraphSummary`.\n" +
            "- `Diff` — per-op `ChangedFields` + `ObjectId` only. No `GraphSummary`, no structure echo. Cheapest payload.\n" +
            "- `Selection` — per-op summaries plus a `Selection` projection scoped to the nodes touched by this batch (same shape as `assets-shadergraph-query-structure`).\n" +
            "- `Full` — per-op summaries plus the full read-only `Structure` block (equivalent to calling `assets-shadergraph-get-structure` after the batch).")]
        [Description("Apply an ordered list of Shader Graph mutation operations in one MCP call.")]
        public ShaderGraphBatchResultData Batch(
            AssetObjectRef assetRef,
            ShaderGraphBatchInput batch)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (batch == null)
                throw new ArgumentNullException(nameof(batch));

            return MainThread.Instance.Run(() => RunShaderGraphBatch(assetRef, batch));
        }

        static ShaderGraphBatchResultData RunShaderGraphBatch(
            AssetObjectRef assetRef,
            ShaderGraphBatchInput batch)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphFamilyAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var operations = batch.Operations ?? new List<ShaderGraphBatchOperationInput>();
            var stopOnError = batch.StopOnError ?? true;

            var fullPath = ResolvePhysicalAssetPath(assetPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Physical file does not exist at '{fullPath}'.", fullPath);

            // Batch rollback must be anchored to the file that is actually on disk at
            // batch entry, not to any Shader Graph object Unity may still have cached.
            FinalizeShaderGraphExternalDiskWrite(assetPath);

            var snapshot = File.ReadAllBytes(fullPath);
            var snapshotHash = ComputeBatchSnapshotHash(snapshot);
            // E-1 fingerprint: capture pre-batch counts so the rollback message can ship them. If the
            // graph fails to parse here we still want the batch to run and (likely) fail with a clearer
            // per-op error, so any exception in the summary build is swallowed and we ship "unknown"
            // counts in the suffix instead.
            ShaderGraphSummaryData? preBatchSummary = null;
            try { preBatchSummary = BuildShaderGraphSummary(assetRef); }
            catch { /* surface via fingerprint suffix below */ }

            var aliases = new ShaderGraphAliasBag();
            var results = new List<ShaderGraphBatchOperationResultData>(operations.Count);
            var completed = 0;
            var anyFailure = false;
            Exception? firstFailure = null;
            int firstFailureIndex = -1;

            for (var i = 0; i < operations.Count; i++)
            {
                var op = operations[i];
                var result = new ShaderGraphBatchOperationResultData
                {
                    Index = i,
                    Kind = op.Kind,
                    Alias = op.Alias,
                    Success = false
                };

                try
                {
                    ExecuteBatchOperation(assetRef, op, aliases, result);
                    result.Success = true;
                    completed++;
                }
                catch (Exception ex)
                {
                    anyFailure = true;
                    result.Success = false;
                    result.Error = $"{ex.GetType().Name}: {ex.Message}";

                    if (firstFailure == null)
                    {
                        firstFailure = ex;
                        firstFailureIndex = i;
                    }

                    if (stopOnError)
                    {
                        results.Add(result);
                        break;
                    }
                }

                results.Add(result);
            }

            if (!anyFailure || !stopOnError)
                FinalizeShaderGraphMutation(assetPath);

            if (anyFailure && stopOnError && firstFailure != null)
            {
                string rollbackNote;
                try
                {
                    File.WriteAllBytes(fullPath, snapshot);
                    // Bump mtime so Unity's importer treats the restored content as a fresh write.
                    // Without this, the importer can short-circuit on equal mtime and keep its cached
                    // GraphData, which surfaces as "property already exists" on a clean retry.
                    File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow);
                    // Do not call SaveAssets here: a dirty Shader Graph editor window can flush stale
                    // GraphData over the restored bytes. Import/reload directly from disk instead.
                    FinalizeShaderGraphExternalDiskWrite(assetPath);

                    // E-2 hash verification: confirm the on-disk bytes match the snapshot we just
                    // wrote. If something stomped the file between WriteAllBytes and the import (most
                    // likely a dirty Shader Graph window getting saved by AssetDatabase.SaveAssets()),
                    // we downgrade the reassuring wording.
                    var postRollbackBytes = File.ReadAllBytes(fullPath);
                    var postRollbackHash = ComputeBatchSnapshotHash(postRollbackBytes);
                    rollbackNote = string.Equals(snapshotHash, postRollbackHash, StringComparison.Ordinal)
                        ? $"Asset rolled back to exact pre-batch disk content; retries are safe. Snapshot hash {snapshotHash}."
                        : $"WARNING: rollback verification FAILED. Snapshot hash {snapshotHash} but on-disk hash {postRollbackHash} after restore. " +
                          $"Something stomped the restored bytes (most likely a dirty Shader Graph editor window). Close the open Shader Graph editor before retrying.";
                }
                catch (Exception rollbackEx)
                {
                    rollbackNote =
                        $"WARNING: rollback failed to fully restore '{assetPath}': {rollbackEx.GetType().Name}: {rollbackEx.Message}. " +
                        $"The on-disk snapshot was written, but Unity may still hold stale state — close and reopen the asset before retrying.";
                }

                // E-1 fingerprint suffix: ship the pre-batch counts so the caller can spot prior-run
                // contamination by comparing against their own post-failure readback.
                var fingerprint = preBatchSummary == null
                    ? "Pre-batch fingerprint: unavailable (graph failed to summarize at batch start)."
                    : $"Pre-batch fingerprint: NodeCount={preBatchSummary.NodeCount}, EdgeCount={preBatchSummary.EdgeCount}, ShaderResolved={preBatchSummary.ShaderResolved}. " +
                      "If a post-failure readback shows a different shape than this fingerprint, the batch you just ran was not the first run against this asset and the rollback faithfully restored the prior-run state.";

                throw new InvalidOperationException(
                    $"Shader Graph batch aborted at op[{firstFailureIndex}] ({operations[firstFailureIndex].Kind}). " +
                    $"{rollbackNote} {fingerprint} Underlying error: {firstFailure.Message}",
                    firstFailure);
            }

            var graphRef = new AssetObjectRef(assetPath);
            var responseMode = batch.ResponseMode ?? ShaderGraphResponseMode.Summary;

            var batchResult = new ShaderGraphBatchResultData
            {
                Operations = results,
                AliasMap = aliases.Nodes.Concat(aliases.Properties)
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
                CompletedOperationCount = completed,
                Success = !anyFailure,
                ResponseMode = responseMode
            };

            ApplyBatchResponseMode(batchResult, graphRef, responseMode, results);
            return batchResult;
        }

        static void ApplyBatchResponseMode(
            ShaderGraphBatchResultData batchResult,
            AssetObjectRef graphRef,
            ShaderGraphResponseMode mode,
            List<ShaderGraphBatchOperationResultData> results)
        {
            if (mode == ShaderGraphResponseMode.Diff)
                return;

            batchResult.GraphSummary = BuildShaderGraphSummary(graphRef);

            if (mode == ShaderGraphResponseMode.Summary)
                return;

            var fullStructure = BuildShaderGraphStructureData(graphRef);

            if (mode == ShaderGraphResponseMode.Full)
            {
                batchResult.Structure = fullStructure;
                return;
            }

            if (mode == ShaderGraphResponseMode.Selection)
            {
                var touchedIds = results
                    .Where(r => r.Success && !string.IsNullOrEmpty(r.ObjectId))
                    .Select(r => r.ObjectId!)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var query = new ShaderGraphQueryStructureInput
                {
                    NodeObjectIds = touchedIds,
                    EdgesTouchingNodeIds = touchedIds,
                    IncludeTargets = false
                };
                batchResult.Selection = ProjectQueryStructure(fullStructure, query);
            }
        }

        static void ExecuteBatchOperation(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            var kind = NormalizeEnumValue(op.Kind ?? string.Empty);
            switch (kind)
            {
                case "addnode":
                    RunBatchAddNode(assetRef, op, aliases, result);
                    break;
                case "updatenodesettings":
                    RunBatchUpdateNodeSettings(assetRef, op, aliases, result);
                    break;
                case "deletenode":
                    RunBatchDeleteNode(assetRef, op, aliases, result);
                    break;
                case "addproperty":
                    RunBatchAddProperty(assetRef, op, aliases, result);
                    break;
                case "updateproperty":
                    RunBatchUpdateProperty(assetRef, op, aliases, result);
                    break;
                case "deleteproperty":
                    RunBatchDeleteProperty(assetRef, op, aliases, result);
                    break;
                case "addpropertynode":
                    RunBatchAddPropertyNode(assetRef, op, aliases, result);
                    break;
                case "connectedge":
                    RunBatchConnectEdge(assetRef, op, aliases, result);
                    break;
                case "updatenodeposition":
                    RunBatchUpdateNodePosition(assetRef, op, aliases, result);
                    break;
                case "setsettings":
                    RunBatchSetSettings(assetRef, op, result);
                    break;
                case "setblocks":
                    RunBatchSetBlocks(assetRef, op, result);
                    break;
                case "setoutputs":
                    RunBatchSetOutputs(assetRef, op, result);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unsupported batch operation kind '{op.Kind}'. Supported values: addNode, updateNodeSettings, deleteNode, addProperty, updateProperty, deleteProperty, addPropertyNode, connectEdge, updateNodePosition, setSettings, setBlocks, setOutputs.");
            }
        }

        static void RunBatchAddNode(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.AddNode == null)
                throw new ArgumentException("op.AddNode payload is required for kind=addNode.");

            var addResult = AddShaderGraphNode(
                assetRef,
                op.AddNode,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = addResult.Operation;
            result.ObjectId = addResult.NodeObjectId;
            result.ChangedFields = addResult.ChangedFields;

            if (!string.IsNullOrWhiteSpace(op.Alias) && !string.IsNullOrEmpty(addResult.NodeObjectId))
                aliases.Nodes[op.Alias!.Trim()] = addResult.NodeObjectId!;
        }

        static void RunBatchUpdateNodeSettings(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.UpdateNodeSettings == null)
                throw new ArgumentException("op.UpdateNodeSettings payload is required for kind=updateNodeSettings.");

            ResolveSettingsTarget(op.UpdateNodeSettings, assetRef, aliases);

            var updateResult = UpdateShaderGraphNodeSettings(
                assetRef,
                op.UpdateNodeSettings,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = updateResult.Operation;
            result.ObjectId = updateResult.NodeObjectId;
            result.ChangedFields = updateResult.ChangedFields;
        }

        static void RunBatchDeleteNode(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.DeleteNode == null)
                throw new ArgumentException("op.DeleteNode payload is required for kind=deleteNode.");

            ResolveDeleteNodeTarget(op.DeleteNode, assetRef, aliases);

            var deleteResult = DeleteShaderGraphNode(
                assetRef,
                op.DeleteNode,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = deleteResult.Operation;
            result.ObjectId = deleteResult.NodeObjectId;
            result.ChangedFields = deleteResult.ChangedFields;
        }

        static void RunBatchAddProperty(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.AddProperty == null)
                throw new ArgumentException("op.AddProperty payload is required for kind=addProperty.");

            var addResult = AddShaderGraphProperty(
                assetRef,
                op.AddProperty,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = addResult.Operation;
            result.ObjectId = addResult.PropertyObjectId;
            result.ChangedFields = addResult.ChangedFields;

            if (!string.IsNullOrWhiteSpace(op.Alias) && !string.IsNullOrEmpty(addResult.PropertyObjectId))
                aliases.Properties[op.Alias!.Trim()] = addResult.PropertyObjectId!;
        }

        static void RunBatchUpdateProperty(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.UpdateProperty == null)
                throw new ArgumentException("op.UpdateProperty payload is required for kind=updateProperty.");

            ResolvePropertyUpdateTarget(op.UpdateProperty, assetRef, aliases);

            var updateResult = UpdateShaderGraphProperty(
                assetRef,
                op.UpdateProperty,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = updateResult.Operation;
            result.ObjectId = updateResult.PropertyObjectId;
            result.ChangedFields = updateResult.ChangedFields;
        }

        static void RunBatchDeleteProperty(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.DeleteProperty == null)
                throw new ArgumentException("op.DeleteProperty payload is required for kind=deleteProperty.");

            ResolveDeletePropertyTarget(op.DeleteProperty, assetRef, aliases);

            var deleteResult = DeleteShaderGraphProperty(
                assetRef,
                op.DeleteProperty,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = deleteResult.Operation;
            result.ObjectId = deleteResult.PropertyObjectId;
            result.ChangedFields = deleteResult.ChangedFields;
        }

        static void RunBatchAddPropertyNode(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.AddPropertyNode == null)
                throw new ArgumentException("op.AddPropertyNode payload is required for kind=addPropertyNode.");

            ResolveAddPropertyNodeTarget(op.AddPropertyNode, assetRef, aliases);

            if (string.IsNullOrWhiteSpace(op.AddPropertyNode.PropertyObjectId)
                && string.IsNullOrWhiteSpace(op.AddPropertyNode.PropertyReferenceName))
            {
                throw new ArgumentException(
                    "addPropertyNode requires PropertyObjectId, PropertyReferenceName, or a Property selector (Alias / ReferenceName / DisplayName / ObjectId).");
            }

            var addResult = AddShaderGraphPropertyNode(
                assetRef,
                op.AddPropertyNode,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = addResult.Operation;
            result.ObjectId = addResult.NodeObjectId;
            result.ChangedFields = addResult.ChangedFields;

            if (!string.IsNullOrWhiteSpace(op.Alias) && !string.IsNullOrEmpty(addResult.NodeObjectId))
                aliases.Nodes[op.Alias!.Trim()] = addResult.NodeObjectId!;
        }

        static void RunBatchConnectEdge(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.ConnectEdge == null)
                throw new ArgumentException("op.ConnectEdge payload is required for kind=connectEdge.");

            ApplyConnectEdgeSlotRefs(op.ConnectEdge, new AssetObjectRef(ResolveAssetPath(assetRef)), aliases);

            var connectResult = ConnectShaderGraphEdge(
                assetRef,
                op.ConnectEdge,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = "connect";
            result.ChangedFields = connectResult.ChangedFields;
            result.ObjectId = null;
        }

        static void RunBatchUpdateNodePosition(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphAliasBag aliases,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.UpdateNodePosition == null)
                throw new ArgumentException("op.UpdateNodePosition payload is required for kind=updateNodePosition.");

            ResolveUpdatePositionTarget(op.UpdateNodePosition, assetRef, aliases);

            var moveResult = UpdateShaderGraphNodePosition(
                assetRef,
                op.UpdateNodePosition,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = moveResult.Operation;
            result.ObjectId = moveResult.NodeObjectId;
            result.ChangedFields = moveResult.ChangedFields;
        }

        static void RunBatchSetSettings(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.SetSettings == null)
                throw new ArgumentException("op.SetSettings payload is required for kind=setSettings.");

            var settingsResult = UpdateShaderGraphSettings(
                assetRef,
                op.SetSettings,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = "setSettings";
            result.ObjectId = null;
            result.ChangedFields = settingsResult.ChangedFields;
        }

        static void RunBatchSetBlocks(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.SetBlocks == null)
                throw new ArgumentException("op.SetBlocks payload is required for kind=setBlocks.");

            var blocksResult = SetShaderGraphBlocks(
                assetRef,
                op.SetBlocks,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = "setBlocks";
            result.ObjectId = null;
            result.ChangedFields = blocksResult.ChangedFields;
        }

        static void RunBatchSetOutputs(
            AssetObjectRef assetRef,
            ShaderGraphBatchOperationInput op,
            ShaderGraphBatchOperationResultData result)
        {
            if (op.SetOutputs == null)
                throw new ArgumentException("op.SetOutputs payload is required for kind=setOutputs.");

            var outputsResult = SetSubGraphOutputs(
                assetRef,
                op.SetOutputs,
                includeStructure: false,
                includeGraph: false,
                includeMessages: false,
                includeProperties: false,
                deferImport: true);

            result.Operation = "setOutputs";
            result.ObjectId = null;
            result.ChangedFields = outputsResult.ChangedFields;
        }

        // ---- Resolution helpers ----
        //
        // Each existing single-op DTO has a *ObjectId selector field. For the batch we accept
        // either a real serialized object id OR a batch-local alias in that same string field.
        // The helpers below swap an alias to the resolved id before the downstream single-op
        // helper sees the DTO. If the string is already a real id (or empty), the swap is a no-op.

        static void ResolveSettingsTarget(
            ShaderGraphUpdateNodeSettingsInput settings,
            AssetObjectRef assetRef,
            ShaderGraphAliasBag aliases)
        {
            if (settings.Node != null)
            {
                var structure = BuildShaderGraphStructureData(assetRef);
                settings.NodeObjectId = ResolveNodeObjectId(settings.Node, structure, aliases, "updateNodeSettings.node");
                settings.Node = null;
                return;
            }
            settings.NodeObjectId = ResolveNodeIdOrAlias(settings.NodeObjectId, aliases);
        }

        static void ResolveDeleteNodeTarget(
            ShaderGraphDeleteNodeInput input,
            AssetObjectRef assetRef,
            ShaderGraphAliasBag aliases)
            => input.NodeObjectId = ResolveNodeIdOrAlias(input.NodeObjectId, aliases);

        static void ResolveUpdatePositionTarget(
            ShaderGraphUpdateNodePositionInput input,
            AssetObjectRef assetRef,
            ShaderGraphAliasBag aliases)
            => input.NodeObjectId = ResolveNodeIdOrAlias(input.NodeObjectId, aliases);

        static void ResolvePropertyUpdateTarget(
            ShaderGraphPropertyUpdateInput input,
            AssetObjectRef assetRef,
            ShaderGraphAliasBag aliases)
        {
            if (input.Property != null)
            {
                var structure = BuildShaderGraphStructureData(assetRef);
                input.PropertyObjectId = ResolvePropertyObjectId(input.Property, structure, aliases, "updateProperty.property");
                input.Property = null;
                return;
            }
            input.PropertyObjectId = ResolvePropertyIdOrAlias(input.PropertyObjectId, aliases);
        }

        static void ResolveDeletePropertyTarget(
            ShaderGraphDeletePropertyInput input,
            AssetObjectRef assetRef,
            ShaderGraphAliasBag aliases)
            => input.PropertyObjectId = ResolvePropertyIdOrAlias(input.PropertyObjectId, aliases);

        static void ResolveAddPropertyNodeTarget(
            ShaderGraphAddPropertyNodeInput input,
            AssetObjectRef assetRef,
            ShaderGraphAliasBag aliases)
        {
            if (input.Property != null)
            {
                var structure = BuildShaderGraphStructureData(assetRef);
                input.PropertyObjectId = ResolvePropertyObjectId(input.Property, structure, aliases, "addPropertyNode.property");
                input.Property = null;
                return;
            }
            input.PropertyObjectId = ResolvePropertyIdOrAlias(input.PropertyObjectId, aliases);
        }

        static string? ResolveNodeIdOrAlias(string? value, ShaderGraphAliasBag aliases)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            var trimmed = StripAliasPrefix(value!.Trim());
            return aliases.Nodes.TryGetValue(trimmed, out var resolved) ? resolved : trimmed;
        }

        static string? ResolvePropertyIdOrAlias(string? value, ShaderGraphAliasBag aliases)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            var trimmed = StripAliasPrefix(value!.Trim());
            return aliases.Properties.TryGetValue(trimmed, out var resolved) ? resolved : trimmed;
        }

        // Accept either a bare alias ("noise") or the agent-friendly "@noise" form. Both resolve
        // against the in-batch alias bag; non-aliased strings fall through untouched.
        static string StripAliasPrefix(string value)
            => value.StartsWith("@", StringComparison.Ordinal) ? value.Substring(1) : value;

        // Short MD5 fingerprint used for batch rollback verification (E-2). 12 hex chars is enough
        // to disambiguate two snapshots of the same .shadergraph file in human-readable messages.
        static string ComputeBatchSnapshotHash(byte[] data)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(data);
            var sb = new System.Text.StringBuilder(12);
            for (var i = 0; i < 6 && i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
