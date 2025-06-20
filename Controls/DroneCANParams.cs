﻿using log4net;
using MissionPlanner.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using DroneCAN;

namespace MissionPlanner.Controls
{
    public partial class DroneCANParams : MyUserControl, IActivate, IDeactivate
    {
        // ?
        internal bool startup = true;

        // from http://stackoverflow.com/questions/2512781/winforms-big-paragraph-tooltip/2512895#2512895
        private const int maximumSingleLineTooltipLength = 50;

        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static Hashtable tooltips = new Hashtable();
        // Changes made to the params between writing to the copter
        private readonly Hashtable _changes = new Hashtable();
        private DroneCAN.DroneCAN _can;
        private byte _node;
        private List<DroneCAN.DroneCAN.uavcan_protocol_param_GetSet_res> _paramlist;
        private readonly System.Timers.Timer _filterTimer = new System.Timers.Timer();

        public DroneCANParams(DroneCAN.DroneCAN can, byte node, List<DroneCAN.DroneCAN.uavcan_protocol_param_GetSet_res> paramlist)
        {
            _can = can;
            _paramlist = paramlist;
            _node = node;

            InitializeComponent();

            this.Text = "Параметры UAVCAN - " + node;

            Params.CellValidating += CellValidatingEvtHdlr;
        }

        public void Activate()
        {
            startup = true;

            ThemeManager.ApplyThemeTo(this);

            _changes.Clear();

            BUT_writePIDS.Enabled = true;
            BUT_rerequestparams.Enabled = true;
            BUT_reset_params.Enabled = true;
            BUT_commitToFlash.Visible = true;


            Params.Enabled = false;

            foreach (DataGridViewColumn col in Params.Columns)
            {
                if (!String.IsNullOrEmpty(Settings.Instance["rawparamuavcan_" + col.Name + "_width"]))
                {
                    col.Width = Math.Max(50, Settings.Instance.GetInt32("rawparamuavcan_" + col.Name + "_width"));
                    log.InfoFormat("{0} to {1}", col.Name, col.Width);
                }
            }

            processToScreen();

            Params.Enabled = true;

            startup = false;

            txt_search.Focus();
        }

        public void Deactivate()
        {
            foreach (DataGridViewColumn col in Params.Columns)
            {
                Settings.Instance["rawparamuavcan_" + col.Name + "_width"] = col.Width.ToString();
            }
        }

