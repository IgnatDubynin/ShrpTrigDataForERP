using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Accord.IO;
using Accord.Math;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Numerics;

namespace TrigDataForERP
{
    public partial class Form1 : Form
    {
        public struct EpochStruct
        {
            public double[] epoch;
            public int index1;//индекс начала эпохи, соответствующий индексу в ЭЭГ данных
        }
        public List<double[]> EpochsList = new List<double[]>();//надо добавить сюда индекс данных, соответствующий началу интервала

        public List<EpochStruct> TestList = new List<EpochStruct>();

        public List<double[]> EpochsFilList = new List<double[]>();    
        public List<double> DataDiffRMSList = new List<double>();
        Functions fc = new Functions();
        double[] EEGDataArr;
        double[] EEGDataFilArr;
        double[] EEGDataFilArr2;
        double[] EpochDataArr;
        double[] EpochDataFilArr;
        double[] ERPDataFilArr;
        double[] TemplateForLSM;
        double[] TemplateSubEpochForLSM;
        public double[] ResultEpoch;
        bool EEGDataFiltered = false;
        double[] ERPepochAvrg;
        int SrcIndx = 0;
        int ViewBlockSize = 3000;//размер отображаемой области графиков по Х
        int CurPos = 0;//текущая позиция скролла

        VerticalLineAnnotation VA;
        RectangleAnnotation RA;
        ChartArea CA;

        public Form1()
        {
            InitializeComponent();
        }
        private void AddChartAnnotationByX(int x, string label, Color lblClr)
        {
            // factors to convert values to pixels
            double xFactor = 1;         // use your numbers!
            double yFactor = 2;        // use your numbers!

            // the vertical line
            VA = new VerticalLineAnnotation();
            VA.AxisX = CA.AxisX;
            VA.AllowMoving = false;
            VA.IsInfinitive = true;
            VA.ClipToChartArea = CA.Name;
            VA.Name = "myLine" + x.ToString();
            VA.LineColor = lblClr;
            VA.LineWidth = 2;         // use your numbers!
            VA.X = x;

            // the rectangle
            RA = new RectangleAnnotation();
            RA.AxisX = CA.AxisX;
            RA.IsSizeAlwaysRelative = false;
            RA.Width = 80 * xFactor;         // use your numbers!
            RA.Height = 8 * yFactor;        // use your numbers!
            RA.Name = "myRect" + x.ToString();
            RA.LineColor = lblClr;
            RA.BackColor = Color.Gray;
            RA.AxisY = CA.AxisY;
            RA.Y = -RA.Height + 205;
            RA.X = VA.X - RA.Width / 2;

            RA.Text = label;
            RA.ForeColor = Color.White;
            RA.Font = new System.Drawing.Font("Arial", 8f);

            chrtNativeData.Annotations.Add(VA);
            chrtNativeData.Annotations.Add(RA);
        }

        public void AddChartRectangleAnnotation(Chart chrt, int t1, int t2)
        {
            int xFactor = 1;
            int yFactor = 1;
            // the rectangle
            chrt.Annotations.Clear();
            RectangleAnnotation ra = new RectangleAnnotation();
            ChartArea ca = chrt.ChartAreas[0];
            ra.AxisX = ca.AxisX;
            ra.IsSizeAlwaysRelative = false;
            ra.Width = (t2-t1) * xFactor;         // use your numbers!
            ra.Height = 380 * yFactor;        // use your numbers!
            ra.Name = "myRect1";
            ra.LineColor = Color.LightGray;
            ra.BackColor = Color.FromArgb(20, Color.LightGreen);
            ra.AxisY = ca.AxisY;
            ra.Y = -ra.Height + 85;
            ra.X = t1;
            ra.Text = "";
            ra.ForeColor = Color.White;
            ra.Font = new System.Drawing.Font("Arial", 8f);
            
            chrt.Annotations.Add(ra);
        }

