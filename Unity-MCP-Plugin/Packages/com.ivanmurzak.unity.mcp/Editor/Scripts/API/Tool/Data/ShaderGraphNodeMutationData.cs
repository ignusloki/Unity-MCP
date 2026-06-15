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

namespace AIGD
{
    [Description("Structured input for moving an existing Shader Graph node by serialized node id.")]
    public class ShaderGraphUpdateNodePositionInput
    {
        [Description("Serialized object id of the node to move.")]
        public string? NodeObjectId { get; set; }

        [Description("New serialized X position for the node.")]
        public float? PositionX { get; set; }

        [Description("New serialized Y position for the node.")]
        public float? PositionY { get; set; }
    }

    [Description("Structured input for adding a safe allowlisted node to an existing Shader Graph.")]
    public class ShaderGraphAddPropertyNodeInput
    {
        [Description("Serialized object id of the blackboard property to expose as a node. Optional if propertyReferenceName is provided.")]
        public string? PropertyObjectId { get; set; }

        [Description("Effective property reference name to expose as a node, such as '_BaseColor'. Optional if propertyObjectId is provided.")]
        public string? PropertyReferenceName { get; set; }

        [Description("Serialized X position for the new Property node. Default: 0.")]
        public float? PositionX { get; set; }

        [Description("Serialized Y position for the new Property node. Default: 0.")]
        public float? PositionY { get; set; }
    }

    [Description("Structured input for adding a safe allowlisted Shader Graph node.")]
    public class ShaderGraphAddNodeInput
    {
        [Description("Allowlisted node type to create. Supported values: add, subtract, multiply, divide, lerp, oneMinus, split, combine, sampleTexture2D, tilingAndOffset, branch, viewDirection, viewVector, normalVector, position, object, transform, gradientNoise, screenPosition, sceneDepth, sceneColor, comparison, normalFromHeight, blend, remap, swizzle, time, smoothstep, saturate, vector2, sine, cosine, negate.")]
        public string? NodeType { get; set; }

        [Description("Serialized X position for the new node. Default: 0.")]
        public float? PositionX { get; set; }

        [Description("Serialized Y position for the new node. Default: 0.")]
        public float? PositionY { get; set; }
    }

    [Description("Structured input for duplicating a supported Shader Graph node by serialized node id.")]
    public class ShaderGraphDuplicateNodeInput
    {
        [Description("Serialized object id of the node to duplicate.")]
        public string? NodeObjectId { get; set; }

        [Description("Absolute serialized X position for the duplicate. If omitted, source position plus positionOffsetX is used.")]
        public float? PositionX { get; set; }

        [Description("Absolute serialized Y position for the duplicate. If omitted, source position plus positionOffsetY is used.")]
        public float? PositionY { get; set; }

        [Description("X offset added to the source node position when positionX is omitted. Default: 40.")]
        public float? PositionOffsetX { get; set; }

        [Description("Y offset added to the source node position when positionY is omitted. Default: 40.")]
        public float? PositionOffsetY { get; set; }
    }

    [Description("Structured input for deleting an existing Shader Graph node by serialized node id.")]
    public class ShaderGraphDeleteNodeInput
    {
        [Description("Serialized object id of the node to delete.")]
        public string? NodeObjectId { get; set; }
    }

    [Description("Structured input for updating supported serialized settings on an existing Shader Graph node.")]
    public class ShaderGraphUpdateNodeSettingsInput
    {
        [Description("Serialized object id of the node to update.")]
        public string? NodeObjectId { get; set; }

        [Description("Structured settings updates for a Sample Texture 2D node.")]
        public ShaderGraphSampleTexture2DNodeSettingsUpdateInput? SampleTexture2D { get; set; }

        [Description("Structured settings updates for a Tiling And Offset node.")]
        public ShaderGraphTilingAndOffsetNodeSettingsUpdateInput? TilingAndOffset { get; set; }

        [Description("Structured settings updates for a Branch node.")]
        public ShaderGraphBranchNodeSettingsUpdateInput? Branch { get; set; }

        [Description("Structured settings updates for a Split node.")]
        public ShaderGraphSplitNodeSettingsUpdateInput? Split { get; set; }

        [Description("Structured settings updates for a Combine node.")]
        public ShaderGraphCombineNodeSettingsUpdateInput? Combine { get; set; }

        [Description("Structured settings updates for an Add node.")]
        public ShaderGraphBinaryVectorNodeSettingsUpdateInput? Add { get; set; }

