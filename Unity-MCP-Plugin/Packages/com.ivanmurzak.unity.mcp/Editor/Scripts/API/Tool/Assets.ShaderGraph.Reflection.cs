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
                    ApiName = "power",
                    DisplayName = "Power",
                    TypeName = "UnityEditor.ShaderGraph.PowerNode",
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
                    ApiName = "fraction",
                    DisplayName = "Fraction",
                    TypeName = "UnityEditor.ShaderGraph.FractionNode",
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
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "viewDirection",
                    DisplayName = "View Direction",
                    TypeName = "UnityEditor.ShaderGraph.ViewDirectionNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "viewVector",
                    DisplayName = "View Vector",
                    TypeName = "UnityEditor.ShaderGraph.ViewVectorNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "normalVector",
                    DisplayName = "Normal Vector",
                    TypeName = "UnityEditor.ShaderGraph.NormalVectorNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "position",
                    DisplayName = "Position",
                    TypeName = "UnityEditor.ShaderGraph.PositionNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "object",
                    DisplayName = "Object",
                    TypeName = "UnityEditor.ShaderGraph.ObjectNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 132f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "transform",
                    DisplayName = "Transform",
                    TypeName = "UnityEditor.ShaderGraph.TransformNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "gradientNoise",
                    DisplayName = "Gradient Noise",
                    TypeName = "UnityEditor.ShaderGraph.GradientNoiseNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 112f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "simpleNoise",
                    DisplayName = "Simple Noise",
                    TypeName = "UnityEditor.ShaderGraph.NoiseNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 112f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "uv",
                    DisplayName = "UV",
                    TypeName = "UnityEditor.ShaderGraph.UVNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 144f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "screenPosition",
                    DisplayName = "Screen Position",
                    TypeName = "UnityEditor.ShaderGraph.ScreenPositionNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "sceneDepth",
                    DisplayName = "Scene Depth",
                    TypeName = "UnityEditor.ShaderGraph.SceneDepthNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "camera",
                    DisplayName = "Camera",
                    TypeName = "UnityEditor.ShaderGraph.CameraNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 232f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "sceneColor",
                    DisplayName = "Scene Color",
                    TypeName = "UnityEditor.ShaderGraph.SceneColorNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "comparison",
                    DisplayName = "Comparison",
                    TypeName = "UnityEditor.ShaderGraph.ComparisonNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 136f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "normalFromHeight",
                    DisplayName = "Normal From Height",
                    TypeName = "UnityEditor.ShaderGraph.NormalFromHeightNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 184f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "blend",
                    DisplayName = "Blend",
                    TypeName = "UnityEditor.ShaderGraph.BlendNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 200f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "remap",
                    DisplayName = "Remap",
                    TypeName = "UnityEditor.ShaderGraph.RemapNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 184f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "swizzle",
                    DisplayName = "Swizzle",
                    TypeName = "UnityEditor.ShaderGraph.SwizzleNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 124f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "time",
                    DisplayName = "Time",
                    TypeName = "UnityEditor.ShaderGraph.TimeNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 132f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "smoothstep",
                    DisplayName = "Smoothstep",
                    TypeName = "UnityEditor.ShaderGraph.SmoothstepNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 112f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "step",
                    DisplayName = "Step",
                    TypeName = "UnityEditor.ShaderGraph.StepNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "saturate",
                    DisplayName = "Saturate",
                    TypeName = "UnityEditor.ShaderGraph.SaturateNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "exponential",
                    DisplayName = "Exponential",
                    TypeName = "UnityEditor.ShaderGraph.ExponentialNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "invertColors",
                    DisplayName = "Invert Colors",
                    TypeName = "UnityEditor.ShaderGraph.InvertColorsNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 160f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "vector2",
                    DisplayName = "Vector 2",
                    TypeName = "UnityEditor.ShaderGraph.Vector2Node",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "sine",
                    DisplayName = "Sine",
                    TypeName = "UnityEditor.ShaderGraph.SineNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "cosine",
                    DisplayName = "Cosine",
                    TypeName = "UnityEditor.ShaderGraph.CosineNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "negate",
                    DisplayName = "Negate",
                    TypeName = "UnityEditor.ShaderGraph.NegateNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 80f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "fresnelEffect",
                    DisplayName = "Fresnel Effect",
                    TypeName = "UnityEditor.ShaderGraph.FresnelEffectNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "reciprocal",
                    DisplayName = "Reciprocal",
                    TypeName = "UnityEditor.ShaderGraph.ReciprocalNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
                },
                new ShaderGraphAllowlistedNodeDefinition
                {
                    ApiName = "subGraph",
                    DisplayName = "Sub Graph",
                    TypeName = "UnityEditor.ShaderGraph.SubGraphNode",
                    DefaultWidth = 208f,
                    DefaultHeight = 96f
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

            var normalized = NormalizeEnumValue(nodeType!);
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
            bindings.IsSubGraphProperty.SetValue(graphData, IsSubGraphAssetPath(assetPath));
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

        static void WireSubGraphNodeAsset(
            ShaderGraphReflectionBindings bindings,
            object subGraphNodeObject,
            string? subGraphAssetPath,
            string? subGraphAssetGuid)
        {
            if (string.IsNullOrEmpty(subGraphAssetPath) && string.IsNullOrEmpty(subGraphAssetGuid))
                throw new ArgumentException("When nodeType is 'subGraph', either SubGraphAssetPath or SubGraphAssetGuid must be provided.");

            string resolvedPath;
            if (!string.IsNullOrEmpty(subGraphAssetPath))
            {
                resolvedPath = subGraphAssetPath!;
            }
            else
            {
                resolvedPath = AssetDatabase.GUIDToAssetPath(subGraphAssetGuid!);
                if (string.IsNullOrEmpty(resolvedPath))
                    throw new ArgumentException($"No asset found for SubGraphAssetGuid '{subGraphAssetGuid}'.");
            }

            if (!IsSubGraphAssetPath(resolvedPath))
                throw new ArgumentException($"SubGraphAssetPath must point to a '.shadersubgraph' file, got '{resolvedPath}'.");

            var subGraphAsset = AssetDatabase.LoadMainAssetAtPath(resolvedPath);
            if (subGraphAsset == null)
                throw new ArgumentException($"Failed to load SubGraphAsset at '{resolvedPath}'. Ensure the file exists and has been imported.");

            var subGraphNodeType = subGraphNodeObject.GetType();
            var assetProperty = subGraphNodeType.GetProperty("asset",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (assetProperty == null)
                throw new InvalidOperationException("Could not resolve 'asset' property on SubGraphNode.");

            assetProperty.SetValue(subGraphNodeObject, subGraphAsset);
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

        static void FinalizeShaderGraphExternalDiskWrite(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
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
