﻿using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.MsgBox;

namespace SikRadio
{
    public partial class Terminal : UserControl, ISikRadioForm
    {
        internal static StreamWriter sw;
        private StringBuilder cmd = new StringBuilder();
        private readonly object thisLock = new object();
        bool _RunRxThread = false;
        Thread _RxThread;

        public Terminal()
        {
            InitializeComponent();

            SetupStreamWriter();
        }

        public static void SetupStreamWriter()
        {
            if (sw == null)
                sw = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Terminal-" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".txt");
        }

        private void comPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var comPort = SikRadio.Config.comPort;

            if ((comPort == null) || !comPort.IsOpen)
            {
                return;
            }

            try
            {
                lock (thisLock)
                {
                    var data = comPort.ReadExisting();
                    //Console.Write(data);

                    if (sw != null)
                    {
                        sw.Write(data);
                        sw.Flush();
                    }

                    addText(data);
                }
            }
            catch (Exception)
            {
                //if (!threadrun) return;
                //TXT_terminal.AppendText("Error reading com port\r\n");
            }
        }

        private void addText(string data)
        {
            BeginInvoke((MethodInvoker) delegate
            {
                TXT_terminal.SelectionStart = TXT_terminal.Text.Length;

                data = data.TrimEnd('\r'); // else added \n all by itself
                data = data.Replace("\0", " ");
                TXT_terminal.AppendText(data);
                if (data.Contains("\b"))
                {
                    TXT_terminal.Text = TXT_terminal.Text.Remove(TXT_terminal.Text.IndexOf('\b'));
                    TXT_terminal.SelectionStart = TXT_terminal.Text.Length;
                }
            });
        }

        public void Connect()
        {
            if (!_RunRxThread)
            {
                if (RFDLib.Utils.Retry(() =>
                {
                    var Session = new RFD.RFD900.TSession(SikRadio.Config.comPort, MainV2.comPort.BaseStream.BaudRate);
                    var Result = Session.PutIntoATCommandMode() == RFD.RFD900.TSession.TMode.AT_COMMAND;
                    Session.Dispose();
                    return Result;
                }, 3))
                {
                    _RunRxThread = true;
                    _RxThread = new Thread(RxWorker);
                    _RxThread.Start();
                }
                else
                {
                    MissionPlanner.MsgBox.CustomMessageBox.Show("Не удалось войти в режим AT-команд.");
                }
            }
        }

        public void Disconnect()
        {
            if (_RunRxThread)
            {
                _RunRxThread = false;
                _RxThread.Join();
                _RxThread = null;
            }
        }

        void RxWorker()
        {
            while (_RunRxThread)
            {
                //while (threadrun)
                {
                    try
                    {
                        Thread.Sleep(10);
                        if (SikRadio.Config.comPort.BytesToRead > 0)
                        {
                            comPort_DataReceived(null, null);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void Terminal_Load(object sender, EventArgs e)
        {
            return;
            /*
            try
            {
                if (comPort.IsOpen)
                    comPort.Close();

                comPort.ReadBufferSize = 1024*1024;

                comPort.PortName = MainV2.comPort.BaseStream.PortName;

                comPort.Open();

                comPort.toggleDTR();

                var t11 = new Thread(delegate()
                {
                    threadrun = true;

                    var start = DateTime.Now;

                    while ((DateTime.Now - start).TotalMilliseconds < 2000)
                    {
                        try
                        {
                            if (comPort.BytesToRead > 0)
                            {
                                comPort_DataReceived(null, null);
                            }
                        }
                        catch
                        {
                            return;
                        }
                    }
                    try
                    {
                        comPort.Write("\n\n\n");
                    }
                    catch
                    {
                        return;
                    }
                    while (threadrun)
                    {
                        try
                        {
                            Thread.Sleep(10);
                            if (!comPort.IsOpen)
                                break;
                            if (comPort.BytesToRead > 0)
                            {
                                comPort_DataReceived(null, null);
                            }
                        }
                        catch
                        {
                        }
                    }

                    comPort.DtrEnable = false;

                    try
                    {
                        //if (sw != null)
                        //  sw.Close();
                    }
                    catch
                    {
                    }

                    if (threadrun == false)
                    {
                        comPort.Close();
                    }
                    //Console.WriteLine("Comport thread close");
                });
                t11.IsBackground = true;
                t11.Name = "Terminal serial thread";
                t11.Start();

                // doesnt seem to work on mac
                //comPort.DataReceived += new SerialDataReceivedEventHandler(comPort_DataReceived);

                TXT_terminal.AppendText("Opened com port\r\n");
            }
            catch (Exception)
            {
                TXT_terminal.AppendText("Cant open serial port\r\n");
                return;
            }

            TXT_terminal.Focus();*/
        }

        private void TXT_terminal_Click(object sender, EventArgs e)
        {
            // auto scroll
            TXT_terminal.SelectionStart = TXT_terminal.Text.Length;

            TXT_terminal.ScrollToCaret();

            TXT_terminal.Refresh();
        }

        private void TXT_terminal_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Up || e.KeyData == Keys.Down || e.KeyData == Keys.Left || e.KeyData == Keys.Right)
            {
                e.Handled = true; // ignore it
            }
        }

        private void Terminal_FormClosing(object sender, FormClosingEventArgs e)
        {
            //threadrun = false;
            _RunRxThread = false;

            /*if (comPort.IsOpen)
            {
                comPort.Close();
            }*/
            Thread.Sleep(400);
        }

        private void TXT_terminal_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                var comPort = SikRadio.Config.comPort;

                if ((comPort != null) && comPort.IsOpen)
                {
                    try
                    {
                        // do not change this  \r is correct - no \n
                        var temp = cmd.ToString();

                        if (cmd.ToString() == "+++")
                        {
                            comPort.Write(Encoding.ASCII.GetBytes(cmd.ToString()), 0, cmd.Length);
                        }
                        else
                        {
                            comPort.Write(Encoding.ASCII.GetBytes(cmd + "\r"), 0, cmd.Length + 1);
                        }

                        if (sw != null)
                        {
                            sw.WriteLine(cmd.ToString());
                            sw.Flush();
                        }
                    }
                    catch
                    {
                        CustomMessageBox.Show("Ошибка записи в COM-порт", "Ошибка");
                    }
                }
                cmd = new StringBuilder();
            }
            else
            {
                cmd.Append(e.KeyChar);
            }
        }

        public string Header
        {
            get
            {
                return "Terminal";
            }
        }
    }
}