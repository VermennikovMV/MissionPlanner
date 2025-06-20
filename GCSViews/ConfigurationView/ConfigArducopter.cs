using MissionPlanner.ArduPilot;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Collections;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigArducopter : MyUserControl, IActivate
    {
        // from http://stackoverflow.com/questions/2512781/winforms-big-paragraph-tooltip/2512895#2512895
        private const int maximumSingleLineTooltipLength = 50;
        private static Hashtable tooltips = new Hashtable();
        private readonly Hashtable changes = new Hashtable();
        internal bool startup = true;

        public ConfigArducopter()
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

            if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduCopter2 || MainV2.comPort.MAV.param.ContainsKey("Q_ENABLE") && MainV2.comPort.MAV.param["Q_ENABLE"].Value != 0)
            {
                Enabled = true;
            }
            else
            {
                Enabled = false;
                return;
            }

            startup = true;

            changes.Clear();

            // ensure the fields are populated before setting them
            TUNE.setup(
                ParameterMetaDataRepository
                    .GetParameterOptionsInt("TUNE", MainV2.comPort.MAV.cs.firmware.ToString())
                    .ToList(), "TUNE", MainV2.comPort.MAV.param);

            CH6_OPTION.setup(new[] { "CH6_OPT", "CH6_OPTION", "RC6_OPTION", "RC6_OPTION" }, MainV2.comPort.MAV.param);
            CH7_OPTION.setup(new[] {"CH7_OPT", "CH7_OPTION", "RC7_OPTION", "RC7_OPTION"}, MainV2.comPort.MAV.param);
            CH8_OPTION.setup(new[] {"CH8_OPT", "CH8_OPTION", "RC8_OPTION", "RC8_OPTION"}, MainV2.comPort.MAV.param);
            CH9_OPTION.setup(new[] {"CH9_OPT", "CH9_OPTION", "RC9_OPTION", "RC9_OPTION"}, MainV2.comPort.MAV.param);
            CH10_OPTION.setup(new[] {"CH10_OPT", "CH10_OPTION", "RC10_OPTION", "RC10_OPTION"}, MainV2.comPort.MAV.param);

            TUNE_LOW.setup(0, 10000, 1, 0.01f, new[] {"TUNE_LOW", "TUNE_MIN"},
                MainV2.comPort.MAV.param);
            TUNE_HIGH.setup(0, 10000, 1, 0.01f, new[] {"TUNE_HIGH", "TUNE_MAX"},
                MainV2.comPort.MAV.param);

            HLD_LAT_P.setup(0, 0, 1, 0.001f, new[] {"HLD_LAT_P", "POS_XY_P", "PSC_POSXY_P", "Q_P_POSXY_P"},
                MainV2.comPort.MAV.param);
            LOITER_LAT_D.setup(0, 0, 1, 0.001f, new[] {"LOITER_LAT_D", "PSC_VELXY_D", "Q_P_VELXY_D"},
                MainV2.comPort.MAV.param);
            LOITER_LAT_I.setup(0, 0, 1, 0.001f, new[] {"LOITER_LAT_I", "VEL_XY_I", "PSC_VELXY_I", "Q_P_VELXY_I"},
                MainV2.comPort.MAV.param);
            LOITER_LAT_IMAX.setup(0, 0, 10, 1f,
                new[] {"LOITER_LAT_IMAX", "VEL_XY_IMAX", "PSC_VELXY_IMAX", "Q_P_VELXY_IMAX"},
                MainV2.comPort.MAV.param);
            LOITER_LAT_P.setup(0, 0, 1, 0.001f, new[] {"LOITER_LAT_P", "VEL_XY_P", "PSC_VELXY_P", "Q_P_VELXY_P"},
                MainV2.comPort.MAV.param);

            RATE_PIT_P.setup(0, 0, 1, 0.001f, new[] { "RATE_PIT_P", "ATC_RAT_PIT_P", "Q_A_RAT_PIT_P" }, MainV2.comPort.MAV.param);
            RATE_PIT_I.setup(0, 0, 1, 0.001f, new[] { "RATE_PIT_I", "ATC_RAT_PIT_I", "Q_A_RAT_PIT_I" }, MainV2.comPort.MAV.param);
            RATE_PIT_D.setup(0, 0, 1, 0.0001f, new[] {"RATE_PIT_D", "ATC_RAT_PIT_D", "Q_A_RAT_PIT_D"}, MainV2.comPort.MAV.param);
            if (MainV2.comPort.MAV.param.ContainsKey("ATC_RAT_PIT_IMAX") || MainV2.comPort.MAV.param.ContainsKey("Q_A_RAT_PIT_IMAX")) // 3.4 changes scaling
                RATE_PIT_IMAX.setup(0, 0, 1, 1f, new[] {"ATC_RAT_PIT_IMAX", "Q_A_RAT_PIT_IMAX"},  MainV2.comPort.MAV.param);
            else
                RATE_PIT_IMAX.setup(0, 0, 10, 1f, new[] {"RATE_PIT_IMAX", "RATE_PIT_IMAX"}, MainV2.comPort.MAV.param);
            RATE_PIT_FILT.setup(0, 0, 1, 0.001f, new[] {"RATE_PIT_FILT", "ATC_RAT_PIT_FILT", "ATC_RAT_PIT_FLTE", "Q_A_RAT_PIT_FLTE"}, MainV2.comPort.MAV.param);
            ATC_RAT_PIT_FLTD.setup(0, 0, 1, 1f, new[] { "ATC_RAT_PIT_FLTD", "Q_A_RAT_PIT_FLTD" }, MainV2.comPort.MAV.param);
            ATC_RAT_PIT_FLTT.setup(0, 0, 1, 1f, new[] { "ATC_RAT_PIT_FLTT", "Q_A_RAT_PIT_FLTT" }, MainV2.comPort.MAV.param);

            RATE_RLL_P.setup(0, 0, 1, 0.001f, new[] { "RATE_RLL_P", "ATC_RAT_RLL_P", "Q_A_RAT_RLL_P" }, MainV2.comPort.MAV.param);
            RATE_RLL_I.setup(0, 0, 1, 0.001f, new[] { "RATE_RLL_I", "ATC_RAT_RLL_I", "Q_A_RAT_RLL_I" }, MainV2.comPort.MAV.param);
            RATE_RLL_D.setup(0, 0, 1, 0.0001f, new[] {"RATE_RLL_D", "ATC_RAT_RLL_D", "Q_A_RAT_RLL_D"}, MainV2.comPort.MAV.param);
            if (MainV2.comPort.MAV.param.ContainsKey("ATC_RAT_RLL_IMAX") || MainV2.comPort.MAV.param.ContainsKey("Q_A_RAT_RLL_IMAX")) // 3.4 changes scaling
                RATE_RLL_IMAX.setup(0, 0, 1, 1f, new[] {"ATC_RAT_RLL_IMAX", "Q_A_RAT_RLL_IMAX"}, MainV2.comPort.MAV.param);
            else
                RATE_RLL_IMAX.setup(0, 0, 10, 1f, new[] {"RATE_RLL_IMAX"}, MainV2.comPort.MAV.param);
            RATE_RLL_FILT.setup(0, 0, 1, 0.001f, new[] {"RATE_RLL_FILT", "ATC_RAT_RLL_FILT", "ATC_RAT_RLL_FLTE", "Q_A_RAT_RLL_FLTE"}, MainV2.comPort.MAV.param);
            ATC_RAT_RLL_FLTD.setup(0, 0, 1, 1f, new[] { "ATC_RAT_RLL_FLTD", "Q_A_RAT_RLL_FLTD" }, MainV2.comPort.MAV.param);
            ATC_RAT_RLL_FLTT.setup(0, 0, 1, 1f, new[] { "ATC_RAT_RLL_FLTT", "Q_A_RAT_RLL_FLTT" }, MainV2.comPort.MAV.param);

            RATE_YAW_P.setup(0, 0, 1, 0.001f, new[] { "RATE_YAW_P", "ATC_RAT_YAW_P", "Q_A_RAT_YAW_P" }, MainV2.comPort.MAV.param);
            RATE_YAW_I.setup(0, 0, 1, 0.001f, new[] { "RATE_YAW_I", "ATC_RAT_YAW_I", "Q_A_RAT_YAW_I" }, MainV2.comPort.MAV.param);
            RATE_YAW_D.setup(0, 0, 1, 0.0001f, new[] {"RATE_YAW_D", "ATC_RAT_YAW_D", "Q_A_RAT_YAW_D"}, MainV2.comPort.MAV.param);
            if (MainV2.comPort.MAV.param.ContainsKey("ATC_RAT_YAW_IMAX") || MainV2.comPort.MAV.param.ContainsKey("Q_A_RAT_YAW_IMAX")) // 3.4 changes scaling
                RATE_YAW_IMAX.setup(0, 0, 1, 1f, new[] {"ATC_RAT_YAW_IMAX", "Q_A_RAT_YAW_IMAX"}, MainV2.comPort.MAV.param);
            else
                RATE_YAW_IMAX.setup(0, 0, 10, 1f, new[] {"RATE_YAW_IMAX"}, MainV2.comPort.MAV.param);
            RATE_YAW_FILT.setup(0, 0, 1, 0.001f, new[] {"RATE_YAW_FILT", "ATC_RAT_YAW_FILT", "ATC_RAT_YAW_FLTE", "Q_A_RAT_YAW_FLTE"}, MainV2.comPort.MAV.param);
            ATC_RAT_YAW_FLTD.setup(0, 0, 1, 1f, new[] { "ATC_RAT_YAW_FLTD", "Q_A_RAT_YAW_FLTD" }, MainV2.comPort.MAV.param);
            ATC_RAT_YAW_FLTT.setup(0, 0, 1, 1f, new[] { "ATC_RAT_YAW_FLTT", "Q_A_RAT_YAW_FLTT" }, MainV2.comPort.MAV.param);

            STB_PIT_P.setup(0, 0, 1, 0.001f, new[] {"STB_PIT_P", "ATC_ANG_PIT_P", "Q_A_ANG_PIT_P"},
                MainV2.comPort.MAV.param);
            STB_RLL_P.setup(0, 0, 1, 0.001f, new[] {"STB_RLL_P", "ATC_ANG_RLL_P", "Q_A_ANG_RLL_P"},
                MainV2.comPort.MAV.param);
            STB_YAW_P.setup(0, 0, 1, 0.001f, new[] {"STB_YAW_P", "ATC_ANG_YAW_P", "Q_A_ANG_YAW_P"},
                MainV2.comPort.MAV.param);


            THR_ACCEL_P.setup(0, 0, 1, 0.001f, new[] { "THR_ACCEL_P", "ACCEL_Z_P", "PSC_ACCZ_P", "Q_P_ACCZ_P" },
                MainV2.comPort.MAV.param);
            THR_ACCEL_I.setup(0, 0, 1, 0.001f, new[] { "THR_ACCEL_I", "ACCEL_Z_I", "PSC_ACCZ_I", "Q_P_ACCZ_I" },
                MainV2.comPort.MAV.param);
            THR_ACCEL_D.setup(0, 0, 1, 0.001f, new[] {"THR_ACCEL_D", "ACCEL_Z_D", "PSC_ACCZ_D", "Q_P_ACCZ_D"},
                MainV2.comPort.MAV.param);
            THR_ACCEL_IMAX.setup(0, 0, 10, 1f, new[] {"THR_ACCEL_IMAX", "ACCEL_Z_IMAX", "PSC_ACCZ_IMAX", "Q_P_ACCZ_IMAX"},
                MainV2.comPort.MAV.param);

            THR_ALT_P.setup(0, 0, 1, 0.001f, new[] {"THR_ALT_P", "POS_Z_P", "PSC_POSZ_P", "Q_P_POSZ_P"},
                MainV2.comPort.MAV.param);
            THR_RATE_P.setup(0, 0, 1, 0.001f, new[] {"THR_RATE_P", "VEL_Z_P", "PSC_VELZ_P", "Q_P_VELZ_P"},
                MainV2.comPort.MAV.param);

            WPNAV_LOIT_SPEED.setup(0, 0, 1, 0.001f, new[] {"WPNAV_LOIT_SPEED", "LOIT_SPEED", "Q_LOIT_SPEED"},
                MainV2.comPort.MAV.param);
            WPNAV_RADIUS.setup(0, 0, 1, 0.001f, new[] {"WPNAV_RADIUS", "Q_WP_RADIUS"}, MainV2.comPort.MAV.param);
            WPNAV_SPEED.setup(0, 0, 1, 0.001f, new[] {"WPNAV_SPEED", "Q_WP_SPEED"}, MainV2.comPort.MAV.param);
            WPNAV_SPEED_DN.setup(0, 0, 1, 0.001f, new[] {"WPNAV_SPEED_DN", "Q_WP_SPEED_DN"}, MainV2.comPort.MAV.param);
            WPNAV_SPEED_UP.setup(0, 0, 1, 0.001f, new[] {"WPNAV_SPEED_UP", "Q_WP_SPEED_UP"}, MainV2.comPort.MAV.param);

            INS_GYRO_FILTER.setup(0, 0, 1, 1f, new[] { "INS_GYRO_FILTER" }, MainV2.comPort.MAV.param);
            INS_ACCEL_FILTER.setup(0, 0, 1, 1f, new[] { "INS_ACCEL_FILTER" }, MainV2.comPort.MAV.param);

            INS_LOG_BAT_MASK.setup(new[] { "INS_LOG_BAT_MASK" }, MainV2.comPort.MAV.param);
            INS_LOG_BAT_OPT.setup(0, 0, 1, 1f, new[] { "INS_LOG_BAT_OPT" }, MainV2.comPort.MAV.param);

            INS_NOTCH_ENABLE.setup(new[] { "INS_NOTCH_ENABLE" }, MainV2.comPort.MAV.param);
            INS_NOTCH_FREQ.setup(0, 0, 1, 1f, new[] { "INS_NOTCH_FREQ" }, MainV2.comPort.MAV.param);
            INS_NOTCH_BW.setup(0, 0, 1, 1f, new[] { "INS_NOTCH_BW" }, MainV2.comPort.MAV.param);
            INS_NOTCH_ATT.setup(0, 0, 1, 1f, new[] { "INS_NOTCH_ATT" }, MainV2.comPort.MAV.param);

            INS_HNTCH_ENABLE.setup(new[] { "INS_HNTCH_ENABLE" }, MainV2.comPort.MAV.param);
            INS_HNTCH_MODE.setup(0, 0, 1, 1f, new[] { "INS_HNTCH_MODE" }, MainV2.comPort.MAV.param);
            INS_HNTCH_REF.setup(0, 0, 1, 1f, new[] { "INS_HNTCH_REF" }, MainV2.comPort.MAV.param);
            INS_HNTCH_FREQ.setup(0, 0, 1, 1f, new[] { "INS_HNTCH_FREQ" }, MainV2.comPort.MAV.param);
            INS_HNTCH_ATT.setup(0, 0, 1, 1f, new[] { "INS_HNTCH_ATT" }, MainV2.comPort.MAV.param);
            INS_HNTCH_BW.setup(0, 0, 1, 1f, new[] { "INS_HNTCH_BW" }, MainV2.comPort.MAV.param);
            INS_HNTCH_OPTS.setup(0, 0, 1, 1f, new[] { "INS_HNTCH_OPTS" }, MainV2.comPort.MAV.param);
            INS_HNTCH_HMNCS.setup(0, 0, 1, 1f, new[] { "INS_HNTCH_HMNCS" }, MainV2.comPort.MAV.param);

            mavlinkNumericUpDownatc_accel_r_max.setup(0, 0, 1, 0.001f, new[] {"ATC_ACCEL_R_MAX", "Q_A_ACCEL_R_MAX"},
                MainV2.comPort.MAV.param);
            mavlinkNumericUpDownatc_accel_p_max.setup(0, 0, 1, 0.001f, new[] {"ATC_ACCEL_P_MAX", "Q_A_ACCEL_P_MAX"},
                MainV2.comPort.MAV.param);
            mavlinkNumericUpDownatc_accel_y_max.setup(0, 0, 1, 0.001f, new[] {"ATC_ACCEL_Y_MAX", "Q_A_ACCEL_Y_MAX"},
                MainV2.comPort.MAV.param);
            mavlinkNumericUpDownatc_input_tc.setup(0, 0, 1, 0.001f, new[] {"ATC_INPUT_TC", "Q_A_INPUT_TC"},
                MainV2.comPort.MAV.param);


            // unlock entries if they differ
            if (RATE_RLL_P.Value != RATE_PIT_P.Value || RATE_RLL_I.Value != RATE_PIT_I.Value
                                                     || RATE_RLL_D.Value != RATE_PIT_D.Value ||
                                                     RATE_RLL_IMAX.Value != RATE_PIT_IMAX.Value)
            {
                CHK_lockrollpitch.Checked = false;
            }

            if (MainV2.comPort.MAV.param["H_SWASH_TYPE"] != null)
            {
                CHK_lockrollpitch.Checked = false;
            }

            // add tooltips to all controls
            foreach (Control control1 in Controls)
            {
                foreach (Control control2 in control1.Controls)
                {
                    if (control2 is MavlinkNumericUpDown)
                    {
                        var ParamName = ((MavlinkNumericUpDown) control2).ParamName;
                        toolTip1.SetToolTip(control2,
                            ParameterMetaDataRepository.GetParameterMetaData(ParamName,
                                ParameterMetaDataConstants.Description, MainV2.comPort.MAV.cs.firmware.ToString()));
                    }

                    if (control2 is MavlinkComboBox)
                    {
                        var ParamName = ((MavlinkComboBox) control2).ParamName;
                        toolTip1.SetToolTip(control2,
                            ParameterMetaDataRepository.GetParameterMetaData(ParamName,
                                ParameterMetaDataConstants.Description, MainV2.comPort.MAV.cs.firmware.ToString()));
                    }
                }
            }

            startup = false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                BUT_writePIDS_Click(null, null);
                return true;
            }

            return false;
        }

        private static string AddNewLinesForTooltip(string text)
        {
            if (text.Length < maximumSingleLineTooltipLength)
                return text;
            var lineLength = (int)Math.Sqrt(text.Length) * 2;
            var sb = new StringBuilder();
            var currentLinePosition = 0;
            for (var textIndex = 0; textIndex < text.Length; textIndex++)
            {
                // If we have reached the target line length and the next      
                // character is whitespace then begin a new line.   
                if (currentLinePosition >= lineLength &&
                    char.IsWhiteSpace(text[textIndex]))
                {
                    sb.Append(Environment.NewLine);
                    currentLinePosition = 0;
                }
                // If we have just started a new line, skip all the whitespace.    
                if (currentLinePosition == 0)
                    while (textIndex < text.Length && char.IsWhiteSpace(text[textIndex]))
                        textIndex++;
                // Append the next character.     
                if (textIndex < text.Length) sb.Append(text[textIndex]);
                currentLinePosition++;
            }
            return sb.ToString();
        }

        private void disableNumericUpDownControls(Control inctl)
        {
            foreach (Control ctl in inctl.Controls)
            {
                if (ctl.Controls.Count > 0)
                {
                    disableNumericUpDownControls(ctl);
                }
                if (ctl.GetType() == typeof(NumericUpDown))
                {
                    ctl.Enabled = false;
                }
            }
        }

        internal void EEPROM_View_float_TextChanged(object sender, EventArgs e)
        {
            if (startup)
                return;

            float value = 0;
            var name = ((Control)sender).Name;

            // do domainupdown state check
            try
            {
                if (sender.GetType() == typeof(MavlinkNumericUpDown))
                {
                    value = ((MAVLinkParamChanged)e).value;
                    changes[name] = value;
                }
                else if (sender.GetType() == typeof(MavlinkComboBox))
                {
                    value = ((MAVLinkParamChanged)e).value;
                    changes[name] = value;
                }
                ((Control)sender).BackColor = Color.Green;
            }
            catch (Exception)
            {
                ((Control)sender).BackColor = Color.Red;
            }

            try
            {
                // enable roll and pitch pairing for ac2
                if (CHK_lockrollpitch.Checked)
                {
                    if (name.StartsWith("RATE_") || name.StartsWith("STB_") || name.StartsWith("ACRO_"))
                    {
                        if (name.Contains("_RLL_"))
                        {
                            var newname = name.Replace("_RLL_", "_PIT_");
                            var arr = Controls.Find(newname, true);
                            changes[newname] = value;

                            if (arr.Length > 0)
                            {
                                arr[0].Text = ((Control)sender).Text;
                                arr[0].BackColor = Color.Green;
                            }
                        }
                        else if (name.Contains("_PIT_"))
                        {
                            var newname = name.Replace("_PIT_", "_RLL_");
                            var arr = Controls.Find(newname, true);
                            changes[newname] = value;

                            if (arr.Length > 0)
                            {
                                arr[0].Text = ((Control)sender).Text;
                                arr[0].BackColor = Color.Green;
                            }
                        }
                    }
                }
                // keep nav_lat and nav_lon paired
                if (name.Contains("NAV_LAT_"))
                {
                    var newname = name.Replace("NAV_LAT_", "NAV_LON_");
                    var arr = Controls.Find(newname, true);
                    changes[newname] = value;

                    if (arr.Length > 0)
                    {
                        arr[0].Text = ((Control)sender).Text;
                        arr[0].BackColor = Color.Green;
                    }
                }
                // keep loiter_lat and loiter_lon paired
                if (name.Contains("LOITER_LAT_"))
                {
                    var newname = name.Replace("LOITER_LAT_", "LOITER_LON_");
                    var arr = Controls.Find(newname, true);
                    changes[newname] = value;

                    if (arr.Length > 0)
                    {
                        arr[0].Text = ((Control)sender).Text;
                        arr[0].BackColor = Color.Green;
                    }
                }
            }
            catch
            {
            }
        }

        private void BUT_writePIDS_Click(object sender, EventArgs e)
        {
            var temp = (Hashtable)changes.Clone();

            foreach (string value in temp.Keys)
            {
                try
                {
                    if ((float)changes[value] > (float)MainV2.comPort.MAV.param[value] * 2.0f)
                        if (
                            CustomMessageBox.Show(value + " увеличилось более чем вдвое по сравнению с предыдущим. Вы уверены?",
                                "Большое значение", MessageBoxButtons.YesNo) == (int)DialogResult.No)
                        {
                            try
                            {
                                // set control as well
                                var textControls = Controls.Find(value, true);
                                if (textControls.Length > 0)
                                {
                                    // restore old value
                                    textControls[0].Text = MainV2.comPort.MAV.param[value].Value.ToString();
                                    textControls[0].BackColor = Color.FromArgb(0x43, 0x44, 0x45);
                                }
                            }
                            catch
                            {
                            }
                            return;
                        }

                    if (MainV2.comPort.BaseStream == null || !MainV2.comPort.BaseStream.IsOpen)
                    {
                        CustomMessageBox.Show("Вы не подключены", Strings.ERROR);
                        return;
                    }

                    MainV2.comPort.setParam(value, (float)changes[value]);

                    changes.Remove(value);

                    try
                    {
                        // set control as well
                        var textControls = Controls.Find(value, true);
                        if (textControls.Length > 0)
                        {
                            textControls[0].BackColor = Color.FromArgb(0x43, 0x44, 0x45);
                        }
                    }
                    catch
                    {
                    }
                }
                catch
                {
                    CustomMessageBox.Show(string.Format(Strings.ErrorSetValueFailed, value), Strings.ERROR);
                }
            }
        }

        /// <summary>
        ///     Handles the Click event of the BUT_rerequestparams control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
        protected void BUT_rerequestparams_Click(object sender, EventArgs e)
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
                return;

            ((Control)sender).Enabled = false;

            try
            {
                MainV2.comPort.getParamList();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(Strings.ErrorReceivingParams + ex, Strings.ERROR);
            }


            ((Control)sender).Enabled = true;


            Activate();
        }

        private void BUT_refreshpart_Click(object sender, EventArgs e)
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
                return;

            ((Control)sender).Enabled = false;


            updateparam(this);

            ((Control)sender).Enabled = true;


            Activate();
        }

        private void updateparam(Control parentctl)
        {
            foreach (Control ctl in parentctl.Controls)
            {
                if (typeof(MavlinkNumericUpDown) == ctl.GetType() || typeof(ComboBox) == ctl.GetType())
                {
                    try
                    {
                        MainV2.comPort.GetParam(ctl.Name);
                    }
                    catch
                    {
                    }
                }

                if (ctl.Controls.Count > 0)
                {
                    updateparam(ctl);
                }
            }
        }

        private void numeric_ValueUpdated(object sender, EventArgs e)
        {
            EEPROM_View_float_TextChanged(sender, e);
        }
    }
}