        [Description("Structured settings updates for a Subtract node.")]
        public ShaderGraphBinaryVectorNodeSettingsUpdateInput? Subtract { get; set; }

        [Description("Structured settings updates for a Divide node.")]
        public ShaderGraphBinaryVectorNodeSettingsUpdateInput? Divide { get; set; }

        [Description("Structured settings updates for a Lerp node.")]
        public ShaderGraphLerpNodeSettingsUpdateInput? Lerp { get; set; }

        [Description("Structured settings updates for a One Minus node.")]
        public ShaderGraphOneMinusNodeSettingsUpdateInput? OneMinus { get; set; }

        [Description("Structured settings updates for a Multiply node.")]
        public ShaderGraphMultiplyNodeSettingsUpdateInput? Multiply { get; set; }

        [Description("Structured settings updates for a View Direction node.")]
        public ShaderGraphSpaceNodeSettingsUpdateInput? ViewDirection { get; set; }

        [Description("Structured settings updates for a View Vector node.")]
        public ShaderGraphSpaceNodeSettingsUpdateInput? ViewVector { get; set; }

        [Description("Structured settings updates for a Normal Vector node.")]
        public ShaderGraphSpaceNodeSettingsUpdateInput? NormalVector { get; set; }

        [Description("Structured settings updates for a Position node.")]
        public ShaderGraphPositionNodeSettingsUpdateInput? Position { get; set; }

        [Description("Structured settings updates for a Transform node.")]
        public ShaderGraphTransformNodeSettingsUpdateInput? Transform { get; set; }

        [Description("Structured settings updates for a Gradient Noise node.")]
        public ShaderGraphGradientNoiseNodeSettingsUpdateInput? GradientNoise { get; set; }

        [Description("Structured settings updates for a Screen Position node.")]
        public ShaderGraphScreenPositionNodeSettingsUpdateInput? ScreenPosition { get; set; }

        [Description("Structured settings updates for a Scene Depth node.")]
        public ShaderGraphSceneDepthNodeSettingsUpdateInput? SceneDepth { get; set; }

        [Description("Structured settings updates for a Comparison node.")]
        public ShaderGraphComparisonNodeSettingsUpdateInput? Comparison { get; set; }

        [Description("Structured settings updates for a Normal From Height node.")]
        public ShaderGraphNormalFromHeightNodeSettingsUpdateInput? NormalFromHeight { get; set; }

        [Description("Structured settings updates for a Blend node.")]
        public ShaderGraphBlendNodeSettingsUpdateInput? Blend { get; set; }

        [Description("Structured settings updates for a Swizzle node.")]
        public ShaderGraphSwizzleNodeSettingsUpdateInput? Swizzle { get; set; }

        [Description("Structured settings updates for a Vector 2 node.")]
        public ShaderGraphVector2NodeSettingsUpdateInput? Vector2 { get; set; }

        [Description("Structured settings updates for a Sine node.")]
        public ShaderGraphUnaryVectorNodeSettingsUpdateInput? Sine { get; set; }

        [Description("Structured settings updates for a Cosine node.")]
        public ShaderGraphUnaryVectorNodeSettingsUpdateInput? Cosine { get; set; }

        [Description("Structured settings updates for a Negate node.")]
        public ShaderGraphUnaryVectorNodeSettingsUpdateInput? Negate { get; set; }
    }

    [Description("Structured settings updates for a Sample Texture 2D node.")]
    public class ShaderGraphSampleTexture2DNodeSettingsUpdateInput
    {
        [Description("Texture interpretation mode. Supported values: default, normal.")]
        public string? TextureType { get; set; }

        [Description("Normal map space. Supported values: tangent, object.")]
        public string? NormalMapSpace { get; set; }

        [Description("Whether Use Global Mip Bias should be enabled.")]
        public bool? UseGlobalMipBias { get; set; }

        [Description("Mip sampling mode. Supported values: standard, lod, gradient, bias.")]
        public string? MipSamplingMode { get; set; }

        [Description("Project Texture2D asset path to assign directly to the unconnected Texture input slot, such as 'Assets/Textures/BaseMap.png'. Use an empty string to clear the slot texture.")]
        public string? TextureSlotAssetPath { get; set; }

        [Description("Fallback texture type for the Texture input slot. Supported values: white, black, grey, normalMap, bump, linearGrey, red.")]
        public string? TextureSlotDefaultType { get; set; }
    }

    [Description("Structured vector2 value update.")]
    public class ShaderGraphVector2ValueUpdateInput
    {
        [Description("X component.")]
        public float? X { get; set; }

