using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeploymentTool
{
    public enum CommandResult { Success, Failure };

    public class SshCommand
    {
        public static CommandResult ExecuteCommand(ILogger Logger, string Argument, ITargetDevice Device, out string ReturnValue, out string LogInfo, out string LogError, int TimeoutSeconds = 30)
        {
            ReturnValue = LogInfo = LogError = string.Empty;

            int RetryCount = 0;

            while (RetryCount < 3)
            {
                RetryCount++;

                try
                {
                    using (var Client = new SshClient(Device.DeviceConfig.Address, Device.DeviceConfig.Username, Device.DeviceConfig.Password))
                    {
                        Client.Connect();

                        if (!Client.IsConnected)
                        {
                            continue;
                        }

                        using (var Command = Client.CreateCommand(Argument))
                        {
                            Command.CommandTimeout = TimeSpan.FromSeconds(TimeoutSeconds);
                            Command.Execute();
                            ReturnValue = Command.Result;

                            var OutputStreamReader = new StreamReader(Command.OutputStream);
                            var ErrorStreamReader = new StreamReader(Command.ExtendedOutputStream);

                            LogError = ErrorStreamReader.ReadToEnd();
                            LogInfo = OutputStreamReader.ReadToEnd();
                        }

                        Client.Disconnect();
                    }

                    if (LogError.Length == 0)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    Logger.Warning(string.Format("Failed to run command '{0}' at device '{1}' retry count {2}. Ex: {3}. {4}. {5}. {6}", Argument, Device.DeviceConfig.Address, RetryCount, e.Message, ReturnValue, LogInfo, LogError));
                }

                Thread.Sleep(500);
            }

            return LogError.Length > 0 ? CommandResult.Failure : CommandResult.Success;
        }

        public static void ExecuteAsync(ILogger Logger, string Argument, ITargetDevice Device)
        {
            new Task(() => ExecuteAsyncImpl(Logger, Argument, Device)).Start();
        }

        private static void ExecuteAsyncImpl(ILogger Logger, string Argument, ITargetDevice Device)
        {
            try
            {
                using (var Client = new SshClient(Device.DeviceConfig.Address, Device.DeviceConfig.Username, Device.DeviceConfig.Password))
                {
                    Client.Connect();

                    var Cmd = Client.CreateCommand(Argument);

                    var Result = Cmd.BeginExecute();

                    using (var Reader = new StreamReader(Cmd.OutputStream, Encoding.UTF8, true, 1024, true))
                    {
                        while (!Result.IsCompleted || !Reader.EndOfStream)
                        {
                            string Line = Reader.ReadLine();
                            if (Line != null)
                            {
                                Logger.Info(Line);
                            }
                        }
                    }

                    Cmd.EndExecute(Result);

                    if (!string.IsNullOrEmpty(Cmd.Error))
                    {
                        Logger.Error(Cmd.Error);
                    }

                    Client.Disconnect();
                }
            }
            catch (Exception e)
            {
                Logger.Error(string.Format("Error when executing command {0}. Ex: {1}", Argument, e.Message));
            }
        }

    }
}
