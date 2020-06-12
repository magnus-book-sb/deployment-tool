using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeploymentTool
{
    public class OrbisCtrl
    {
        private TargetDevicePS4 Device;
        private ILogger Logger;
        private IDeploymentSession Callback;
        private CancellationToken Token;
        private string OrbisCtrlPath;
        private Process OrbisCtrlProcess;

        public OrbisCtrl(TargetDevicePS4 Device, ILogger Logger, IDeploymentSession Callback, CancellationToken Token)
        {
            this.Device = Device;
            this.Logger = Logger;
            this.Callback = Callback;
            this.Token = Token;
            this.OrbisCtrlPath = Path.Combine(Environment.ExpandEnvironmentVariables("%SCE_ROOT_DIR%"), @"ORBIS\Tools\Target Manager Server\bin\orbis-ctrl.exe");
        }

        public bool HasExited(out int ExitCode)
        {
            if (OrbisCtrlProcess != null && OrbisCtrlProcess.HasExited)
            {
                ExitCode = OrbisCtrlProcess.ExitCode;
                return true;
            }

            ExitCode = -1;
            return false;
        }

        public void Kill()
        {
            if (OrbisCtrlProcess != null && !OrbisCtrlProcess.HasExited)
            {
                OrbisCtrlProcess.Kill();
            }

        }

        public bool Execute(string CmdLine)
        {
            if (!File.Exists(OrbisCtrlPath))
            {
                return false;
            }

            try
            {
                OrbisCtrlProcess = new Process();

                ProcessStartInfo StartInfo = new ProcessStartInfo(OrbisCtrlPath, CmdLine);
                StartInfo.Password = new System.Security.SecureString();
                StartInfo.UseShellExecute = false;
                StartInfo.RedirectStandardOutput = true;
                StartInfo.RedirectStandardError = true;
                StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
                StartInfo.CreateNoWindow = true;

                OrbisCtrlProcess.OutputDataReceived += new DataReceivedEventHandler(OnInfoReceivedEvent);
                OrbisCtrlProcess.ErrorDataReceived += new DataReceivedEventHandler(OnErrorReceivedEvent);
                OrbisCtrlProcess.Exited += new EventHandler(OnOrbisExitedEvent);
                OrbisCtrlProcess.StartInfo = StartInfo;
                OrbisCtrlProcess.Start();

                OrbisCtrlProcess.BeginOutputReadLine();
                OrbisCtrlProcess.BeginErrorReadLine();

                return true;
            }
            catch(Exception e)
            {
                Logger.Error(string.Format("Failed running orbis-ctrl Ex: {0}", e.Message));
            }

            return false;
        }

        public void OnOrbisExitedEvent(object sender, EventArgs e)
        {
            Logger.Info(string.Format("orbis-ctrl.exe has exited"));
        }

        public void OnInfoReceivedEvent(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Logger.Info(e.Data);
            }
        }

        public void OnErrorReceivedEvent(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Logger.Error(e.Data);
            }
        }

    }
}
