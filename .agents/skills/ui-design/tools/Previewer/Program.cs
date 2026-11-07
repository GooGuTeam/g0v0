// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Platform;

namespace Previewer
{
    /// <summary>
    /// Renders client UI on an offscreen GPU device and saves PNG captures, without a visible window.
    /// <list type="bullet">
    /// <item><c>--scene=TestSceneName [--test=Method] [--case=N] [--capture-before=step|step] [--stop-before=step] [--crop=Type,Type]</c> runs one of the visual test scenes,
    /// capturing before each step matching <c>--capture-before</c> and where it ends.</item>
    /// <item><c>--scene=editor --scripts=compose,timing,verify [--ruleset=osu]</c> runs the scripted editor captures in <see cref="EditorPreviewScene"/>.</item>
    /// </list>
    /// Shared: <c>--out=dir --size=1920x1080 [--locale=zh] [--wait=ms]</c>.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string? scene = value(args, "--scene") ?? "editor";

            string[] size = (value(args, "--size") ?? "1920x1080").Split('x');
            var options = new PreviewOptions(
                Scene: scene,
                Test: value(args, "--test"),
                Case: int.Parse(value(args, "--case") ?? "0", CultureInfo.InvariantCulture),
                StopBefore: value(args, "--stop-before"),
                CaptureBefore: value(args, "--capture-before")?.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>(),
                Crops: list(value(args, "--crop")),
                Scripts: list(value(args, "--scripts") ?? "compose"),
                Output: Path.GetFullPath(value(args, "--out") ?? "captures"),
                Ruleset: value(args, "--ruleset") ?? "osu",
                Wait: int.Parse(value(args, "--wait") ?? "1500", CultureInfo.InvariantCulture));

            int width = int.Parse(size[0], CultureInfo.InvariantCulture);
            int height = int.Parse(size[1], CultureInfo.InvariantCulture);
            string? locale = value(args, "--locale");

            Directory.CreateDirectory(options.Output);

            // Fresh storage each run, so that no state from an earlier run changes what is drawn. Each process gets its own, so a run started
            // while another is still running (or still releasing its files) cannot delete the storage from under it.
            string storage = Path.Combine(Path.GetTempPath(), $"g0v0-previewer-host-{Environment.ProcessId}");

            if (Directory.Exists(storage))
                Directory.Delete(storage, true);

            string gameStorage = Path.Combine(storage, PreviewGame.NAME);
            Directory.CreateDirectory(gameStorage);

            if (locale != null)
                File.WriteAllText(Path.Combine(gameStorage, "framework.ini"), $"Locale = {locale}\n");

            using (var host = new OffscreenGameHost(PreviewGame.NAME, width, height, storage))
                host.Run(new PreviewGame(options));

            try
            {
                Directory.Delete(storage, true);
            }
            catch (IOException)
            {
                // a file still held open only leaves a temporary folder behind.
            }
            catch (UnauthorizedAccessException)
            {
            }

            Console.WriteLine(PreviewGame.Error ?? $"Captures saved to {options.Output}");
            return PreviewGame.Error == null ? 0 : 1;
        }

        private static string? value(string[] args, string name)
            => args.LastOrDefault(a => a.StartsWith(name + "=", StringComparison.Ordinal))?[(name.Length + 1)..];

        private static string[] list(string? value)
            => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
    }

    public record PreviewOptions(string Scene, string? Test, int Case, string? StopBefore, string[] CaptureBefore, string[] Crops, string[] Scripts, string Output, string Ruleset, int Wait);
}
