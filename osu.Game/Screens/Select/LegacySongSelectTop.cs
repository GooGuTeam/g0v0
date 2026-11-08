// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// torii: el panel de arriba del song select estilo osu!stable para la UI legacy. dibuja la
    /// textura "songselect-top" del skin y encima el bloque de info del beatmap (izquierda) y los
    /// dropdowns Group/Sort, los tabs de grouping y el search (derecha). todo va en las coords
    /// exactas de stable x1.6 (la UI legacy vive en un container 1366x768 = espacio stable-480 x1.6),
    /// asi queda alineado con stable y con las texturas de skin.
    /// </summary>
    public partial class LegacySongSelectTop : CompositeDrawable
    {
        // colores de highlight de stable (SongSelection.cs:634/623).
        private static readonly Color4 group_colour = new Color4(146 / 255f, 195 / 255f, 230 / 255f, 1f);
        private static readonly Color4 sort_colour = new Color4(174 / 255f, 210 / 255f, 139 / 255f, 1f);

        private const float dropdown_width = 193;

        // alto de la franja songselect-top de stable en el espacio logico 1366x768 (el art stock es 1366x155).
        private const float panel_height = 155;

        // ancho logico del espacio de la UI legacy (el TargetDrawSize.X del DrawSizePreservingFillContainer).
        private const float top_extension_x = 1365;

        /// <summary>
        /// el filter control del song select que manejan estos controles legacy (el search query).
        /// Group/Sort van por config asi quedan sincronizados con los dropdowns modernos (escondidos).
        /// </summary>
        public FilterControl FilterControl { get; init; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        // solo los controles interactivos agarran input; lo decorativo (la textura songselect-top,
        // la info del beatmap, los labels Group/Sort) deja pasar el input asi se puede drag-scrollear
        // el carousel donde sea que asome detras del panel.
        private Drawable groupDropdown = null!;
        private Drawable sortDropdown = null!;
        private Drawable groupTabs = null!;
        private Drawable searchBox = null!;
        private Sprite searchBackground = null!;

        // Torii: osu!stable draws the skin's selection-mode (0.95) above the song select cookie (0.9) and the beatmap title (0.79), and the
        // dropdowns (0.98) and small stat lines (1) above that, so these are drawn through proxies in the game's front layer.
        private LegacyTopDecoration decoration = null!;
        private LegacyBeatmapInfoPanel infoPanel = null!;
        private ScreenFrontLayer.ProxySet frontProxies = null!;

        [Resolved(CanBeNull = true)]
        private ScreenFrontLayer? frontLayer { get; set; }

        private const float search_background_idle_y = 46 * 1.6f;
        private const float search_background_active_y = 54 * 1.6f;
        private const float search_background_idle_alpha = 0.4f;
        private const float search_background_active_alpha = 0.95f;

        private ISkinSource skin = null!;
        private SkinManager skins = null!;

        // van como fields (NO locals) asi las copias de config.GetBindable sobreviven al load().
        // osu!framework bindea con weak references, asi que una copia local se la come el GC apenas
        // termina load() y desconecta los tabs/dropdowns de la config sin avisar (el click setea el
        // Current local pero nunca llega a la config, asi que no reagrupa nada). un strong ref lo mantiene vivo.
        private Bindable<GroupMode> groupBindable = null!;
        private Bindable<SortMode> sortBindable = null!;
        private IBindable<string> searchQuery = null!;

        // las capas de la textura songselect-top (el default + la propia del skin). se rearman al
        // cambiar de skin asi el panel se actualiza al toque en vez de mostrar el top del skin viejo hasta que salis.
        private readonly List<Drawable> topLayers = new List<Drawable>();

        [BackgroundDependencyLoader]
        private void load(ISkinSource skinSource, SkinManager skinManager)
        {
            skin = skinSource;
            skins = skinManager;

            RelativeSizeAxes = Axes.Both;

            groupBindable = config.GetBindable<GroupMode>(OsuSetting.SongSelectGroupMode);
            sortBindable = config.GetBindable<SortMode>(OsuSetting.SongSelectSortingMode);

            InternalChildren = new Drawable[]
            {
                // la decoracion "mode button" skinneable del skin (selection-mode + mode-*-small), va
                // ENTRE el songselect-top (atras, lo agrega rebuildTopLayers) y la info del beatmap /
                // Group / Sort / Rankings (adelante). el orden de stable: labels > skinnable top > default.
                decoration = new LegacyTopDecoration { Depth = 2 },
                infoPanel = new LegacyBeatmapInfoPanel
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    // unos px de padding a la izquierda asi el badge de status + el texto no quedan pegados al borde.
                    Position = new Vector2(5, 0),
                },
                // label Group + dropdown (stable: x = WidthScaled-140-118-sortLabelWidth). todo el
                // cluster de la derecha tiene que entrar arriba del contorno azul del panel (~y78), asi
                // que los dropdowns van bien arriba y los tabs se meten justo abajo.
                label(@"Group", group_colour, x: -481),
                groupDropdown = new LegacyStableDropdown<GroupMode>
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    // va adelante asi el menu abierto queda por encima de los tabs + el search de abajo.
                    Depth = -1,
                    Position = new Vector2(-284, 28),
                    AccentColour = group_colour,
                    Width = dropdown_width,
                    Items = Enum.GetValues<GroupMode>(),
                    Current = { BindTarget = groupBindable },
                },
                // label Sort + dropdown (stable: x = WidthScaled-130).
                label(@"Sort", sort_colour, x: -212),
                sortDropdown = new LegacyStableDropdown<SortMode>
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Depth = -1,
                    Position = new Vector2(-15, 28),
                    AccentColour = sort_colour,
                    Width = dropdown_width,
                    Items = Enum.GetValues<SortMode>(),
                    Current = { BindTarget = sortBindable },
                },
                // tabs de grouping (arriba a la derecha). van DETRAS de la decoracion (Depth 2.5 > 2) asi
                // el grafico mode-button del skin simula donde estan. stable esconde los botones reales
                // pero siguen clickeables (la decoracion es un sprite sin input, asi que los clicks pasan).
                groupTabs = new LegacyGroupTabs
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Depth = 2.5f,
                    Margin = new MarginPadding { Top = 56, Right = 16 },
                    Current = { BindTarget = groupBindable },
                },
                // stable's searchBackground: a black menu-button-background (depth 0.69) behind the songselect-top layers (0.7), so only its lower
                // edge shows below the bar; it drops 8 units and darkens while a search is active. The search text itself (0.81) stays in front.
                searchBackground = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-14 * 1.6f, search_background_idle_y),
                    Colour = Color4.Black,
                    Alpha = search_background_idle_alpha,
                    Depth = 5,
                },
                // campo de search (arriba a la derecha, justo abajo del contorno del panel).
                // SongSelection's searchText: field TopRight at (220, 54).
                searchBox = new LegacySearchBox
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(-220, 54) * 1.6f,
                    Current = { BindTarget = FilterControl.SearchQuery },
                },
            };

            rebuildTopLayers();

            frontProxies = new ScreenFrontLayer.ProxySet(
                (decoration, ScreenFrontLayer.Depths.SELECTION_MODE),
                (groupDropdown, ScreenFrontLayer.Depths.TOP_CONTROLS),
                (sortDropdown, ScreenFrontLayer.Depths.TOP_CONTROLS),
                (infoPanel.StatsLine, ScreenFrontLayer.Depths.TOP_CONTROLS));

            searchBackground.Texture = skins.DefaultClassicSkin.GetStableTexture(@"menu-button-background");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // rearmar las capas songselect-top cuando cambia el skin asi un cambio en vivo actualiza el
            // panel al toque (la decoracion se refresca sola con su propio handler de SourceChanged).
            skin.SourceChanged += rebuildTopLayers;

            // SongSelection.ShowSearch / the search reset: 300ms (fade) and 200ms (move) while searching, 200ms back.
            searchQuery = FilterControl.SearchQuery.GetBoundCopy();
            searchQuery.BindValueChanged(q =>
            {
                bool searching = !string.IsNullOrEmpty(q.NewValue);

                searchBackground.FadeTo(searching ? search_background_active_alpha : search_background_idle_alpha, searching ? 300 : 200);
                searchBackground.MoveToY(searching ? search_background_active_y : search_background_idle_y, 200, Easing.Out);
            }, true);
            searchBackground.FinishTransforms();
        }

        /// <summary>
        /// rearma las dos capas apiladas del songselect-top, como stable:
        ///   (atras) el top classic "default" bundleado, siempre; su panel opaco a la izquierda banca la
        ///           info del beatmap asi se lee aunque el top propio del skin sea transparente ahi.
        ///   (medio) el songselect-top PROPIO del skin, solo si trae uno (muchos skins no traen).
        /// con su Depth mas alto quedan detras de la decoracion (Depth 2) y del chrome.
        /// </summary>
        private void rebuildTopLayers()
        {
            if (topLayers.Count > 0)
            {
                foreach (var layer in topLayers)
                    RemoveInternal(layer, true);

                topLayers.Clear();
            }

            var bundledTop = skins.DefaultClassicSkin.GetStableTexture(@"songselect-top");
            var skinTop = skins.CurrentSkin.Value.GetStableTexture(@"songselect-top");

            topLayers.Add(topLayer(bundledTop, depth: 4));

            if (skinTop != null && skins.CurrentSkin.Value != skins.DefaultClassicSkin)
                topLayers.Add(topLayer(skinTop, depth: 3));

            AddRangeInternal(topLayers);
        }

        protected override void Update()
        {
            base.Update();
            frontProxies.Update(frontLayer);
        }

        protected override void Dispose(bool isDisposing)
        {
            frontProxies?.Release(frontLayer);

            if (skin.IsNotNull())
                skin.SourceChanged -= rebuildTopLayers;

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// una capa de textura songselect-top a su tamaño natural (pegada arriba), recortada
        /// a la franja de 155px de stable asi una textura de skin alta/opaca no tapa la banda de controles.
        /// mas <paramref name="depth"/> = mas atras, asi el backing default queda detras del propio del skin.
        /// </summary>
        private Drawable topLayer(Texture? tex, float depth)
        {
            // sin textura (ni la classic bundleada): un gradiente oscuro de fallback asi la info
            // del beatmap y los controles se leen igual sobre cualquier fondo.
            if (tex == null)
            {
                return new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = panel_height,
                    Masking = true,
                    MaskingSmoothness = 0,
                    Depth = depth,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(new Color4(0f, 0f, 0f, 0.78f), new Color4(0f, 0f, 0f, 0f)),
                    },
                };
            }

            var artwork = new Sprite { Texture = tex };
            Drawable[] sprites = tex.DisplayWidth < top_extension_x + 1
                ? new Drawable[] { artwork }
                : new Drawable[]
                {
                    artwork,
                    // Stable extends column 1365 to the right edge on wider windows.
                    new Sprite
                    {
                        Texture = tex.Crop(new RectangleF(top_extension_x, 0, 1, tex.DisplayHeight)),
                        RelativeSizeAxes = Axes.X,
                        Width = 1,
                        X = top_extension_x,
                        Height = tex.DisplayHeight,
                    },
                };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Width = 1,
                Height = panel_height,
                Masking = true,
                MaskingSmoothness = 0,
                Depth = depth,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Children = sprites,
            };
        }

        // este overlay es full-screen, pero solo los controles interactivos tienen que agarrar input.
        // todo lo demas (incluida la textura decorativa songselect-top / la info del beatmap) deja pasar
        // asi se puede drag-scrollear el carousel donde sea que asome detras del panel.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => groupDropdown.ReceivePositionalInputAt(screenSpacePos)
               || sortDropdown.ReceivePositionalInputAt(screenSpacePos)
               || groupTabs.ReceivePositionalInputAt(screenSpacePos)
               || searchBox.ReceivePositionalInputAt(screenSpacePos);

        /// <summary>
        /// un label coloreado estilo stable ("Group" / "Sort"), anclado por el borde derecho a
        /// <paramref name="x"/> px del borde derecho de la pantalla, justo arriba de su dropdown (stable y=40 x1.6).
        /// </summary>
        private static Drawable label(LocalisableString text, Color4 colour, float x) => new StrokedLegacyText
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.BottomRight,
            // stable pone el label grande bien abajo, asi los descendentes (la "p" de Group) se meten
            // sobre la fila de tabs, igualito a como se ve en stable. detras de la decoracion (2.6 > 2),
            // como los tabs, asi el grafico mode-button del skin queda por encima.
            Position = new Vector2(x, 56),
            Depth = 2.6f,
            Text = text,
            Font = LegacyFonts.Get(34, FontWeight.Light),
            // texto tintado con el contorno fino oscuro de stable (reemplaza el drop shadow suave).
            Colour = colour,
        };
    }
}
