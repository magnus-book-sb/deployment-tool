using BrightIdeasSoftware;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
	public class ThreadHelperClass
	{
        delegate void UpdateDeviceDeploymentProgressDelegate(Form form, ObjectListView ListView, ITargetDevice Device);
		public static void UpdateDeviceDeploymentProgress(Form form, ObjectListView listView, ITargetDevice device)
		{
			if (form.InvokeRequired)
			{
				var d = new UpdateDeviceDeploymentProgressDelegate(UpdateDeviceDeploymentProgress);
				form.Invoke(d, new object[] { form, listView, device });
			}
			else
			{
				
				listView.SelectedObject = device;
				device.Build.Progress++;
				listView.RefreshObject(device);
			}
		}

		delegate void SetDeviceDeploymentResultDelegate(Form form, ObjectListView ListView, ITargetDevice Device, BuildDeploymentResult Result);
		public static void SetDeviceDeploymentResult(Form form, ObjectListView listView, ITargetDevice device, BuildDeploymentResult result)
		{
			if (form.InvokeRequired)
			{
				var d = new SetDeviceDeploymentResultDelegate(SetDeviceDeploymentResult);
				form.Invoke(d, new object[] { form, listView, device, result });
			}
			else
			{
				device.Build.Status = result.ToString();
				listView.RefreshObject(device);
			}
		}

		delegate void AddListViewItemCallback(Form f, ListView ctrl, ListViewItem Item);
		public static void AddListViewItem(Form WinForm, ListView Ctrl, ListViewItem Item)
		{
			if (Ctrl.InvokeRequired)
			{
				AddListViewItemCallback d = new AddListViewItemCallback(AddListViewItem);
				WinForm.Invoke(d, new object[] { WinForm, Ctrl, Item });
			}
			else
			{
				Ctrl.Items.Insert(0, Item);
			}
		}
	}
}
