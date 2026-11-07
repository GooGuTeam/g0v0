// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// torii: el campo de busqueda estilo osu!stable para la barra de arriba del song-select legacy.
    /// un label "Search:" verde-amarillo seguido de un text box inline sobre un fondo oscuro suave (asi
    /// se sigue leyendo sobre el fondo del beatmap, como el menu-button-background tenue de stable). va
    /// two-way con el filter query compartido asi escribir filtra el carousel igual que la busqueda
    /// moderna (que esta oculta).
    /// </summary>
    public partial class LegacySearchBox : CompositeDrawable
    {
        public Bindable<string> Current { get; } = new Bindable<string>();

        private const float font_size = 13 * 1.6f;
        private const float line_height = font_size * 1.25f;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                Children = new Drawable[]
                {
                    // SongSelection: the "Search:" label (13pt bold), with the text box starting where it ends, 174 units long. skins that draw
                    // their top through selection-mode often cover the label, leaving the text box's start as the edge to line up with.
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.X,
                        Height = line_height,
                        Direction = FillDirection.Horizontal,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = @"Search:",
                                Font = LegacyFonts.Get(font_size, FontWeight.Bold),
                                Colour = Color4.GreenYellow,
                                Shadow = true,
                            },
                            new SearchTextBox
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 174 * 1.6f,
                                Height = line_height,
                                FontSize = font_size,
                                Current = { BindTarget = Current },
                            },
                        },
                    },
                },
            };
        }

        private partial class SearchTextBox : FocusedTextBox
        {
            // las flechas izquierda/derecha quedan libres para el carousel (grupo anterior/siguiente),
            // como en stable; el caret no las necesita con busquedas cortas.
            public override bool HandleLeftRightArrows => false;

            protected override float LeftRightPadding => 0;

            protected override FontUsage Font => LegacyFonts.Get(FontSize, FontWeight.Bold);

            protected override SpriteText CreatePlaceholder() => new OsuSpriteText { Font = LegacyFonts.Get(font_size, FontWeight.Bold) };

            private Caret caret = null!;
            private bool caretSuppressed;

            protected override Caret CreateCaret() => caret = base.CreateCaret();

            protected override void Update()
            {
                base.Update();

                // pTextBox draws no cursor while it shows its default text ("Type to search!"), only once something is typed.
                bool suppress = string.IsNullOrEmpty(Text);

                if (suppress)
                {
                    caret.ClearTransforms(targetMember: nameof(Alpha));
                    caret.Alpha = 0;
                }
                else if (caretSuppressed && HasFocus)
                    caret.Show();

                caretSuppressed = suppress;
            }

            public SearchTextBox()
            {
                PlaceholderText = @"Type to search!";
                // como en lazer: agarra y mantiene el foco asi se puede tipear directo sin clickear.
                // solo esta presente en modo legacy, asi que no roba foco en el resto.
                HoldFocus = true;
                DrawBorder = false;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                BackgroundUnfocused = Color4.Transparent;
                BackgroundFocused = Color4.Transparent;
                Placeholder.Colour = Color4.White;
            }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                // NO consumimos Enter: FocusedTextBox vendria a commitearlo y se lo comeria, y entonces
                // GlobalAction.Select (empezar el mapa) nunca dispara. lo dejamos pasar, igual que
                // FocusedTextBox ya hace con Escape para GlobalAction.Back. la busqueda filtra live por el
                // binding two-way, asi que no necesitamos el commit.
                if (e.Key == Key.Enter || e.Key == Key.KeypadEnter)
                    return false;

                return base.OnKeyDown(e);
            }
        }
    }
}