        public Series setSeriesCfg(Series srs, Color clr)
        {
            srs.ChartType = SeriesChartType.FastLine;
            srs.YValueType = ChartValueType.Double;
            srs.BorderWidth = 2;
            srs.Color = clr;
            return srs;
        }
        private void LoadChartAnnotations(byte[] trigData)
        {
            CA = chrtNativeData.ChartAreas[0];  // pick the right ChartArea..
                                                //S1 = chrtNativeData.Series[0];      // ..and Series!
            string ctypeStr = "";
            Color clr = Color.Black;

            for (int i = 0; i < trigData.Length; i++)
            {
                if (trigData[i] == 12)
                {
                    ctypeStr = "12";
                    clr = Color.Red;
                    AddChartAnnotationByX(i, ctypeStr, clr);
                }
                if (trigData[i] == 10)
                {
                    ctypeStr = "10";
                    clr = Color.Green;
                    AddChartAnnotationByX(i, ctypeStr, clr);
                }
                if (trigData[i] == 13)
                {
                    ctypeStr = "13";
                    clr = Color.Red;
                    AddChartAnnotationByX(i, ctypeStr, clr);
                }
            }

        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            string localPath = @"d:\SINEP\Data\29042016\";
            string pathToTriggers = Path.Combine(localPath, "relmaska.mat");//придётся для данных [0;1] отдельно делать byte[], т.к. double[] не канает
            string pathToEEGData = Path.Combine(localPath, "27ChnlEEGFil1to20Hz.mat");

            var rdrTrig = new MatReader(pathToTriggers);
            string[] names0 = rdrTrig.FieldNames;
            object unknown0 = rdrTrig.Read(names0[0]);
            byte[] trigDataArr = ((IEnumerable)unknown0).Cast<object>()
                             .Select(x => (byte)x)
                             .ToArray();

            var reader = new MatReader(pathToEEGData);
            string[] names = reader.FieldNames;
            object unknown = reader.Read(names[0]);
            EEGDataArr = ((IEnumerable)unknown).Cast<object>()
                             .Select(x => (double)x)
                             .ToArray();

            int[] indices = Enumerable.Range(0, trigDataArr.Length)
                          .Where(index => trigDataArr[index] == 10)//варианты меток: 12 (начало интервала),10 (erp),13 (конец интервала) !!!!
                          .ToArray();

            int epocLength = 1400;//400 отсчётов это 2 сек при 200Гц
            gv.NPOINTS = epocLength;

            int[] newindices = new int[indices.Length];
            int val = 0;
            int indx = 0;
            for (int i = 0; i < indices.Length; i++)
            {
                val = indices[i] - (int)epocLength / 2;
                if (val > 0)
                {
                    newindices[indx] = val;
                    indx++;
                }
            }

            LoadChartAnnotations(trigDataArr);


            double[] eegSubdataArr = new double[epocLength];
            int srcIndx = 0;
            for (int i = 0; i < newindices.Length; i++)
            {
                srcIndx = newindices[i];
                Array.Copy(EEGDataArr, srcIndx, eegSubdataArr, 0, epocLength);
                double maxVal = eegSubdataArr.Max();
                double minVal = eegSubdataArr.Min();
                if (maxVal < 50 && minVal > -50)
                    EpochsList.Add(eegSubdataArr.Clone() as double[]);
            }

            gv.CtrlLock = true;
            dataGridView1.DataSource = null;
            dataGridView1.Rows.Clear();

            dataGridView1.DataSource = EpochsList;
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
                dataGridView1.Rows[i].HeaderCell.Value = (i + 1).ToString();
            gv.CtrlLock = false;

            txtBxEpochCnt.Text = EpochsList.Count.ToString();

            ERPepochAvrg = Enumerable.Range(0, EpochsList[0].Length)
               .Select(index => EpochsList.Average(item => item[index]))
               .ToArray();

            hScrollBar1.Maximum = EEGDataArr.Length;
            hScrollBar1.Minimum = 0;

            EEGDataDraw(EEGDataArr);

            ERPDataDraw(ERPepochAvrg);

            AddChartRectangleAnnotation(chart2, gv.FIRST_EP, gv.LAST_EP);

            gv.CtrlLock = true;
            dataGridView1.CurrentCell = dataGridView1.Rows[12].Cells[0];
            //dataGridView1.Rows[12].Selected = true;
            gv.CtrlLock = false;
            //dataGridView1.CurrentRow.Index = 13;
            txtBxCurEpoch.Text = (dataGridView1.CurrentRow.Index + 1).ToString();
            EpochDataArr = EpochsList[dataGridView1.CurrentRow.Index];
            EpochDataDraw(EpochDataArr);

            AddChartRectangleAnnotation(chart3, gv.FIRST_EP, gv.LAST_EP);
        }

        private void ERPDataDraw(double[] epoch)
        {
            if (epoch != null)
            {
                Chart chrt = chart2;
                chrt.Series.Clear();
                Series srs2 = new Series();
                setSeriesCfg(srs2, Color.Red);
                chrt.Series.Add(srs2);

                chrt.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
                chrt.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
                chrt.ChartAreas[0].AxisY.Minimum = -30;//Math.Round(ERPepochAvrg.Min() - 25);
                chrt.ChartAreas[0].AxisY.Maximum = 30;//Math.Round(ERPepochAvrg.Max() + 25);
                chrt.Series[0].IsVisibleInLegend = false;
                //chrt.ChartAreas[0].AxisY.Interval = 2;

                double[] epochDataScaled = new double[epoch.Length];
                for (int k = 0; k < epochDataScaled.Length; k++)
                    epochDataScaled[k] = (epoch[k] * gv.ERPAmplCoef);

                srs2.Points.DataBindY(epochDataScaled);
            }
        }

