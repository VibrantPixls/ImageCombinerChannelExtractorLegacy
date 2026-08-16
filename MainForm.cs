using Image_Combiner.components.classes;
using Image_Combiner.components.enums;
using System.Drawing.Imaging;

namespace Image_Combiner
{
    public partial class MainForm : Form
    {
        private static readonly (ColorChannelEnum Channel, Color PadColor)[] CombineChannelOrder =
        {
            (ColorChannelEnum.Red, Color.Black),
            (ColorChannelEnum.Green, Color.Black),
            (ColorChannelEnum.Blue, Color.Black),
            (ColorChannelEnum.Alpha, Color.White),
        };

        private readonly Dictionary<ColorChannelEnum, ChannelSlot> _channels;
        private readonly Dictionary<ColorChannelEnum, ExtractedSlot> _extracted;

        private string? extractPNGPath;
        private Bitmap? bitmapExtractSource;

        private int resHorizontal;
        private int resVertical;

        private int resHorizontalExtract;
        private int resVerticalExtract;

        private bool differentResolutions;
        private bool extractAllInGreyscale;
        private bool extractAlphaAsAlpha;

        private readonly LoadingOverlay loadingOverlay = new();

        // cache for combnined
        private Bitmap? _combinedPreviewCache;
        private bool _combinedPreviewDirty = true;
        private bool _isBuildingCombinedPreview;

        public MainForm()
        {
            InitializeComponent();

            _channels = new Dictionary<ColorChannelEnum, ChannelSlot>
            {
                [ColorChannelEnum.Red] = new ChannelSlot { Label = lblRed, SelectButton = btnSelectRed, DeleteButton = btnDeleteRed, DisplayName = "Red", PreviewColor = Color.FromArgb(192, 0, 0) },
                [ColorChannelEnum.Green] = new ChannelSlot { Label = lblGreen, SelectButton = btnSelectGreen, DeleteButton = btnDeleteGreen, DisplayName = "Green", PreviewColor = Color.FromArgb(0, 192, 0) },
                [ColorChannelEnum.Blue] = new ChannelSlot { Label = lblBlue, SelectButton = btnSelectBlue, DeleteButton = btnDeleteBlue, DisplayName = "Blue", PreviewColor = Color.FromArgb(0, 0, 192) },
                [ColorChannelEnum.Alpha] = new ChannelSlot { Label = lblAlpha, SelectButton = btnSelectAlpha, DeleteButton = btnDeleteAlpha, DisplayName = "Alpha", PreviewColor = Color.Gray },
            };

            _extracted = new Dictionary<ColorChannelEnum, ExtractedSlot>
            {
                [ColorChannelEnum.Red] = new ExtractedSlot { PreviewBox = previewboxExtractRed, DisplayName = "Red" },
                [ColorChannelEnum.Green] = new ExtractedSlot { PreviewBox = previewboxExtractGreen, DisplayName = "Green" },
                [ColorChannelEnum.Blue] = new ExtractedSlot { PreviewBox = previewboxExtractBlue, DisplayName = "Blue" },
                [ColorChannelEnum.Alpha] = new ExtractedSlot { PreviewBox = previewboxExtractAlpha, DisplayName = "Alpha" },
            };

            Controls.Add(loadingOverlay);
            loadingOverlay.BringToFront();

            foreach (KeyValuePair<ColorChannelEnum, ChannelSlot> entry in _channels)
            {
                ColorChannelEnum channel = entry.Key;
                entry.Value.SelectButton.MouseEnter += (s, e) => ShowChannelPreview(channel);
                entry.Value.DeleteButton.MouseEnter += (s, e) => ShowChannelPreview(channel);
            }

            btnCreate.MouseEnter += async (s, e) => await ShowCombinedPreviewAsync();

            StopLoading();

            //Extracting panel
            pnlAlphaExtract.Visible = false;
        }

        #region Input channel selection

        private void btnSelectRed_Click(object sender, EventArgs e) => SelectChannel(ColorChannelEnum.Red);
        private void btnSelectGreen_Click(object sender, EventArgs e) => SelectChannel(ColorChannelEnum.Green);
        private void btnSelectBlue_Click(object sender, EventArgs e) => SelectChannel(ColorChannelEnum.Blue);
        private void btnSelectAlpha_Click(object sender, EventArgs e) => SelectChannel(ColorChannelEnum.Alpha);

