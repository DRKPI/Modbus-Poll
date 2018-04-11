using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Timers;
using System.IO.Ports;

namespace Modbus_Poll_CS
{
    public partial class Form1 : Form
    {
        modbus mb = new modbus();
        SerialPort sp = new SerialPort();
        System.Timers.Timer timer = new System.Timers.Timer();
        string dataType;
        bool isPolling = false;
        int pollCount;

        #region GUI Delegate Declarations
        public delegate void GUIDelegate(string paramString);
        public delegate void GUIClear();
        public delegate void GUIStatus(string paramString);
        #endregion

        public Form1()
        {
            InitializeComponent();
            LoadListboxes();
            timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
        }

        #region Delegate Functions
        public void DoGUIClear()
        {
            if (this.InvokeRequired)
            {
                GUIClear delegateMethod = new GUIClear(this.DoGUIClear);
                this.Invoke(delegateMethod);
            }
            else
                this.lstRegisterValues.Items.Clear();
        }
        public void DoGUIStatus(string paramString)
        {
            if (this.InvokeRequired)
            {
                GUIStatus delegateMethod = new GUIStatus(this.DoGUIStatus);
                this.Invoke(delegateMethod, new object[] { paramString });
            }
            else
                this.lblStatus.Text = paramString;
        }
        public void DoGUIUpdate(string paramString)
        {
            if (this.InvokeRequired)
            {
                GUIDelegate delegateMethod = new GUIDelegate(this.DoGUIUpdate);
                this.Invoke(delegateMethod, new object[] { paramString });
            }
            else
                this.lstRegisterValues.Items.Add(paramString);
        }
        #endregion

        #region Timer Elapsed Event Handler
        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            PollFunction();
        }
        #endregion

        #region Load Listboxes
        private void LoadListboxes()
        {
            //Three to load - ports, baudrates, datetype.  Also set default textbox values:
            //1) Available Ports:
            string[] ports = SerialPort.GetPortNames();

            foreach (string port in ports)
            {
                lstPorts.Items.Add(port);
            }

            lstPorts.SelectedIndex = 0;

            //2) Baudrates:
            string[] baudrates = { "230400", "115200", "57600", "38400", "19200", "9600" };

            foreach (string baudrate in baudrates)
            {
                lstBaudrate.Items.Add(baudrate);
            }

            lstBaudrate.SelectedIndex = 1;

            //3) Datatype:
            string[] dataTypes = { "Decimal", "Hexadecimal", "Float", "Reverse" };

            foreach (string dataType in dataTypes)
            {
                lstDataType.Items.Add(dataType);
            }

            lstDataType.SelectedIndex = 0;

            //Textbox defaults:
            txtRegisterQty.Text = "20";
            txtSampleRate.Text = "1000";
            txtSlaveID.Text = "1";
            txtStartAddr.Text = "0";
        }
        #endregion

        #region Start and Stop Procedures
        private void StartPoll()
        {
            pollCount = 0;

            //Open COM port using provided settings:
            if (mb.Open(lstPorts.SelectedItem.ToString(), Convert.ToInt32(lstBaudrate.SelectedItem.ToString()),
                8, Parity.None, StopBits.One))
            {
                //Disable double starts:
                btnStart.Enabled = false;
                dataType = lstDataType.SelectedItem.ToString();

                //Set polling flag:
                isPolling = true;

                //Start timer using provided values:
                timer.AutoReset = true;
                if (txtSampleRate.Text != "")
                    timer.Interval = Convert.ToDouble(txtSampleRate.Text);
                else
                    timer.Interval = 1000;
                timer.Start();
            }

            lblStatus.Text = mb.modbusStatus;
        }
        private void StopPoll()
        {
            //Stop timer and close COM port:
            isPolling = false;
            timer.Stop();
            mb.Close();

            btnStart.Enabled = true;

            lblStatus.Text = mb.modbusStatus;
        }
        private void btnStart_Click(object sender, EventArgs e)
        {
            StartPoll();
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            StopPoll();
        }
        #endregion

