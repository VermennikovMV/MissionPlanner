﻿using GeoidHeightsDotNet;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using System;
using System.Globalization;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace MissionPlanner.Controls
{
    public partial class SerialOutputNMEA : Form
    {
        static TcpListener listener;
        static ICommsSerial NmeaStream = new TcpSerial();
        static double updaterate = 5;
        System.Threading.Thread t12;
        static bool threadrun = false;
        static internal PointLatLngAlt HomeLoc = new PointLatLngAlt(0, 0, 0, "Home");

        public SerialOutputNMEA()
        {
            InitializeComponent();

            CMB_serialport.Items.AddRange(SerialPort.GetPortNames());
            CMB_serialport.Items.Add("TCP Host - 14551");
            CMB_serialport.Items.Add("TCP Client");
            CMB_serialport.Items.Add("UDP Host - 14551");
            CMB_serialport.Items.Add("UDP Client");

            CMB_updaterate.Text = updaterate + "hz";

            if (threadrun)
            {
                BUT_connect.Text = Strings.Stop;
            }

            MissionPlanner.Utilities.Tracking.AddPage(this.GetType().ToString(), this.Text);
        }

        private void BUT_connect_Click(object sender, EventArgs e)
        {
            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }

            if (NmeaStream.IsOpen)
            {
                threadrun = false;
                NmeaStream.Close();
                BUT_connect.Text = Strings.Connect;
            }
            else
            {
                try
                {
                    switch (CMB_serialport.Text)
                    {
                        case "TCP Host - 14551":
                        case "TCP Host":
                            NmeaStream = new TcpSerial();
                            CMB_baudrate.SelectedIndex = 0;
                            listener = new TcpListener(System.Net.IPAddress.Any, 14551);
                            listener.Start(0);
                            listener.BeginAcceptTcpClient(new AsyncCallback(DoAcceptTcpClientCallback), listener);
                            BUT_connect.Text = Strings.Stop;
                            break;
                        case "TCP Client":
                            NmeaStream = new TcpSerial() { retrys = 999999, autoReconnect = true, ConfigRef = "SerialOutputNMEATCP" };
                            CMB_baudrate.SelectedIndex = 0;
                            break;
                        case "UDP Host - 14551":
                            NmeaStream = new UdpSerial();
                            CMB_baudrate.SelectedIndex = 0;
                            break;
                        case "UDP Client":
                            NmeaStream = new UdpSerialConnect();
                            CMB_baudrate.SelectedIndex = 0;
                            break;
                        default:
                            NmeaStream = new SerialPort();
                            NmeaStream.PortName = CMB_serialport.Text;
                            break;
                    }
                }
                catch
                {
                    CustomMessageBox.Show(Strings.InvalidPortName);
                    return;
                }
                try
                {
                    NmeaStream.BaudRate = int.Parse(CMB_baudrate.Text);
                }
                catch
                {
                    CustomMessageBox.Show(Strings.InvalidBaudRate);
                    return;
                }
                try
                {
                    if (listener == null)
                        NmeaStream.Open();
                }
                catch
                {
                    CustomMessageBox.Show("Ошибка подключения\nесли используется com0com, переименуйте порты в COM??");
                    return;
                }

                t12 = new System.Threading.Thread(new System.Threading.ThreadStart(mainloop))
                {
                    IsBackground = true,
                    Name = "Nmea output"
                };
                t12.Start();

                BUT_connect.Text = Strings.Stop;
            }
        }

        void mainloop()
        {
            threadrun = true;
            //NmeaStream.NewLine = "\r\n";
            int counter = 0;
            while (threadrun)
            {
                try
                {
                    if (!NmeaStream.IsOpen)
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    //GGA
                    double lat = (int)MainV2.comPort.MAV.cs.lat +
                                 ((MainV2.comPort.MAV.cs.lat - (int)MainV2.comPort.MAV.cs.lat) * .6f);
                    double lng = (int)MainV2.comPort.MAV.cs.lng +
                                 ((MainV2.comPort.MAV.cs.lng - (int)MainV2.comPort.MAV.cs.lng) * .6f);
                    string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1:HHmmss.fff},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},", "GGA",
                        DateTime.Now.ToUniversalTime(), Math.Abs(lat * 100).ToString("0000.00000", CultureInfo.InvariantCulture), MainV2.comPort.MAV.cs.lat < 0 ? "S" : "N",
                        Math.Abs(lng * 100).ToString("00000.00000", CultureInfo.InvariantCulture), MainV2.comPort.MAV.cs.lng < 0 ? "W" : "E",
                        MainV2.comPort.MAV.cs.gpsstatus >= 3 ? 1 : 0, MainV2.comPort.MAV.cs.satcount,
                        MainV2.comPort.MAV.cs.gpshdop, MainV2.comPort.MAV.cs.altasl / CurrentState.multiplieralt, "M", 
                        GeoidHeights.undulation(MainV2.comPort.MAV.cs.lat, MainV2.comPort.MAV.cs.lng).ToString("0.0", CultureInfo.InvariantCulture), "M", "");

                    string checksum = GetChecksum(line);
                    NmeaStream.WriteLine(line + "*" + checksum+"\r");

                    //GLL
                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1},{2},{3},{4},{5:HHmmss.fff},{6},{7}", "GLL",
                        Math.Abs(lat * 100).ToString("0000.00", CultureInfo.InvariantCulture), MainV2.comPort.MAV.cs.lat < 0 ? "S" : "N",
                        Math.Abs(lng * 100).ToString("00000.00", CultureInfo.InvariantCulture), MainV2.comPort.MAV.cs.lng < 0 ? "W" : "E",
                        DateTime.Now.ToUniversalTime(), "A", "A");

                    checksum = GetChecksum(line);
                    NmeaStream.WriteLine(line + "*" + checksum + "\r");
                    /*
                    //GSA
                    // $GPGSA,A,3,19,28,14,18,27,22,31,39,,,,,1.7,1.0,1.3*35
                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}", "GSA",
                        "A", 3, 19, 28, 14, 18, 27, 22, 31,
                        39, "", "", "", "", 1.7, 1.0, 1.3);

                    checksum = GetChecksum(line);
                    NmeaStream.WriteLine(line + "*" + checksum + "\r");

                    //GSV
                    // $GPGSV,3,1,11,03,03,111,00,04,15,270,00,06,01,010,00,13,06,292,00*74
                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19}",
                        "GSV", 1, 1, 8,
                        "03", 50, 0, 41,
                        "19", 45, 90, 42, 
                        "28", 50, 180, 43, 
                        "14", 45, 270, 44);

                    checksum = GetChecksum(line);
                    NmeaStream.WriteLine(line + "*" + checksum + "\r");

                    //ZDA
                    //ZDA,hhmmss.ss,xx,xx,xxxx,xx,xx
                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1:HHmmss.ff},{2:00},{3:00},{4:0000},{5:00},{6:00}", "ZDA", DateTime.Now.ToUniversalTime(), DateTime.Now.ToUniversalTime().Day, DateTime.Now.ToUniversalTime().Month, DateTime.Now.ToUniversalTime().Year, "00", "00");

                    checksum = GetChecksum(line);
                    NmeaStream.WriteLine(line + "*" + checksum + "\r");
                    */
                    //HDG
                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1:0.0},{2},{3},{4},{5}", "HDG",
                        MainV2.comPort.MAV.cs.yaw, 0, "E", 0, "E");

                    checksum = GetChecksum(line);
                    NmeaStream.WriteLine(line + "*" + checksum + "\r");

                    //VTG
                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1},{2},{3},{4}", "VTG",
                        MainV2.comPort.MAV.cs.groundcourse.ToString("000"), MainV2.comPort.MAV.cs.yaw.ToString("000"),
                        (MainV2.comPort.MAV.cs.groundspeed * 1.943844).ToString("00.0", CultureInfo.InvariantCulture),
                        (MainV2.comPort.MAV.cs.groundspeed * 3.6).ToString("00.0", CultureInfo.InvariantCulture));

                    checksum = GetChecksum(line);
                    NmeaStream.WriteLine(line + "*" + checksum + "\r");

                    //RMC
                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "$GP{0},{1:HHmmss.fff},{2},{3},{4},{5},{6},{7},{8},{9:ddMMyy},{10},{11},{12}", "RMC",
                        DateTime.Now.ToUniversalTime(), "A", Math.Abs(lat * 100).ToString("0.00000", CultureInfo.InvariantCulture),
                        MainV2.comPort.MAV.cs.lat < 0 ? "S" : "N", Math.Abs(lng * 100).ToString("0.00000", CultureInfo.InvariantCulture),
                        MainV2.comPort.MAV.cs.lng < 0 ? "W" : "E", (MainV2.comPort.MAV.cs.groundspeed * 1.943844).ToString("0.0", CultureInfo.InvariantCulture),
                        MainV2.comPort.MAV.cs.groundcourse.ToString("0.0", CultureInfo.InvariantCulture), DateTime.Now, 0, "E", "A");

                    checksum = GetChecksum(line);
                    NmeaStream.WriteLine(line + "*" + checksum + "\r");

                    if (counter % 20 == 0 && HomeLoc.Lat != 0 && HomeLoc.Lng != 0)
                    {
                        line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "$GP{0},{1:HHmmss.fff},{2},{3},{4},{5},{6},{7},", "HOM", DateTime.Now.ToUniversalTime(),
                            Math.Abs(HomeLoc.Lat * 100).ToString("0.00000", CultureInfo.InvariantCulture), HomeLoc.Lat < 0 ? "S" : "N", Math.Abs(HomeLoc.Lng * 100).ToString("0.00000", CultureInfo.InvariantCulture),
                            HomeLoc.Lng < 0 ? "W" : "E", HomeLoc.Alt, "M");

                        checksum = GetChecksum(line);
                        NmeaStream.WriteLine(line + "*" + checksum + "\r");
                    }

                    line = string.Format(System.Globalization.CultureInfo.InvariantCulture, "$GP{0},{1},{2},{3},", "RPY",
                        MainV2.comPort.MAV.cs.roll.ToString("0.00000", CultureInfo.InvariantCulture), MainV2.comPort.MAV.cs.pitch.ToString("0.00000", CultureInfo.InvariantCulture), MainV2.comPort.MAV.cs.yaw.ToString("0.00000", CultureInfo.InvariantCulture));

                    checksum = GetChecksum(line);
                    NmeaStream.WriteLine(line + "*" + checksum + "\r");

                    var nextsend = DateTime.Now.AddMilliseconds(1000 / updaterate);
                    var sleepfor = Math.Min((int)Math.Abs((nextsend - DateTime.Now).TotalMilliseconds), 4000);
                    System.Threading.Thread.Sleep(sleepfor);
                    counter++;
                }
                catch
                {
                }
            }
        }

        private void SerialOutput_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        // Calculates the checksum for a sentence
        string GetChecksum(string sentence)
        {
            // Loop through all chars to get a checksum
            int Checksum = 0;
            foreach (char Character in sentence.ToCharArray())
            {
                switch (Character)
                {
                    case '$':
                        // Ignore the dollar sign
                        break;
                    case '*':
                        // Stop processing before the asterisk
                        continue;
                    default:
                        // Is this the first value for the checksum?
                        if (Checksum == 0)
                        {
                            // Yes. Set the checksum to the value
                            Checksum = Convert.ToByte(Character);
                        }
                        else
                        {
                            // No. XOR the checksum with this character's value
                            Checksum = Checksum ^ Convert.ToByte(Character);
                        }
                        break;
                }
            }
            // Return the checksum formatted as a two-character hexadecimal
            return Checksum.ToString("X2");
        }

        private void CMB_updaterate_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                updaterate = float.Parse(CMB_updaterate.Text.Replace("hz", ""));
            }
            catch
            {
                CustomMessageBox.Show(Strings.InvalidUpdateRate, Strings.ERROR);
            }
        }

        void DoAcceptTcpClientCallback(IAsyncResult ar)
        {
            // Get the listener that handles the client request.
            TcpListener listener = (TcpListener)ar.AsyncState;

            try
            {
                // End the operation and display the received data on  
                // the console.
                TcpClient client = listener.EndAcceptTcpClient(ar);

                ((TcpSerial)NmeaStream).client = client;

                listener.BeginAcceptTcpClient(new AsyncCallback(DoAcceptTcpClientCallback), listener);
            }
            catch { }
        }
    }
}