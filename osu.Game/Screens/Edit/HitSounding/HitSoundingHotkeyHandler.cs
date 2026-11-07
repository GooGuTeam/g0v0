// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Edit;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingHotkeyHandler : Component, IKeyBindingHandler<GlobalAction>
    {
        private readonly HitSoundingEditModel model;

        [Resolved]
        private HitSoundingRoll roll { get; set; } = null!;

        private InputManager inputManager = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            inputManager = GetContainingInputManager()!;
        }

        public HitSoundingHotkeyHandler(HitSoundingEditModel model)
        {
            this.model = model;
            AlwaysPresent = true;
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat || inputManager.FocusedDrawable != null)
                return false;

            string? sample = e.Action switch
            {
                GlobalAction.EditorToggleWhistleSound => HitSampleInfo.HIT_WHISTLE,
                GlobalAction.EditorToggleFinishSound => HitSampleInfo.HIT_FINISH,
                GlobalAction.EditorToggleClapSound => HitSampleInfo.HIT_CLAP,
                _ => null
            };

            if (sample == null)
            {
                string? bank = e.Action switch
                {
                    GlobalAction.EditorToggleNormalNormalBank or GlobalAction.EditorToggleAdditionNormalBank => HitSampleInfo.BANK_NORMAL,
                    GlobalAction.EditorToggleNormalSoftBank or GlobalAction.EditorToggleAdditionSoftBank => HitSampleInfo.BANK_SOFT,
                    GlobalAction.EditorToggleNormalDrumBank or GlobalAction.EditorToggleAdditionDrumBank => HitSampleInfo.BANK_DRUM,
                    GlobalAction.EditorToggleAdditionAutoBank => HitObjectComposer.HIT_BANK_AUTO,
                    _ => null
                };

                if (bank == null)
                    return false;

                selectHovered();

                if (model.SelectedNodes.Count == 0)
                    return false;

                bool additionsOnly = e.Action is GlobalAction.EditorToggleAdditionNormalBank or GlobalAction.EditorToggleAdditionSoftBank
                    or GlobalAction.EditorToggleAdditionDrumBank or GlobalAction.EditorToggleAdditionAutoBank;

                model.SetBank(model.SelectedNodes, bank, -1, additionsOnly);
                return true;
            }

            selectHovered();

            if (model.SelectedNodes.Count == 0)
                return false;

            model.ToggleAddition(model.SelectedNodes, sample);
            return true;
        }

        private void selectHovered()
        {
            var hovered = roll.HoveredNode;
            if (hovered == null || model.SelectedNodes.Contains(hovered))
                return;

            model.SelectedNodes.Clear();
            model.SelectedNodes.Add(hovered);
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }
}