        private void EEGDataDraw(double[] data = null)
        {
            if (data != null)
            {
                Chart chrt = chrtNativeData;
                chrt.Series.Clear();
                Series srs1 = new Series();
                setSeriesCfg(srs1, Color.Red);
                chrt.Series.Add(srs1);

                chrt.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
                chrt.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
                chrt.ChartAreas[0].AxisY.Minimum = Math.Round(data.Min() - 100);
                chrt.ChartAreas[0].AxisY.Maximum = Math.Round(data.Max() + 100);
                chrt.ChartAreas[0].AxisX.Interval = 100;
                chrt.Series[0].IsVisibleInLegend = false;
                //chrt.ChartAreas[0].AxisY.Interval = 2;

                double[] XValues = new double[ViewBlockSize];
                for (int k = 0; k < XValues.Length; k++)
                {
                    XValues[k] = SrcIndx + k;
                }

                double[] chDataDoubleArr = new double[ViewBlockSize];
                int shift = (SrcIndx * sizeof(double));
                System.Buffer.BlockCopy(data, shift, chDataDoubleArr, 0, ViewBlockSize * sizeof(double));

                for (int k = 0; k < chDataDoubleArr.Length; k++)
                    chDataDoubleArr[k] = (chDataDoubleArr[k] * gv.EEGAmplCoef);

                srs1.Points.DataBindXY(XValues, chDataDoubleArr);

                hScrollBar1.LargeChange = ViewBlockSize;

                chrt.ChartAreas[0].AxisX.ScaleView.Size = ViewBlockSize;
                chrt.ChartAreas[0].AxisX.ScaleView.Position = SrcIndx;
                chrt.ChartAreas[0].AxisX.Minimum = SrcIndx;
                chrt.ChartAreas[0].AxisX.Maximum = SrcIndx + ViewBlockSize;
            }
        }
        private void DrawResult(double[] data = null)
        {
            if (data != null)
            {
                Chart chrt = chart4;
                chrt.Series.Clear();
                Series srs = new Series();// { Title = "Series " + i.ToString() };
                srs.Name = "Series " + (dataGridView1.CurrentRow.Index + 1).ToString();
                srs = setSeriesCfg(srs, Color.Red);

                double[] chDataDoubleArr = new double[data.Length];
                int shift = (0 * sizeof(double));
                System.Buffer.BlockCopy(data, shift, chDataDoubleArr, 0, data.Length * sizeof(double));

                for (int k = 0; k < chDataDoubleArr.Length; k++)
                    chDataDoubleArr[k] = (chDataDoubleArr[k] * gv.RsltEpochAmplCoef);

                srs.Points.DataBindY(chDataDoubleArr);
                chrt.Series.Add(srs);
                chrt.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
                chrt.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
                chrt.ChartAreas[0].AxisY.Minimum = chart2.ChartAreas[0].AxisY.Minimum - 0;
                chrt.ChartAreas[0].AxisY.Maximum = chart2.ChartAreas[0].AxisY.Maximum + 0;
                chrt.Series[0].IsVisibleInLegend = false;
            }
        }
        private void EpochDataDraw(double[] data = null)
        {
            if (data != null)
            {
                Chart chrt = chart3;
                chrt.Series.Clear();
                Series srs = new Series();// { Title = "Series " + i.ToString() };
                srs.Name = "Series " + (dataGridView1.CurrentRow.Index + 1).ToString();
                srs = setSeriesCfg(srs, Color.Red);

                double[] chDataDoubleArr = new double[data.Length];
                int shift = (0 * sizeof(double));
                System.Buffer.BlockCopy(data, shift, chDataDoubleArr, 0, data.Length * sizeof(double));

                for (int k = 0; k < chDataDoubleArr.Length; k++)
                    chDataDoubleArr[k] = (chDataDoubleArr[k] * gv.EpochAmplCoef);

                srs.Points.DataBindY(chDataDoubleArr);
                chrt.Series.Add(srs);
                chrt.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
                chrt.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
                chrt.ChartAreas[0].AxisY.Minimum = chart2.ChartAreas[0].AxisY.Minimum;
                chrt.ChartAreas[0].AxisY.Maximum = chart2.ChartAreas[0].AxisY.Maximum;
                chrt.Series[0].IsVisibleInLegend = false;
            }
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow != null && !gv.CtrlLock)
            {
                if (dataGridView1.CurrentRow.Index != -1)
                {
                    txtBxCurEpoch.Text = (dataGridView1.CurrentRow.Index + 1).ToString();
                    EpochDataArr = EpochsList[dataGridView1.CurrentRow.Index];
                    EpochDataDraw(EpochDataArr);

                    double thrshld = 30;
                    string str = "";
                    bool artefact = false;
                    for(int i=0; i< gv.LAST_EP+200 && !artefact; i++)
                    {
                        str = "0";
                        if (EpochDataArr[i] > thrshld)
                        {
                            str = "1";
                            artefact = true;
                        }                           
                    }

                    txtBxArtefactCntr.Text = str;

                    ResultEpoch = new double[EpochDataArr.Length];

                    ResultEpoch = ExtractERP(EpochDataArr);

                    DrawResult(ResultEpoch);

                    //Вычисление разницы с заранее подготовленным шаблоном RP
                    TemplateSubEpochForLSM = new double[gv.LAST_EP - gv.FIRST_EP];
                    System.Buffer.BlockCopy(ResultEpoch, gv.FIRST_EP * sizeof(double), TemplateSubEpochForLSM, (0) * sizeof(double), TemplateSubEpochForLSM.Length * sizeof(double));

                    double[] resltLSM = new double[TemplateForLSM.Length];
                    double lsmVal = 0;
                    for (int i = 0; i < TemplateForLSM.Length; i++)
                    {
                        lsmVal += Math.Pow(TemplateForLSM[i] - TemplateSubEpochForLSM[i], 2);
                    }
                    txtBxLSMVal.Text = lsmVal.ToString();

                    AddChartRectangleAnnotation(chart4, gv.FIRST_EP, gv.LAST_EP);

                    double rmsGnd, rmsErp = 0;
                    double diffRmsErp = 0;
                    CalcRMSs(ResultEpoch, out rmsGnd, out rmsErp);

                    txtBxGndRMS.Text = rmsGnd.ToString();
                    txtBxErpRMS.Text = rmsErp.ToString();
                    diffRmsErp = rmsErp - rmsGnd;
                    txtBxDiffRMS.Text = (diffRmsErp).ToString();


                    textBox1.Text = "0";

                    if (artefact)
                        System.Media.SystemSounds.Hand.Play();

                    if (!artefact)
                    {
                        if ((rmsErp - rmsGnd) > 4.5 && lsmVal < 50000)
                        {
                            textBox1.Text = "1";
                            System.Media.SystemSounds.Beep.Play();
                            return;
                        }

                        if (diffRmsErp > 4)
                        {
                            if (diffRmsErp > 6)
                            {
                                if (lsmVal < 50000)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                            if (diffRmsErp > 7)
                            {
                                if (lsmVal < 60000)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                            if (diffRmsErp > 8)
                            {
                                if (lsmVal < 70000)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                            if (diffRmsErp > 9)
                            {
                                if (lsmVal < 100000)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                        }
                        if (lsmVal < 100000)
                        {
                            if (lsmVal < 70000)
                            {
                                if (diffRmsErp > 12)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                            if (lsmVal < 60000)
                            {
                                if (diffRmsErp > 11)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                            if (lsmVal < 50000)
                            {
                                if (diffRmsErp > 6)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                            if (lsmVal < 40000)
                            {
                                if (diffRmsErp > 4)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                            if (lsmVal < 30000)
                            {
                                if (diffRmsErp > 3)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                            if (lsmVal < 20000)
                            {
                                if (diffRmsErp > 2.5)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                            if (lsmVal < 16000)
                            {
                                if (diffRmsErp > 1.4)
                                {
                                    textBox1.Text = "1";
                                    System.Media.SystemSounds.Beep.Play();
                                    return;
                                }
                            }
                        }
                    }


                    /*rTxtBxIn.Clear();
                    for (int i = 0; i < epoch.Length; i++)
                    {
                        rTxtBxIn.AppendText(epoch[i].ToString() + Environment.NewLine);
                    }
                    string[] resultStr = rTxtBxIn.Lines.Where((x, y) => y != rTxtBxIn.Lines.Length - 1).ToArray();
                    rTxtBxIn.Lines = resultStr;*/
                }
            }
        }

        private void btnGetData_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow != null && dataGridView1.CurrentRow.Index != -1)
            {
                var epoch = EpochsList[dataGridView1.CurrentRow.Index];

                rTxtBxIn.Clear();
                for (int i = 0; i < epoch.Length; i++)
                {
                    rTxtBxIn.AppendText(epoch[i].ToString() + Environment.NewLine);
                }
                string[] resultStr = rTxtBxIn.Lines.Where((x, y) => y != rTxtBxIn.Lines.Length - 1).ToArray();//удалить последнюю пустую строку 
                rTxtBxIn.Lines = resultStr;
            }
        }

        private void btnCalcFFT_Click(object sender, EventArgs e)
        {
            double[] data = fc.Sine(512);

            //применение окна Ханна
            for (int k = 0; k < data.Length; k++)
            {
                double multiplier = 0.5 * (1 - Math.Cos(2 * Math.PI * k / (data.Length - 1)));
                data[k] = multiplier * data[k];
            }

            Chart chrt = chrtNativeData;
            chrt.Series.Clear();
            Series srs = new Series();// { Title = "Series " + i.ToString() };
            srs.Name = "Series " + (1).ToString();
            srs = setSeriesCfg(srs, Color.Red);
            srs.Points.DataBindY(data);
            chrt.Series.Add(srs);
            chrt.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
            chrt.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
            chrt.ChartAreas[0].AxisY.Minimum = chrtNativeData.ChartAreas[0].AxisY.Minimum;
            chrt.ChartAreas[0].AxisY.Maximum = chrtNativeData.ChartAreas[0].AxisY.Maximum;
            chrt.Series[0].IsVisibleInLegend = false;

            Complex[] cData = fc.ConvertToComplex(data);

            FourierTransform.FFT(cData, FourierTransform.Direction.Forward);

            double[] resData = new double[cData.Length];
            for (int i=0; i<cData.Length; i++)
            {
                resData[i] = cData[i].Magnitude;
            }
            chrt = chart2;
            chrt.Series.Clear();
            Series srs1 = new Series();// { Title = "Series " + i.ToString() };
            srs1.Name = "Series " + (1).ToString();
            srs1 = setSeriesCfg(srs1, Color.Red);
            srs1.Points.DataBindY(resData);
            chrt.Series.Add(srs1);
            chrt.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
            chrt.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
            chrt.ChartAreas[0].AxisY.Minimum = chrtNativeData.ChartAreas[0].AxisY.Minimum;
            chrt.ChartAreas[0].AxisY.Maximum = chrtNativeData.ChartAreas[0].AxisY.Maximum;
            chrt.Series[0].IsVisibleInLegend = false;
            chrt.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
            //chrt.ChartAreas[0].AxisY.ScaleView.Zoomable = true;

            FourierTransform.FFT(cData, FourierTransform.Direction.Backward);

            double[] resData2 = new double[cData.Length];
            for (int i = 0; i < cData.Length; i++)
            {
                resData2[i] = cData[i].Magnitude;
            }
            chrt = chart3;
            chrt.Series.Clear();
            Series srs2 = new Series();// { Title = "Series " + i.ToString() };
            srs2.Name = "Series " + (1).ToString();
            srs2 = setSeriesCfg(srs2, Color.Red);
            srs2.Points.DataBindY(resData2);
            chrt.Series.Add(srs2);
            chrt.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
            chrt.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.DashDot;
            chrt.ChartAreas[0].AxisY.Minimum = chrtNativeData.ChartAreas[0].AxisY.Minimum;
            chrt.ChartAreas[0].AxisY.Maximum = chrtNativeData.ChartAreas[0].AxisY.Maximum;
            chrt.Series[0].IsVisibleInLegend = false;
            chrt.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            chrtNativeData.MouseWheel += chart3_MouseWheel;
            chrtNativeData.MouseEnter += friendChart_MouseEnter;
            chrtNativeData.MouseLeave += friendChart_MouseLeave;

            chart2.MouseWheel += chart3_MouseWheel;
            chart2.MouseEnter += friendChart_MouseEnter;
            chart2.MouseLeave += friendChart_MouseLeave;

            chart3.MouseWheel += chart3_MouseWheel;
            chart3.MouseEnter += friendChart_MouseEnter;
            chart3.MouseLeave += friendChart_MouseLeave;

            tabControl1.SelectedTab = tabPage2;

            gv.FIRST_EP = (int)UpDwnFirstERP.Value;
            gv.LAST_EP = (int)UpDwnLastERP.Value;

            string sLine = "";
            string localPath = @"d:\SINEP\Data\";
            string pathToFilCoefsDataFileName = Path.Combine(localPath, "ERP2Template400.txt");//шаблон реального ВП для сравнения рез-та фильтрации МНК
            TemplateForLSM = new double[gv.LAST_EP - gv.FIRST_EP];
            using (StreamReader sReader = new StreamReader(pathToFilCoefsDataFileName))
            {
                TemplateForLSM = new double[gv.LAST_EP - gv.FIRST_EP];
                int indx = 0;
                while (!sReader.EndOfStream)
                {
                    sLine = sReader.ReadLine();
                    double.TryParse(sLine, out double dblVal);
                    TemplateForLSM[indx] = dblVal;
                    indx++;
                }
            }
        }
        void friendChart_MouseLeave(object sender, EventArgs e)
        {
            var chart = (Chart)sender;
            if (chart.Focused) chart.Parent.Focus(); 
        }
        void friendChart_MouseEnter(object sender, EventArgs e) 
        {
            var chart = (Chart)sender;
            if (!chart.Focused) chart.Focus(); 
        }
        private void chart3_MouseWheel(object sender, MouseEventArgs e)
        {
            var chart = (Chart)sender;
            var xAxis = chart.ChartAreas[0].AxisX;
            var yAxis = chart.ChartAreas[0].AxisY;

            try
            {
                if (e.Delta < 0) // Scrolled down.
                {
                    xAxis.ScaleView.ZoomReset();
                    yAxis.ScaleView.ZoomReset();
                }
                else if (e.Delta > 0) // Scrolled up.
                {
                    var xMin = xAxis.ScaleView.ViewMinimum;
                    var xMax = xAxis.ScaleView.ViewMaximum;
                    var yMin = yAxis.ScaleView.ViewMinimum;
                    var yMax = yAxis.ScaleView.ViewMaximum;

                    var posXStart = xAxis.PixelPositionToValue(e.Location.X) - (xMax - xMin) / 4;
                    var posXFinish = xAxis.PixelPositionToValue(e.Location.X) + (xMax - xMin) / 4;
                    var posYStart = yAxis.PixelPositionToValue(e.Location.Y) - (yMax - yMin) / 4;
                    var posYFinish = yAxis.PixelPositionToValue(e.Location.Y) + (yMax - yMin) / 4;

                    xAxis.ScaleView.Zoom(posXStart, posXFinish);
                    yAxis.ScaleView.Zoom(posYStart, posYFinish);
                }
            }
            catch { }
        }

        private void btnFIRFil_Click(object sender, EventArgs e)
        {
            if (EEGDataArr != null)
            {
                int firCoefsCnt = fc.FIRFiltersList[0].Length;

                EEGDataFilArr = new double[EEGDataArr.Length - firCoefsCnt + 1];
                int indx = 0;
                for (int i = 0; i <= EEGDataArr.Length - firCoefsCnt; i++)
                {
                    int srcIndx = Math.Min(EEGDataArr.Length - firCoefsCnt, Math.Max(0, i));

                    double[] chDataDoubleArr = new double[firCoefsCnt];
                    int shift = (srcIndx * sizeof(double));
                    System.Buffer.BlockCopy(EEGDataArr, shift, chDataDoubleArr, 0, firCoefsCnt * sizeof(double));

                    double filVal = fc.FIRFil(chDataDoubleArr, fc.FIRFiltersList[0]);

                    EEGDataFilArr[indx] = filVal;
                    indx++;
                }
                //обратный проход для сохранения фазы
/*                Array.Resize(ref fc.FirFil0, fc.FirFil0.Length);
                EEGDataFilArr2 = new double[EEGDataFilArr.Length - firCoefsCnt + 1];
                indx = 0;
                for (int i = EEGDataFilArr.Length - firCoefsCnt; i >= 0; i--)
                {
                    int srcIndx = i;

                    double[] chDataDoubleArr = new double[firCoefsCnt];
                    int shift = (srcIndx * sizeof(double));
                    System.Buffer.BlockCopy(EEGDataFilArr, shift, chDataDoubleArr, 0, firCoefsCnt * sizeof(double));

                    double filVal = fc.FIRFil(chDataDoubleArr, fc.FirFil0);

                    EEGDataFilArr2[indx] = filVal;
                    indx++;
                }
                System.Buffer.BlockCopy(EEGDataFilArr2, 0, EEGDataFilArr, 0, EEGDataFilArr2.Length * sizeof(double));*/

                //в офлайне можно совместить ряды по фазе, дополнив нулями фильтрованную последовательность
                int shft = (int)firCoefsCnt/2;
                double[] tmpData = new double[EEGDataFilArr.Length + shft];
                System.Buffer.BlockCopy(EEGDataFilArr, 0, tmpData, (shft) * sizeof(double), EEGDataFilArr.Length * sizeof(double));
                EEGDataFilArr = tmpData;

                EEGDataDraw(EEGDataFilArr);

                gv.CtrlLock = true;
                checkBox1.Checked = true;
                gv.CtrlLock = false;

                EEGDataFiltered = true;
            }
        }

        private void hScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            if (EEGDataArr != null)
            {
                CurPos = (sender as ScrollBar).Value;
                if (!checkBox1.Checked)
                {               
                    SrcIndx = Math.Min(EEGDataArr.Length - ViewBlockSize, Math.Max(0, CurPos));
                    EEGDataDraw(EEGDataArr);
                }
                else
                {
                    SrcIndx = Math.Min(EEGDataFilArr.Length - ViewBlockSize, Math.Max(0, CurPos));
                    EEGDataDraw(EEGDataFilArr);
                }
            }
        }

        private void btnEEGXLeftScale_Click(object sender, EventArgs e)
        {
            if (EEGDataArr != null)
            {
                ViewBlockSize = Math.Max(100, ViewBlockSize -= 100);
                if (!checkBox1.Checked)
                {
                    SrcIndx = Math.Min(EEGDataArr.Length - ViewBlockSize, Math.Max(0, CurPos));
                    EEGDataDraw(EEGDataArr);
                }
                    
                else
                {
                    if (checkBox1.Checked)
                    {
                        SrcIndx = Math.Min(EEGDataFilArr.Length - ViewBlockSize, Math.Max(0, CurPos));
                        EEGDataDraw(EEGDataFilArr);
                    }
                }
            }
        }

        private void btnEEGXRightScale_Click(object sender, EventArgs e)
        {
            if (EEGDataArr != null)
            {
                ViewBlockSize = Math.Min(EEGDataArr.Length, ViewBlockSize += 100);
                if (!checkBox1.Checked)
                {
                    SrcIndx = Math.Min(EEGDataArr.Length - ViewBlockSize, Math.Max(0, CurPos));
                    EEGDataDraw(EEGDataArr);
                }

                else
                {
                    if (checkBox1.Checked)
                    {
                        SrcIndx = Math.Min(EEGDataFilArr.Length - ViewBlockSize, Math.Max(0, CurPos));
                        EEGDataDraw(EEGDataFilArr);
                    }
                }
            }
        }

        private void btnEEGYUpScale_Click(object sender, EventArgs e)
        {
            if (EEGDataArr != null)
            {
                gv.EEGAmplCoef += 1;
                if (!checkBox1.Checked)
                    EEGDataDraw(EEGDataArr);
                else
                    EEGDataDraw(EEGDataFilArr);
            }
        }

        private void btnEEGYDwnScale_Click(object sender, EventArgs e)
        {
            if (EEGDataArr != null)
            {
                gv.EEGAmplCoef -= 1;
                if (!checkBox1.Checked)
                    EEGDataDraw(EEGDataArr);
                else
                    EEGDataDraw(EEGDataFilArr);
            }
        }

        private void btnERPYUpScale_Click(object sender, EventArgs e)
        {
            if (ERPepochAvrg != null)
            {
                gv.ERPAmplCoef += 0.5;
                if (!chckBxFIR.Checked)
                    ERPDataDraw(ERPepochAvrg);
                else
                    ERPDataDraw(ERPDataFilArr);
            }
        }

        private void btnERPYDwnScale_Click(object sender, EventArgs e)
        {
            if (ERPepochAvrg != null)
            {
                gv.ERPAmplCoef -= 0.5;
                if (!chckBxFIR.Checked)
                    ERPDataDraw(ERPepochAvrg);
                else
                    ERPDataDraw(ERPDataFilArr);
            }
        }

        private void btnGetFilCoefs_Click(object sender, EventArgs e)
        {
            if (fc != null && fc.FIRFiltersList[0] != null)
            {
                rTxtBxIn.Clear();
                for (int i = 0; i < fc.FIRFiltersList[0].Length; i++)
                {
                    rTxtBxIn.AppendText(fc.FIRFiltersList[0][i].ToString() + Environment.NewLine);
                }
                string[] resultStr = rTxtBxIn.Lines.Where((x, y) => y != rTxtBxIn.Lines.Length - 1).ToArray();//удалить последнюю пустую строку 
                rTxtBxIn.Lines = resultStr;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (!gv.CtrlLock)
            {
                if (checkBox1.Checked)
                    EEGDataDraw(EEGDataFilArr);
                else
                    EEGDataDraw(EEGDataArr);
            }
        }

        private double[] EpochDataFirFil(double[] epoch, double[] firFilCoefs)
        {
            int firCoefsCnt = firFilCoefs.Length;
            double[] epochDataFilArr = new double[epoch.Length - firCoefsCnt + 1];

            int indx = 0;
            for (int i = 0; i <= epoch.Length - firCoefsCnt; i++)
            {
                int srcIndx = Math.Min(epoch.Length - firCoefsCnt, Math.Max(0, i));

                double[] chDataDoubleArr = new double[firCoefsCnt];
                int shift = (srcIndx * sizeof(double));
                System.Buffer.BlockCopy(epoch, shift, chDataDoubleArr, 0, firCoefsCnt * sizeof(double));

                double filVal = fc.FIRFil(chDataDoubleArr, firFilCoefs);

                epochDataFilArr[indx] = filVal;
                indx++;
            }
            //в оффлайне можно совместить ряды по фазе, дополнив нулями фильтрованную последовательность
            int shft = (int)firCoefsCnt / 2;
            double[] tmpData = new double[epoch.Length];
            System.Buffer.BlockCopy(epochDataFilArr, 0, tmpData, (shft) * sizeof(double), epochDataFilArr.Length * sizeof(double));
            epochDataFilArr = tmpData;

            return epochDataFilArr;
        }

        private void btnEpochFil_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow.Index != -1)
            {
                double[] epoch = EpochsList[dataGridView1.CurrentRow.Index];

                EpochDataFilArr = EpochDataFirFil(epoch, fc.FIRFiltersList[0]);

                gv.CtrlLock = true;
                checkBox2.Checked = true;
                gv.CtrlLock = false;

                fc.HannDoubles = MathNet.Numerics.Window.Hamming(EpochDataFilArr.Length);
                for (int i = 0; i < EpochDataFilArr.Length; i++)
                {
                 //   EpochDataFilArr[i] = fc.HannDoubles[i] * EpochDataFilArr[i];
                }

                EpochDataDraw(EpochDataFilArr);
            }
        }

        private void btnEpochYUpScale_Click(object sender, EventArgs e)
        {
            if (EpochDataArr != null)
            {
                gv.EpochAmplCoef += 0.5;
                if (!checkBox2.Checked)
                    EpochDataDraw(EpochDataArr);
                else
                    EpochDataDraw(EpochDataFilArr);
            }
        }

        private void btnEpochYDwnScale_Click(object sender, EventArgs e)
        {
            if (EpochDataArr != null)
            {
                gv.EpochAmplCoef -= 0.5;
                if (!checkBox2.Checked)
                    EpochDataDraw(EpochDataArr);
                else
                    EpochDataDraw(EpochDataFilArr);
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (!gv.CtrlLock)
            {
                if (checkBox2.Checked)
                    EpochDataDraw(EpochDataFilArr);
                else
                    EpochDataDraw(EpochDataArr);
            }
        }
        private double[] ExtractERP(double[] epoch)
        {
            for (int i = 0; i < fc.FIRFiltersList.Count; i++)
            {
                double freq = i + 1;
                double fs = 200;
                double betta = freq * Math.PI * 2 * gv.NMS / fs;

                double[] mas_cos = new double[epoch.Length];
                double[] mas_sin = new double[epoch.Length];

                //1.Модуляция исходной эпохи с кандидатной реализацией ВП
                fc.modulation(betta, ref epoch, mas_cos, mas_sin);

                //System.Buffer.BlockCopy(mas_sin, 0, ResultEpoch, 0, ResultEpoch.Length * sizeof(double));
                //DrawResult(ResultEpoch);
                //return;

                //2.Узкополосная fir фильтрация
                double[] epochFilcos = EpochDataFirFil(mas_cos, fc.FIRFiltersList[i]);
                double[] epochFilsin = EpochDataFirFil(mas_sin, fc.FIRFiltersList[i]);

/*                double[] epochFilcos = new double[mas_cos.Length];
                double[] epochFilsin = new double[mas_sin.Length];
                System.Buffer.BlockCopy(mas_cos, 0, epochFilcos, 0, mas_cos.Length * sizeof(double));
                System.Buffer.BlockCopy(mas_sin, 0, epochFilsin, 0, mas_sin.Length * sizeof(double));*/

                /*                    for (int k = 0; k < epochFilcos.Length; k++)
                                    {
                                        epochFilcos[k] = (epochFilcos[k] * 2);
                                        epochFilsin[k] = (epochFilsin[k] * 2);
                                    }*/

                /*                    System.Buffer.BlockCopy(epochFilsin, 0, ResultEpoch, 0, ResultEpoch.Length * sizeof(double));
                                    DrawResult(ResultEpoch);
                                    return;*/

                fc.ini_Gram(gv.ORDER);

                //3.интерполяция полиномом центрального участка с кандидатной реализацией ВП
                //отдельно для косинусных последовательностей
                double[] buf1 = new double[epochFilcos.Length];
                fc.Gram(epochFilcos, buf1);//вход; результат
                double[] epochFilGramCos = new double[buf1.Length];
                System.Buffer.BlockCopy(buf1, 0, epochFilGramCos, (0) * sizeof(double), buf1.Length * sizeof(double));
                //отдельно для синусных последовательностей
                double[] buf2 = new double[epochFilsin.Length];
                fc.Gram(epochFilsin, buf2);//вход; результат
                double[] epochFilGramSin = new double[buf2.Length];
                System.Buffer.BlockCopy(buf2, 0, epochFilGramSin, (0) * sizeof(double), buf2.Length * sizeof(double));

                /*                    System.Buffer.BlockCopy(epochFilGramSin, 0, ResultEpoch, 0, ResultEpoch.Length * sizeof(double));
                                    DrawResult(ResultEpoch);
                                    return;*/

                //4.вычитаем результат интерполяции из исходной последовательности
                double[] ResSubtrctnCos = new double[epoch.Length];
                double[] ResSubtrctnSin = new double[epoch.Length];
                fc.subtracking(ResSubtrctnCos, epochFilcos, epochFilGramCos);//результат; от чего вычитаем; что вычитаем
                fc.subtracking(ResSubtrctnSin, epochFilsin, epochFilGramSin);

                /*                    System.Buffer.BlockCopy(ResSubtrctnCos, 0, ResultEpoch, 0, ResultEpoch.Length * sizeof(double));
                                    DrawResult(ResultEpoch);
                                    return;*/

                //5.демодулируем разности
                double[] ResDemodultn = new double[epoch.Length];
                fc.demodulation(betta, ResDemodultn, ResSubtrctnCos, ResSubtrctnSin);

                /*                    System.Buffer.BlockCopy(ResDemodultn, 0, ResultEpoch, 0, ResultEpoch.Length * sizeof(double));
                                    DrawResult(ResultEpoch);
                                    return;*/

                //6.суммируем результаты по частотным полосам
                fc.summing(ResultEpoch, ResultEpoch, ResDemodultn);//

            }

            for (int k = 0; k < ResultEpoch.Length; k++)
            {
                ResultEpoch[k] = (ResultEpoch[k] * 0.23);//масштабный коэф-т. Подобран на глаз
            }
            return ResultEpoch;
        }
        public void CalcRMSs(double[] epoch, out double rmsRes3, out double rmsResERP)
        {
            bool res = false;
            int t1 = 0;
            int t2 = gv.FIRST_EP;
            int t3 = gv.LAST_EP;
            int t4 = 0;
            for (int i = 0; i < epoch.Length && !res; i++)
            {
                if (Math.Abs(epoch[i]) > 0.05)
                {
                    t1 = i;
                    res = true;
                }
            }
            res = false;
            for (int i = epoch.Length - 1; i >= 0 && !res; i--)
            {
                if (Math.Abs(epoch[i]) > 0.05)
                {
                    t4 = i;
                    res = true;
                }
            }
            double sum = 0;
            double div = t2 - t1 + 1;
            for (int i = t1; i < t2; i++)
                sum += Math.Pow(epoch[i], 2);
            double rmsRes1 = 0;
            if (div > 0)
                rmsRes1 = Math.Sqrt(sum / div);

            sum = 0;
            div = t4 - t3 + 1;
            for (int i = t3; i < t4; i++)
                sum += Math.Pow(epoch[i], 2);
            double rmsRes2 = 0;
            if (div > 0)
                rmsRes2 = Math.Sqrt(sum / div);

            sum = 0;
            div = t3 - t2 + 1;
            for (int i = t2; i < t3; i++)
                sum += Math.Pow(epoch[i], 2);
            rmsResERP = 0;
            if (div > 0)
                rmsResERP = Math.Sqrt(sum / div);

            rmsRes3 = 0;
            rmsRes3 = rmsRes1;//(rmsRes1 + rmsRes2) / 2;//на усреднённом ВП есть что-то и после 900 мс, поэтому правильнее брать интервал только до ВП
        }
        private void btnCalc_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow.Index != -1)
            {                
                double[] epoch = EpochsList[dataGridView1.CurrentRow.Index];
                ResultEpoch = new double[epoch.Length];

                ResultEpoch = ExtractERP(epoch);

                DrawResult(ResultEpoch);

                double rmsGnd, rmsErp = 0;
                CalcRMSs(ResultEpoch, out rmsGnd, out rmsErp);

                txtBxGndRMS.Text = rmsGnd.ToString();
                txtBxErpRMS.Text = rmsErp.ToString();
                txtBxDiffRMS.Text = (rmsErp - rmsGnd).ToString();
            }
        }

        private void UpDwnFirstERP_ValueChanged(object sender, EventArgs e)
        {
            gv.FIRST_EP = (int)UpDwnFirstERP.Value;
        }

        private void UpDwnLastERP_ValueChanged(object sender, EventArgs e)
        {
            gv.LAST_EP = (int)UpDwnLastERP.Value;
        }

        private void btnRsltYUpScale_Click(object sender, EventArgs e)
        {
            if (ResultEpoch != null)
            {
                gv.RsltEpochAmplCoef += 0.5;
                DrawResult(ResultEpoch);
            }
        }

        private void btnRsltYDwnScale_Click(object sender, EventArgs e)
        {
            if (ResultEpoch != null)
            {
                gv.RsltEpochAmplCoef -= 0.5;
                DrawResult(ResultEpoch);
            }
        }

        private void btnERPFIRFil_Click(object sender, EventArgs e)
        {
            if (ERPepochAvrg != null)
            {
                ERPDataFilArr = EpochDataFirFil(ERPepochAvrg, fc.FIRFiltersList[0]);

                ERPDataDraw(ERPDataFilArr);

                gv.CtrlLock = true;
                chckBxFIR.Checked = true;
                gv.CtrlLock = false;

            }
        }

        private void chckBxFIR_CheckedChanged(object sender, EventArgs e)
        {
            if (!gv.CtrlLock)
            {
                if ((sender as CheckBox).Checked)
                    ERPDataDraw(ERPDataFilArr);
                else
                    ERPDataDraw(ERPepochAvrg);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (ERPDataFilArr != null)
            {
                double[] epochDataScaled = new double[ERPDataFilArr.Length];
                for (int k = 0; k < epochDataScaled.Length; k++)
                    epochDataScaled[k] = (ERPDataFilArr[k] * gv.ERPAmplCoef);

                TemplateForLSM = new double[gv.LAST_EP - gv.FIRST_EP];
                System.Buffer.BlockCopy(epochDataScaled, gv.FIRST_EP * sizeof(double), TemplateForLSM, (0) * sizeof(double), TemplateForLSM.Length * sizeof(double));

/*                TemplateSubEpochForLSM = new double[gv.LAST_EP - gv.FIRST_EP];
                System.Buffer.BlockCopy(ResultEpoch, gv.FIRST_EP * sizeof(double), TemplateSubEpochForLSM, (0) * sizeof(double), TemplateSubEpochForLSM.Length * sizeof(double));

                double[] resltLSM = new double[TemplateForLSM.Length];
                double dblVal = 0;
                for (int i=0; i < TemplateForLSM.Length; i++)
                {
                    dblVal += Math.Pow(TemplateForLSM[i] - TemplateSubEpochForLSM[i], 2);                  
                }
                txtBxLSMVal.Text = dblVal.ToString();*/
            }
        }

        private void dataGridView1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                this.Text = "fgdf";
                double.TryParse(txtBxDiffRMS.Text, out double dblVal);
                DataDiffRMSList.Add(dblVal);
            }
               
        }

        private void btnGetDataDiffRMS_Click(object sender, EventArgs e)
        {
            rTxtBxIn.Clear();
            for (int i = 0; i < DataDiffRMSList.Count; i++)
            {
                rTxtBxIn.AppendText(DataDiffRMSList[i].ToString() + Environment.NewLine);
            }
            string[] resultStr = rTxtBxIn.Lines.Where((x, y) => y != rTxtBxIn.Lines.Length - 1).ToArray();//удалить последнюю пустую строку 
            rTxtBxIn.Lines = resultStr;
        }

        private void btnGetTmpltForLSM_Click(object sender, EventArgs e)
        {
            if (TemplateForLSM != null)
            {
                rTxtBxIn.Clear();
                for (int i = 0; i < TemplateForLSM.Length; i++)
                {
                    rTxtBxIn.AppendText(TemplateForLSM[i].ToString() + Environment.NewLine);
                }
                string[] resultStr = rTxtBxIn.Lines.Where((x, y) => y != rTxtBxIn.Lines.Length - 1).ToArray();//удалить последнюю пустую строку 
                rTxtBxIn.Lines = resultStr;
            }
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
/*                int rindx = e.RowIndex;

                double[] epDataArr = EpochsList[rindx];

                int indx1 = epDataArr[0];

                hScrollBar1.Value = rindx;
                hScrollBar1_Scroll(hScrollBar1, null);*/
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            EpochStruct eps = new EpochStruct();

            double[] eegSubdataArr = new double[1000];

            eps.epoch = eegSubdataArr.Clone() as double[];
            eps.index1 = 100;

            TestList.Add(eps);

            eegSubdataArr[0] = 10;

            EpochStruct eps2 = TestList[0];

            hScrollBar1.Value = eps2.index1;
            hScrollBar1_Scroll(hScrollBar1, null);
        }
    }
}
