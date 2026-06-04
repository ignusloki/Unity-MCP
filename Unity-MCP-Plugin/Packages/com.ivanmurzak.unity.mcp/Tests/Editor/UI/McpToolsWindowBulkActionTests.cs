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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Unity.MCP.Editor.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace com.IvanMurzak.Unity.MCP.Editor.Tests
{
    [TestFixture]
    public class McpToolsWindowBulkActionTests : BaseTest
    {
        [UnityTest]
        public IEnumerator BulkActionButtons_DisableWhenFilterHasNoMatches()
        {
            var window = ScriptableObject.CreateInstance<McpToolsWindow>();

            try
            {
                window.CreateGUI();
                yield return null;

                var enableButton = window.rootVisualElement.Q<Button>("btn-enable-filtered");
                var disableButton = window.rootVisualElement.Q<Button>("btn-disable-filtered");
                var filterField = window.rootVisualElement.Q<TextField>("filter-textfield");
                var listView = window.rootVisualElement.Q<ListView>("mcp-list-view");

                Assert.IsNotNull(enableButton, "Enable Filtered button should exist.");
                Assert.IsNotNull(disableButton, "Disable Filtered button should exist.");
                Assert.IsNotNull(filterField, "Filter text field should exist.");
                Assert.IsNotNull(listView, "Tools list view should exist.");

                var itemsSource = listView!.itemsSource as IList;
                Assert.IsNotNull(itemsSource, "List view should have an items source after GUI creation.");
                Assert.Greater(itemsSource!.Count, 0, "This test expects at least one registered tool.");
                Assert.IsTrue(enableButton!.enabledSelf, "Enable Filtered should start enabled when tools are visible.");
                Assert.IsTrue(disableButton!.enabledSelf, "Disable Filtered should start enabled when tools are visible.");

                filterField!.value = "__no_matching_tool_filter__";
                yield return null;

                itemsSource = listView.itemsSource as IList;
                Assert.IsNotNull(itemsSource, "List view should keep an items source after filtering.");
                Assert.AreEqual(0, itemsSource!.Count, "The sentinel filter should hide every tool.");
                Assert.IsFalse(enableButton.enabledSelf, "Enable Filtered should disable when the filtered list is empty.");
                Assert.IsFalse(disableButton.enabledSelf, "Disable Filtered should disable when the filtered list is empty.");
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator ApplyToolEnabledStateBatch_ChangesOnlyProvidedTools()
        {
            yield return null;

            var toolManager = UnityMcpPluginEditor.Instance.Tools;
            Assert.IsNotNull(toolManager, "ToolManager should not be null.");

            var tools = toolManager!.GetAllTools()
                .Where(tool => tool != null)
                .Take(3)
                .ToList();

            Assert.AreEqual(3, tools.Count, "This test expects at least three registered tools.");

            var originalStates = tools.ToDictionary(tool => tool.Name, tool => toolManager.IsToolEnabled(tool.Name));

            try
            {
                foreach (var tool in tools)
                    toolManager.SetToolEnabled(tool.Name, true);
                UnityMcpPluginEditor.Instance.Save();

                var filteredTools = tools
                    .Take(2)
                    .Select(tool => new McpToolsWindow.ToolViewModel(toolManager, tool))
                    .ToList();

                var changedCount = McpToolsWindow.ApplyToolEnabledStateBatch(toolManager, filteredTools, isEnabled: false);

                Assert.AreEqual(2, changedCount, "Only the targeted filtered tools should change state.");
                Assert.IsFalse(toolManager.IsToolEnabled(tools[0].Name), $"Tool '{tools[0].Name}' should be disabled.");
                Assert.IsFalse(toolManager.IsToolEnabled(tools[1].Name), $"Tool '{tools[1].Name}' should be disabled.");
                Assert.IsTrue(toolManager.IsToolEnabled(tools[2].Name), $"Non-filtered tool '{tools[2].Name}' should remain enabled.");
            }
            finally
            {
                foreach (var originalState in originalStates)
                    toolManager.SetToolEnabled(originalState.Key, originalState.Value);
                UnityMcpPluginEditor.Instance.Save();
            }
        }

        [UnityTest]
        public IEnumerator ApplyToolEnabledStateBatch_NoOpWhenToolsAlreadyMatchTargetState()
        {
            yield return null;

            var toolManager = UnityMcpPluginEditor.Instance.Tools;
            Assert.IsNotNull(toolManager, "ToolManager should not be null.");

            var tools = toolManager!.GetAllTools()
                .Where(tool => tool != null)
                .Take(2)
                .ToList();

            Assert.AreEqual(2, tools.Count, "This test expects at least two registered tools.");

            var originalStates = tools.ToDictionary(tool => tool.Name, tool => toolManager.IsToolEnabled(tool.Name));

            try
            {
                foreach (var tool in tools)
                    toolManager.SetToolEnabled(tool.Name, false);
                UnityMcpPluginEditor.Instance.Save();

                var filteredTools = tools
                    .Select(tool => new McpToolsWindow.ToolViewModel(toolManager, tool))
                    .ToList();

                var changedCount = McpToolsWindow.ApplyToolEnabledStateBatch(toolManager, filteredTools, isEnabled: false);

                Assert.AreEqual(0, changedCount, "Already-disabled tools should not be rewritten.");
                Assert.IsFalse(toolManager.IsToolEnabled(tools[0].Name), $"Tool '{tools[0].Name}' should remain disabled.");
                Assert.IsFalse(toolManager.IsToolEnabled(tools[1].Name), $"Tool '{tools[1].Name}' should remain disabled.");
            }
            finally
            {
                foreach (var originalState in originalStates)
                    toolManager.SetToolEnabled(originalState.Key, originalState.Value);
                UnityMcpPluginEditor.Instance.Save();
            }
        }
    }
}
