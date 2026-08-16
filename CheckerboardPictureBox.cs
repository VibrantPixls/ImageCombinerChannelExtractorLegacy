namespace Image_Combiner
{
    public class CheckerboardPictureBox : PictureBox
    {
        protected override void OnPaint(PaintEventArgs pe)
        {
            int cellSize = 10;
            using (Brush lightBrush = new SolidBrush(Color.Gainsboro))
            using (Brush darkBrush = new SolidBrush(Color.LightGray))
            {
                for (int y = 0; y < Height; y += cellSize)
                {
                    for (int x = 0; x < Width; x += cellSize)
                    {
                        bool isLight = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                        pe.Graphics.FillRectangle(isLight ? lightBrush : darkBrush, x, y, cellSize, cellSize);
                    }
                }
            }
            base.OnPaint(pe);
        }
    }
}
