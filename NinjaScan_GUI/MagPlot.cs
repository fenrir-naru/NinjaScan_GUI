﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZedGraph;
using NinjaScan;

namespace NinjaScan_GUI
{
    public partial class MagPlot : Form
    {
        int tickStart = 0;
        GraphPane MagPane;

        M_Page m_page;

        public MagPlot(Form1 owner)
        {
            m_page = owner.pages.m;
            InitializeComponent();
            MagPane = zedGraphControl1.GraphPane;
            MagPane.Title.IsVisible = false;
            MagPane.XAxis.Title.Text = "time (sec)";
            MagPane.YAxis.Title.Text = "Magnetism (uT)";

            // Save 1200 points.  At 50 ms sample rate, this is one minute
            // The RollingPointPairList is an efficient storage class that always
            // keeps a rolling set of point data without needing to shift any data values
            RollingPointPairList xlist = new RollingPointPairList(2000);
            RollingPointPairList ylist = new RollingPointPairList(2000);
            RollingPointPairList zlist = new RollingPointPairList(2000);

            // Initially, a curve is added with no data points (list is empty)
            // Color is blue, and there will be no symbols
            LineItem curve = MagPane.AddCurve("x axis", xlist, Color.Blue, SymbolType.None);
            curve = MagPane.AddCurve("y axis", ylist, Color.Red, SymbolType.None);
            curve = MagPane.AddCurve("z axis", zlist, Color.Green, SymbolType.None);

            // Sample at 50ms intervals
            timer1.Interval = 500;
            timer1.Enabled = true;
            timer1.Start();

            // Just manually control the X axis range so it scrolls continuously
            // instead of discrete step-sized jumps
            MagPane.XAxis.Scale.Min = 0;
            MagPane.XAxis.Scale.Max = (double)numericUpDown1.Value;
            MagPane.XAxis.Scale.MinorStep = 0.5;
            MagPane.XAxis.Scale.MajorStep = 1;
            MagPane.YAxis.Scale.Max = M_Page.defaultCalibrationData.fullScale_mag / 2;
            MagPane.YAxis.Scale.Min = -M_Page.defaultCalibrationData.fullScale_mag / 2;

            // Scale the axes
            zedGraphControl1.AxisChange();

            // Save the beginning time for reference
            tickStart = Environment.TickCount;
        }

        private void zedGraphControl1_Load(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // Make sure that the curvelist has at least one curve
            if (zedGraphControl1.GraphPane.CurveList.Count <= 0)
                return;

            // Get the first CurveItem in the graph
            LineItem xcurve = zedGraphControl1.GraphPane.CurveList[0] as LineItem;
            LineItem ycurve = zedGraphControl1.GraphPane.CurveList[1] as LineItem;
            LineItem zcurve = zedGraphControl1.GraphPane.CurveList[2] as LineItem;
            if (xcurve == null || ycurve == null || zcurve == null)
                return;

            // Get the PointPairList
            IPointListEdit xlist = xcurve.Points as IPointListEdit;
            IPointListEdit ylist = ycurve.Points as IPointListEdit;
            IPointListEdit zlist = zcurve.Points as IPointListEdit;
            // If this is null, it means the reference at curve.Points does not
            // support IPointListEdit, so we won't be able to modify it
            if (xlist == null || ylist == null || zlist == null)
                return;

            // Time is measured in seconds
            //double time = (Environment.TickCount - tickStart) / 1000.0;
            double time = m_page.gps_time / 1000.0;

            // 3 seconds per cycle
            //list.Add(time, Math.Sin(2.0 * Math.PI * time / 3.0));
            xlist.Add(time, m_page.cal_mx);
            ylist.Add(time, m_page.cal_my);
            zlist.Add(time, m_page.cal_mz);

            // Keep the X scale at a rolling 30 second interval, with one
            // major step between the max X value and the end of the axis
            Scale xScale = zedGraphControl1.GraphPane.XAxis.Scale;
            //if (time > xScale.Max - xScale.MajorStep)
            //{
                xScale.Max = time + (double)numericUpDown1.Value / 8;
                xScale.Min = xScale.Max - (double)numericUpDown1.Value;
            //}
                if (xScale.Min < 0)
                    xScale.Min = 0;

            // Make sure the Y axis is rescaled to accommodate actual data
            zedGraphControl1.AxisChange();
            // Force a redraw
            zedGraphControl1.Invalidate();
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }


        /// <summary>
        /// X軸のパラメータ変更したときのイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            MagPane.XAxis.Scale.Max = (double)numericUpDown1.Value;
        }

        /// <summary>
        /// Y軸のパラメータ変更したときのイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                MagPane.YAxis.Scale.Max = M_Page.defaultCalibrationData.fullScale_mag / 2;
                MagPane.YAxis.Scale.Min = -M_Page.defaultCalibrationData.fullScale_mag / 2;
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                MagPane.YAxis.Scale.Max = M_Page.defaultCalibrationData.fullScale_mag;
                MagPane.YAxis.Scale.Min = -M_Page.defaultCalibrationData.fullScale_mag;
            }
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton3.Checked)
            {
                MagPane.YAxis.Scale.Max = M_Page.defaultCalibrationData.fullScale_mag * 3;
                MagPane.YAxis.Scale.Min = -M_Page.defaultCalibrationData.fullScale_mag * 3;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Image img = MagPane.GetImage();

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = "NinjaScanLite_MAG";
            sfd.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
            sfd.Filter = "PNG file(*.png)|*.png";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                img.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private void MagPlot_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }


    }
}
