namespace Image_Combiner.components.classes
{
    internal class ChannelSlot
    {
        internal string? Path;
        internal Bitmap? Bitmap;
        internal required Label Label;
        internal required Button SelectButton;
        internal required Button DeleteButton;
        internal required string DisplayName;
        internal required Color PreviewColor;
    }
}
