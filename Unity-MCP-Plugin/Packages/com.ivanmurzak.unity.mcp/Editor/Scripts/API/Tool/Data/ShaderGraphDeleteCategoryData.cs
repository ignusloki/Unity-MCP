/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/

#nullable enable
using System.ComponentModel;

namespace AIGD
{
    [Description("Structured input for deleting a Shader Graph blackboard category.")]
    public class ShaderGraphDeleteCategoryInput
    {
        [Description("Serialized object id of the category to delete. Optional if categoryName is provided.")]
        public string? CategoryObjectId { get; set; }

        [Description("Display name of the category to delete. Optional if categoryObjectId is provided. The default category (empty name) cannot be deleted.")]
        public string? CategoryName { get; set; }

        [Description("When true, any properties currently inside the category are moved to the default category before the category is deleted. When false, deleting a non-empty category fails loudly. Default: false.")]
        public bool? ReassignPropertiesToDefault { get; set; }
    }
}
