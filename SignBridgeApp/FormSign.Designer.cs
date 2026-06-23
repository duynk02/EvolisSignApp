namespace SignBridgeApp
{
    partial class FormSign
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.PictureBox picSignaturePreview;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnRetry;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Label lblTitle;

        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.picSignaturePreview = new System.Windows.Forms.PictureBox();
            this.btnConfirm = new System.Windows.Forms.Button();
            this.btnRetry = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.pnlButtons = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.picSignaturePreview)).BeginInit();
            this.pnlButtons.SuspendLayout();
            this.SuspendLayout();

            // lblTitle
            this.lblTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Height = 44;
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblTitle.Text = "XÁC NHẬN CHỮ KÝ";
            this.lblTitle.Name = "lblTitle";

            // lblStatus
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 10.5F);
            this.lblStatus.Height = 50;
            this.lblStatus.Padding = new System.Windows.Forms.Padding(15, 5, 15, 0);
            this.lblStatus.Text = "Đang kết nối thiết bị...";
            this.lblStatus.Name = "lblStatus";

            // picSignaturePreview
            this.picSignaturePreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picSignaturePreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picSignaturePreview.BackColor = System.Drawing.Color.White;
            this.picSignaturePreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picSignaturePreview.Margin = new System.Windows.Forms.Padding(15);
            this.picSignaturePreview.Name = "picSignaturePreview";

            // pnlButtons
            this.pnlButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlButtons.Height = 64;
            this.pnlButtons.Name = "pnlButtons";

            // btnConfirm
            this.btnConfirm.Text = "✓ Xác nhận hợp lệ";
            this.btnConfirm.Enabled = false;
            this.btnConfirm.BackColor = System.Drawing.Color.FromArgb(46, 160, 67);
            this.btnConfirm.ForeColor = System.Drawing.Color.White;
            this.btnConfirm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnConfirm.Size = new System.Drawing.Size(160, 42);
            this.btnConfirm.Location = new System.Drawing.Point(15, 12);
            this.btnConfirm.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.btnConfirm.Name = "btnConfirm";
            this.btnConfirm.Click += new System.EventHandler(this.btnConfirm_Click);

            // btnRetry
            this.btnRetry.Text = "↺ Ký lại";
            this.btnRetry.Size = new System.Drawing.Size(140, 42);
            this.btnRetry.Location = new System.Drawing.Point(185, 12);
            this.btnRetry.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnRetry.Name = "btnRetry";
            this.btnRetry.Click += new System.EventHandler(this.btnRetry_Click);

            // btnCancel
            this.btnCancel.Text = "✕ Hủy";
            this.btnCancel.Size = new System.Drawing.Size(120, 42);
            this.btnCancel.Location = new System.Drawing.Point(335, 12);
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            this.pnlButtons.Controls.Add(this.btnConfirm);
            this.pnlButtons.Controls.Add(this.btnRetry);
            this.pnlButtons.Controls.Add(this.btnCancel);

            // FormSign
            this.ClientSize = new System.Drawing.Size(700, 560);
            this.Controls.Add(this.picSignaturePreview);
            this.Controls.Add(this.pnlButtons);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Ký số - signotec Sigma";
            this.Name = "FormSign";

            ((System.ComponentModel.ISupportInitialize)(this.picSignaturePreview)).EndInit();
            this.pnlButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