        [Description("Y component.")]
        public float? Y { get; set; }
    }

    [Description("Structured vector4 value update.")]
    public class ShaderGraphVector4ValueUpdateInput
    {
        [Description("X component.")]
        public float? X { get; set; }

        [Description("Y component.")]
        public float? Y { get; set; }

        [Description("Z component.")]
        public float? Z { get; set; }

        [Description("W component.")]
        public float? W { get; set; }
    }

    [Description("Structured settings updates for a Tiling And Offset node.")]
    public class ShaderGraphTilingAndOffsetNodeSettingsUpdateInput
    {
        [Description("Default value for the Tiling input slot.")]
        public ShaderGraphVector2ValueUpdateInput? Tiling { get; set; }

        [Description("Default value for the Offset input slot.")]
        public ShaderGraphVector2ValueUpdateInput? Offset { get; set; }
    }

    [Description("Structured settings updates for a Branch node.")]
    public class ShaderGraphBranchNodeSettingsUpdateInput
    {
        [Description("Default value for the Predicate input slot.")]
        public bool? Predicate { get; set; }

        [Description("Default value for the True input slot.")]
        public ShaderGraphVector4ValueUpdateInput? TrueValue { get; set; }

        [Description("Default value for the False input slot.")]
        public ShaderGraphVector4ValueUpdateInput? FalseValue { get; set; }
    }

    [Description("Structured settings updates for a Split node.")]
    public class ShaderGraphSplitNodeSettingsUpdateInput
    {
        [Description("Default value for the In input slot.")]
        public ShaderGraphVector4ValueUpdateInput? Input { get; set; }
    }

    [Description("Structured settings updates for a Combine node.")]
    public class ShaderGraphCombineNodeSettingsUpdateInput
    {
        [Description("Default value for the R input slot.")]
        public float? R { get; set; }

        [Description("Default value for the G input slot.")]
        public float? G { get; set; }

        [Description("Default value for the B input slot.")]
        public float? B { get; set; }

        [Description("Default value for the A input slot.")]
        public float? A { get; set; }
    }

    [Description("Structured settings updates for binary vector nodes such as Add, Subtract, and Divide.")]
    public class ShaderGraphBinaryVectorNodeSettingsUpdateInput
    {
        [Description("Default value for the A input slot.")]
        public ShaderGraphVector4ValueUpdateInput? A { get; set; }

        [Description("Default value for the B input slot.")]
        public ShaderGraphVector4ValueUpdateInput? B { get; set; }
    }

    [Description("Structured settings updates for a Lerp node.")]
    public class ShaderGraphLerpNodeSettingsUpdateInput
    {
        [Description("Default value for the A input slot.")]
        public ShaderGraphVector4ValueUpdateInput? A { get; set; }

        [Description("Default value for the B input slot.")]
        public ShaderGraphVector4ValueUpdateInput? B { get; set; }

        [Description("Default value for the T input slot.")]
        public ShaderGraphVector4ValueUpdateInput? T { get; set; }
    }

    [Description("Structured settings updates for a One Minus node.")]
    public class ShaderGraphOneMinusNodeSettingsUpdateInput
    {
        [Description("Default value for the In input slot.")]
        public ShaderGraphVector4ValueUpdateInput? Input { get; set; }
    }

    [Description("Structured settings updates for a Multiply node.")]
    public class ShaderGraphMultiplyNodeSettingsUpdateInput
    {
        [Description("Multiply mode. Supported values: vector, matrix, mixed.")]
        public string? MultiplyType { get; set; }
    }

    [Description("Structured settings updates for source-vector nodes with a space selector.")]
    public class ShaderGraphSpaceNodeSettingsUpdateInput
    {
        [Description("Coordinate space. Supported values: object, view, world, tangent. Position additionally supports absoluteWorld.")]
        public string? Space { get; set; }
    }

    [Description("Structured settings updates for a Position node.")]
    public class ShaderGraphPositionNodeSettingsUpdateInput
    {
        [Description("Coordinate space. Supported values: object, view, world, tangent, absoluteWorld.")]
        public string? Space { get; set; }

        [Description("Position source. Supported values: default, predisplacement.")]
        public string? PositionSource { get; set; }
    }

    [Description("Structured settings updates for a Transform node.")]
    public class ShaderGraphTransformNodeSettingsUpdateInput
    {
        [Description("Input coordinate space. Supported values: object, view, world, tangent, absoluteWorld, screen.")]
        public string? InputSpace { get; set; }

