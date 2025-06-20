﻿using Microsoft.Diagnostics.Runtime.Interop;
using MissionPlanner.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using DroneCAN;
using ZedGraph;

namespace MissionPlanner.Controls
{
    public class DroneCANInspector : Form
    {
        private GroupBox groupBox1;
        private MyTreeView treeView1;
        private Timer timer1;
        private IContainer components;

        private PacketInspector<(DroneCAN.CANFrame frame, object message)>
            pktinspect = new PacketInspector<(DroneCAN.CANFrame, object)>();
        private MyButton but_graphit;
        private MyButton but_subscribe;
        private DroneCAN.DroneCAN can;

        public DroneCANInspector(DroneCAN.DroneCAN can)
        {
            InitializeComponent();

            this.can = can;

            can.MessageReceived += Can_MessageReceived;

            pktinspect.NewSysidCompid += (sender, args) =>
            {

            };

            timer1.Tick += (sender, args) => Update();

            timer1.Start();

            ThemeManager.ApplyThemeTo(this);
        }

        private void Can_MessageReceived(DroneCAN.CANFrame frame, object msg, byte transferID)
        {
            pktinspect.Add(frame.SourceNode, 0, frame.MsgTypeID, (frame, msg), frame.SizeofEntireMsg);
        }

        public new void Update()
        {
            treeView1.BeginUpdate();

            bool added = false;

            foreach (var dronecanMessage in pktinspect.GetPacketMessages())
            {
                TreeNode sysidnode;
                TreeNode msgidnode;

                var sysidnodes = treeView1.Nodes.Find(dronecanMessage.frame.SourceNode.ToString(), false);
                if (sysidnodes.Length == 0)
                {
                    sysidnode = new TreeNode("ID " + dronecanMessage.frame.SourceNode)
                    {
                        Name = dronecanMessage.frame.SourceNode.ToString()
                    };
                    treeView1.Nodes.Add(sysidnode);
                    added = true;
                }
                else
                {
                    sysidnode = sysidnodes.First();
                    sysidnode.Text = "ID " + dronecanMessage.frame.SourceNode + " - " + can.GetNodeName(dronecanMessage.frame.SourceNode) + " " + pktinspect
                        .SeenBps(dronecanMessage.frame.SourceNode, 0)
                        .ToString("~0Bps");
                }

                var msgidnodes = sysidnode.Nodes.Find(dronecanMessage.frame.MsgTypeID.ToString(), false);
                if (msgidnodes.Length == 0)
                {
                    msgidnode = new TreeNode(dronecanMessage.frame.MsgTypeID.ToString())
                    {
                        Name = dronecanMessage.frame.MsgTypeID.ToString()
                    };
                    sysidnode.Nodes.Add(msgidnode);
                    added = true;
                }
                else
                    msgidnode = msgidnodes.First();

                var seenrate = (pktinspect.SeenRate(dronecanMessage.frame.SourceNode, 0, dronecanMessage.frame.MsgTypeID));

                var msgidheader = dronecanMessage.message.GetType().Name + " (" +
                                  seenrate.ToString("0.0 Hz") + ", #" + dronecanMessage.frame.MsgTypeID + ") " +
                                  pktinspect.SeenBps(dronecanMessage.frame.SourceNode, 0, dronecanMessage.frame.MsgTypeID).ToString("~0Bps");

                if (msgidnode.Text != msgidheader)
                    msgidnode.Text = msgidheader;

                var minfo = DroneCAN.DroneCAN.MSG_INFO.First(a => a.Item1 == dronecanMessage.Item2.GetType());
                var fields = minfo.Item1.GetFields().Where(f => !f.IsLiteral).ToArray();

                PopulateMSG(fields, msgidnode, dronecanMessage.message);
            }

            if (added)
                treeView1.Sort();

            treeView1.EndUpdate();
        }