        private void btnDeleteRed_Click(object sender, EventArgs e) => DeleteChannel(ColorChannelEnum.Red);
        private void btnDeleteGreen_Click(object sender, EventArgs e) => DeleteChannel(ColorChannelEnum.Green);
        private void btnDeleteBlue_Click(object sender, EventArgs e) => DeleteChannel(ColorChannelEnum.Blue);
        private void btnDeleteAlpha_Click(object sender, EventArgs e) => DeleteChannel(ColorChannelEnum.Alpha);

        private void SelectChannel(ColorChannelEnum channel) => LoadChannelFromPath(channel, SelectPNGFile());

        private void LoadChannelFromPath(ColorChannelEnum channel, string? path)
        {
            ShowLoading("Importing image...", 0, 0);
            if (path != null)
            {
                ChannelSlot slot = _channels[channel];
                slot.Path = path;
                slot.Label.Text = $"{slot.DisplayName} channel: {Path.GetFileName(path)}";
                SafeDispose(ref slot.Bitmap);
                slot.Bitmap = LoadImageIndependent(path);
                UpdateResolution();
            }
            StopLoading();
        }

        private void DeleteChannel(ColorChannelEnum channel)
        {
            ChannelSlot slot = _channels[channel];
            slot.Path = null;
            slot.Label.Text = "No Image Selected";
            SafeDispose(ref slot.Bitmap);
            ClearPreview(previewBox);
            UpdateResolution();
        }

        private void ShowChannelPreview(ColorChannelEnum channel)
        {
            ChannelSlot slot = _channels[channel];
            if (slot.Path is null)
            {
                return;
            }

            previewBox.Image?.Dispose();
            previewBox.Image = LoadImageIndependent(slot.Path);

            lblPreview.Text = $"{slot.DisplayName} channel";
            lblPreview.ForeColor = slot.PreviewColor;
        }

        private List<ColorChannelEnum> GetFilledColorChannels() => _channels.Where(kv => kv.Value.Bitmap != null).Select(kv => kv.Key).ToList();

        private void UpdateResolution()
        {
            List<ColorChannelEnum> filledChannels = GetFilledColorChannels();

            if (filledChannels.Count == 0)
            {
                differentResolutions = false;
                resHorizontal = 0;
                resVertical = 0;
                lblRes.Text = string.Empty;
                InvalidateCombinedPreview();
                return;
            }

            List<Size> sizes = filledChannels.Select(c => _channels[c].Bitmap!.Size).ToList();
            differentResolutions = sizes.Distinct().Count() > 1;

            resHorizontal = sizes.Max(s => s.Width);
            resVertical = sizes.Max(s => s.Height);

            lblRes.Text = differentResolutions ? $"Mismatched sizes - output will be {resHorizontal}x{resVertical}" : $"{resHorizontal}x{resVertical}";

            InvalidateCombinedPreview();
        }

        private void InvalidateCombinedPreview()
        {
            _combinedPreviewDirty = true;
            SafeDispose(ref _combinedPreviewCache);
        }

        #endregion

        #region Combine