        internal void processToScreen()
        {
            toolTip1.RemoveAll();
            Params.Rows.Clear();

            log.Info("processToScreen");

            var list = new List<string>();
            foreach (string item in _paramlist.Select(a => ASCIIEncoding.ASCII.GetString(a.name, 0, a.name_len)))
                list.Add(item);

            var rowlist = new List<DataGridViewRow>();

            // process hashdefines and update display
            Parallel.ForEach(list, value =>
            {
                if (value == null || value == "")
                    return;

                var row = new DataGridViewRow();
                lock (rowlist)
                    rowlist.Add(row);
                row.CreateCells(Params);
                row.Cells[Command.Index].Value = value;
                row.Cells[Value.Index].Value = _paramlist
                    .First(a => ASCIIEncoding.ASCII.GetString(a.name, 0, a.name_len) == value).value.GetValue();
                row.Cells[Min.Index].Value = _paramlist
                    .First(a => ASCIIEncoding.ASCII.GetString(a.name, 0, a.name_len) == value).min_value.GetValue();
                row.Cells[Max.Index].Value = _paramlist
                    .First(a => ASCIIEncoding.ASCII.GetString(a.name, 0, a.name_len) == value).max_value.GetValue();
                row.Cells[Default.Index].Value = _paramlist
                    .First(a => ASCIIEncoding.ASCII.GetString(a.name, 0, a.name_len) == value).default_value.GetValue();
                var fav_params = Settings.Instance.GetList("fav_params");
                row.Cells[Fav.Index].Value = fav_params.Contains(value);

            });

            log.Info("about to add all");

            Params.SuspendLayout();
            Params.Visible = false;
            Params.Enabled = false;

            Params.Rows.AddRange(rowlist.ToArray());

            log.Info("about to sort");

            Params.SortCompare += OnParamsOnSortCompare;

            Params.Sort(Params.Columns[Command.Index], ListSortDirection.Ascending);

            Params.Enabled = true;
            Params.Visible = true;
            Params.ResumeLayout();

            log.Info("Done");
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

        /// <summary>
        /// Returns whether the given cell validating event args are for the value column.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool GetIsValue(DataGridViewCellValidatingEventArgs e)
        {
            return e.ColumnIndex == Value.Index;
        }
        
        /// <summary>
        /// Returns true if the edit is within min-to-max range or there is no min/max.  Otherwise false.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool GetIsInRange(DataGridViewCellValidatingEventArgs e)
        {
            float mi, ma;

            //If there's a min and max value...
            if (float.TryParse(Params[Min.Index, e.RowIndex].Value.ToString(), out mi) &&
                float.TryParse(Params[Max.Index, e.RowIndex].Value.ToString(), out ma))
            {
                float v;

                //If the proposed new value is a number...
                if (float.TryParse(Params.EditingControl.Text, out v))
                {
                    //Return whether it's within min and max.
                    return v >= mi && v <= ma;
                }
                else
                {
                    //Existing value is a number, but new value isn't.  Not in range.
                    return false;
                }
            }
            else
            {
                //The existing value isn't a number.  Assume it can be any text.
                return true;
            }
        }

        /// <summary>
        /// Checks that a value edit is within min and max.
        /// </summary>
        /// <param name="sender">ignored</param>
        /// <param name="e"></param>
        private void CellValidatingEvtHdlr(object sender, DataGridViewCellValidatingEventArgs e)
        {
            try
            {   
                //If it's the value column, but the new value isn't in min-to-max range...
                if (GetIsValue(e) && !GetIsInRange(e))
                {
                    CustomMessageBox.Show("Недопустимое значение \"" + Params.EditingControl.Text + "\"");
                    //Replace the editor's text with the existing cell text.
                    Params.EditingControl.Text = Params[e.ColumnIndex, e.RowIndex].Value.ToString();
                }
            }
            catch
            {
            }
        }

        private void BUT_commitToFlash_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_can.SaveConfig(_node))
                    CustomMessageBox.Show("Не удалось сохранить");
            }
            catch
            {
                CustomMessageBox.Show("Недопустимая команда");
                return;
            }

