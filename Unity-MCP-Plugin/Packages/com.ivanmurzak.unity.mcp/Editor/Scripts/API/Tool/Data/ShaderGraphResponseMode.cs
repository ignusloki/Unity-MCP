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
    [Description("Controls how much detail a Shader Graph mutation response carries. Pick the cheapest mode that still gives the agent what it needs for its next step.")]
    public enum ShaderGraphResponseMode
    {
        [Description("Default. Per-op changed-field summaries plus one consolidated post-batch GraphSummary. No structure echo.")]
        Summary = 0,

        [Description("Smallest payload. Per-op ChangedFields + ObjectId only. No GraphSummary, no structure echo. Use when the agent only needs to know which fields were touched.")]
        Diff = 1,

        [Description("Per-op summary plus a Selection projection of the structure scoped to the nodes touched by this batch. Reuses the assets-shadergraph-query-structure projection. Use when the agent must inspect what it just authored without paying for the full graph.")]
        Selection = 2,

        [Description("Per-op summary plus the full read-only ShaderGraphStructureData. Equivalent to calling assets-shadergraph-get-structure after the batch. Use only when an agent really needs the whole post-batch graph.")]
        Full = 3
    }
}
