using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
    public partial class DeviceForm : Form
    {
        private ILogger Logger;

        private ITargetDevice TargetDevice;

        public Device DeviceConfig { get; set; }

        public DeviceForm()
        {
            InitializeComponent();

            this.Logger = new ListViewLogger(this, DeviceLog);
        }

        private void DeviceForm_Load(object sender, EventArgs e)
        {
            this.Text = string.Format("Device - {0} {1}", DeviceConfig.Name, DeviceConfig.Address);
            this.TargetDevice = DeviceFactory.CreateTargetDevice(DeviceConfig, DeviceConfig.Build, Logger);

            FillDeviceStatus();
        }

        private void FillDeviceStatus()
        {
            DeviceStatus.Items.Clear();

            string ProcessRunning = TargetDevice.IsProcessRunning() ? "Running" : "Not running";

            string Status = string.Format("{0}", NetworkHelper.PingDevice(DeviceConfig.Address, Logger) ? "Online" : "Offline");

            string[] row = { Status, ProcessRunning, DeviceConfig.Build.Number, DeviceConfig.Build.Platform, DeviceConfig.Build.Role };

            ListViewItem Item = new ListViewItem(row);

            DeviceStatus.Items.Add(Item);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                TargetDevice.StartProcess();
            }
            catch(Exception Ex)
            {
                MessageBox.Show(string.Format("Failed to start game instance, {0}", Ex.Message), "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            FillDeviceStatus();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            try
            {
                TargetDevice.StopProcess();
            }
            catch (Exception Ex)
            {
                MessageBox.Show(string.Format("Failed to start game instance, {0}", Ex.Message), "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            FillDeviceStatus();
        }
    }
}