        [Description("Output coordinate space. Supported values: object, view, world, tangent, absoluteWorld, screen.")]
        public string? OutputSpace { get; set; }

        [Description("Transform conversion type. Supported values: position, direction, normal.")]
        public string? TransformType { get; set; }

        [Description("Whether the transform result should be normalized where the selected transform type supports it.")]
        public bool? Normalize { get; set; }
    }

    [Description("Structured settings updates for a Gradient Noise node.")]
    public class ShaderGraphGradientNoiseNodeSettingsUpdateInput
    {
        [Description("Default value for the Scale input slot.")]
        public float? Scale { get; set; }

        [Description("Gradient Noise hash type. Supported values: deterministic, legacyMod.")]
        public string? HashType { get; set; }
    }

    [Description("Structured settings updates for a Screen Position node.")]
    public class ShaderGraphScreenPositionNodeSettingsUpdateInput
    {
        [Description("Screen position mode. Supported values: default, raw.")]
        public string? Mode { get; set; }
    }

    [Description("Structured settings updates for a Scene Depth node.")]
    public class ShaderGraphSceneDepthNodeSettingsUpdateInput
    {
        [Description("Scene depth sampling mode. Supported values: linear01, raw, eye.")]
        public string? SamplingMode { get; set; }
    }

    [Description("Structured settings updates for a Comparison node.")]
    public class ShaderGraphComparisonNodeSettingsUpdateInput
    {
        [Description("Comparison operator. Supported values: equal, notEqual, less, lessOrEqual, greater, greaterOrEqual.")]
        public string? ComparisonType { get; set; }
    }

    [Description("Structured settings updates for a Normal From Height node.")]
    public class ShaderGraphNormalFromHeightNodeSettingsUpdateInput
    {
        [Description("Output space. Supported values: tangent, world.")]
        public string? OutputSpace { get; set; }
    }

    [Description("Structured settings updates for a Blend node.")]
    public class ShaderGraphBlendNodeSettingsUpdateInput
    {
        [Description("Blend mode. Supported values: burn, darken, difference, dodge, divide, exclusion, hardLight, hardMix, lighten, linearBurn, linearDodge, linearLight, linearLightAddSub, multiply, negation, overlay, pinLight, screen, softLight, subtract, vividLight, overwrite.")]
        public string? BlendMode { get; set; }
    }

    [Description("Structured settings updates for a Swizzle node.")]
    public class ShaderGraphSwizzleNodeSettingsUpdateInput
    {
        [Description("Swizzle mask. Use 1-4 characters from either xyzw or rgba. Mixed notation such as 'xg' is rejected.")]
        public string? Mask { get; set; }
    }

    [Description("Structured settings updates for a Vector 2 node.")]
    public class ShaderGraphVector2NodeSettingsUpdateInput
    {
        [Description("Default value for the X input slot.")]
        public float? X { get; set; }

        [Description("Default value for the Y input slot.")]
        public float? Y { get; set; }
    }

    [Description("Structured settings updates for unary vector nodes such as Sine, Cosine, and Negate.")]
    public class ShaderGraphUnaryVectorNodeSettingsUpdateInput
    {
        [Description("Default value for the In input slot.")]
        public ShaderGraphVector4ValueUpdateInput? Input { get; set; }
    }

    [Description("Result of mutating a Shader Graph node and re-importing the graph.")]
    public class ShaderGraphNodeMutationResultData
    {
        [Description("Stable operation identifier such as add, addPropertyNode, duplicate, delete, updatePosition, or updateSettings.")]
        public string? Operation { get; set; }

        [Description("Serialized object id of the affected node. For delete operations this is the deleted node id.")]
        public string? NodeObjectId { get; set; }

        [Description("Serialized type of the affected node, when resolved.")]
        public string? NodeType { get; set; }

        [Description("Snapshot of the affected node. For add and duplicate operations this is the created node after import; for delete operations this is the deleted node before removal.")]
        public ShaderGraphNodeDefinitionData? Node { get; set; }

        [Description("Updated read-only graph structure after the mutation.")]
        public ShaderGraphStructureData? Structure { get; set; }

        [Description("Post-import Shader Graph summary and diagnostics.")]
        public ShaderGraphData? Graph { get; set; }

        [Description("Number of connected edges that were removed automatically while deleting the node, if applicable.")]
        public int? RemovedEdgeCount { get; set; }

        [Description("List of node fields that actually changed.")]
        public List<string>? ChangedFields { get; set; }
    }
}
