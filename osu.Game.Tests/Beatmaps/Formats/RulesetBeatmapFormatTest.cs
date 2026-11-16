// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Text;
using NUnit.Framework;
using osu.Game.Rulesets.Formats;

namespace osu.Game.Tests.Beatmaps.Formats
{
    [TestFixture]
    public class RulesetBeatmapFormatTest
    {
        [TestCase("{\"g0v0RulesetFormat\":\"test\",\"data\":{}}", true)]
        [TestCase("{\"data\":{\"nested\":[{},{}]},\"g0v0RulesetFormat\":\"test\"}", true)]
        [TestCase("{\"data\":{\"g0v0RulesetFormat\":\"test\"}}", false)]
        [TestCase("{\"data\":[{\"g0v0RulesetFormat\":\"test\"}]}", false)]
        [TestCase("{\"g0v0RulesetFormat\":1,\"data\":{}}", false)]
        [TestCase("{\"data\":{},\"g0v0RulesetFormat\":null}", false)]
        [TestCase("{}", false)]
        [TestCase("[]", false)]
        [TestCase("osu file format v14", false)]
        public void TestNativeContentDetection(string content, bool expected)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("prefix" + content));
            stream.Position = 6;

            Assert.That(RulesetBeatmapFormat.IsNativeContent(stream), Is.EqualTo(expected));
            Assert.That(stream.Position, Is.EqualTo(6));
        }
    }
}
