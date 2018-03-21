using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
            string baseFile = (string)BaseLabel.Content;
            
            string alphaFile = (string)AlphaLabel.Content;
            if (File.Exists(baseFile) && File.Exists(alphaFile))
            {
                applyAlphaMask(baseFile, alphaFile);
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

        private void applyAlphaMask(string baseFile, string alphaFile)
        {
            Bitmap baseImage = new Bitmap(Image.FromFile(baseFile));
            Bitmap alphaImage = new Bitmap(Image.FromFile(alphaFile));

            int w = baseImage.Width;
            int h = baseImage.Height;

            Color baseColor;
            Color alphaColor;
            Color outColor;
            int a, r, g, b;

            Bitmap outputImage = new Bitmap(baseImage.Width, baseImage.Height, PixelFormat.Format32bppArgb);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
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

            string outputFile = openSaveDialog();
            if (File.Exists(outputFile)) { File.Delete(outputFile); }
            outputImage.Save(outputFile);
        }

    }
}