        private async void btnCreate_Click(object sender, EventArgs e)
        {
            List<ColorChannelEnum> filledChannels = GetFilledColorChannels();
            if (filledChannels.Count == 0)
            {
                MessageBox.Show("Please input at least one PNG first", "Missing Requirements", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowLoading("Combining images...", 4, 67);

            Bitmap finalImage;
            if (!_combinedPreviewDirty && _combinedPreviewCache != null)
            {
                finalImage = new Bitmap(_combinedPreviewCache);
            }
            else
            {
                int width = resHorizontal;
                int height = resVertical;

                Dictionary<ColorChannelEnum, Bitmap?> sources = _channels.ToDictionary(kv => kv.Key, kv => kv.Value.Bitmap);
                IProgress<double> progress = CreateProgressReporter(4, 67);

                finalImage = await Task.Run(() => CombineChannels(sources, width, height, progress));
            }

            using (finalImage)
            {
                ShowLoading("Exporting image...", 12, 56);
                using (SaveFileDialog sfd = new() { Filter = "PNG Image|*.png" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        finalImage.Save(sfd.FileName, ImageFormat.Png);
                        MessageBox.Show("Combined PNG created successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            StopLoading();
        }

        private async Task ShowCombinedPreviewAsync()
        {
            List<ColorChannelEnum> filledChannels = GetFilledColorChannels();
            if (filledChannels.Count == 0 || _isBuildingCombinedPreview)
            {
                return;
            }

            if (!_combinedPreviewDirty && _combinedPreviewCache != null)
            {
                SetPreview(previewBox, _combinedPreviewCache);
                lblPreview.Text = "Combined preview";
                lblPreview.ForeColor = Color.Black;
                return;
            }

            _isBuildingCombinedPreview = true;
            try
            {
                int width = resHorizontal;
                int height = resVertical;
                Dictionary<ColorChannelEnum, Bitmap?> sources = _channels.ToDictionary(kv => kv.Key, kv => kv.Value.Bitmap);

                Bitmap preview = await Task.Run(() => CombineChannels(sources, width, height));

                SafeDispose(ref _combinedPreviewCache);
                _combinedPreviewCache = preview;
                _combinedPreviewDirty = false;

                SetPreview(previewBox, _combinedPreviewCache);
                lblPreview.Text = "Combined preview";
                lblPreview.ForeColor = Color.Black;
            }
            finally
            {
                _isBuildingCombinedPreview = false;
            }
        }

        private static Bitmap CombineChannels(Dictionary<ColorChannelEnum, Bitmap?> sources, int width, int height, IProgress<double>? progress = null)
        {
            Rectangle rect = new(0, 0, width, height);
            Bitmap finalImage = new(width, height, PixelFormat.Format32bppArgb);
            BitmapData bdFinal = finalImage.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            List<Bitmap> tempBitmaps = new();
            Bitmap[] prepared = new Bitmap[CombineChannelOrder.Length];
            bool[] hasAlpha = new bool[CombineChannelOrder.Length];

            for (int i = 0; i < CombineChannelOrder.Length; i++)
            {
                (ColorChannelEnum channel, Color padColor) = CombineChannelOrder[i];
                sources.TryGetValue(channel, out Bitmap? source);

                if (source != null)
                {
                    hasAlpha[i] = HasAlphaChannel(source);
                    prepared[i] = PrepareChannelSource(source, width, height, padColor, tempBitmaps);
                }
                else
                {
                    Bitmap blank = new(width, height, PixelFormat.Format32bppArgb);
                    if (channel == ColorChannelEnum.Alpha)
                    {
                        using Graphics g = Graphics.FromImage(blank);
                        g.Clear(Color.White);
                    }
                    tempBitmaps.Add(blank);
                    prepared[i] = blank;
                }
            }

            BitmapData[] locks = prepared.Select(b => b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)).ToArray();
            try
            {
                IntPtr redScan = locks[0].Scan0, greenScan = locks[1].Scan0, blueScan = locks[2].Scan0, alphaScan = locks[3].Scan0;
                IntPtr finalScan = bdFinal.Scan0;
                int stride = locks[0].Stride;
                bool redHasAlpha = hasAlpha[0], greenHasAlpha = hasAlpha[1], blueHasAlpha = hasAlpha[2], alphaHasAlpha = hasAlpha[3];

                int reportStep = Math.Max(1, height / 100);
                int rowsDone = 0;

                Parallel.For(0, height, y =>
                {
                    unsafe
                    {
                        byte* rowRed = (byte*)redScan + y * stride;
                        byte* rowGreen = (byte*)greenScan + y * stride;
                        byte* rowBlue = (byte*)blueScan + y * stride;
                        byte* rowAlpha = (byte*)alphaScan + y * stride;
                        byte* rowFinal = (byte*)finalScan + y * stride;

                        for (int x = 0; x < width; x++)
                        {
                            int idx = x * 4;

                            rowFinal[idx + 0] = ToGrayscale(rowBlue, idx, blueHasAlpha);
                            rowFinal[idx + 1] = ToGrayscale(rowGreen, idx, greenHasAlpha);
                            rowFinal[idx + 2] = ToGrayscale(rowRed, idx, redHasAlpha);
                            rowFinal[idx + 3] = ToGrayscale(rowAlpha, idx, alphaHasAlpha);
                        }
                    }

                    if (progress != null)
                    {
                        int done = Interlocked.Increment(ref rowsDone);
                        if (done % reportStep == 0 || done == height)
                        {
                            progress.Report((double)done / height);
                        }
                    }
                });
            }
            finally
            {
                for (int i = 0; i < prepared.Length; i++)
                {
                    prepared[i].UnlockBits(locks[i]);
                }
                finalImage.UnlockBits(bdFinal);

                foreach (Bitmap temp in tempBitmaps)
                {
                    temp.Dispose();
                }
            }

            return finalImage;
        }

        private static unsafe byte ToGrayscale(byte* row, int idx, bool sourceHasAlpha)
        {
            byte b = row[idx + 0];
            byte g = row[idx + 1];
            byte r = row[idx + 2];
            byte a = sourceHasAlpha ? row[idx + 3] : (byte)255;

            int gray = (77 * r + 151 * g + 28 * b) >> 8;
            return (byte)((gray * a + 255 * (255 - a)) / 255);
        }

        private static Bitmap PrepareChannelSource(Bitmap source, int width, int height, Color padColor, List<Bitmap> tempBitmaps)
        {
            if (source.Width == width && source.Height == height)
            {
                return source;
            }
            Bitmap fitted = FitToCanvas(source, width, height, padColor);
            tempBitmaps.Add(fitted);
            return fitted;
        }

        private static Bitmap FitToCanvas(Bitmap source, int canvasWidth, int canvasHeight, Color padColor)
        {
            Bitmap canvas = new(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);

            float scale = Math.Min((float)canvasWidth / source.Width, (float)canvasHeight / source.Height);
            int scaledWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int scaledHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            int offsetX = (canvasWidth - scaledWidth) / 2;
            int offsetY = (canvasHeight - scaledHeight) / 2;

            using Graphics g = Graphics.FromImage(canvas);
            g.Clear(padColor);
            g.DrawImage(source, offsetX, offsetY, scaledWidth, scaledHeight);
            return canvas;
        }

        #endregion

        #region Extract

        private async void btnExtractPNG_Click(object sender, EventArgs e)
        {
            ShowLoading("Importing image...", 0, 0);
            string? path = SelectPNGFile();
            await LoadExtractSourceAsync(path);
        }

        private async Task LoadExtractSourceAsync(string? path)
        {
            if (path != null)
            {
                extractPNGPath = path;
                lblExtractPNG.Text = $"Combined: {Path.GetFileName(extractPNGPath)}";
                SafeDispose(ref bitmapExtractSource);
                bitmapExtractSource = LoadImageIndependent(path);
                resHorizontalExtract = bitmapExtractSource.Width;
                resVerticalExtract = bitmapExtractSource.Height;
                SetPreview(previewboxExtractPNG, bitmapExtractSource);
                await ExtractInputImageAsync();
            }
            StopLoading();
        }

        private void btnDeleteExtractPNG_Click(object sender, EventArgs e)
        {
            extractPNGPath = null;
            lblExtractPNG.Text = "No Image Selected";
            SafeDispose(ref bitmapExtractSource);
            DeleteAllExtractedBitmaps();
            ClearPreview(previewboxExtractPNG);
        }

        private void DeleteAllExtractedBitmaps()
        {
            foreach (ExtractedSlot slot in _extracted.Values)
            {
                SafeDispose(ref slot.Bitmap);
                ClearPreview(slot.PreviewBox);
            }
            pnlAlphaExtract.Visible = false;
        }

        private async void checkBoxExtractGrey_CheckedChanged(object sender, EventArgs e)
        {
            extractAllInGreyscale = checkBoxExtractGrey.Checked;
            DeleteAllExtractedBitmaps();
            await ExtractInputImageAsync();
        }

        private async void checkBoxAlphaExtractOpaque_CheckedChanged(object sender, EventArgs e)
        {
            extractAlphaAsAlpha = !checkBoxAlphaExtractOpaque.Checked;
            DeleteAllExtractedBitmaps();
            await ExtractInputImageAsync();
        }

        private async Task ExtractInputImageAsync()
        {
            if (bitmapExtractSource == null)
            {
                return;
            }

            ShowLoading("Extracting color channels...", 4, 34);

            Bitmap source = bitmapExtractSource;
            int width = resHorizontalExtract;
            int height = resVerticalExtract;
            bool greyscale = extractAllInGreyscale;
            bool alphaAsAlpha = extractAlphaAsAlpha;
            IProgress<double> progress = CreateProgressReporter(4, 34);

            (Bitmap red, Bitmap green, Bitmap blue, Bitmap? alpha, bool hasAlpha) = await Task.Run(() => ExtractChannels(source, width, height, greyscale, alphaAsAlpha, progress));

            pnlAlphaExtract.Visible = hasAlpha;
            SetExtracted(ColorChannelEnum.Red, red);
            SetExtracted(ColorChannelEnum.Green, green);
            SetExtracted(ColorChannelEnum.Blue, blue);

            if (hasAlpha && alpha != null)
            {
                SetExtracted(ColorChannelEnum.Alpha, alpha);
            }
            else
            {
                _extracted[ColorChannelEnum.Alpha].Bitmap = null;
            }

            StopLoading();
            void SetExtracted(ColorChannelEnum channel, Bitmap bitmap)
            {
                ExtractedSlot slot = _extracted[channel];
                SafeDispose(ref slot.Bitmap);
                slot.Bitmap = bitmap;
                SetPreview(slot.PreviewBox, bitmap);
            }
        }

        private static (Bitmap Red, Bitmap Green, Bitmap Blue, Bitmap? Alpha, bool HasAlpha) ExtractChannels(Bitmap source, int width, int height, bool greyscale, bool alphaAsAlpha, IProgress<double>? progress = null)
        {
            Bitmap redBmp = new(width, height, PixelFormat.Format32bppArgb);
            Bitmap greenBmp = new(width, height, PixelFormat.Format32bppArgb);
            Bitmap blueBmp = new(width, height, PixelFormat.Format32bppArgb);

            bool hasAlpha = HasAlphaChannel(source);
            Bitmap? alphaBmp = hasAlpha ? new Bitmap(width, height, PixelFormat.Format32bppArgb) : null;

            Rectangle rect = new(0, 0, width, height);
            BitmapData srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData redData = redBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            BitmapData greenData = greenBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            BitmapData blueData = blueBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            BitmapData? alphaData = alphaBmp?.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                IntPtr srcScan = srcData.Scan0, rScan = redData.Scan0, gScan = greenData.Scan0, bScan = blueData.Scan0;
                IntPtr aScan = alphaData?.Scan0 ?? IntPtr.Zero;
                int stride = srcData.Stride;

                int reportStep = Math.Max(1, height / 100);
                int rowsDone = 0;

                Parallel.For(0, height, y =>
                {
                    unsafe
                    {
                        byte* srcRow = (byte*)srcScan + y * stride;
                        byte* rRow = (byte*)rScan + y * stride;
                        byte* gRow = (byte*)gScan + y * stride;
                        byte* bRow = (byte*)bScan + y * stride;
                        byte* aRow = hasAlpha ? (byte*)aScan + y * stride : null;

                        for (int x = 0; x < width; x++)
                        {
                            int idx = x * 4;
                            byte b = srcRow[idx + 0];
                            byte g = srcRow[idx + 1];
                            byte r = srcRow[idx + 2];
                            byte a = srcRow[idx + 3];

                            if (greyscale)
                            {
                                rRow[idx + 0] = r; rRow[idx + 1] = r; rRow[idx + 2] = r; rRow[idx + 3] = 255;
                                gRow[idx + 0] = g; gRow[idx + 1] = g; gRow[idx + 2] = g; gRow[idx + 3] = 255;
                                bRow[idx + 0] = b; bRow[idx + 1] = b; bRow[idx + 2] = b; bRow[idx + 3] = 255;
                            }
                            else
                            {
                                rRow[idx + 0] = 0; rRow[idx + 1] = 0; rRow[idx + 2] = r; rRow[idx + 3] = 255;
                                gRow[idx + 0] = 0; gRow[idx + 1] = g; gRow[idx + 2] = 0; gRow[idx + 3] = 255;
                                bRow[idx + 0] = b; bRow[idx + 1] = 0; bRow[idx + 2] = 0; bRow[idx + 3] = 255;
                            }

                            if (hasAlpha)
                            {
                                byte alphaOut = alphaAsAlpha ? a : (byte)255;
                                aRow[idx + 0] = a; aRow[idx + 1] = a; aRow[idx + 2] = a; aRow[idx + 3] = alphaOut;
                            }
                        }
                    }

                    if (progress != null)
                    {
                        int done = Interlocked.Increment(ref rowsDone);
                        if (done % reportStep == 0 || done == height)
                        {
                            progress.Report((double)done / height);
                        }
                    }
                });
            }
            finally
            {
                source.UnlockBits(srcData);
                redBmp.UnlockBits(redData);
                greenBmp.UnlockBits(greenData);
                blueBmp.UnlockBits(blueData);

                if (alphaBmp != null && alphaData != null)
                {
                    alphaBmp.UnlockBits(alphaData);
                }
            }

            return (redBmp, greenBmp, blueBmp, alphaBmp, hasAlpha);
        }

        private void btnExtractRed_Click(object sender, EventArgs e) => SaveExtractedChannel(ColorChannelEnum.Red);
        private void btnExtractGreen_Click(object sender, EventArgs e) => SaveExtractedChannel(ColorChannelEnum.Green);
        private void btnExtractBlue_Click(object sender, EventArgs e) => SaveExtractedChannel(ColorChannelEnum.Blue);
        private void btnExtractAlpha_Click(object sender, EventArgs e) => SaveExtractedChannel(ColorChannelEnum.Alpha);

        private void SaveExtractedChannel(ColorChannelEnum channel)
        {
            ExtractedSlot slot = _extracted[channel];
            if (slot.Bitmap == null)
            {
                return;
            }

            ShowLoading("Exporting image...", 12, 56);
            using (SaveFileDialog sfd = new() { Filter = "PNG Image|*.png" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    slot.Bitmap.Save(sfd.FileName, ImageFormat.Png);
                    MessageBox.Show($"{slot.DisplayName} channel extracted successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            StopLoading();
        }

        #endregion

        #region Shared helpers

        private static Bitmap LoadImageIndependent(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using MemoryStream ms = new(bytes);
            using Bitmap fileBound = new(ms);
            return new Bitmap(fileBound);
        }

        private static string? SelectPNGFile()
        {
            using OpenFileDialog ofd = new() { Filter = "PNG files (*.png)|*.png|JPEG Image|*.jpg" };
            return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
        }

        private static bool HasAlphaChannel(Bitmap bmp) => Image.IsAlphaPixelFormat(bmp.PixelFormat);

        private static void SafeDispose(ref Bitmap? bmp)
        {
            bmp?.Dispose();
            bmp = null;
        }

        private void ClearPreview(CheckerboardPictureBox box)
        {
            if (box.Image != null)
            {
                box.Image.Dispose();
                box.Image = null;
                lblPreview.Text = string.Empty;
            }
        }

        private static void SetPreview(CheckerboardPictureBox box, Bitmap source)
        {
            box.Image?.Dispose();
            box.Image = new Bitmap(source);
        }

        private void ShowLoading(string loadingText, int minimumProgress, int maximumProgress)
        {
            UseWaitCursor = true;
            loadingOverlay.ShowLoading(loadingText, minimumProgress, maximumProgress);
        }

        private IProgress<double> CreateProgressReporter(int minimumProgress, int maximumProgress)
        {
            return new Progress<double>(fraction =>
            {
                int range = maximumProgress - minimumProgress;
                int value = minimumProgress + (int)Math.Round(range * Math.Clamp(fraction, 0.0, 1.0));
                loadingOverlay.SetProgress(value);
            });
        }

        private void StopLoading()
        {
            loadingOverlay.StopLoading();
            UseWaitCursor = false;
        }
        #endregion
    }
}
