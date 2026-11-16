// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Rulesets.Edit.Native
{
    /// <summary>
    /// The default inspector plus form controls for every <see cref="EditablePropertyAttribute"/> on the selected objects.
    /// Edits apply to all selected objects sharing the property, as one undoable change.
    /// </summary>
    public partial class EditablePropertyInspector : CompositeDrawable
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private FillFlowContainer controls = null!;
        private ScheduledDelegate? pendingRebuild;
        private bool applying;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new HitObjectInspector(),
                    controls = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(5),
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            editorBeatmap.SelectedHitObjects.CollectionChanged += (_, _) => scheduleRebuild();
            editorBeatmap.TransactionEnded += () =>
            {
                if (!applying)
                    scheduleRebuild();
            };

            rebuild();
        }

        private void scheduleRebuild()
        {
            pendingRebuild?.Cancel();
            pendingRebuild = Schedule(rebuild);
        }

        /// <summary>
        /// The editable properties shared by every object, in display order.
        /// </summary>
        public static IReadOnlyList<(PropertyInfo property, EditablePropertyAttribute attribute)> GetEditableProperties(IReadOnlyCollection<HitObject> objects)
        {
            if (objects.Count == 0)
                return Array.Empty<(PropertyInfo, EditablePropertyAttribute)>();

            return objects.First().GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                          .Select(p => (property: p, attribute: p.GetCustomAttribute<EditablePropertyAttribute>()))
                          .Where(p => p.attribute != null && p.property.CanWrite && objects.All(o => p.property.DeclaringType!.IsInstanceOfType(o)))
                          .OrderBy(p => p.attribute!.Order)
                          .Select(p => (p.property, p.attribute!))
                          .ToArray();
        }

        /// <summary>
        /// Sets a property on every object as a single editor change.
        /// </summary>
        public static void Apply(EditorBeatmap beatmap, IEnumerable<HitObject> objects, PropertyInfo property, object value)
        {
            object converted = property.PropertyType.IsEnum
                ? Enum.Parse(property.PropertyType, value.ToString()!)
                : Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture);

            beatmap.BeginChange();

            foreach (var hitObject in objects)
            {
                property.SetValue(hitObject, converted);
                beatmap.Update(hitObject);
            }

            beatmap.EndChange();
        }

        private void rebuild()
        {
            controls.Clear();

            var objects = editorBeatmap.SelectedHitObjects.ToArray();

            foreach (var (property, attribute) in GetEditableProperties(objects))
            {
                var control = createControl(objects, property, attribute);
                if (control != null)
                    controls.Add(control);
            }
        }

        private void apply(HitObject[] objects, PropertyInfo property, object value)
        {
            applying = true;

            try
            {
                Apply(editorBeatmap, objects, property, value);
            }
            finally
            {
                applying = false;
            }
        }

        private Drawable? createControl(HitObject[] objects, PropertyInfo property, EditablePropertyAttribute attribute)
        {
            object? current = property.GetValue(objects[0]);
            Type type = property.PropertyType;

            if (type == typeof(bool))
            {
                var bindable = new BindableBool((bool)current!);
                bindable.BindValueChanged(v => apply(objects, property, v.NewValue));
                return new FormCheckBox { Caption = attribute.Label, Current = bindable };
            }

            if (type.IsEnum)
            {
                var bindable = new Bindable<string>(current!.ToString()!);
                bindable.BindValueChanged(v => apply(objects, property, v.NewValue));
                return new EnumPropertyDropdown(type) { Caption = attribute.Label, Items = Enum.GetNames(type), Current = bindable };
            }

            if (type != typeof(int) && type != typeof(float) && type != typeof(double))
                return null;

            double value = Convert.ToDouble(current, CultureInfo.InvariantCulture);

            if (double.IsFinite(attribute.Min) && double.IsFinite(attribute.Max))
            {
                var bindable = new BindableDouble(value)
                {
                    MinValue = attribute.Min,
                    MaxValue = attribute.Max,
                    Precision = type == typeof(int) ? 1 : attribute.Precision,
                };
                bindable.BindValueChanged(v => apply(objects, property, v.NewValue));
                return new FormSliderBar<double> { Caption = attribute.Label, Current = bindable, TransferValueOnCommit = true };
            }

            var text = new Bindable<string>(value.ToString(CultureInfo.InvariantCulture));
            var box = new FormNumberBox(allowDecimals: type != typeof(int)) { Caption = attribute.Label, Current = text };
            box.OnCommit += (_, _) =>
            {
                if (double.TryParse(text.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                    && double.IsFinite(parsed) && parsed >= attribute.Min && parsed <= attribute.Max)
                    apply(objects, property, parsed);
                else
                    text.Value = value.ToString(CultureInfo.InvariantCulture);
            };
            return box;
        }

        private partial class EnumPropertyDropdown : FormDropdown<string>
        {
            private readonly Type enumType;

            public EnumPropertyDropdown(Type enumType)
            {
                this.enumType = enumType;
            }

            protected override LocalisableString GenerateItemText(string item) => ((Enum)Enum.Parse(enumType, item)).GetLocalisableDescription();
        }
    }
}
