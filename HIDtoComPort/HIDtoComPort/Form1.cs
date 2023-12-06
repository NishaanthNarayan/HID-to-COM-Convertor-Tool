// USB HID Scale Reader by Matt Galanto using HidLibrary.dll by Mike O'Brien
//  http://www.bumderland.com/dev


// Reffer from : http://www.bumderland.com/dev/USBHIDScale.html
// Com port writing implementation by pgsamila

using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HidLibrary;

namespace HIDtoComPort
{
	public partial class Form1 : Form
	{
		/// <summary>
		/// Serial Port data
		/// </summary>
		SerialPort serialPort;
		bool isSerialPortOpen = false;
		bool isReading = false;
		String vsText = "none";

		/// <summary>
		/// Reading Thread
		/// </summary>
		Thread hidReaderThread;

		/// <summary>
		/// HID device data
		/// </summary>
		HidDevice[] mahdDevices;
		HidDevice mhdDevice;

		/// <summary>
		/// Form1 constroctor
		/// </summary>
		public Form1()
		{
			InitializeComponent();
			comboBox1.Items.AddRange(SerialPort.GetPortNames());
		}

		/// <summary>
		/// If no HID device is selected and try to open HID device
		/// </summary>
		/// <returns></returns>
		private bool FailNoDevice()
		{
			if (mhdDevice == null)
			{
				MessageBox.Show("No Device Selected", "Error", MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Com port not selected, but try to open
		/// </summary>
		/// <returns></returns>
		private bool FailNoComPort()
		{
			if (comboBox1.SelectedItem == null)
			{
				MessageBox.Show("No Com port Selected", "Error", MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Failed to close
		/// </summary>
		/// <returns></returns>
		private bool FailDeviceClosed()
		{
			if (!mhdDevice.IsOpen)
			{
				MessageBox.Show("Device is not open.", "Error", MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Load form 1
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Form1_Load(object sender, EventArgs e)
		{
			mahdDevices = HidDevices.Enumerate().ToArray();
			mhdDevice = null;

			for (int i = 0; i < mahdDevices.Length; i++)
			{
				cmbDevices.Items.Add(mahdDevices[i].Description);
			}
		}

		/// <summary>
		/// HID device selecting com port
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void cmbDevices_SelectedIndexChanged(object sender, EventArgs e)
		{
			mhdDevice = mahdDevices[cmbDevices.SelectedIndex];

			if (mhdDevice.IsOpen)
				btnOpen.Text = "Close";
			else
				btnOpen.Text = "Open";
		}

		/// <summary>
		/// HID device open/ close Button
		/// Open & close HID device
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnOpen_Click(object sender, EventArgs e)
		{
			if (FailNoDevice())
				return;

			if (mhdDevice.IsOpen)
			{
				mhdDevice.CloseDevice();
				btnOpen.Text = "Open";
			}
			else
			{
				mhdDevice.OpenDevice();
				btnOpen.Text = "Close";
			}
		}

		/// <summary>
		/// Com port Open/ Close buttn
		/// open & close com port
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void comButton_Click(object sender, EventArgs e)
		{
			if (FailNoComPort())
				return;

			if (comboBox1.SelectedItem != null & !isSerialPortOpen)
			{
				string comPort = comboBox1.GetItemText(comboBox1.SelectedItem);
				serialPort = new SerialPort(comPort);
				if (!serialPort.IsOpen)
				{
					serialPort.BaudRate = 9600;
					serialPort.Parity = Parity.None;
					serialPort.DataBits = 8;
					serialPort.StopBits = StopBits.One;
					serialPort.Open();
					serialPort.DataReceived += this.OnDataReceived;
					comButton.Text = "Close";
					isSerialPortOpen = true;
					btnRead.Enabled = true;
				}
			}
			else if (isSerialPortOpen)
			{
				if (hidReaderThread != null)
				{
					hidReaderThread.Abort();
					isReading = false;
				}
				btnRead.Enabled = false;
				btnRead.Text = "Start Reading";

				serialPort.DataReceived -= OnDataReceived;
				serialPort.Close();
				comButton.Text = "Open";
			}
		}

		private List<byte> receivedBytes = new List<byte>();

		private void OnDataReceived(object sender, SerialDataReceivedEventArgs args)
		{
			try
			{
				var bytesToRead = serialPort.BytesToRead;
				var bytesReaded = new byte[bytesToRead];
				serialPort.Read(bytesReaded, 0, bytesToRead);
				foreach (var readedByte in bytesReaded)
				{
					if (readedByte == 0x24 || readedByte == 0x25)
					{
						receivedBytes = new List<byte>();
					}

					if (readedByte == 0x0A)
					{
						continue;
					}

					receivedBytes.Add(readedByte);
					if (readedByte == 0x0D)
					{
						if (mhdDevice != null && mhdDevice.IsOpen)
						{
							var status = true;
							byte bytesToWriteLength = (byte)receivedBytes.Count;
							byte bytesWritten = 0;
							byte maxReportSize = 30;
							while (bytesToWriteLength > bytesWritten && status)
							{
								var writeReport = mhdDevice.CreateReport();
								writeReport.ReportId = 0;
								var bytesCountToWrite = maxReportSize; 
								if ((bytesToWriteLength - bytesWritten) < maxReportSize)
								{
									bytesCountToWrite = (byte)(bytesToWriteLength - bytesWritten);
								}

								/// MessageBox.Show(bytesToRead.ToString());
								writeReport.Data[0] = bytesCountToWrite;
								var bytesToWrite = receivedBytes.Skip(bytesWritten).Take(bytesCountToWrite).ToArray();
								Array.Copy(bytesToWrite, 0, writeReport.Data, 1, bytesCountToWrite);
								
								status = mhdDevice.WriteReport(writeReport);
								bytesWritten += bytesCountToWrite;
							}

							receivedBytes = new List<byte>();
						}
					}
				}



			}
			catch (Exception ex)
			{

				MessageBox.Show(ex.Message, "Error in HID Write");
			}
		}

		/// <summary>
		/// Read/Stop read button click event action
		/// hitRead thread will start and keep reading
		/// if it is "Stop Reading", then the thread will kill and stop reading
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnRead_Click_1(object sender, EventArgs e)
		{
			/**
			 * Knows ISSUE -> Stop the thread & close the port at exit
			 */
			if (!isReading)
			{
				hidReaderThread = new Thread(hidReader);
				hidReaderThread.Start();
				isReading = true;
				btnRead.Text = "Stop Reading";
			}
			else
			{
				hidReaderThread.Abort();
				isReading = false;
				btnRead.Text = "Start Reading";
			}
		}

		/// <summary>
		/// Reading HID device and write on Com port
		/// </summary>
		private void hidReader()
		{
			while (true)
			{
				if (FailNoDevice() || FailDeviceClosed())
					return;

				HidDeviceData hddData = mhdDevice.Read();
				var dataTowrite = hddData.Data;


				List<byte> dataList = dataTowrite.ToList();
				dataList.RemoveAll(item => item == 255);
				dataTowrite = dataList.ToArray();



				/// MessageBox.Show(hddData.Status.ToString());
				////vsText = BitConverter.ToString(hddData.Data);
				serialPort.Write(dataTowrite, 2, dataTowrite.Length - 2);
				/// MessageBox.Show(hddData.Data[hddData.Data.Length - 1].ToString());
				/// MessageBox.Show(((char)hddData.Data[hddData.Data.Length - 1]).ToString());
				Thread.Sleep(1);
			}
		}

		private void lblRead_Click(object sender, EventArgs e)
		{

		}
	}
}
