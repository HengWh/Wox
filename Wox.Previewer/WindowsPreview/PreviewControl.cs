namespace Wox.Previewer.WindowsPreview
{
    public class PreviewControl : Control
    {
        public PreviewControl() : base()
        {
            BackColor = Color.White;
            // enable transparency
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.UserPaint, true);
        }
    }
}
