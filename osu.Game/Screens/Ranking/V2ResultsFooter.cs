// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Overlays;
using osu.Game.Scoring;
using osu.Game.Screens.Footer;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    public partial class V2ResultsFooter : CompositeDrawable
    {
        public const float DESIGN_HEIGHT = ScreenFooter.HEIGHT;

        public Action? BackAction { get; init; }
        public Action? RankingAction { get; init; }
        public Action? DetailsAction { get; init; }
        public Action? RetryAction { get; init; }
        public ScoreInfo? Score { get; init; }
        public bool AllowWatchingReplay { get; init; }

        public ScreenBackButton BackButton { get; private set; } = null!;
        public ScreenFooterButton ExportButton { get; private set; } = null!;
        public ScreenFooterButton RankingButton { get; private set; } = null!;
        public ScreenFooterButton DetailsButton { get; private set; } = null!;
        public ScreenFooterButton ReplayButton { get; private set; } = null!;
        public ScreenFooterButton RetryButton { get; private set; } = null!;

        protected override void Update()
        {
            base.Update();

            bool upload = GetContainingInputManager()?.CurrentState.Keyboard.ShiftPressed == true;
            if (screenshotUploadMode == upload)
                return;

            screenshotUploadMode = upload;
            screenshotButton.Text = upload ? ResultsScreenStrings.UploadScreenshotShort : ResultsScreenStrings.Screenshot;
            screenshotButton.TooltipText = upload ? GlobalActionKeyBindingStrings.TakeAndUploadScreenshot : GlobalActionKeyBindingStrings.TakeScreenshot;
            screenshotButton.Icon = upload ? FontAwesome.Solid.CloudUploadAlt : FontAwesome.Solid.Camera;
        }

        private const int padding = 60;
        private const float delay_per_button = 30;

        // ���� ScreenFooter���ر����ֲü�����Ϊ��ťԲ�ǺͶ����ᳬ�� 50px �ĵ����߶�
        public override bool UpdateSubTreeMasking() => false;

        private FillFlowContainer<ScreenFooterButton> buttonsFlow = null!;
        private Box background = null!;
        private ScreenFooterButton screenshotButton = null!;
        private bool screenshotUploadMode;

        private readonly Bindable<DownloadState> replayState = new Bindable<DownloadState>();
        private bool exportWhenDownloaded;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private ScoreModelDownloader downloader { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private OverlayColourProvider? colourProvider { get; set; }

        public V2ResultsFooter()
        {
            RelativeSizeAxes = Axes.X;
            Height = ScreenFooter.HEIGHT;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, ScreenshotManager? screenshots)
        {
            ScreenFooterButton[] buttons =
            {
                ExportButton = new ScreenFooterButton
                {
                    Text = GlobalActionKeyBindingStrings.SaveReplay,
                    Icon = FontAwesome.Solid.Download,
                    AccentColour = colours.Blue2,
                    Hotkey = GlobalAction.ExportReplay,
                    Action = exportReplay,
                },
                screenshotButton = new ScreenFooterButton
                {
                    Text = ResultsScreenStrings.Screenshot,
                    Icon = FontAwesome.Solid.Camera,
                    AccentColour = colours.Pink1,
                    Action = () =>
                    {
                        if (GetContainingInputManager()?.CurrentState.Keyboard.ShiftPressed == true)
                            _ = screenshots?.UploadScreenshotAsync();
                        else
                            _ = screenshots?.TakeScreenshotAsync();
                    },
                    Enabled = { Value = screenshots != null },
                    TooltipText = GlobalActionKeyBindingStrings.TakeScreenshot,
                },
                RankingButton = new ScreenFooterButton
                {
                    Text = SongSelectStrings.Ranking,
                    Icon = FontAwesome.Solid.Sitemap,
                    AccentColour = colours.Purple1,
                    Action = () => RankingAction?.Invoke(),
                    Enabled = { Value = RankingAction != null },
                    TooltipText = SongSelectStrings.Ranking,
                },
                DetailsButton = new ScreenFooterButton
                {
                    Text = ResultsScreenStrings.MoreInfo,
                    Icon = FontAwesome.Solid.InfoCircle,
                    AccentColour = colours.Purple2,
                    Action = () => DetailsAction?.Invoke(),
                    Enabled = { Value = DetailsAction != null },
                    TooltipText = ResultsScreenStrings.MoreInfo,
                },
                ReplayButton = new ScreenFooterButton
                {
                    Text = SongSelectStrings.WatchReplay,
                    Icon = FontAwesome.Solid.Film,
                    AccentColour = colours.Yellow,
                    Hotkey = GlobalAction.SaveReplay,
                    Action = watchReplay,
                },
                RetryButton = new ScreenFooterButton
                {
                    Text = ResultsScreenStrings.PlayAgain,
                    Icon = FontAwesome.Solid.Redo,
                    AccentColour = colours.Green1,
                    Action = () => RetryAction?.Invoke(),
                    Enabled = { Value = RetryAction != null },
                    TooltipText = ResultsScreenStrings.PlayAgain,
                },
            };

            InternalChildren = new Drawable[]
            {
                // 1. ������ɫ�ĵף����� ScreenFooter��
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider?.Background5 ?? Colour4.FromHex("#1C2125"),
                },
                // 2. ��ť��������λ�á������ȫ���� ScreenFooter��
                new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Padding = new MarginPadding { Left = OsuGame.SCREEN_EDGE_MARGIN + ScreenBackButton.BUTTON_WIDTH + padding },
                    AutoSizeAxes = Axes.Both,
                    Child = buttonsFlow = new FillFlowContainer<ScreenFooterButton>
                    {
                        Name = "Visible footer buttons",
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Y = ScreenFooterButton.CORNER_RADIUS,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(7, 0),
                        AutoSizeAxes = Axes.Both,
                    }
                },
                // 3. ���ذ�ť����ȫ���� ScreenFooter��
                BackButton = new ScreenBackButton
                {
                    Margin = new MarginPadding { Bottom = OsuGame.SCREEN_EDGE_MARGIN, Left = OsuGame.SCREEN_EDGE_MARGIN },
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Action = () => BackAction?.Invoke(),
                },
                // 4. ȫ�� ESC ���ؼ�����������ȫ���� ScreenFooter��
                new ScreenFooter.BackReceptor
                {
                    OnBackPressed = () => BackButton.TriggerClick(),
                }
            };

            // ���Ӱ�ť������ ScreenFooter һģһ������������볡����
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                // AppearFromBottom fades the button back in, so alpha alone cannot hide unavailable actions.
                if (button == RetryButton && RetryAction == null)
                    continue;

                buttonsFlow.Add(button);

                int index = i;
                button.OnLoadComplete += _ => button.AppearFromBottom(index * delay_per_button);
            }

            if (Score != null && AllowWatchingReplay)
                AddInternal(new ScoreDownloadTracker(Score) { State = { BindTarget = replayState } });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            replayState.BindValueChanged(_ => updateReplayState(), true);
        }

        private void updateReplayState()
        {
            bool local = replayState.Value == DownloadState.LocallyAvailable;
            bool busy = replayState.Value is DownloadState.Downloading or DownloadState.Importing;
            bool available = AllowWatchingReplay && Score != null && (local || Score.HasOnlineReplay);

            if (replayState.Value == DownloadState.NotDownloaded)
                exportWhenDownloaded = false;

            ExportButton.Enabled.Value = available && !busy;
            ReplayButton.Enabled.Value = available && !busy && game != null;
            ReplayButton.TooltipText = busy ? ResultsScreenStrings.DownloadingReplay : available ? SongSelectStrings.WatchReplay : ResultsScreenStrings.ReplayUnavailable;
            ExportButton.TooltipText = busy ? ResultsScreenStrings.DownloadingReplay : available ? GlobalActionKeyBindingStrings.ExportReplay : ResultsScreenStrings.ReplayUnavailable;

            if (local && exportWhenDownloaded)
            {
                exportWhenDownloaded = false;
                _ = scoreManager.Export(Score!);
            }
        }

        private void watchReplay()
        {
            if (!ReplayButton.Enabled.Value || Score == null)
                return;

            if (replayState.Value == DownloadState.LocallyAvailable)
                game?.PresentScore(Score, ScorePresentType.Gameplay);
            else
                downloader.Download(Score);
        }

        private void exportReplay()
        {
            if (!ExportButton.Enabled.Value || Score == null)
                return;

            if (replayState.Value == DownloadState.LocallyAvailable)
                _ = scoreManager.Export(Score);
            else
            {
                exportWhenDownloaded = true;
                downloader.Download(Score);
            }
        }
    }
}