        #region Poll Function
        private void PollFunction()
        {
            //Update GUI:
            DoGUIClear();
            pollCount++;
            DoGUIStatus("Poll count: " + pollCount.ToString());

            //Create array to accept read values:
            short[] values = new short[Convert.ToInt32(txtRegisterQty.Text)];
            ushort pollStart;
            ushort pollLength;

            if (txtStartAddr.Text != "")
                pollStart = Convert.ToUInt16(txtStartAddr.Text);
            else
                pollStart = 0;
            if (txtRegisterQty.Text != "")
                pollLength = Convert.ToUInt16(txtRegisterQty.Text);
            else
                pollLength = 20;

            //Read registers and display data in desired format:
            try
            {
                while (!mb.SendFc3(Convert.ToByte(txtSlaveID.Text), pollStart, pollLength, ref values)) ;
            }
            catch(Exception err)
            {
                DoGUIStatus("Error in modbus read: " + err.Message);
            }

            string itemString;

            switch (dataType)
            {
                case "Decimal":
                    for (int i = 0; i < pollLength; i++)
                    {
                        itemString = "[" + Convert.ToString(pollStart + i + 40001) + "] , MB[" +
                            Convert.ToString(pollStart + i) + "] = " + values[i].ToString();
                        DoGUIUpdate(itemString);
                    }
                    break;
                case "Hexadecimal":
                    for (int i = 0; i < pollLength; i++)
                    {
                        itemString = "[" + Convert.ToString(pollStart + i + 40001) + "] , MB[" +
                            Convert.ToString(pollStart + i) + "] = " + values[i].ToString("X");
                        DoGUIUpdate(itemString);
                    }
                    break;
                case "Float":
                    for (int i = 0; i < (pollLength / 2); i++)
                    {
                        int intValue = (int)values[2 * i];
                        intValue <<= 16;
                        intValue += (int)values[2 * i + 1];
                        itemString = "[" + Convert.ToString(pollStart + 2 * i + 40001) + "] , MB[" +
                            Convert.ToString(pollStart + 2 * i) + "] = " +
                            (BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0)).ToString();
                        DoGUIUpdate(itemString);
                    }
                    break;
                case "Reverse":
                    for (int i = 0; i < (pollLength / 2); i++)
                    {
                        int intValue = (int)values[2 * i + 1];
                        intValue <<= 16;
                        intValue += (int)values[2 * i];
                        itemString = "[" + Convert.ToString(pollStart + 2 * i + 40001) + "] , MB[" +
                            Convert.ToString(pollStart + 2 * i) + "] = " +
                            (BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0)).ToString();
                        DoGUIUpdate(itemString);
                    }
                    break;
            }
        }
        #endregion

        #region Write Function
        private void WriteFunction()
        {
            //StopPoll();

            if (txtWriteRegister.Text != "" && txtWriteValue.Text != "" && txtSlaveID.Text != "")
            {
                byte address = Convert.ToByte(txtSlaveID.Text);
                ushort start = Convert.ToUInt16(txtWriteRegister.Text);
                short[] value = new short[1];
                value[0] = Convert.ToInt16(txtWriteValue.Text);

                try
                {
                    while (!mb.SendFc16(address, start, (ushort)1, value)) ;
                }
                catch (Exception err)
                {
                    DoGUIStatus("Error in write function: " + err.Message);
                }
                DoGUIStatus(mb.modbusStatus);
            }
            else
                DoGUIStatus("Enter all fields before attempting a write");

            //StartPoll();
        }
        private void btnWrite_Click(object sender, EventArgs e)
        {
            WriteFunction();
        }
        #endregion

        #region Data Type Event Handler
        private void lstDataType_SelectedIndexChanged(object sender, EventArgs e)
        {
            //restart the data poll if datatype is changed during the process:
            if (isPolling)
            {
                StopPoll();
                dataType = lstDataType.SelectedItem.ToString();
                StartPoll();
            }

        }
        #endregion
    }
}