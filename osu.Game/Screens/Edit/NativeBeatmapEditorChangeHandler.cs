// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Formats;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Undo/redo for beatmaps stored in a <see cref="RulesetBeatmapFormat"/>, which cannot go through legacy .osu diff patching.
    /// States contain the format's own serialisation and editor bookmarks; restoring a state replaces hit objects and timing wholesale.
    /// </summary>
    public partial class NativeBeatmapEditorChangeHandler : EditorChangeHandler
    {
        private readonly EditorBeatmap editorBeatmap;
        private readonly RulesetBeatmapFormat format;

        public NativeBeatmapEditorChangeHandler(EditorBeatmap editorBeatmap, RulesetBeatmapFormat format)
        {
            this.editorBeatmap = editorBeatmap;
            this.format = format;

            editorBeatmap.TransactionBegan += BeginChange;
            editorBeatmap.TransactionEnded += EndChange;
            editorBeatmap.SaveStateTriggered += SaveState;
        }

        protected override void WriteCurrentStateToStream(MemoryStream stream)
        {
            var state = new JObject
            {
                ["data"] = format.Encode(editorBeatmap.PlayableBeatmap),
                // Formats are not required to serialise bookmarks. Use the playable copy so BeginChange can capture the old values.
                ["bookmarks"] = new JArray(editorBeatmap.PlayableBeatmap.Bookmarks),
            };
            byte[] bytes = Encoding.UTF8.GetBytes(state.ToString(Newtonsoft.Json.Formatting.None));
            stream.Write(bytes, 0, bytes.Length);
        }

        protected override void ApplyStateChange(byte[] previousState, byte[] newState)
        {
            var decoded = new Beatmap { BeatmapInfo = editorBeatmap.BeatmapInfo.Clone() };
            var state = JObject.Parse(Encoding.UTF8.GetString(newState));
            format.Decode((JObject)state["data"]!, decoded);

            var ruleset = editorBeatmap.BeatmapInfo.Ruleset.CreateInstance();
            var converted = ruleset.CreateBeatmapConverter(decoded).Convert();

            editorBeatmap.BeginChange();

            editorBeatmap.Clear();
            editorBeatmap.AddRange(converted.HitObjects);

            var controlPoints = EditorBeatmap.ConvertControlPoints(converted.ControlPointInfo);
            editorBeatmap.ControlPointInfo.Clear();

            foreach (var group in controlPoints.Groups)
            {
                foreach (var point in group.ControlPoints.ToArray())
                    editorBeatmap.ControlPointInfo.Add(group.Time, point);
            }

            editorBeatmap.Bookmarks.Clear();
            editorBeatmap.Bookmarks.AddRange(state["bookmarks"]!.Values<int>());

            editorBeatmap.EndChange();
        }
    }
}
