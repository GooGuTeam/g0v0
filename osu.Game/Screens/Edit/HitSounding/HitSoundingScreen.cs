namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingScreen : EditorScreen
    {
        public HitSoundingScreen()
            : base(EditorScreenMode.HitSounding)
        {
            Child = new ScreenWhiteBox.UnderConstructionMessage("The new hitsounding mode");
        }
    }
}
