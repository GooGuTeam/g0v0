// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Replays;
using osu.Game.Scoring;

namespace osu.Game.Tests.Scoring
{
    [TestFixture]
    public class ReplayFrameworkTest
    {
        [Test]
        public void TestCodecsSelectedByRulesetAndExtension()
        {
            Assert.That(ScoreReplayFormat.ForRuleset(new OsuRuleset()), Is.TypeOf<LegacyReplayFileFormat>());
            Assert.That(ScoreReplayFormat.ForRuleset(new NativeTestRuleset()), Is.TypeOf<NativeReplayFileFormat>());
            Assert.That(ScoreReplayFormat.ForFilename("test.G0R"), Is.TypeOf<NativeReplayFileFormat>());
            Assert.That(ScoreReplayFormat.ForFilename("test.osr"), Is.TypeOf<LegacyReplayFileFormat>());
            Assert.That(ScoreReplayFormat.ForFilename("test.zip"), Is.Null);
        }

        [Test]
        public void TestNativeArchivePreservesCustomFrames()
        {
            var beatmap = new Beatmap { BeatmapInfo = { Ruleset = new NativeTestRuleset().RulesetInfo, Hash = "hash", MD5Hash = "md5" } };
            var score = new Score { ScoreInfo = new ScoreInfo(beatmap.BeatmapInfo, beatmap.BeatmapInfo.Ruleset) };
            score.Replay.Frames.Add(new CustomFrame { Time = 123.5, Pressure = 0.75f });
            var format = ScoreReplayFormat.ForRuleset(new NativeTestRuleset())!;
            using var archive = format.CreateArchive(score, beatmap);
            Assert.That(archive.Filenames.Single(), Is.EqualTo("replay.g0r"));
            using var stream = archive.GetStream(archive.Filenames.Single());
            var restored = NativeScoreFormat.Decode(stream, _ => new NativeTestRuleset(), _ => beatmap.BeatmapInfo);
            Assert.That(restored.Replay.Frames.Single(), Is.TypeOf<CustomFrame>());
            Assert.That(((CustomFrame)restored.Replay.Frames.Single()).Pressure, Is.EqualTo(0.75f));
        }

        [Test]
        public void TestExporterUsesStoredReplayExtension()
        {
            using var storage = new TemporaryNativeStorage($"replay-export-{Guid.NewGuid()}");
            var exporter = new TestScoreExporter(storage);
            var score = new ScoreInfo();
            score.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "hash" }, "replay.g0r"));
            Assert.That(exporter.ExtensionFor(score), Is.EqualTo(".g0r"));
            score.Files.Clear();
            score.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "hash" }, "replay.osr"));
            Assert.That(exporter.ExtensionFor(score), Is.EqualTo(".osr"));
        }

        [Test]
        public void TestFrameVersionsAndTimingValidated()
        {
            var format = new RulesetReplayFormat<CustomFrame>();
            Assert.Throws<InvalidDataException>(() => format.Decode(new JArray(), 2));
            Assert.Throws<InvalidDataException>(() => format.Decode(new JArray(new JObject { ["time"] = double.NaN }), 1));
        }

        [TestCase(".custom.g0r")]
        [TestCase("..g0r")]
        [TestCase(".")]
        [TestCase("g0r")]
        [TestCase(".a/b")]
        [TestCase(".a\\b")]
        public void TestInvalidReplayExtensionRejected(string extension)
        {
            Assert.Throws<ArgumentException>(() => ScoreReplayFormat.Register(new TestReplayFormat(extension)));
        }

        private class TestReplayFormat : ScoreReplayFormat
        {
            public TestReplayFormat(string extension)
            {
                Extension = extension;
            }

            public override string Extension { get; }
            public override bool CanEncode(Ruleset ruleset) => false;
            public override void Encode(Score score, IBeatmap beatmap, Stream stream) => throw new NotSupportedException();
            public override Score Decode(Stream stream, IRulesetStore rulesets, BeatmapManager beatmaps) => throw new NotSupportedException();
        }

        public class NativeTestRuleset : OsuRuleset
        {
            public override string ShortName => "native-test";
            public override RulesetReplayFormat CreateReplayFormat() => new RulesetReplayFormat<CustomFrame>();
        }

        public class CustomFrame : ReplayFrame
        {
            public float Pressure;
        }

        private class TestScoreExporter : ScoreExporter
        {
            public TestScoreExporter(Storage storage)
                : base(storage)
            {
            }

            public string ExtensionFor(ScoreInfo score) => GetFileExtension(score);
        }
    }
}
