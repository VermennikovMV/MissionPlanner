﻿using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigESCCalibration : MyUserControl, IActivate
    {
        public ConfigESCCalibration()
        {
            InitializeComponent();
        }

        public void Activate()
        {
            mavlinkComboBox1.setup(ParameterMetaDataRepository.GetParameterOptionsInt("MOT_PWM_TYPE",
                MainV2.comPort.MAV.cs.firmware.ToString()), "MOT_PWM_TYPE", MainV2.comPort.MAV.param);

            mavlinkNumericUpDown1.setup(0, 1500, 1, 1, "MOT_PWM_MIN", MainV2.comPort.MAV.param);
            mavlinkNumericUpDown2.setup(0, 2200, 1, 1, "MOT_PWM_MAX", MainV2.comPort.MAV.param);

            mavlinkNumericUpDown3.setup(0, 1, 1, 0.01f, "MOT_SPIN_ARM", MainV2.comPort.MAV.param);
            mavlinkNumericUpDown4.setup(0, 1, 1, 0.01f, "MOT_SPIN_MIN", MainV2.comPort.MAV.param);
            mavlinkNumericUpDown5.setup(0, 1, 1, 0.01f, "MOT_SPIN_MAX", MainV2.comPort.MAV.param);
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (!MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "ESC_CALIBRATION", 3))
                {
                    CustomMessageBox.Show("Ошибка установки параметра. Убедитесь, что версия прошивки AC3.3+.");
                    return;
                }
            }
            catch
            {
                CustomMessageBox.Show("Ошибка установки параметра. Убедитесь, что версия прошивки AC3.3+.");
                return;
            }

            buttonStart.Enabled = false;
        }
    }
}