            CustomMessageBox.Show("Параметры сохранены в энергонезависимой памяти");
            return;
        }

        private void BUT_compare_Click(object sender, EventArgs e)
        {
            var param2 = new Dictionary<string, double>();

            using (var ofd = new OpenFileDialog
            {
                AddExtension = true,
                DefaultExt = ".param",
                RestoreDirectory = true,
                Filter = ParamFile.FileMask
            })
            {
                var dr = ofd.ShowDialog();
                if (dr == DialogResult.OK)
                {
                    param2 = ParamFile.loadParamFile(ofd.FileName);

                    Form paramCompareForm = new ParamCompare(Params, MainV2.comPort.MAV.param, param2);

                    ThemeManager.ApplyThemeTo(paramCompareForm);
                    paramCompareForm.ShowDialog();
                }
            }
        }

        private void BUT_load_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog
            {
                AddExtension = true,
                DefaultExt = ".param",
                RestoreDirectory = true,
                Filter = ParamFile.FileMask
            })
            {
                var dr = ofd.ShowDialog();

                if (dr == DialogResult.OK)
                {
                    loadparamsfromfile(ofd.FileName, true);
                }
            }
        }

        private void BUT_rerequestparams_Click(object sender, EventArgs e)
        {

            if ((int)DialogResult.OK ==
                CustomMessageBox.Show(Strings.WarningUpdateParamList, Strings.ERROR, MessageBoxButtons.OKCancel))
            {
                ((Control)sender).Enabled = false;

                try
                {

                    _paramlist = _can.GetParameters(_node);
                }
                catch (Exception ex)
                {
                    log.Error("Exception getting param list", ex);
                    CustomMessageBox.Show(Strings.ErrorReceivingParams, Strings.ERROR);
                }


                ((Control)sender).Enabled = true;

                startup = true;

                processToScreen();

                startup = false;
            }
        }

        private void BUT_reset_params_Click(object sender, EventArgs e)
        {
            if (
                CustomMessageBox.Show("Сбросить все параметры по умолчанию\nВы уверены?", "Reset",
                    MessageBoxButtons.YesNo) == (int)DialogResult.Yes)
            {
                try
                {
                    MainV2.comPort.setParam(new[] { "FORMAT_VERSION", "SYSID_SW_MREV" }, 0);
                    Thread.Sleep(1000);
                    MainV2.comPort.doReboot(false, true);
                    MainV2.comPort.BaseStream.Close();


                    CustomMessageBox.Show(
                        "Плата перезагружается. Потребуется переподключиться к автопилоту.");
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    CustomMessageBox.Show(Strings.ErrorCommunicating + "\n" + ex, Strings.ERROR);
                }
            }
        }

        private void BUT_save_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = ".param",
                RestoreDirectory = true,
                Filter = "Список параметров|*.param;*.parm"
            })
            {
                var dr = sfd.ShowDialog();
                if (dr == DialogResult.OK)
                {
                    var data = new Hashtable();
                    foreach (DataGridViewRow row in Params.Rows)
                    {
                        try
                        {
                            var value = double.Parse(row.Cells[1].Value.ToString());

                            data[row.Cells[0].Value.ToString()] = value;
                        }
                        catch (Exception)
                        {
                            CustomMessageBox.Show(Strings.InvalidNumberEntered + " " + row.Cells[0].Value);
                        }
                    }

                    ParamFile.SaveParamFile(sfd.FileName, data);
                }
            }
        }

        private void BUT_writePIDS_Click(object sender, EventArgs e)
        {
            if (Common.MessageShowAgain("Записать параметры", "Вы уверены?") != DialogResult.OK)
                return;

            // sort with enable at the bottom - this ensures params are set before the function is disabled
            var temp = new List<string>();
            foreach (var item in _changes.Keys)
            {
                temp.Add((string) item);
            }

            temp.SortENABLE();

            int failed = 0;

            foreach (string value in temp)
            {
                try
                {
                    _can.SetParameter(_node, value, _changes[value]);

                    try
                    {
                        // set control as well
                        var textControls = Controls.Find(value, true);
                        if (textControls.Length > 0)
                        {
                            ThemeManager.ApplyThemeTo(textControls[0]);
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        // set param table as well
                        foreach (DataGridViewRow row in Params.Rows)
                        {
                            if (row.Cells[0].Value.ToString() == value)
                            {
                                row.Cells[1].Style.BackColor = ThemeManager.ControlBGColor;
                                _changes.Remove(value);
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    failed++;
                    CustomMessageBox.Show("Не удалось установить " + value + " " + ex.ToString());
                }
            }

            _can.SaveConfig(_node);

            if (failed > 0)
                CustomMessageBox.Show("Некоторые параметры не удалось сохранить.", "Сохранено");
            else
                CustomMessageBox.Show("Параметры успешно сохранены.", "Сохранено");
        }

        private void chk_modified_CheckedChanged(object sender, EventArgs e)
        {
            FilterTimerOnElapsed(null, null);
        }

        void filterList(string searchfor)
        {
            DateTime start = DateTime.Now;
            Params.SuspendLayout();
            Params.Enabled = false;
            if (searchfor.Length >= 2 || searchfor.Length == 0)
            {
                Regex filter = new Regex(searchfor.Replace("*", ".*").Replace("..*", ".*"), RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

                foreach (DataGridViewRow row in Params.Rows)
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Value != null && filter.IsMatch(cell.Value.ToString()))
                        {
                            row.Visible = true;
                            break;
                        }
                        row.Visible = false;
                    }
                }
            }

            if (chk_modified.Checked)
            {
                foreach (DataGridViewRow row in Params.Rows)
                {
                    // is it modified? - always show
                    if (_changes.ContainsKey(row.Cells[Command.Index].Value))
                    {
                        row.Visible = true;
                    }
                    else
                    {
                        row.Visible = false;
                    }
                }
            }
            Params.Enabled = true;
            Params.ResumeLayout();

            log.InfoFormat("Filter: {0}ms", (DateTime.Now - start).TotalMilliseconds);
        }

        private void FilterTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            _filterTimer.Stop();
            Invoke((Action)delegate
           {
               filterList(txt_search.Text);
           });
        }

        private void loadparamsfromfile(string fn, bool offline = false)
        {
            var param2 = ParamFile.loadParamFile(fn);

            var loaded = 0;
            var missed = 0;
            List<string> missing = new List<string>();

            foreach (string name in param2.Keys)
            {
                var set = false;
                var value = param2[name].ToString();
                // set param table as well
                foreach (DataGridViewRow row in Params.Rows)
                {
                    if (name == "FORMAT_VERSION")
                        continue;
                    if (row.Cells[0].Value.ToString() == name)
                    {
                        set = true;
                        if (row.Cells[1].Value.ToString() != value)
                            row.Cells[1].Value = value;
                        break;
                    }
                }

                if (offline && !set)
                {
                    set = true;                    
                }

                if (set)
                {
                    loaded++;
                }
                else
                {
                    missed++;
                    missing.Add(name);
                }
            }

            if (missed > 0)
            {
                string list = "";
                foreach (var item in missing)
                {
                    list += item + " ";
                }
                CustomMessageBox.Show("Отсутствует " + missed + " параметров\n" + list, "Нет соответствующих параметров", MessageBoxButtons.OK);
            }
        }
        private void OnParamsOnSortCompare(object sender, DataGridViewSortCompareEventArgs args)
        {
            var fav1obj = Params[Fav.Index, args.RowIndex1].Value;
            var fav2obj = Params[Fav.Index, args.RowIndex2].Value;

            var fav1 = fav1obj == null ? false : (bool)fav1obj;

            var fav2 = fav2obj == null ? false : (bool)fav2obj;

            if (args.CellValue1 == null)
                return;

            if (args.CellValue2 == null)
                return;

            args.SortResult = args.CellValue1.ToString().CompareTo(args.CellValue2.ToString());
            args.Handled = true;

            if (fav1 && fav2)
            {
                return;
            }

            if (fav1 || fav2)
                args.SortResult = fav1.CompareTo(fav2) * (Params.SortOrder == SortOrder.Ascending ? -1 : 1);
        }

        private void Params_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Only process the Description column
            if (e.RowIndex == -1 || startup)
                return;


            if (e.ColumnIndex == Fav.Index)
            {
                var check = Params[e.ColumnIndex, e.RowIndex].EditedFormattedValue;
                var name = Params[Command.Index, e.RowIndex].Value.ToString();

                if (check != null && (bool)check)
                {
                    // add entry
                    Settings.Instance.AppendList("fav_params", name);
                }
                else
                {
                    // remove entry
                    var list = Settings.Instance.GetList("fav_params");
                    Settings.Instance.SetList("fav_params", list.Where(s => s != name));
                }

                Params.Sort(Command, ListSortDirection.Ascending);
            }
        }

        private void Params_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1 || e.ColumnIndex == -1 || startup || e.ColumnIndex != 1)
                return;
            try
            {
                if (Params[Command.Index, e.RowIndex].Value.ToString().EndsWith("_REV") &&
                    (Params[Command.Index, e.RowIndex].Value.ToString().StartsWith("RC") ||
                     Params[Command.Index, e.RowIndex].Value.ToString().StartsWith("HS")))
                {
                    if (Params[e.ColumnIndex, e.RowIndex].Value.ToString() == "0")
                        Params[e.ColumnIndex, e.RowIndex].Value = "-1";
                }

                var value = (string)Params[e.ColumnIndex, e.RowIndex].Value;

                Params[e.ColumnIndex, e.RowIndex].Style.BackColor = Color.Green;
                double asdouble = 0;
                if (double.TryParse((string)Params[e.ColumnIndex, e.RowIndex].Value, out asdouble))
                {
                    _changes[Params[Command.Index, e.RowIndex].Value] = asdouble;
                }
                else
                {
                    _changes[Params[Command.Index, e.RowIndex].Value] =
                        ((string)Params[e.ColumnIndex, e.RowIndex].Value);
                }
            }
            catch (Exception)
            {
                Params[e.ColumnIndex, e.RowIndex].Style.BackColor = Color.Red;
            }


            Params.Focus();
        }
        private void txt_search_TextChanged(object sender, EventArgs e)
        {
            _filterTimer.Elapsed -= FilterTimerOnElapsed;
            _filterTimer.Stop();
            _filterTimer.Interval = 500;
            _filterTimer.Elapsed += FilterTimerOnElapsed;
            _filterTimer.Start();
        }
    }
}