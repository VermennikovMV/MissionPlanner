﻿using log4net;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigFriendlyParams : MyUserControl, IActivate
    {
        string searchfor = "";

        private void BUT_Find_Click(object sender, EventArgs e)
        {
            InputBox.TextChanged += InputBox_TextChanged;
            if (InputBox.Show("Поиск", "Введите одно слово для поиска", ref searchfor) == DialogResult.OK)
            {
                filterList(searchfor);
            }
            else
            {
                filterList("");
            }
        }

        private void InputBox_TextChanged(object sender, EventArgs e)
        {
            var textbox = sender as TextBox;

            searchfor = textbox.Text;

            _filterTimer.Elapsed -= FilterTimerOnElapsed;
            _filterTimer.Stop();
            _filterTimer.Interval = 500;
            _filterTimer.Elapsed += FilterTimerOnElapsed;
            _filterTimer.Start();
        }

        private readonly System.Timers.Timer _filterTimer = new System.Timers.Timer();

        private void FilterTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            _filterTimer.Stop();
            Invoke((Action)delegate
            {
                filterList(searchfor);
            });
        }

        void filterList(string searchfor)
        {
            if (searchfor.Length >= 2 || searchfor.Length == 0)
            {
                y = 10;
                flowLayoutPanel1.Enabled = false;
                flowLayoutPanel1.SuspendLayout();

                foreach (Control ctl in flowLayoutPanel1.Controls)
                {
                    if (ctl.GetType() == typeof(RangeControl))
                    {
                        var rng = (RangeControl)ctl;
                        if (rng.LabelText.ToLower().Contains(searchfor.ToLower()) ||
                            rng.DescriptionText.ToLower().Contains(searchfor.ToLower()))
                        {
                            ctl.Visible = true;
                            //ctl.Location = new Point(ctl.Location.X, y);
                            y += ctl.Height;
                        }
                        else
                        {
                            ctl.Visible = false;
                        }
                    }
                    else if (ctl.GetType() == typeof(ValuesControl))
                    {
                        var vctl = (ValuesControl)ctl;
                        if (vctl.LabelText.ToLower().Contains(searchfor.ToLower()) ||
                            vctl.DescriptionText.ToLower().Contains(searchfor.ToLower()))
                        {
                            ctl.Visible = true;
                            //ctl.Location = new Point(ctl.Location.X, y);
                            y += ctl.Height;
                        }
                        else
                        {
                            ctl.Visible = false;
                        }
                    }
                    else if (ctl.GetType() == typeof(MavlinkCheckBoxBitMask))
                    {
                        var bctl = (MavlinkCheckBoxBitMask)ctl;
                        if (bctl.label1.Text.ToLower().Contains(searchfor.ToLower()) ||
                            bctl.myLabel1.Text.ToLower().Contains(searchfor.ToLower()))
                        {
                            ctl.Visible = true;
                            //ctl.Location = new Point(ctl.Location.X, y);
                            y += ctl.Height;
                        }
                        else
                        {
                            ctl.Visible = false;
                        }
                    }
                }

                flowLayoutPanel1.ResumeLayout();
                flowLayoutPanel1.Enabled = true;
            }
        }

        #region Class Fields

        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<string, string> _params = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _params_changed = new Dictionary<string, string>();

        #endregion

        #region Properties

        /// <summary>
        ///     Gets or sets the parameter mode.
        /// </summary>
        /// <value>
        ///     The parameter mode.
        /// </value>
        public string ParameterMode { get; set; }

        private int y = 10;

        #endregion

        #region Constructor

        public ConfigFriendlyParams()
        {
            InitializeComponent();
            flowLayoutPanel1.Height = Height;

            Resize += this_Resize;

            BUT_rerequestparams.Click += BUT_rerequestparams_Click;
            BUT_writePIDS.Click += BUT_writePIDS_Click;

            ParameterMode = ParameterMode = ParameterMetaDataConstants.Standard;
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

        #endregion

        #region Events

        /// <summary>
        ///     Handles the Click event of the BUT_writePIDS control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
        protected void BUT_writePIDS_Click(object sender, EventArgs e)
        {
            var errorThrown = false;

            var list = _params_changed.Keys.ToList();

            // set enable last
            list.SortENABLE();

            list.ForEach(x =>
            {
                try
                {
                    MainV2.comPort.setParam(x, float.Parse(_params_changed[x], CultureInfo.InvariantCulture));
                }
                catch
                {
                    errorThrown = true;
                    CustomMessageBox.Show(string.Format(Strings.ErrorSetValueFailed, x), Strings.ERROR);
                }
            });
            if (!errorThrown)
            {
                _params_changed.Clear();
                CustomMessageBox.Show("Параметры успешно сохранены.", "Сохранено");
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

            if (DialogResult.OK ==  Common.MessageShowAgain("Refresh Params", Strings.WarningUpdateParamList, true))
            {
                ((Control)sender).Enabled = false;

                try
                {
                    MainV2.comPort.getParamList();
                }
                catch (Exception ex)
                {
                    log.Error("Exception getting param list", ex);
                    CustomMessageBox.Show(Strings.ErrorReceivingParams, Strings.ERROR);
                }


                ((Control)sender).Enabled = true;

                Activate();
            }
        }

        /// <summary>
        ///     Handles the Resize event of the this control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
        protected void this_Resize(object sender, EventArgs e)
        {
            flowLayoutPanel1.Height = Height - 50;
        }

        /// <summary>
        ///     Handles the Load event of the ConfigRawParamsV2 control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
        public void Activate()
        {
            y = 10;

            Console.WriteLine("Activate " + DateTime.Now.ToString("ss.fff"));
            BindParamList();
            Console.WriteLine("Activate Done " + DateTime.Now.ToString("ss.fff"));
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Sorts the param list.
        /// </summary>
        private void FilterParamList()
        {
            // Clear list
            _params.Clear();
            var locker = new object();

            // When the parameter list is changed, re sort the list for our View's purposes
            Parallel.ForEach(MainV2.comPort.MAV.param.Keys, x =>
            {
                var displayName = ParameterMetaDataRepository.GetParameterMetaData(x.ToString(),
                    ParameterMetaDataConstants.DisplayName, MainV2.comPort.MAV.cs.firmware.ToString());
                var parameterMode = ParameterMetaDataRepository.GetParameterMetaData(x.ToString(),
                    ParameterMetaDataConstants.User, MainV2.comPort.MAV.cs.firmware.ToString());

                // If we have a friendly display name AND
                if (!string.IsNullOrEmpty(displayName) &&
                    // The user type is equal to the ParameterMode specified at class instantiation OR
                    ((!string.IsNullOrEmpty(parameterMode) && parameterMode == ParameterMode) ||
                     // The user type is empty and this is in Advanced mode
                     string.IsNullOrEmpty(parameterMode) && ParameterMode == ParameterMetaDataConstants.Advanced))
                {
                    lock (locker)
                        _params.Add(x.ToString(), displayName);
                }
            });
        }

        /// <summary>
        ///     Binds the param list.
        /// </summary>
        private void BindParamList()
        {
            Console.WriteLine("BindParamList " + DateTime.Now.ToString("ss.fff"));

            try
            {
                FilterParamList();
                Console.WriteLine("Sorted " + DateTime.Now.ToString("ss.fff"));
            }
            catch
            {
            }

            flowLayoutPanel1.VerticalScroll.Value = 0;

            var toadd = new List<Control>();

            var favlist = Settings.Instance.GetList("fav_params");

            Parallel.ForEach(_params, x =>
            {
                AddControl(x, toadd);
                Console.WriteLine("add ctl " + x.Key + " " + DateTime.Now.ToString("ss.fff"));
            });

            // sort and favs
            toadd = toadd.OrderBy(x =>
            {
                if (favlist.Contains(x.Name))
                    return "0" + x.Name;
                return x.Name;
            }).ToList();

            flowLayoutPanel1.Visible = false;
            flowLayoutPanel1.Controls.AddRange(toadd.ToArray());
            flowLayoutPanel1.Visible = true;

            Console.WriteLine("Add done" + DateTime.Now.ToString("ss.fff"));
        }

        private object locker = new object();

        private void AddControl(KeyValuePair<string, string> x, List<Control> toadd) //, ref int ypos)
        {
            if (!string.IsNullOrEmpty(x.Key))
            {
                try
                {
                    var controlAdded = false;

                    var value = (MainV2.comPort.MAV.param[x.Key].Value).ToString("0.###");

                    var items = flowLayoutPanel1.Controls.Find(x.Key, false);
                    if (items.Length > 0)
                    {
                        if (items[0].GetType() == typeof(RangeControl))
                        {
                            ((RangeControl)items[0]).ValueChanged -= Control_ValueChanged;
                            ((RangeControl)items[0]).DeAttachEvents();
                            ((RangeControl)items[0]).Value = value;
                            ThemeManager.ApplyThemeTo(((RangeControl)items[0]));
                            ((RangeControl)items[0]).AttachEvents();
                            ((RangeControl)items[0]).ValueChanged += Control_ValueChanged;
                            return;
                        }
                        if (items[0].GetType() == typeof(ValuesControl))
                        {
                            ((ValuesControl)items[0]).ValueChanged -= Control_ValueChanged;
                            ((ValuesControl)items[0]).Value = value;
                            ((ValuesControl)items[0]).ValueChanged += Control_ValueChanged;
                            return;
                        }
                        if (items[0].GetType() == typeof(MavlinkCheckBoxBitMask))
                        {
                            ((MavlinkCheckBoxBitMask)items[0]).ValueChanged -= Control_ValueChanged;
                            ((MavlinkCheckBoxBitMask)items[0]).Value = Convert.ToSingle(value);
                            ((MavlinkCheckBoxBitMask)items[0]).ValueChanged += Control_ValueChanged;
                            return;
                        }
                    }

                    var description = ParameterMetaDataRepository.GetParameterMetaData(x.Key,
                        ParameterMetaDataConstants.Description, MainV2.comPort.MAV.cs.firmware.ToString());
                    var displayName = x.Value + " (" + x.Key + ")";
                    var units = ParameterMetaDataRepository.GetParameterMetaData(x.Key, ParameterMetaDataConstants.Units,
                        MainV2.comPort.MAV.cs.firmware.ToString());

                    // If this is a range
                    var rangeRaw = ParameterMetaDataRepository.GetParameterMetaData(x.Key,
                        ParameterMetaDataConstants.Range, MainV2.comPort.MAV.cs.firmware.ToString());
                    var incrementRaw = ParameterMetaDataRepository.GetParameterMetaData(x.Key,
                        ParameterMetaDataConstants.Increment, MainV2.comPort.MAV.cs.firmware.ToString());

                    if (!string.IsNullOrEmpty(rangeRaw) && !string.IsNullOrEmpty(incrementRaw))
                    {
                        float increment, intValue;
                        float.TryParse(incrementRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out increment);
                        // this is in local culture
                        float.TryParse(value, out intValue);

                        var rangeParts = rangeRaw.Split(' ');
                        if (rangeParts.Count() == 2 && increment > 0)
                        {
                            float lowerRange;
                            float.TryParse(rangeParts[0], NumberStyles.Float, CultureInfo.InvariantCulture,
                                out lowerRange);
                            float upperRange;
                            float.TryParse(rangeParts[1], NumberStyles.Float, CultureInfo.InvariantCulture,
                                out upperRange);

                            float displayscale = 1;

                            //    var rangeControl = new RangeControl();

                            if (units.ToLower() == "centi-degrees")
                            {
                                //Console.WriteLine(x.Key + " scale");
                                displayscale = 100;
                                units = "Degrees (Scaled)";
                                increment /= 100;
                            }
                            else if (units.ToLower() == "centimeters")
                            {
                                //Console.WriteLine(x.Key + " scale");
                                //  displayscale = 100;
                                //  units = "Meters (Scaled)";
                                //  increment /= 100;
                            }

                            var desc = FitDescriptionText(units, description, flowLayoutPanel1.Width);

                            var rangeControl = new RangeControl(x.Key, desc, displayName, increment, displayscale,
                                lowerRange, upperRange, value);

                            rangeControl.Width = flowLayoutPanel1.Width - 50;

                            //Console.WriteLine("{0} {1} {2} {3} {4}", x.Key, increment, lowerRange, upperRange, value);

                            ThemeManager.ApplyThemeTo(rangeControl);

                            if (intValue < lowerRange)
                                rangeControl.NumericUpDownControl.BackColor = Color.Orange;

                            if (intValue > upperRange)
                                rangeControl.NumericUpDownControl.BackColor = Color.Orange;

                            rangeControl.AttachEvents();

                            rangeControl.ValueChanged += Control_ValueChanged;

                            // set pos
                         //   rangeControl.Location = new Point(0, y);
                            // add control - let it autosize height
                            lock(locker)
                                toadd.Add(rangeControl);
                            // add height for next control
                            y += rangeControl.Height;

                            controlAdded = true;
                        }
                    }

                    // try bitmask next
                    if (!controlAdded)
                    {
                        var availableBitMask = ParameterMetaDataRepository.GetParameterBitMaskInt(x.Key,
                            MainV2.comPort.MAV.cs.firmware.ToString());
                        if (availableBitMask.Count > 0)
                        {
                            var bitmask = new MavlinkCheckBoxBitMask();
                            bitmask.Name = x.Key;
                            bitmask.setup(x.Key, MainV2.comPort.MAV.param);

                            bitmask.myLabel1.Text = displayName;
                            bitmask.label1.Text = FitDescriptionText(units, description, flowLayoutPanel1.Width - 50);
                            bitmask.Width = flowLayoutPanel1.Width - 50;

                            ThemeManager.ApplyThemeTo(bitmask);

                            // set pos
                            //bitmask.Location = new Point(0, y);
                            // add control - let it autosize height
                            lock (locker)
                                toadd.Add(bitmask);
                            // add height for next control
                            y += bitmask.Height;

                            bitmask.ValueChanged += Control_ValueChanged;

                            controlAdded = true;
                        }
                    }

                    if (!controlAdded)
                    {
                        // If this is a subset of values
                        var availableValuesRaw = ParameterMetaDataRepository.GetParameterMetaData(x.Key,
                            ParameterMetaDataConstants.Values, MainV2.comPort.MAV.cs.firmware.ToString());
                        if (!string.IsNullOrEmpty(availableValuesRaw))
                        {
                            var availableValues = availableValuesRaw.Split(',');
                            if (availableValues.Any())
                            {
                                var valueControl = new ValuesControl();
                                valueControl.Width = flowLayoutPanel1.Width - 50;
                                valueControl.Name = x.Key;
                                valueControl.DescriptionText = FitDescriptionText(units, description,
                                    flowLayoutPanel1.Width);
                                valueControl.LabelText = displayName;

                                ThemeManager.ApplyThemeTo(valueControl);

                                var splitValues = new List<KeyValuePair<string, string>>();
                                // Add the values to the ddl
                                foreach (var val in availableValues)
                                {
                                    var valParts = val.Split(':');
                                    splitValues.Add(new KeyValuePair<string, string>(valParts[0].Trim(),
                                        (valParts.Length > 1) ? valParts[1].Trim() : valParts[0].Trim()));
                                }
                                ;
                                valueControl.ComboBoxControl.DisplayMember = "Value";
                                valueControl.ComboBoxControl.ValueMember = "Key";
                                valueControl.ComboBoxControl.DataSource = splitValues;
                                valueControl.ComboBoxControl.SelectedValue = value;

                                valueControl.ValueChanged += Control_ValueChanged;

                                // set pos
                                //valueControl.Location = new Point(0, y);
                                // add control - let it autosize height
                                lock (locker)
                                    toadd.Add(valueControl);
                                // add height for next control
                                y += valueControl.Height;
                            }
                        }
                    }
                } // if there is an error simply dont show it, ie bad pde file, bad scale etc
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }
        }

        private void Control_ValueChanged(object Sender, string name, string value)
        {
            _params_changed[name] = value;
        }

        /// <summary>
        ///     Fits the description text.
        /// </summary>
        /// <param name="units">The units.</param>
        /// <param name="description">The description.</param>
        /// <returns></returns>
        private string FitDescriptionText(string units, string description, int width)
        {
            var returnDescription = new StringBuilder();

            if (!string.IsNullOrEmpty(units))
            {
                returnDescription.Append(string.Format(Strings.Units, units, Environment.NewLine));
            }

            if (!string.IsNullOrEmpty(description))
            {
                returnDescription.Append(Strings.Desc);
                //returnDescription.Append(description);
                //return returnDescription.ToString();

                var descriptionParts = description.Split(' ');
                for (var i = 0; i < descriptionParts.Length; i++)
                {
                    // what we are adding to the string
                    var appendtext = string.Format("{0} ", descriptionParts[i]);
                    returnDescription.Append(appendtext);

                    if (i != 0 && i % (width / 40) == 0)
                    {
                        returnDescription.Append(Environment.NewLine);
                    }
                }
            }

            return returnDescription.ToString();
        }

        #endregion
    }
}