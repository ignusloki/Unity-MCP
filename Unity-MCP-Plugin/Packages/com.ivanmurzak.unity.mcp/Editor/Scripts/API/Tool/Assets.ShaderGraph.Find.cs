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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphFindToolId = "assets-shadergraph-find";

        [AiTool
        (
            AssetsShaderGraphFindToolId,
            Title = "Assets / Find Shader Graphs",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [AiSkillDescription("Find Shader Graph and Sub Graph assets by reusing Unity asset search and filtering to '.shadergraph' and '.shadersubgraph' files.")]
        [AiSkillBody("Find Shader Graph and Sub Graph assets in the project and installed packages. " +
            "The search uses Unity's asset database filter semantics and then narrows results to '.shadergraph' and '.shadersubgraph' source files.\n\n" +
            "## Inputs\n\n" +
            "- `filter` — optional Unity asset search filter.\n" +
            "- `searchInFolders` — optional folder roots under `Assets/` or `Packages/`.\n" +
            "- `maxResults` — caps returned assets.\n\n" +
            "## Notes\n\n" +
            "Use this tool before '" + AssetsShaderGraphGetDataToolId + "' when you need a valid graph asset reference.")]
        [Description("Find Shader Graph and Sub Graph assets in the project and installed packages.")]
        public List<AssetObjectRef> Find
        (
            [Description("Optional Unity asset search filter string.")]
            string? filter = null,
            [Description("Optional folder roots under 'Assets/' or 'Packages/' to limit the search.")]
            string[]? searchInFolders = null,
            [Description("Maximum number of Shader Graph assets to return.")]
            int maxResults = 10
        )
        {
            if (maxResults <= 0)
                throw new System.ArgumentException($"{nameof(maxResults)} must be greater than zero.");

            return MainThread.Instance.Run(() =>
            {
                var assetGuids = (searchInFolders?.Length ?? 0) == 0
                    ? AssetDatabase.FindAssets(filter ?? string.Empty)
                    : AssetDatabase.FindAssets(filter ?? string.Empty, searchInFolders);

                return assetGuids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(IsShaderGraphFamilyAssetPath)
                    .Take(maxResults)
                    .Select(assetPath =>
                    {
                        var assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
                        return assetObject == null ? null : new AssetObjectRef(assetObject);
                    })
                    .Where(assetRef => assetRef != null)
                    .Cast<AssetObjectRef>()
                    .ToList();
            });
        }
    }
}
