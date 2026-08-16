namespace Image_Combiner
{
    partial class LoadingOverlay
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lblLoadingText = new Label();
            progressBarLoading = new ProgressBar();
            flowLayoutPanel1 = new FlowLayoutPanel();
            flowLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // lblLoadingText
            // 
            lblLoadingText.Anchor = AnchorStyles.None;
            lblLoadingText.AutoSize = true;
            lblLoadingText.BackColor = Color.Transparent;
            lblLoadingText.Font = new Font("Segoe UI", 27.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblLoadingText.Location = new Point(231, 25);
            lblLoadingText.Name = "lblLoadingText";
            lblLoadingText.Size = new Size(193, 50);
            lblLoadingText.TabIndex = 0;
            lblLoadingText.Text = "Loading...";
            lblLoadingText.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // progressBarLoading
            // 
            progressBarLoading.Anchor = AnchorStyles.None;
            progressBarLoading.Location = new Point(28, 90);
            progressBarLoading.Margin = new Padding(3, 15, 3, 3);
            progressBarLoading.Name = "progressBarLoading";
            progressBarLoading.Size = new Size(600, 30);
            progressBarLoading.TabIndex = 1;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = AnchorStyles.None;
            flowLayoutPanel1.AutoSize = true;
            flowLayoutPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowLayoutPanel1.Controls.Add(lblLoadingText);
            flowLayoutPanel1.Controls.Add(progressBarLoading);
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.Location = new Point(84, 214);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Padding = new Padding(25);
            flowLayoutPanel1.Size = new Size(656, 148);
            flowLayoutPanel1.TabIndex = 2;
            flowLayoutPanel1.WrapContents = false;
            // 
            // LoadingOverlay
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(flowLayoutPanel1);
            Name = "LoadingOverlay";
            Size = new Size(800, 550);
            Resize += LoadingOverlay_Resize;
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private void LoadingOverlay_Resize(object sender, System.EventArgs e)
        {
            CenterFlowLayoutPanel();
        }

        private void CenterFlowLayoutPanel()
        {
            if (flowLayoutPanel1 != null) flowLayoutPanel1.Location = new Point((this.Width - flowLayoutPanel1.Width) / 2, (this.Height - flowLayoutPanel1.Height) / 2);
        }

        private Label lblLoadingText;
        private ProgressBar progressBarLoading;
        private FlowLayoutPanel flowLayoutPanel1;
    }
}