namespace Image_Combiner
{
    public partial class LoadingOverlay : UserControl
    {
        private readonly Random random = new Random();

        public LoadingOverlay()
        {
            InitializeComponent();
        }

        public void ShowLoading(string loadingText, int minimumProgress, int maximumProgress)
        {
            lblLoadingText.Text = loadingText;
            progressBarLoading.Value = random.Next(minimumProgress, maximumProgress + 1);
            Visible = true;
            BringToFront();
            Update();
        }

        public void StopLoading()
        {
            Visible = false;
        }

        public void SetProgress(int progress)
        {
            progressBarLoading.Value = progress;
        }
    }
}