        private static void PopulateMSG(FieldInfo[] Fields, TreeNode MsgIdNode, object message)
        {
            foreach (var field in Fields)
            {
                if (!MsgIdNode.Nodes.ContainsKey(field.Name))
                {
                    MsgIdNode.Nodes.Add(new TreeNode() {Name = field.Name});
                }

                object value = field.GetValue(message);

                if (field.Name == "time_unix_usec")
                {
                    DateTime date1 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    try
                    {
                        value = date1.AddMilliseconds((ulong)value / 1000);
                    }
                    catch
                    {
                    }
                }

                if (field.FieldType.IsArray)
                {
                    var subtype = value.GetType();

                    var value2 = (Array)value;

                    if (field.Name == "param_id" || field.Name == "text" ||
                        field.Name == "string_value" || field.Name == "name") // param_value
                    {
                        value = ASCIIEncoding.ASCII.GetString((byte[])value2);
                    }
                    else if (value2.Length > 0)
                    {
                        if (field.FieldType.IsClass)
                        {
                            var elementtype = field.FieldType.GetElementType();
                            var fields = elementtype.GetFields().Where(f => !f.IsLiteral).ToArray();

                            if (!elementtype.IsPrimitive)
                            {
                                MsgIdNode.Nodes[field.Name].Text = field.Name;
                                int a = 0;
                                foreach (var valuei in value2)
                                {
                                    var name = field.Name + "[" + a.ToString() + "]" ;
                                    if (!MsgIdNode.Nodes[field.Name].Nodes.ContainsKey(name))
                                    {
                                        MsgIdNode.Nodes[field.Name].Nodes.Add(new TreeNode()
                                        {
                                            Name = name,
                                            Text = name
                                        });
                                    }

                                    PopulateMSG(fields, MsgIdNode.Nodes[field.Name].Nodes[name], valuei);
                                    a++;
                                }
                                continue;
                            }
                        }
                        value = value2.Cast<object>().Aggregate((a, b) => a + "," + b);
                    } 
                    else if (value2.Length == 0)
                    {
                        value = null;
                    }
                }

                if (!field.FieldType.IsArray && field.FieldType.IsClass)
                {
                    MsgIdNode.Nodes[field.Name].Text = field.Name;
                    PopulateMSG(field.FieldType.GetFields(), MsgIdNode.Nodes[field.Name], value);
                    continue;
                }
                
                MsgIdNode.Nodes[field.Name].Text = (String.Format("{0,-32} {1,20} {2,-20}", field.Name, value,
                    field.FieldType.Name));
            }
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.treeView1 = new MissionPlanner.Controls.DroneCANInspector.MyTreeView();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.but_graphit = new MissionPlanner.Controls.MyButton();
            this.but_subscribe = new MissionPlanner.Controls.MyButton();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView1.Location = new System.Drawing.Point(3, 16);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(693, 259);
            this.treeView1.TabIndex = 0;
            this.treeView1.DrawNode += new System.Windows.Forms.DrawTreeNodeEventHandler(this.treeView1_DrawNode);
            this.treeView1.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.treeView1);
            this.groupBox1.Location = new System.Drawing.Point(0, 30);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(699, 278);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            // 
            // timer1
            // 
            this.timer1.Interval = 333;
            // 
            // but_graphit
            // 
            this.but_graphit.Enabled = false;
            this.but_graphit.Location = new System.Drawing.Point(12, 3);
            this.but_graphit.Name = "but_graphit";
            this.but_graphit.Size = new System.Drawing.Size(75, 23);
            this.but_graphit.TabIndex = 4;
            this.but_graphit.Text = "Построить график";
            this.but_graphit.UseVisualStyleBackColor = true;
            this.but_graphit.Click += new System.EventHandler(this.but_graphit_Click);
            // 
            // but_subscribe
            // 
            this.but_subscribe.Location = new System.Drawing.Point(93, 3);
            this.but_subscribe.Name = "but_subscribe";
            this.but_subscribe.Size = new System.Drawing.Size(75, 23);
            this.but_subscribe.TabIndex = 5;
            this.but_subscribe.Text = "Подписаться";
            this.but_subscribe.UseVisualStyleBackColor = true;
            this.but_subscribe.Click += new System.EventHandler(this.but_subscribe_Click);
            // 
            // UAVCANInspector
            // 
            this.ClientSize = new System.Drawing.Size(698, 311);
            this.Controls.Add(this.but_subscribe);
            this.Controls.Add(this.but_graphit);
            this.Controls.Add(this.groupBox1);
            this.Name = "UAVCANInspector";
            this.Text = "Инспектор UAVCAN";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MAVLinkInspector_FormClosing);
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        private void treeView1_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Bounds.Y < 0 || e.Bounds.X == -1)
                return;

            var tv = sender as TreeView;

            new SolidBrush(Color.FromArgb(e.Bounds.Y % 200, e.Bounds.Y % 200, e.Bounds.Y % 200));

            e.Graphics.DrawString(e.Node.Text, tv.Font, new SolidBrush(this.ForeColor)
                , e.Bounds.X,
                e.Bounds.Y);
        }

        private void MAVLinkInspector_FormClosing(object sender, FormClosingEventArgs e)
        {
            can.MessageReceived -= Can_MessageReceived;

            timer1.Stop();
        }

        public class MyTreeView : TreeView
        {
            public MyTreeView()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
                //SetStyle(ControlStyles.AllPaintingInWmPaint, true);
                //UpdateStyles();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (GetStyle(ControlStyles.UserPaint))
                {
                    Message m = new Message
                    {
                        HWnd = Handle
                    };
                    int WM_PRINTCLIENT = 0x318;
                    m.Msg = WM_PRINTCLIENT;
                    m.WParam = e.Graphics.GetHdc();
                    int PRF_CLIENT = 0x00000004;
                    m.LParam = (IntPtr)PRF_CLIENT;
                    DefWndProc(ref m);
                    e.Graphics.ReleaseHdc(m.WParam);
                }
                base.OnPaint(e);
            }
        }

        private string selectedmsgid;

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e == null || e.Node == null || e.Node.Parent == null)
                return;

            int throwaway = 0;
            //if (int.TryParse(e.Node.Parent.Name, out throwaway))
            {
                selectedmsgid = e.Node.Name;
                var current = e.Node.Parent;
                while (current != null)
                {
                    selectedmsgid = current.Name + "/" + selectedmsgid;
                    current = current.Parent;
                }

                but_graphit.Enabled = true;
                but_subscribe.Enabled = true;
            }
            //else
            {
               // but_graphit.Enabled = false;
            }
        }

        int history = 50;

        private void but_graphit_Click(object sender, EventArgs e)
        {
            InputBox.Show("Точки", "Сколько точек истории?", ref history);
            var form = new Form() { Size = new Size(640, 480) };
            var zg1 = new ZedGraphControl() { Dock = DockStyle.Fill };
            var msgpath = selectedmsgid.Split('/');
            var nodeid = int.Parse(msgpath[0]);
            var msgid = int.Parse(msgpath[1]);
            var path = msgpath.Skip(2);
            var msgidfield = msgpath.Last();
            var line = new LineItem(msgidfield, new RollingPointPairList(history), Color.Red, SymbolType.None);
            zg1.GraphPane.Title.Text = "";

            zg1.GraphPane.CurveList.Add(line);

            zg1.GraphPane.XAxis.Type = AxisType.Date;
            zg1.GraphPane.XAxis.Scale.Format = "HH:mm:ss.fff";
            zg1.GraphPane.XAxis.Scale.MajorUnit = DateUnit.Minute;
            zg1.GraphPane.XAxis.Scale.MinorUnit = DateUnit.Second;

            var timer = new Timer() { Interval = 100 };
            DroneCAN.DroneCAN.MessageRecievedDel msgrecv = (frame, msg, id) =>
            {
                if (frame.SourceNode == nodeid && frame.MsgTypeID == msgid)
                {
                    object data = msg;
                    foreach (var subpath in path)
                    {
                        // array member
                        if (subpath.Contains("["))
                        {
                            var count = subpath.Split('[', ']');
                            var index = int.Parse(count[1]);
                            data = ((IList) data)[index];
                            continue;
                        }
                        var field = data.GetType().GetField(subpath);
                        data = field.GetValue(data);
                    }

                    var item = data;

                    if (item.GetType().IsClass && !item.GetType().IsArray)
                    {
                        var items = data.GetType().GetFields();
                        var dict = items.ToDictionary(ks => ks.Name, es => es.GetValue(item));

                        zg1.GraphPane.CurveList.Remove(line);

                        int a = 0;
                        foreach (var newitem in dict)
                        {
                            var label = msgidfield + "." + newitem.Key;
                            var lines = zg1.GraphPane.CurveList.Where(ci =>
                                ci.Label.Text == label || ci.Label.Text.StartsWith(label + " "));
                            if (lines.Count() == 0)
                            {
                                line = new LineItem(label, new RollingPointPairList(history), color[a % color.Length],
                                    SymbolType.None);
                                zg1.GraphPane.CurveList.Add(line);
                            }
                            else
                                line = (LineItem) lines.First();
                            
                            AddToGraph(newitem.Value, zg1, newitem.Key, line);
                            a++;
                        }
                        return;
                    }

                    AddToGraph(item, zg1, msgidfield, line);
                }
            };
            can.MessageReceived += msgrecv;
            timer.Tick += (o, args) =>
            {
                // Make sure the Y axis is rescaled to accommodate actual data
                zg1.AxisChange();

                // Force a redraw

                zg1.Invalidate();

            };
            form.Controls.Add(zg1);
            form.Closing += (o2, args2) => { can.MessageReceived -= msgrecv; };
            ThemeManager.ApplyThemeTo(form);
            form.Show(this);
            timer.Start();
            but_graphit.Enabled = false;
        }

        Color[] color = new Color[]
            {Color.Red, Color.Green, Color.Blue, Color.Black, Color.Violet, Color.Orange};

        private void AddToGraph(object item, ZedGraphControl zg1, string msgidfield, LineItem line)
        {
            if (item is IEnumerable)
            {
                int a = 0;
                foreach (var subitem in (IEnumerable) item)
                {
                    if (subitem is IConvertible)
                    {
                        while (zg1.GraphPane.CurveList.Count < (a + 1))
                        {
                            zg1.GraphPane.CurveList.Add(new LineItem(msgidfield + "[" + a + "]",
                                new RollingPointPairList(history), color[a % color.Length],
                                SymbolType.None));
                        }

                        zg1.GraphPane.CurveList[a].AddPoint(new XDate(DateTime.Now),
                            ((IConvertible) subitem).ToDouble(null));
                        a++;
                    }
                }
            }
            else if (item is IConvertible)
            {
                line.AddPoint(new XDate(DateTime.Now),
                    ((IConvertible) item).ToDouble(null));
            } 
            else if (item.GetType().IsClass)
            {

            }
            else
            {
                line.AddPoint(new XDate(DateTime.Now),
                    (double) (dynamic) item);
            }
        }

        private void but_subscribe_Click(object sender, EventArgs e)
        {
            if(String.IsNullOrEmpty(selectedmsgid))
                return;
            new DroneCANSubscriber(can, selectedmsgid).ShowUserControl();
        }
    }
}
