using BrightIdeasSoftware;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
	public interface ILogger
	{
		void Info(string Text);

		void Warning(string Text);

		void Error(string Text);
	}

	public class FileLogger : ILogger
	{
		private Device DeviceConfig;

		private ObjectListView ListView;

		private List<Device> DeviceList;

		private string LogFile;

		public FileLogger(Device DeviceConfig, ObjectListView ListView, List<Device> DeviceList)
		{
			this.DeviceConfig = DeviceConfig;
			this.ListView = ListView;
			this.DeviceList = DeviceList;
			this.LogFile = CreateLogFile();
		}

		public void Error(string Text)
		{
			//DeviceConfig.Status = Text;

			Write(string.Format("{0}\tError      \t\t{1}", GetFormattedDateString(), Text));
		}

		public void Info(string Text)
		{
			//DeviceConfig.Status = Text;

			Write(string.Format("{0}\tInformation\t\t{1}", GetFormattedDateString(), Text));
		}

		public void Warning(string Text)
		{
			//DeviceConfig.Status = Text;

			Write(string.Format("{0}\tWarning    \t\t{1}", GetFormattedDateString(), Text));
		}

		public static string GetLogFile(Device DeviceConfig)
		{
			string LogPath = GetLogFilePath();

			string LogFileName = GetLogFileName(DeviceConfig);

			return Path.Combine(LogPath, LogFileName);
		}

		private string CreateLogFile()
		{
			string LogPath = GetLogFilePath();

			if (!Directory.Exists(LogPath))
			{
				Directory.CreateDirectory(LogPath);
			}

			//string LogFileName = Path.Combine(LogPath, string.Format("Deployment-{0}-{1}.log", DeviceConfig.Address, GetFormattedDateString()));
			string LogFileName = GetLogFileName(DeviceConfig);

			string LogFile = Path.Combine(LogPath, LogFileName);

			if (File.Exists(LogFile))
			{
				File.Delete(LogFile);
			}

			return LogFile;
		}

		private void Write(string Text)
		{
            lock (this)
            {
                //ListView.SetObjects(DeviceList);

                if (!File.Exists(LogFile))
                {
                    // Create a file to write to.
                    using (StreamWriter FileWriter = File.CreateText(LogFile))
                    {
                        FileWriter.WriteLine(Text);
                    }
                }
                else
                {
                    using (StreamWriter FileWriter = File.AppendText(LogFile))
                    {
                        FileWriter.WriteLine(Text);
                    }
                }
            }
		}

		private static string GetLogFilePath()
		{
			DateTime StartTime = DateTime.Now;

			string LogPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs",
				string.Format("{0}-{1}-{2}", StartTime.Year,
				((StartTime.Month > 9) ? StartTime.Month.ToString() : string.Format("0{0}", StartTime.Month)),
				((StartTime.Day > 9) ? StartTime.Day.ToString() : string.Format("0{0}", StartTime.Day))));

			return LogPath;
		}

		private static string GetLogFileName(Device DeviceConfig)
		{
			return string.Format("Deployment-{0}-{1}-{2}.log", DeviceConfig.Address, DeviceConfig.Role, DeviceConfig.Platform);
		}

		private string GetFormattedDateString()
		{
			DateTime StartTime = DateTime.Now;

			return string.Format("{0}-{1}-{2}-{3}{4}{5}", StartTime.Year,
					((StartTime.Month  > 9) ? StartTime.Month.ToString()  : string.Format("0{0}", StartTime.Month)),
					((StartTime.Day    > 9) ? StartTime.Day.ToString()    : string.Format("0{0}", StartTime.Day)),
					((StartTime.Hour   > 9) ? StartTime.Hour.ToString()   : string.Format("0{0}", StartTime.Hour)),
					((StartTime.Minute > 9) ? StartTime.Minute.ToString() : string.Format("0{0}", StartTime.Minute)),
					((StartTime.Second > 9) ? StartTime.Second.ToString() : string.Format("0{0}", StartTime.Second)));
		}
	}

	static class StringExtensions
	{
		public static IEnumerable<String> SplitInParts(this String s, Int32 partLength)
		{
			if (s == null)
			{
				throw new ArgumentNullException("s");
			}

			if (partLength <= 0)
			{
				throw new ArgumentException("Part length has to be positive.", "partLength");
			}

			for (var i = 0; i < s.Length; i += partLength)
			{
				yield return s.Substring(i, Math.Min(partLength, s.Length - i));
			}
		}
	}

	public class ListViewLogger : ILogger
	{
		private Form MainForm;

		private ListView View;

		public ListViewLogger(Form MainForm, ListView View)
		{
			this.MainForm = MainForm;

			//var LogPanels = MainForm.Controls.Find("LogPanel", true);
			//LogPanels[0].Controls.Add(View);

			//this.MainForm.Controls.Add(View);
			this.View = View;
		}

		public void Error(string Text)
		{
			Log(Color.Red, Text);
		}

		public void Info(string Text)
		{
			Log(Color.Blue, Text);
		}

		public void Warning(string Text)
		{
			Log(Color.Orange, Text);
		}

		private void Log(Color Color, string Text)
		{
			const int MaxListViewItemTextLength = 258;

			// If the text exceeds the max number of characters for the ListViewItem we need to split it to different rows in the list view.
			var TextParts = Text.SplitInParts(MaxListViewItemTextLength);

			foreach (var TextPart in TextParts)
			{
				string[] row = { DateTime.Now.ToString(), TextPart };

				ListViewItem Item = new ListViewItem(row);
				
				Item.ForeColor = Color;

				ThreadHelperClass.AddListViewItem(MainForm, View, (ListViewItem)Item.Clone());
			}
		}

	}
}
