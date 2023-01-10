using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;

namespace EdgeDetection
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        byte strong = 255;
        byte weak = 25;
        public MainWindow()
        {
            InitializeComponent();
            LoadImage.Click += LoadImage_Click;

        }

        private async void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".*";
            dlg.Filter = "All Files|*.*|JPEG Files (*.jpeg)|*.jpeg|PNG Files (*.png)|*.png|JPG Files (*.jpg)|*.jpg|GIF Files (*.gif)|*.gif|WEBP Files (*.webp)|*.webp";
            bool? result = dlg.ShowDialog();
            // Get the selected file name and display in a TextBox 
            if (!result.HasValue || !result.Value)
                return;
            var path = dlg.FileName;
            if (File.Exists(path))
            {
                    var bitmap = new BitmapImage(new Uri(path, UriKind.Absolute));

                    var matrix = ImageToMatrix(bitmap);
                    var sobel = MatrixToImage(ProcessBitmapAsSobel(matrix, bitmap.PixelWidth, bitmap.PixelHeight).bitmap, bitmap);
                    var prewit = MatrixToImage(ProcessBitmapAsPrewitt(matrix, bitmap.PixelWidth, bitmap.PixelHeight), bitmap);
                    var laplacian = MatrixToImage(ProcessBitmapAsLaplacian(matrix, bitmap.PixelWidth, bitmap.PixelHeight), bitmap);
                    var canny = MatrixToImage(ProcessBitmapAsCanny(matrix, bitmap.PixelWidth, bitmap.PixelHeight,bitmap), bitmap);

                InputImage.Source = bitmap;
                Sobel.Source = sobel;
                Prewitt.Source = prewit;
                Laplacian.Source = laplacian;
                CannyEdgeTracking.Source = canny;

            }
            else
            {
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Error;
                MessageBoxResult res = MessageBox.Show("File does not exist", "Error", button, icon, MessageBoxResult.Yes);
            }
        }

        private int[,] ProcessBitmapAsCanny(int[,] bitmap, int width, int height,BitmapImage org)
        {
            var grayscale = Grayscale(bitmap, width, height);
            var blured = Blur(grayscale, width, height);
            CannyBlurred.Source = MatrixToImage(blured, org);
            var sobeled = ProcessBitmapAsSobel(blured, width, height);
            CannySobbel.Source = MatrixToImage(sobeled.bitmap, org);
            var suppresed = NonMaxSuppression(sobeled.bitmap, sobeled.theta, width, height);
            CannyNonMax.Source = MatrixToImage(suppresed, org);
            var thresholded = DoubleTresholding(suppresed, width, height);
            CannyTresholding.Source = MatrixToImage(thresholded, org);
            var edgeTracked = EdgeTracking(thresholded, width, height);
            return edgeTracked;
        }

        private int[,] ProcessBitmapAsLaplacian(int[,] bitmap, int width, int height)
        {
            var grayscale = Grayscale(bitmap, width, height);
            var blured = Blur(grayscale, width, height);
            double[,] kernel = new double[,]
       { { -1, -1, -1, },
         { -1,  8, -1, },
         { -1, -1, -1, }, };

            var laplacian = ConvolutionFilter(blured,kernel,width,height);
            return laplacian;
        }

        private int[,] Grayscale(int[,] bitmap, int width, int height)
        {
            int[,] greyscaled = new int[height, width];
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    byte v = (byte)(0.299 * GetChannelByte(bitmap[i, j], 0) +
                    0.587 * GetChannelByte(bitmap[i, j], 1) +
                    0.114 * GetChannelByte(bitmap[i, j], 2));
                    greyscaled[i, j] = BitConverter.ToInt32(new byte[] { v, v, v, 0xff });
                }
            }
            return greyscaled;
        }

        private int[,] EdgeTracking(int[,] img, int width, int height)
        {
            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {
                    if (GetChannelByte(img[i, j], 0) == weak)
                    {
                        if (
                            GetChannelByte(img[i + 1, j - 1], 0) == strong || GetChannelByte(img[i + 1, j], 0) == strong || GetChannelByte(img[i + 1, j + 1], 0) == strong
                            || GetChannelByte(img[i, j - 1], 0) == strong || GetChannelByte(img[i, j], 0) == strong || GetChannelByte(img[i, j + 1], 0) == strong
                            || GetChannelByte(img[i - 1, j - 1], 0) == strong || GetChannelByte(img[i - 1, j], 0) == strong || GetChannelByte(img[i - 1, j + 1], 0) == strong
                            )
                        {
                            img[i, j] = BitConverter.ToInt32(new byte[] { strong, strong, strong, 0xff });
                        }
                        else
                        {

                            img[i, j] = BitConverter.ToInt32(new byte[] { 0, 0, 0, 0xff });
                        }
                    }
                }
            }
            return img;
        }

        private int[,] DoubleTresholding(int[,] suppresed, int width, int height)
        {
            var thresholded = new int[height, width];
            var max = 0;
            for (var i = 0; i < height; i++)
            {
                for (var j = 0; j < width; j++)
                {
                    max = Math.Max(max, GetChannelByte(suppresed[i, j], 0));
                }
            }
            var highThreshold = max * 0.25;
            var lowThreshold = max * 0.05;
            for (var i = 0; i < height; i++)
            {
                for (var j = 0; j < width; j++)
                {
                    byte value = (byte)GetChannelByte(suppresed[i, j], 0);
                    value = (byte)(value > highThreshold ? strong : (value > lowThreshold ? weak : 0));
                    thresholded[i, j] = BitConverter.ToInt32(new byte[] { value, value, value, 0xff });
                }
            }
            return thresholded;
        }

        private int[,] NonMaxSuppression(int[,] img, double[,] theta, int width, int height)
        {
            var modified = new int[height, width];
            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {

                    var angle = theta[i, j] * 180.00 / Math.PI;
                    int q = 255;
                    var r = 255;
                    if ((angle >= 0 && angle < 22.5) || (angle >= 157.5 && angle <= 180))
                    {
                        q = GetChannelByte(img[i, j + 1], 0);
                        r = GetChannelByte(img[i, j - 1], 0);
                    }
                    else if ((angle >= 22.5 && angle < 67.5))
                    {
                        q = GetChannelByte(img[i + 1, j - 1], 0);
                        r = GetChannelByte(img[i - 1, j + 1], 0);
                    }
                    else if ((angle >= 67.5 && angle < 112.5))
                    {
                        q = GetChannelByte(img[i + 1, j], 0);
                        r = GetChannelByte(img[i - 1, j], 0);
                    }
                    else if ((angle >= 112.5 && angle < 157.5))
                    {
                        q = GetChannelByte(img[i - 1, j - 1], 0);
                        r = GetChannelByte(img[i + 1, j + 1], 0);
                    }
                    if (GetChannelByte(img[i, j], 0) >= q && GetChannelByte(img[i, j], 0) >= r)
                        modified[i, j] = img[i, j];
                    else
                        modified[i, j] = BitConverter.ToInt32(new byte[] { 0x00, 0x00, 0x00, 0xff });

                }
            }
            return modified;
        }

        private int[,] ProcessBitmapAsPrewitt(int[,] bitmap, int width, int height)
        {
            int[,] hMask = new int[,] {
            { 1, 0, -1 },
            { 1, 0, -1 },
            { 1, 0, -1 }
            };

            int[,] vMask = new int[,] {
            { 1, 1, 1 },
            { 0, 0, 0 },
            { -1, -1, -1 }
            };
            return ApplyMasksOnBitmap(vMask, hMask, bitmap, width, height).bitmap;
        }

        private (int[,] bitmap, double[,] theta) ProcessBitmapAsSobel(int[,] bitmap, int width, int height)
        {
            //D:\Poze\2022\20-24 02 Feb Genova\Social\BMC_7110.jpg
            int[,] hMask = new int[,] {
            { 1, 0 ,-1 },
            { 2, 0 ,-2 },
            { 1, 0 ,-1 }
            };
            int[,] vMask = new int[,] {
             { 1, 2 ,1 },
             { 0, 0 ,0 },
             { -1, -2 ,-1 }
            };
            return ApplyMasksOnBitmap(vMask, hMask, bitmap, width, height);
        }

        private (int[,] bitmap, double[,] theta) ApplyMasksOnBitmap(int[,] vMask, int[,] hMask, int[,] bitmap, int width, int height)
        {
            int[,] modifiedData = new int[height, width];
            var theta = new double[height, width];
            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {
                    int[,] cluster = new int[,] {
                    { bitmap[i - 1,j-1], bitmap[i - 1, j ], bitmap[i - 1,j+1]},
                    { bitmap[i, j - 1], bitmap[i, j], bitmap[i, j + 1] },
                    { bitmap[i + 1, j - 1], bitmap[i + 1, j], bitmap[i + 1, j + 1] },
                    };
                    int[] values = new int[3];
                    // 0, 1,  2
                    // w , w + 1, w+ 2
                    // 2 w , 2w +1, w + 2
                    int[] hValues = new int[3];
                    int[] vValues = new int[3];
                    for (int c = 0; c < 3; c++)
                    {

                        hValues[c] = ApplyMaskOnCluster(cluster, hMask, c);
                        vValues[c] = ApplyMaskOnCluster(cluster, vMask, c);
                        values[c] = (int)Math.Sqrt(hValues[c] * hValues[c] + vValues[c] * vValues[c]);
                    }
                    int s = values.Sum() / 3;
                    modifiedData[i, j] = BitConverter.ToInt32(new byte[] { (byte)s, (byte)s, (byte)s, 0xff });
                    theta[i, j] = Math.Atan2(vValues.Average(), hValues.Average());
                }
            }
            return (modifiedData, theta);
        }

        private int ApplyMaskOnCluster(int[,] cluster, int[,] m, int c)
        {
            return Math.Abs(GetChannelByte(cluster[0, 0], c) * m[0, 0]
                            + GetChannelByte(cluster[0, 1], c) * m[0, 1]
                            + GetChannelByte(cluster[0, 2], c) * m[0, 2]
                            + GetChannelByte(cluster[1, 0], c) * m[1, 0]
                            + GetChannelByte(cluster[1, 1], c) * m[1, 1]
                            + GetChannelByte(cluster[1, 2], c) * m[1, 2]
                            + GetChannelByte(cluster[2, 0], c) * m[2, 0]
                            + GetChannelByte(cluster[2, 1], c) * m[2, 1]
                            + GetChannelByte(cluster[2, 2], c) * m[2, 2]);
        }

        private int GetChannelByte(int pixel, int channel)
        {
            return BitConverter.GetBytes(pixel)[channel];
        }
        private int[,] ImageToMatrix(BitmapImage bitmap)
        {
            BitmapSource bitmapSource = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0);
            WriteableBitmap modifiedImage = new WriteableBitmap(bitmapSource);

            int h = modifiedImage.PixelHeight;
            int w = modifiedImage.PixelWidth;
            int[] pixelData = new int[w * h];
            var matrix = new int[h, w];
            modifiedImage.CopyPixels(pixelData, w * 4, 0);

            for (int i = 0; i < h; i++)
            {
                for (int j = 0; j < w; j++)
                {
                    matrix[i, j] = pixelData[i * w + j];
                }
            }
            return matrix;

        }
        public BitmapSource MatrixToImage(int[,] matrix, BitmapImage bitmap)
        {
            BitmapSource bitmapSource = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0);
            WriteableBitmap modifiedImage = new WriteableBitmap(bitmapSource);
            int h = modifiedImage.PixelHeight;
            int w = modifiedImage.PixelWidth;


            int widthInByte = 4 * w;
            modifiedImage.WritePixels(new Int32Rect(0, 0, w, h), matrix, widthInByte, 0);

            return modifiedImage;

        }
        public int[,] Blur(int[,] bitmap, int width, int height)
        {
            int[,] blured = new int[height, width];
            int blurSize = 5;
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    int avgR = 0, avgG = 0, avgB = 0;
                    int blurPixelCount = 0;

                    for (int x = i; (x < i + blurSize && x < width); x++)
                    {
                        for (int y = j; (y < j + blurSize && y < height); y++)
                        {

                            avgB += GetChannelByte(bitmap[y, x], 0); // Blue
                            avgG += GetChannelByte(bitmap[y, x], 1); // Green
                            avgR += GetChannelByte(bitmap[y, x], 2); // Red

                            blurPixelCount++;
                        }
                    }

                    avgR /= blurPixelCount;
                    avgG /= blurPixelCount;
                    avgB /= blurPixelCount;
                    for (int x = i; (x < i + blurSize && x < width); x++)
                    {
                        for (int y = j; (y < j + blurSize && y < height); y++)
                        {
                            blured[y, x] = BitConverter.ToInt32(new byte[] { (byte)avgB, (byte)avgG, (byte)avgR, 0xff });
                        }
                    }

                }
            }
            return blured;

        }

        public int[,] ApplyKernel(int[,] bitmap, int width, int height, int[,] kernel)
        {

            int[,] modified = new int[height, width];
            int kernelSize = (int)Math.Sqrt(kernel.Length);
            int h = kernelSize / 2;

            for (int i = h; i < height - h; i++)
            {
                for (int j = h; j < width - h; j++)
                {

                    int v = 0;
                    for (int y = -h; y < h; y++)
                    {
                        for (int x = -h; x < h; x++)
                        {
                            v += GetChannelByte(bitmap[i + y, j + x], 0) * kernel[y + h, x + h];
                        }
                    }

                    modified[i, j] = BitConverter.ToInt32(new byte[] { (byte)v, (byte)v, (byte)v, 0xff });



                }
            }
            return modified;

        }
        private static int[,] ConvolutionFilter(int[,] sourceBitmap,
                                     double[,] filterMatrix,
                                     int width,int height,
                                          double factor = 1,
                                               int bias = 0,
                                     bool grayscale = false)
        {
            int[,] resultImage = new int[height, width];

          


           


            double blue = 0.0;
            double green = 0.0;
            double red = 0.0;


            int filterWidth = filterMatrix.GetLength(1);


            int filterOffset = (filterWidth - 1) / 2;
        


            for (int offsetY = filterOffset; offsetY <
                height - filterOffset; offsetY++)
            {
                for (int offsetX = filterOffset; offsetX < width- filterOffset; offsetX++)
                {
                    blue = 0;
                    green = 0;
                    red = 0;
                    for (int filterY = -filterOffset;
                        filterY <= filterOffset; filterY++)
                    {
                        for (int filterX = -filterOffset;
                            filterX <= filterOffset; filterX++)
                        {

                            byte[] bytes = BitConverter.GetBytes(sourceBitmap[offsetY + filterY,  offsetX + filterX]);
                            blue += bytes[0] *
                                    filterMatrix[filterY + filterOffset,
                                                 filterX + filterOffset];


                            green += bytes[1] *
                                     filterMatrix[filterY + filterOffset,
                                                  filterX + filterOffset];


                            red += bytes[2] *
                                   filterMatrix[filterY + filterOffset,
                                                filterX + filterOffset];
                        }
                    }


                    blue = factor * blue + bias;
                    green = factor * green + bias;
                    red = factor * red + bias;


                    if (blue > 255)
                    { blue = 255; }
                    else if (blue < 0)
                    { blue = 0; }


                    if (green > 255)
                    { green = 255; }
                    else if (green < 0)
                    { green = 0; }


                    if (red > 255)
                    { red = 255; }
                    else if (red < 0)
                    { red = 0; }

                    resultImage[offsetY, offsetX] = BitConverter.ToInt32(new byte[] { (byte)blue, (byte)green, (byte)red, 0xff });
                    
                }
            }


            
            return resultImage;
        }

    }
}
