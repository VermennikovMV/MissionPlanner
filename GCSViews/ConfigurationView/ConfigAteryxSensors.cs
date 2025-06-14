﻿using MissionPlanner.ArduPilot;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigAteryxSensors : MyUserControl, IActivate, IDeactivate
    {
        public ConfigAteryxSensors()
        {
            InitializeComponent();
        }

        public void Activate()
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
            {
                Enabled = false;
                return;
            }
            if (MainV2.comPort.MAV.cs.firmware == Firmwares.Ateryx)
            {
                Enabled = true;
            }
            else
            {
                Enabled = false;
                return;
            }

            timer1.Start();
        }

        public void Deactivate()
        {
            timer1.Stop();
        }

        private void BUT_levelplane_Click(object sender, EventArgs e)
        {
            try
            {
                ((Button)sender).Enabled = false;


                if ((MainV2.comPort.MAV.cs.airspeed > 7.0) || (MainV2.comPort.MAV.cs.groundspeed > 10.0))
                {
                    MessageBox.Show("Невозможно – БПЛА в воздухе");
                    ((Button)sender).Enabled = true;
                    return;
                }
                //MainV2.comPort.doCommand((MAVLink.MAV_CMD)Enum.Parse(typeof(MAVLink.MAV_CMD), "MAV_CMD_PREFLIGHT_STORAGE"));
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 2.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
            }
            catch
            {
                MessageBox.Show("Не удалось сбросить ориентацию");
            }
            ((Button)sender).Enabled = true;
        }

        private void BUT_zero_press_Click(object sender, EventArgs e)
        {
            try
            {
                ((Button)sender).Enabled = false;

                if ((MainV2.comPort.MAV.cs.airspeed > 7.0) || (MainV2.comPort.MAV.cs.groundspeed > 10.0))
                {
                    MessageBox.Show("Невозможно – БПЛА в воздухе");
                    ((Button)sender).Enabled = true;
                    return;
                }

                //MainV2.comPort.doCommand((MAVLink.MAV_CMD)Enum.Parse(typeof(MAVLink.MAV_CMD), "MAV_CMD_PREFLIGHT_STORAGE"));
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
            }
            catch
            {
                MessageBox.Show("Не удалось выполнить команду");
            }
            ((Button)sender).Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            MainV2.comPort.MAV.cs.UpdateCurrentSettings(bindingSource1.UpdateDataSource(MainV2.comPort.MAV.cs));
        }
    }
}