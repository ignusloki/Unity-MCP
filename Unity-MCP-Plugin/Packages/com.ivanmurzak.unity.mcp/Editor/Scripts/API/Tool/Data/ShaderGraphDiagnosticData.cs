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
using System.ComponentModel;

namespace AIGD
{
    public class ShaderGraphDiagnosticData
    {
        [Description("Short stable diagnostic code for filtering and automation.")]
        public string? Code { get; set; }

        [Description("Severity level (e.g. 'Info', 'Warning', 'Error').")]
        public string? Severity { get; set; }

        [Description("Human-readable diagnostic message.")]
        public string? Message { get; set; }

        [Description("Suggested next step or remediation, when available.")]
        public string? Hint { get; set; }
    }
}
