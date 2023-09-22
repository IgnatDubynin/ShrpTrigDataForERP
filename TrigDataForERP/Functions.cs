using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TrigDataForERP
{
    class Functions
    {
        public List<double[]> FIRFiltersList = new List<double[]>();
        public double[] HannDoubles;

        public double s1, s2, c1, c2, kb;

        public int M;
        public List<double[]> orto = new List<double[]>();
        public int nep, nd2;
        public double[] xx;

        public Functions()
        {
            string localPath = @"d:\SINEP\Data\FIR_Filters\";

            int filCnt = 20;

            //надо разобраться с полосами фильтров! Сейчас модуляция работает не так как описано в радиотехнике. Происходит перевод отрицательных значений в положительные,
            //за счёт чего модуляция происходит с частотой 2х от модулируемой. Вроде так, если не путаю. Поэтому надо с каждой новой полосой расширять правую полосу на 2х
            string pathToFilCoefsDataFileName_ = Path.Combine(localPath, "bp1_5_2_5_200.fcf");//!!
            double[] firFilCoefs_ = LoadFIRFilCoefs(pathToFilCoefsDataFileName_);
            FIRFiltersList.Add(firFilCoefs_.Clone() as double[]);

            pathToFilCoefsDataFileName_ = Path.Combine(localPath, "bp1_5_4_5_200.fcf");
            firFilCoefs_ = LoadFIRFilCoefs(pathToFilCoefsDataFileName_);
            FIRFiltersList.Add(firFilCoefs_.Clone() as double[]);

            pathToFilCoefsDataFileName_ = Path.Combine(localPath, "bp2_5_6_5_200.fcf");
            firFilCoefs_ = LoadFIRFilCoefs(pathToFilCoefsDataFileName_);
            FIRFiltersList.Add(firFilCoefs_.Clone() as double[]);

            pathToFilCoefsDataFileName_ = Path.Combine(localPath, "bp3_5_8_5_200.fcf");
            firFilCoefs_ = LoadFIRFilCoefs(pathToFilCoefsDataFileName_);
            FIRFiltersList.Add(firFilCoefs_.Clone() as double[]);

            pathToFilCoefsDataFileName_ = Path.Combine(localPath, "bp4_5_10_5_200.fcf");
            firFilCoefs_ = LoadFIRFilCoefs(pathToFilCoefsDataFileName_);
            FIRFiltersList.Add(firFilCoefs_.Clone() as double[]);

            pathToFilCoefsDataFileName_ = Path.Combine(localPath, "bp5_5_12_5_200.fcf");
            firFilCoefs_ = LoadFIRFilCoefs(pathToFilCoefsDataFileName_);
            FIRFiltersList.Add(firFilCoefs_.Clone() as double[]);

            pathToFilCoefsDataFileName_ = Path.Combine(localPath, "bp6_5_14_5_200.fcf");
            firFilCoefs_ = LoadFIRFilCoefs(pathToFilCoefsDataFileName_);
            FIRFiltersList.Add(firFilCoefs_.Clone() as double[]);

            pathToFilCoefsDataFileName_ = Path.Combine(localPath, "bp7_5_16_5_200.fcf");
            firFilCoefs_ = LoadFIRFilCoefs(pathToFilCoefsDataFileName_);
            FIRFiltersList.Add(firFilCoefs_.Clone() as double[]);

            for (int i=8; i<filCnt; i++)
            {
                string pathToFilCoefsDataFileName = Path.Combine(localPath, "bp" + i.ToString() + "_5_" + (i+1).ToString() + "_5_200.fcf");
                double[] firFilCoefs = LoadFIRFilCoefs(pathToFilCoefsDataFileName);
                FIRFiltersList.Add(firFilCoefs.Clone() as double[]);
            }
        }

        public double[] LoadFIRFilCoefs(string filename)
        {
            string sLine = "";

            using (StreamReader sReader = new StreamReader(filename))
            {
                int filLength = 0;
                while (sLine.Contains("Filter Length") == false)
                    sLine = sReader.ReadLine();

                string[] bits = sLine.Split(' ');
                int indx0 = 0;
                for (int i = 0; i < bits.Length && filLength == 0; i++)
                {
                    if (bits[i] != "")
                    {
                        int.TryParse(bits[i], out filLength);
                        indx0++;
                    }
                }

                while (sLine.Contains("Numerator:") == false)
                    sLine = sReader.ReadLine();

                double[] firFil0 = new double[filLength];
                int indx = 0;
                while (!sReader.EndOfStream)
                {
                    sLine = sReader.ReadLine();

                    double dblVal = 0;
                    double.TryParse(sLine, out dblVal);
                    if (dblVal != 0)
                    {
                        firFil0[indx] = dblVal;
                        indx++;
                    }
                }
                return firFil0;
            }
        }
        public void subtracking(double[] sum, double[] mas1, double[] mas2)
        {
            int i, j, ind = 0;
            for (i = 0; i < gv.NPOINTS; i++)
            {
                for (j = 0; j < gv.NCAN; j++)
                {
                    sum[ind] = mas1[ind] - mas2[ind];
                    ind++;
                }
            }
        }
        public void summing(double[] sum, double[] mas1, double[] mas2)
        {
            int i, j, ind = 0;
            for (i = 0; i < gv.NPOINTS; i++)
            {
                for (j = 0; j < gv.NCAN; j++)
                {
                    sum[ind] = mas1[ind] + mas2[ind];
                    ind++;
                }
            }
        }
        public void ini_oscillator(double betta)
        {
            s1 = -Math.Sin(betta);
            s2 = -Math.Sin(betta * 2);
            c1 = Math.Cos(betta);
            c2 = Math.Cos(betta * 2);
            kb = c1 * 2;
        }
        public double SIN_()
        {
            double s0 = s1 * kb - s2;
            s2 = s1;
            s1 = s0;
            return s0;
        }
        public double COS_()
        {
            double c0 = c1 * kb - c2;
            c2 = c1;
            c1 = c0;
            return c0;
        }
        public void modulation(double betta, ref double[] mas, double[] mas_cos, double[] mas_sin)
        {
            int ind = 0;
            double sn, cn;
            ini_oscillator(betta);
            for (int i = 0; i < gv.NPOINTS; i++)
            {
                sn = SIN_(); cn = COS_();
                for (int j = 0; j < gv.NCAN; j++)
                {
                    mas_cos[ind] = mas[ind] * cn; 
                    mas_sin[ind] = mas[ind] * sn;
                    ind++;
                }
            }
        }
        public void demodulation(double betta, double[] mas, double[] mas_cos, double[] mas_sin)
        {
            int i, j, ind = 0;
            double sn, cn;
            ini_oscillator(betta);
            for (i = 0; i < gv.NPOINTS; i++)
            {
                sn = SIN_(); cn = COS_();
                for (j = 0; j < gv.NCAN; j++)
                {
                    mas[ind] = mas_cos[ind] * cn + mas_sin[ind] * sn;
                    ind++;
                }
            }
        }
        public void ini_Gram(int m_)
        {
            int i, j, k;
            double r;
            double[] coeff = new double[gv.N21];
            double[] rv;
            double rr;

            M = m_;
            //* созАаАим массив аргументов */
            nep = gv.LAST_EP - gv.FIRST_EP + 1;
            nd2 = gv.NPOINTS - gv.LAST_EP - 1;
            xx = new double[gv.NPOINTS];
            rr = 2.0 / gv.NPOINTS;
            r = -1.0;
            for (i = 0; i < gv.NPOINTS; i++)
            {
                xx[i] = r;
                r += rr;
            }
            for (i = 0; i <= M; i++)
            {
                orto.Add(new double[gv.NPOINTS]);
            }
            //* многочлен нулевой степени */
            r = Math.Sqrt(1.0 / (nd2 + gv.FIRST_EP));
            for (i = 0; i < gv.NPOINTS; i++)
            {
                orto[0][i] = r;
            }
            //* вычисление многочленов старших поряАков */
            rv = new double[gv.NPOINTS];
            for (k = 1; k <= M; k++)
            {
                for (i = 0; i < gv.NPOINTS; i++)
                {
                    rv[i] = Math.Pow(xx[i], k);
                }
                for (j = 0; j < k; j++)
                {
                    rr = 0;
                    for (i = 0; i < gv.FIRST_EP; i++)
                        rr += rv[i] * orto[j][i];

                    for (i = gv.LAST_EP + 1; i < gv.NPOINTS; i++)
                        rr += rv[i] * orto[j][i];

                    coeff[j] = rr;
                }
                for (j = 0; j < k; j++)
                {
                    for (i = 0; i < gv.NPOINTS; i++)
                    {
                        rv[i] -= coeff[j] * orto[j][i];
                    }
                }
                rr = 0;
                for (i = 0; i < gv.FIRST_EP; i++)
                    rr += rv[i] * rv[i];

                for (i = gv.LAST_EP + 1; i < gv.NPOINTS; i++)
                    rr += rv[i] * rv[i];

                rr = Math.Sqrt(rr);
                for (i = 0; i < gv.NPOINTS; i++)
                {
                    orto[k][i] = rv[i] / rr;
                }
            }
        }

        public void Gram(double[] inp, double[] Out_)
        {
            int i, k = 0;
            double r, rr;
            double[] coeff = new double[gv.N21];

            for (k = 0; k <= M; k++)
            {
                r = 0;
                for (i = 0; i < gv.FIRST_EP; i++)
                {
                    r += inp[i] * orto[k][i];
                }
                for (i = gv.LAST_EP + 1; i < gv.NPOINTS; i++)
                {
                    r += inp[i] * orto[k][i];
                }
                coeff[k] = r;
            }
            for (i = 0; i < gv.NPOINTS; i++)
            {
                Out_[i] = 0;
                for (k = 0; k <= M; k++)
                {
                    Out_[i] = Out_[i] + coeff[k] * orto[k][i];
                }
            }
            rr = 1.0 / (double)gv.FIRST_EP;
            r = 1.0;
            for (i = 0; i < gv.FIRST_EP; i++)
            {
                Out_[i] = inp[i] * r + Out_[i] * (1.0 - r);
                r -= rr;
            }
            k = gv.NPOINTS - gv.LAST_EP - 1;
            if (k <= 0)
                rr = 1.0;
            else
                rr = 1.0 / k;
            r = 0;
            for (i = gv.LAST_EP + 1; i < gv.NPOINTS; i++)
            {
                Out_[i] = inp[i] * r + Out_[i] * (1.0 - r);
                r += rr;
            }
        }

        public double[] Sine(int n)
        {
            const int FS = 200; // частота дискретизации

            return MathNet.Numerics.Generate.Sinusoidal(n, FS, 10, 10.0);
        }
        //Метод преобразующий массив double в complex
        public Complex[] ConvertToComplex(Double[] data)
        {
            Complex[] signal = new Complex[data.Length];

            for (var i = 0; i < data.Length; i++)
            {
                var value = data[i];
                Complex c1 = new Complex(value, 0);
                signal[i] = c1;
            }
            return signal;
        }
        public double FIRFil(double[] data, double[] coefs)
        {
            double filDataVal = 0;
            for (int i = 0; i< data.Length; i++)
                filDataVal += data[i] * coefs[i];
            return filDataVal;
        }
    }
}
