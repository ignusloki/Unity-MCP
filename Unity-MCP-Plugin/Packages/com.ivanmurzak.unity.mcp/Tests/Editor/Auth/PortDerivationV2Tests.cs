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
using com.IvanMurzak.McpPlugin.AgentConfig;
using NUnit.Framework;

namespace com.IvanMurzak.Unity.MCP.Editor.Tests
{
    /// <summary>
    /// Verifies that <see cref="UnityMcpPlugin.GeneratePortFromDirectory(string)"/> delegates its port
    /// derivation to the current shared <see cref="ProjectIdentity"/> implementation rather than keeping a
    /// stale local copy of the hashing rules.
    /// </summary>
    public class PortDerivationV2Tests
    {
        [Test]
        public void GeneratePortFromDirectory_DelegatesToProjectIdentity()
        {
            const string dir = @"C:\Users\user\my-game";
            Assert.AreEqual(ProjectIdentity.DerivePort(dir), UnityMcpPlugin.GeneratePortFromDirectory(dir));
        }

        // These vectors intentionally mirror the current shared ProjectIdentity implementation. If upstream
        // changes the derivation again, these tests should be updated to the new authoritative values rather
        // than reintroducing local hashing logic here.
        [TestCase(@"C:\Users\user\my-game", 24298)]   // Windows backslash form
        [TestCase(@"C:\Users\user\my-game\", 24298)]  // trailing backslash trimmed → identical
        [TestCase("C:/Users/user/my-game", 24298)]    // forward-slash form → SAME port under v2
        [TestCase("C:/Users/user/my-game/", 24298)]   // trailing slash trimmed → identical
        [TestCase("/home/user/my-game", 23940)]       // POSIX typical → v2 == v1
        [TestCase("/home/user/my-game/", 23940)]      // trailing slash trimmed → identical
        public void GeneratePortFromDirectory_MatchesV2GoldenPort(string dir, int expectedPort)
        {
            Assert.AreEqual(expectedPort, UnityMcpPlugin.GeneratePortFromDirectory(dir));
        }

        [Test]
        public void GeneratePortFromDirectory_BackslashAndForwardSlash_Converge()
        {
            Assert.AreEqual(
                UnityMcpPlugin.GeneratePortFromDirectory(@"C:\Users\user\my-game"),
                UnityMcpPlugin.GeneratePortFromDirectory("C:/Users/user/my-game"));
        }

        [Test]
        public void GeneratePortFromDirectory_TrailingSeparatorInsensitive()
        {
            Assert.AreEqual(
                UnityMcpPlugin.GeneratePortFromDirectory(@"C:\Users\user\my-game"),
                UnityMcpPlugin.GeneratePortFromDirectory(@"C:\Users\user\my-game\"));
        }

        [Test]
        public void GeneratePortFromDirectory_StaysInDeterministicRange()
        {
            var port = UnityMcpPlugin.GeneratePortFromDirectory(@"C:\Users\user\my-game");
            Assert.GreaterOrEqual(port, ProjectIdentity.MinPort);
            Assert.LessOrEqual(port, ProjectIdentity.MaxPort);
        }

        [Test]
        public void GeneratePortFromDirectory_NullDirectory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => UnityMcpPlugin.GeneratePortFromDirectory(null!));
        }
    }
}
