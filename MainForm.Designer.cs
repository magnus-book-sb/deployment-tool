namespace DeploymentTool
{
	partial class MainForm
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
            this.components = new System.ComponentModel.Container();
            this.BuildView = new BrightIdeasSoftware.TreeListView();
            this.btnDeploy = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnAbort = new System.Windows.Forms.Button();
            this.DeviceView = new BrightIdeasSoftware.TreeListView();
            ((System.ComponentModel.ISupportInitialize)(this.BuildView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.DeviceView)).BeginInit();
            this.SuspendLayout();
            // 
            // BuildView
            // 
            this.BuildView.CellEditUseWholeCell = false;
            this.BuildView.FullRowSelect = true;
            this.BuildView.GridLines = true;
            this.BuildView.HideSelection = false;
            this.BuildView.Location = new System.Drawing.Point(12, 24);
            this.BuildView.MultiSelect = false;
            this.BuildView.Name = "BuildView";
            this.BuildView.ShowGroups = false;
            this.BuildView.Size = new System.Drawing.Size(1008, 230);
            this.BuildView.TabIndex = 0;
            this.BuildView.UseCompatibleStateImageBehavior = false;
            this.BuildView.View = System.Windows.Forms.View.Details;
            this.BuildView.VirtualMode = true;
            // 
            // btnDeploy
            // 
            this.btnDeploy.Location = new System.Drawing.Point(432, 722);
            this.btnDeploy.Name = "btnDeploy";
            this.btnDeploy.Size = new System.Drawing.Size(75, 23);
            this.btnDeploy.TabIndex = 1;
            this.btnDeploy.Text = "Deploy";
            this.btnDeploy.UseVisualStyleBackColor = true;
            this.btnDeploy.Click += new System.EventHandler(this.btnDeploy_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 5);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(81, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Available Builds";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 269);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(60, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Device List";
            // 
            // btnAbort
            // 
            this.btnAbort.Location = new System.Drawing.Point(522, 722);
            this.btnAbort.Name = "btnAbort";
            this.btnAbort.Size = new System.Drawing.Size(75, 23);
            this.btnAbort.TabIndex = 4;
            this.btnAbort.Text = "Abort";
            this.btnAbort.UseVisualStyleBackColor = true;
            this.btnAbort.Click += new System.EventHandler(this.btnAbort_Click);
            // 
            // DeviceView
            // 
            this.DeviceView.CellEditUseWholeCell = false;
            this.DeviceView.FullRowSelect = true;
            this.DeviceView.GridLines = true;
            this.DeviceView.HideSelection = false;
            this.DeviceView.Location = new System.Drawing.Point(12, 287);
            this.DeviceView.Name = "DeviceView";
            this.DeviceView.ShowGroups = false;
            this.DeviceView.Size = new System.Drawing.Size(1008, 429);
            this.DeviceView.TabIndex = 5;
            this.DeviceView.UseCompatibleStateImageBehavior = false;
            this.DeviceView.View = System.Windows.Forms.View.Details;
            this.DeviceView.VirtualMode = true;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1031, 761);
            this.Controls.Add(this.DeviceView);
            this.Controls.Add(this.btnAbort);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnDeploy);
            this.Controls.Add(this.BuildView);
            this.Name = "MainForm";
            this.Text = "Starbreeze Internal Deployment Tool";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.BuildView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DeviceView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private BrightIdeasSoftware.TreeListView BuildView;
		private System.Windows.Forms.Button btnDeploy;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Button btnAbort;
        private BrightIdeasSoftware.TreeListView DeviceView;
    }
}

