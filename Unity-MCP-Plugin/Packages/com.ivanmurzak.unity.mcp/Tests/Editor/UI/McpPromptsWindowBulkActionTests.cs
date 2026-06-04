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
    public class McpPromptsWindowBulkActionTests : BaseTest
    {
        [UnityTest]
        public IEnumerator BulkActionButtons_DisableWhenFilterHasNoMatches()
        {
            var window = ScriptableObject.CreateInstance<McpPromptsWindow>();

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
                Assert.IsNotNull(listView, "Prompts list view should exist.");

                var itemsSource = listView!.itemsSource as IList;
                Assert.IsNotNull(itemsSource, "List view should have an items source after GUI creation.");
                Assert.Greater(itemsSource!.Count, 0, "This test expects at least one registered prompt.");
                Assert.IsTrue(enableButton!.enabledSelf, "Enable Filtered should start enabled when prompts are visible.");
                Assert.IsTrue(disableButton!.enabledSelf, "Disable Filtered should start enabled when prompts are visible.");

                filterField!.value = "__no_matching_prompt_filter__";
                yield return null;

                itemsSource = listView.itemsSource as IList;
                Assert.IsNotNull(itemsSource, "List view should keep an items source after filtering.");
                Assert.AreEqual(0, itemsSource!.Count, "The sentinel filter should hide every prompt.");
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
        public IEnumerator ApplyPromptEnabledStateBatch_ChangesOnlyProvidedPrompts()
        {
            yield return null;

            var promptManager = UnityMcpPluginEditor.Instance.Prompts;
            Assert.IsNotNull(promptManager, "PromptManager should not be null.");

            var prompts = promptManager!.GetAllPrompts()
                .Where(prompt => prompt != null)
                .Take(3)
                .ToList();

            Assert.AreEqual(3, prompts.Count, "This test expects at least three registered prompts.");

            var originalStates = prompts.ToDictionary(prompt => prompt.Name, prompt => promptManager.IsPromptEnabled(prompt.Name));

            try
            {
                foreach (var prompt in prompts)
                    promptManager.SetPromptEnabled(prompt.Name, true);
                UnityMcpPluginEditor.Instance.Save();

                var filteredPrompts = prompts
                    .Take(2)
                    .Select(prompt => new McpPromptsWindow.PromptViewModel(promptManager, prompt))
                    .ToList();

                var changedCount = McpPromptsWindow.ApplyPromptEnabledStateBatch(promptManager, filteredPrompts, isEnabled: false);

                Assert.AreEqual(2, changedCount, "Only the targeted filtered prompts should change state.");
                Assert.IsFalse(promptManager.IsPromptEnabled(prompts[0].Name), $"Prompt '{prompts[0].Name}' should be disabled.");
                Assert.IsFalse(promptManager.IsPromptEnabled(prompts[1].Name), $"Prompt '{prompts[1].Name}' should be disabled.");
                Assert.IsTrue(promptManager.IsPromptEnabled(prompts[2].Name), $"Non-filtered prompt '{prompts[2].Name}' should remain enabled.");
            }
            finally
            {
                foreach (var originalState in originalStates)
                    promptManager.SetPromptEnabled(originalState.Key, originalState.Value);
                UnityMcpPluginEditor.Instance.Save();
            }
        }

        [UnityTest]
        public IEnumerator ApplyPromptEnabledStateBatch_NoOpWhenPromptsAlreadyMatchTargetState()
        {
            yield return null;

            var promptManager = UnityMcpPluginEditor.Instance.Prompts;
            Assert.IsNotNull(promptManager, "PromptManager should not be null.");

            var prompts = promptManager!.GetAllPrompts()
                .Where(prompt => prompt != null)
                .Take(2)
                .ToList();

            Assert.AreEqual(2, prompts.Count, "This test expects at least two registered prompts.");

            var originalStates = prompts.ToDictionary(prompt => prompt.Name, prompt => promptManager.IsPromptEnabled(prompt.Name));

            try
            {
                foreach (var prompt in prompts)
                    promptManager.SetPromptEnabled(prompt.Name, false);
                UnityMcpPluginEditor.Instance.Save();

                var filteredPrompts = prompts
                    .Select(prompt => new McpPromptsWindow.PromptViewModel(promptManager, prompt))
                    .ToList();

                var changedCount = McpPromptsWindow.ApplyPromptEnabledStateBatch(promptManager, filteredPrompts, isEnabled: false);

                Assert.AreEqual(0, changedCount, "Already-disabled prompts should not be rewritten.");
                Assert.IsFalse(promptManager.IsPromptEnabled(prompts[0].Name), $"Prompt '{prompts[0].Name}' should remain disabled.");
                Assert.IsFalse(promptManager.IsPromptEnabled(prompts[1].Name), $"Prompt '{prompts[1].Name}' should remain disabled.");
            }
            finally
            {
                foreach (var originalState in originalStates)
                    promptManager.SetPromptEnabled(originalState.Key, originalState.Value);
                UnityMcpPluginEditor.Instance.Save();
            }
        }
    }
}
