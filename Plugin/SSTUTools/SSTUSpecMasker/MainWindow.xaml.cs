using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.IO;

namespace SSTUSpecMasker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private string baseFile;
        private string alphaFile;
        private string outputFile;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BaseButton_Click(object sender, RoutedEventArgs e)
        {
            string file = openFileDialog();
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                BaseLabel.Content = "Invalid File";
                return;
            }
            BaseLabel.Content = file;
        }

        private void AlphaButton_Click(object sender, RoutedEventArgs e)
        {
            string file = openFileDialog();
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                AlphaLabel.Content = "Invalid File";
                return;
            }
            AlphaLabel.Content = file;
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            baseFile = (string)BaseLabel.Content;            
            alphaFile = (string)AlphaLabel.Content;
            if (File.Exists(baseFile) && File.Exists(alphaFile))
            {
                outputFile = openSaveDialog();
                if (string.IsNullOrEmpty(outputFile)) { return; }
                if (File.Exists(outputFile)) { File.Delete(outputFile); }
                BackgroundWorker worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                worker.DoWork += applyAlphaMask;
                worker.ProgressChanged += progressChanged;
                worker.RunWorkerAsync();
            }
        }

        private string openFileDialog()
        {
            string path = "";

            OpenFileDialog dlg = new OpenFileDialog();
            //dlg.InitialDirectory = "/";
            dlg.DefaultExt = ".png";
            dlg.Filter = "PNG Image Files (*.png)|*.png";

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                path = dlg.FileName;
            }
            return path;
        }
        
        private string openSaveDialog()
        {
            string path = "";

            SaveFileDialog dlg = new SaveFileDialog();
            //dlg.InitialDirectory = "/";
            dlg.DefaultExt = ".png";
            dlg.Filter = "PNG Image Files (*.png)|*.png";

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                path = dlg.FileName;
            }
            return path;
        }

        private void applyAlphaMask(object sender, DoWorkEventArgs e)
        {
            Bitmap baseImage = new Bitmap(Image.FromFile(baseFile));
            Bitmap alphaImage = new Bitmap(Image.FromFile(alphaFile));

            int w = baseImage.Width;
            int h = baseImage.Height;

            int totalPixels = w * h;
            int currentPixels = 0;

            float progressDecimalPercent;

            Color baseColor;
            Color alphaColor;
            Color outColor;
            int a, r, g, b;

            int prevProg = 0;

            Bitmap outputImage = new Bitmap(baseImage.Width, baseImage.Height, PixelFormat.Format32bppArgb);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++, currentPixels++)
                {
                    progressDecimalPercent = (float)currentPixels / (float)totalPixels;
                    int progressInteger = (int)(progressDecimalPercent * 100f);
                    if (progressInteger > prevProg)
                    {
                        prevProg = progressInteger;
                        ((BackgroundWorker)sender).ReportProgress(progressInteger);
                    }
                    baseColor = baseImage.GetPixel(x, y);
                    r = baseColor.R;
                    g = baseColor.G;
                    b = baseColor.B;
                    alphaColor = alphaImage.GetPixel(x, y);
                    a = (alphaColor.R + alphaColor.G + alphaColor.B) / 3;
                    outColor = Color.FromArgb(a, r, g, b);
                    outputImage.SetPixel(x, y, outColor);                    
                }
            }
            ((BackgroundWorker)sender).ReportProgress(100);
            outputImage.Save(outputFile);
        }

        private void progressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar.Value = e.ProgressPercentage;
        }

    }
}
