using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploymentTool
{
    /*
    public interface IPsExecCallback
    {
        void OnReceivedInfoOutput(string Text);
        void OnReceivedErrorOutput(string Text);
        void OnExecuteFinished(int ExitCode);
    }

    public class PsExec
    {
        private ILogger Logger;

        private IPsExecCallback Callback;

        public PsExec(ILogger Logger, IPsExecCallback Callback)
        {
            this.Logger = Logger;
            this.Callback = Callback;
        }

        public int Execute(string CmdLineArgs)
        {
            int ExitCode = -1;

            try
            {
                // \\192.168.1.178 -u jkoperator -p WhyDoHorsesEatLead
                Logger.Info(string.Format("Start PsExec with Args: {0}", CmdLineArgs));

                Process RunUAT = new Process();

                string ExecutablePath = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "PsExec64.exe");

                ProcessStartInfo StartInfo = new ProcessStartInfo(ExecutablePath, CmdLineArgs);
                StartInfo.Password = new System.Security.SecureString();
                StartInfo.UseShellExecute = false;
                StartInfo.RedirectStandardOutput = true;
                StartInfo.RedirectStandardError = true;
                StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
                StartInfo.CreateNoWindow = true;

                RunUAT.OutputDataReceived += (sender, args) => Callback.OnReceivedInfoOutput(args.Data);
                RunUAT.ErrorDataReceived += (sender, args) => Callback.OnReceivedErrorOutput(args.Data);
                RunUAT.StartInfo = StartInfo;
                RunUAT.Start();

                RunUAT.BeginOutputReadLine();
                RunUAT.BeginErrorReadLine();
                RunUAT.WaitForExit();

                ExitCode = RunUAT.ExitCode;
            }
            catch (Exception e)
            {
                Logger.Error(string.Format("PsExec throw an exception. Ex: {0}", e.Message));
            }

            Callback.OnExecuteFinished(ExitCode);

            return ExitCode;
        }
    }
    */
}
