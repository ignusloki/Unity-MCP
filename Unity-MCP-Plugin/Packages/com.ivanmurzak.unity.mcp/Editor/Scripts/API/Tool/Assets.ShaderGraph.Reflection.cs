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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using AIGD;
using UnityEditor;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        sealed class ShaderGraphAllowlistedNodeDefinition
        {
            public string ApiName { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string TypeName { get; set; } = string.Empty;
            public float DefaultWidth { get; set; }
            public float DefaultHeight { get; set; }
        }

        sealed class ShaderGraphReflectionBindings
        {
            public Assembly ShaderGraphEditorAssembly { get; set; } = null!;
            public Type GraphDataType { get; set; } = null!;
            public Type AbstractMaterialNodeType { get; set; } = null!;
            public MethodInfo DeserializeMethod { get; set; } = null!;
            public MethodInfo SerializeMethod { get; set; } = null!;
            public MethodInfo AddNodeMethod { get; set; } = null!;
            public MethodInfo RemoveNodeMethod { get; set; } = null!;
            public MethodInfo GetNodeFromIdMethod { get; set; } = null!;
            public MethodInfo OnEnableMethod { get; set; } = null!;
            public MethodInfo ValidateGraphMethod { get; set; } = null!;
            public PropertyInfo AssetGuidProperty { get; set; } = null!;
            public PropertyInfo IsSubGraphProperty { get; set; } = null!;
            public PropertyInfo MessageManagerProperty { get; set; } = null!;
            public PropertyInfo DrawStateProperty { get; set; } = null!;
            public PropertyInfo PositionProperty { get; set; } = null!;
            public PropertyInfo ObjectIdProperty { get; set; } = null!;
            public PropertyInfo CanDeleteNodeProperty { get; set; } = null!;
        }

        sealed class ShaderGraphReflectionDocument
        {
            public string AssetPath { get; set; } = string.Empty;
            public string FullPath { get; set; } = string.Empty;
            public ShaderGraphReflectionBindings Bindings { get; set; } = null!;
            public object GraphData { get; set; } = null!;
        }

        static readonly Dictionary<string, ShaderGraphAllowlistedNodeDefinition> AllowlistedNodeDefinitions =
            CreateAllowlistedNodeDefinitions();

        static ShaderGraphReflectionBindings? s_shaderGraphReflectionBindings;

        static Dictionary<string, ShaderGraphAllowlistedNodeDefinition> CreateAllowlistedNodeDefinitions()
        {
            var definitions = new[]
            {
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "add",
                    DisplayName = "Add",
                    TypeName = "UnityEditor.ShaderGraph.AddNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "subtract",
                    DisplayName = "Subtract",
                    TypeName = "UnityEditor.ShaderGraph.SubtractNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "multiply",
                    DisplayName = "Multiply",
                    TypeName = "UnityEditor.ShaderGraph.MultiplyNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 112f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "divide",
                    DisplayName = "Divide",
                    TypeName = "UnityEditor.ShaderGraph.DivideNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "lerp",
                    DisplayName = "Lerp",
                    TypeName = "UnityEditor.ShaderGraph.LerpNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 112f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "oneMinus",
                    DisplayName = "One Minus",
                    TypeName = "UnityEditor.ShaderGraph.OneMinusNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "split",
                    DisplayName = "Split",
                    TypeName = "UnityEditor.ShaderGraph.SplitNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 132f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "combine",
                    DisplayName = "Combine",
                    TypeName = "UnityEditor.ShaderGraph.CombineNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 132f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "sampleTexture2D",
                    DisplayName = "Sample Texture 2D",
                    TypeName = "UnityEditor.ShaderGraph.SampleTexture2DNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 184f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "tilingAndOffset",
                    DisplayName = "Tiling And Offset",
                    TypeName = "UnityEditor.ShaderGraph.TilingAndOffsetNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 112f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "branch",
                    DisplayName = "Branch",
                    TypeName = "UnityEditor.ShaderGraph.BranchNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 126f
                }
            };

            return definitions.ToDictionary(
                definition => NormalizeEnumValue(definition.ApiName),
                definition => definition,
                StringComparer.Ordinal);
        }

        static ShaderGraphAllowlistedNodeDefinition ResolveAllowlistedNodeDefinition(string? nodeType)
        {
            if (string.IsNullOrWhiteSpace(nodeType))
                throw new ArgumentException("nodeType must be provided.");

            var normalized = NormalizeEnumValue(nodeType);
            if (AllowlistedNodeDefinitions.TryGetValue(normalized, out var definition))
                return definition;

            var supportedValues = string.Join(", ", AllowlistedNodeDefinitions.Values
                .Select(def => def.ApiName)
                .OrderBy(name => name, StringComparer.Ordinal));

            throw new ArgumentException(
                $"Unsupported nodeType '{nodeType}'. Supported values: {supportedValues}.");
        }

        static ShaderGraphReflectionBindings GetShaderGraphReflectionBindings()
        {
            if (s_shaderGraphReflectionBindings != null)
                return s_shaderGraphReflectionBindings;

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "Unity.ShaderGraph.Editor", StringComparison.Ordinal));
            if (assembly == null)
            {
                throw new InvalidOperationException(
                    "Unity Shader Graph editor assembly 'Unity.ShaderGraph.Editor' is not loaded. Ensure com.unity.shadergraph is installed in the current project.");
            }

            Type RequireType(string fullName)
                => assembly.GetType(fullName, throwOnError: false)
                    ?? throw new InvalidOperationException(
                        $"Required Shader Graph type '{fullName}' could not be resolved from assembly '{assembly.GetName().Name}'.");

            MethodInfo RequireMethod(Type declaringType, string methodName, params Type[] parameterTypes)
            {
                var matchingMethod = declaringType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                    .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    .Where(method => !method.IsGenericMethodDefinition)
                    .SingleOrDefault(method =>
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length != parameterTypes.Length)
                            return false;

                        for (var i = 0; i < parameters.Length; i++)
                        {
                            if (parameters[i].ParameterType != parameterTypes[i])
                                return false;
                        }

                        return true;
                    });

                return matchingMethod
                    ?? throw new InvalidOperationException(
                        $"Required Shader Graph method '{declaringType.FullName}.{methodName}' could not be resolved.");
            }

            PropertyInfo RequireProperty(Type declaringType, string propertyName)
                => declaringType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    ?? throw new InvalidOperationException(
                        $"Required Shader Graph property '{declaringType.FullName}.{propertyName}' could not be resolved.");

            var graphDataType = RequireType("UnityEditor.ShaderGraph.GraphData");
            var jsonObjectType = RequireType("UnityEditor.ShaderGraph.Serialization.JsonObject");
            var multiJsonType = RequireType("UnityEditor.ShaderGraph.Serialization.MultiJson");
            var abstractMaterialNodeType = RequireType("UnityEditor.ShaderGraph.AbstractMaterialNode");
            var deserializeMethod = multiJsonType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "Deserialize"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 4)
                .MakeGenericMethod(graphDataType);

            s_shaderGraphReflectionBindings = new ShaderGraphReflectionBindings
            {
                ShaderGraphEditorAssembly = assembly,
                GraphDataType = graphDataType,
                AbstractMaterialNodeType = abstractMaterialNodeType,
                DeserializeMethod = deserializeMethod,
                SerializeMethod = RequireMethod(multiJsonType, "Serialize", jsonObjectType),
                AddNodeMethod = RequireMethod(graphDataType, "AddNode", abstractMaterialNodeType, typeof(bool)),
                RemoveNodeMethod = RequireMethod(graphDataType, "RemoveNode", abstractMaterialNodeType),
                GetNodeFromIdMethod = RequireMethod(graphDataType, "GetNodeFromId", typeof(string)),
                OnEnableMethod = RequireMethod(graphDataType, "OnEnable"),
                ValidateGraphMethod = RequireMethod(graphDataType, "ValidateGraph"),
                AssetGuidProperty = RequireProperty(graphDataType, "assetGuid"),
                IsSubGraphProperty = RequireProperty(graphDataType, "isSubGraph"),
                MessageManagerProperty = RequireProperty(graphDataType, "messageManager"),
                DrawStateProperty = RequireProperty(abstractMaterialNodeType, "drawState"),
                PositionProperty = RequireProperty(RequireProperty(abstractMaterialNodeType, "drawState").PropertyType, "position"),
                ObjectIdProperty = RequireProperty(jsonObjectType, "objectId"),
                CanDeleteNodeProperty = RequireProperty(abstractMaterialNodeType, "canDeleteNode")
            };

            return s_shaderGraphReflectionBindings;
        }

        static ShaderGraphReflectionDocument LoadShaderGraphReflectionDocument(string assetPath)
        {
            var bindings = GetShaderGraphReflectionBindings();
            var fullPath = ResolvePhysicalAssetPath(assetPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Physical file does not exist at '{fullPath}'.", fullPath);

            var graphData = Activator.CreateInstance(bindings.GraphDataType, nonPublic: true)
                ?? throw new InvalidOperationException("Failed to create a Shader Graph GraphData instance.");

            bindings.AssetGuidProperty.SetValue(graphData, AssetDatabase.AssetPathToGUID(assetPath));
            bindings.IsSubGraphProperty.SetValue(graphData, false);
            bindings.MessageManagerProperty.SetValue(graphData, null);

            var sourceText = File.ReadAllText(fullPath);
            InvokeShaderGraphMethod(bindings.DeserializeMethod, null, graphData, sourceText, null, false);
            InvokeShaderGraphMethod(bindings.OnEnableMethod, graphData);
            InvokeShaderGraphMethod(bindings.ValidateGraphMethod, graphData);

            return new ShaderGraphReflectionDocument
            {
                AssetPath = assetPath,
                FullPath = fullPath,
                Bindings = bindings,
                GraphData = graphData
            };
        }

        static void SaveShaderGraphReflectionDocument(ShaderGraphReflectionDocument document)
        {
            var serialized = InvokeShaderGraphMethod(document.Bindings.SerializeMethod, null, document.GraphData) as string;
            if (string.IsNullOrEmpty(serialized))
                throw new InvalidOperationException("Shader Graph serialization returned an empty payload.");

            File.WriteAllText(document.FullPath, serialized!);
        }

        static void FinalizeShaderGraphMutation(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ReloadOpenShaderGraphWindows(assetPath);
            com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();
        }

        static void ReloadOpenShaderGraphWindows(string assetPath)
        {
            var bindings = GetShaderGraphReflectionBindings();
            var windowType = bindings.ShaderGraphEditorAssembly.GetType(
                "UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow",
                throwOnError: false);
            if (windowType == null)
                return;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var graphObjectProperty = windowType.GetProperty("graphObject", flags);
            var initializeMethod = windowType.GetMethod(
                "Initialize",
                flags,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);
            var selectedGuidField = windowType.GetField("m_Selected", flags);
            var selectedGuidProperty = windowType.GetProperty("selectedGuid", flags);

            if (selectedGuidProperty == null
                || selectedGuidField == null
                || graphObjectProperty == null
                || initializeMethod == null)
            {
                return;
            }

            var findObjectsMethod = typeof(Resources)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "FindObjectsOfTypeAll"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 0)
                .MakeGenericMethod(windowType);

            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var windows = (findObjectsMethod.Invoke(null, null) as Array)?.Cast<object>()
                ?? Enumerable.Empty<object>();

            foreach (var window in windows)
            {
                var selectedGuid = selectedGuidProperty.GetValue(window) as string;
                if (!string.Equals(selectedGuid, assetGuid, StringComparison.Ordinal))
                    continue;

                graphObjectProperty.SetValue(window, null);
                selectedGuidField.SetValue(window, null);
                initializeMethod.Invoke(window, new object[] { assetGuid });
            }
        }

        static object CreateShaderGraphNodeInstance(
            ShaderGraphReflectionBindings bindings,
            ShaderGraphAllowlistedNodeDefinition definition)
        {
            var nodeType = bindings.ShaderGraphEditorAssembly.GetType(definition.TypeName, throwOnError: false);
            if (nodeType == null)
            {
                throw new InvalidOperationException(
                    $"Allowlisted Shader Graph node type '{definition.TypeName}' could not be resolved.");
            }

            if (!bindings.AbstractMaterialNodeType.IsAssignableFrom(nodeType))
            {
                throw new InvalidOperationException(
                    $"Resolved type '{definition.TypeName}' is not a Shader Graph AbstractMaterialNode.");
            }

            return Activator.CreateInstance(nodeType, nonPublic: true)
                ?? throw new InvalidOperationException(
                    $"Failed to instantiate Shader Graph node type '{definition.TypeName}'.");
        }

        static void SetShaderGraphNodePosition(
            ShaderGraphReflectionBindings bindings,
            object node,
            float positionX,
            float positionY,
            ShaderGraphAllowlistedNodeDefinition definition)
        {
            var drawState = bindings.DrawStateProperty.GetValue(node)
                ?? Activator.CreateInstance(bindings.DrawStateProperty.PropertyType)
                ?? throw new InvalidOperationException("Failed to create a Shader Graph DrawState instance.");

            var currentRect = bindings.PositionProperty.GetValue(drawState) is Rect rect
                ? rect
                : new Rect(0f, 0f, definition.DefaultWidth, definition.DefaultHeight);

            currentRect.x = positionX;
            currentRect.y = positionY;

            if (currentRect.width <= 0f)
                currentRect.width = definition.DefaultWidth;
            if (currentRect.height <= 0f)
                currentRect.height = definition.DefaultHeight;

            bindings.PositionProperty.SetValue(drawState, currentRect);
            bindings.DrawStateProperty.SetValue(node, drawState);
        }

        static object ResolveShaderGraphNodeObject(
            ShaderGraphReflectionDocument document,
            string? nodeObjectIdValue)
        {
            var nodeObjectId = nodeObjectIdValue?.Trim();
            if (string.IsNullOrEmpty(nodeObjectId))
                throw new ArgumentException("nodeObjectId must be provided.");

            var nodeObject = InvokeShaderGraphMethod(document.Bindings.GetNodeFromIdMethod, document.GraphData, nodeObjectId);
            if (nodeObject == null)
                throw new InvalidOperationException($"Shader Graph node '{nodeObjectId}' was not found.");

            return nodeObject;
        }

        static string GetShaderGraphNodeObjectId(ShaderGraphReflectionBindings bindings, object node)
        {
            var objectId = bindings.ObjectIdProperty.GetValue(node) as string;
            if (string.IsNullOrEmpty(objectId))
                throw new InvalidOperationException("Shader Graph node object id was empty.");

            return objectId!;
        }

        static bool CanDeleteShaderGraphNode(ShaderGraphReflectionBindings bindings, object node)
            => bindings.CanDeleteNodeProperty.GetValue(node) is bool canDelete && canDelete;

        static int CountConnectedEdges(ShaderGraphStructureData structure, string nodeObjectId)
            => structure.Edges?.Count(edge =>
                string.Equals(edge.OutputNodeId, nodeObjectId, StringComparison.Ordinal)
                || string.Equals(edge.InputNodeId, nodeObjectId, StringComparison.Ordinal)) ?? 0;

        static object? InvokeShaderGraphMethod(MethodInfo method, object? instance, params object?[]? args)
        {
            try
            {
                return method.Invoke(instance, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
