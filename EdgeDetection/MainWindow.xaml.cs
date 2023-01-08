using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EdgeDetection
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadImage.Click += LoadImage_Click;

        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            string path = ImagePath.Text;
            if (File.Exists(path))
            {
                var bitmap = new BitmapImage(new Uri(path, UriKind.Absolute));
                InputImage.Source = bitmap;
                Sobel.Source = ProcessBitmapAsSobel(bitmap);
                Prewitt.Source = ProcessBitmapAsPrewitt(bitmap);
                //Laplacian.Source = ProcessBitmapAsLaplacian(bitmap);
                //Canny.Source = ProcessBitmapAsCanny(bitmap);
            }
            else
            {
                ;
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Error;
                MessageBoxResult result;

                result = MessageBox.Show("File does not exist", "Error", button, icon, MessageBoxResult.Yes);
            }
        }

        private ImageSource ProcessBitmapAsCanny(BitmapImage bitmap)
        {
            throw new NotImplementedException();
        }

        private ImageSource ProcessBitmapAsLaplacian(BitmapImage bitmap)
        {
            int[][] hMask = new int[][] {
            new int[] { 1, 0 ,-1 },
            new int[] { 2, 0 ,-2 },
            new int[] { 1, 0 ,-1 }
            };

            int[][] vMask = new int[][] {
            new int[] { 1, 2 ,1 },
            new int[] { 0, 0 ,0 },
            new int[] { -1, -2 ,-1 }
            };
            return ApplyMasksOnBitmap(vMask, hMask, bitmap);
        }

        private ImageSource ProcessBitmapAsPrewitt(BitmapImage bitmap)
        {
            int[][] hMask = new int[][] {
            new int[] { 1, 0 ,-1 },
            new int[] { 1, 0 ,-1 },
            new int[] { 1, 0 ,-1 }
            };

            int[][] vMask = new int[][] {
            new int[] { 1, 1 ,1 },
            new int[] { 0, 0 ,0 },
            new int[] { -1, -1 ,-1 }
            };
            return ApplyMasksOnBitmap(vMask, hMask, bitmap);
        }

        private ImageSource ProcessBitmapAsSobel(BitmapImage bitmap)
        {
            //D:\Poze\2022\20-24 02 Feb Genova\Social\BMC_7110.jpg
            int[][] hMask = new int[][] {
            new int[] { 1, 0 ,-1 },
            new int[] { 2, 0 ,-2 },
            new int[] { 1, 0 ,-1 }
            };

            int[][] vMask = new int[][] {
            new int[] { 1, 2 ,1 },
            new int[] { 0, 0 ,0 },
            new int[] { -1, -2 ,-1 }
            };
            return ApplyMasksOnBitmap(vMask,hMask,bitmap);
        }

        private ImageSource ApplyMasksOnBitmap(int[][] vMask, int[][] hMask, BitmapImage bitmap)
        {

            BitmapSource bitmapSource = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0);
            WriteableBitmap modifiedImage = new WriteableBitmap(bitmapSource);

            int h = modifiedImage.PixelHeight;
            int w = modifiedImage.PixelWidth;
            int[] pixelData = new int[w * h];
            int[] modifiedData = new int[w * h];
            int widthInByte = 4 * w;

            modifiedImage.CopyPixels(pixelData, widthInByte, 0);

            for (int i = 1; i < bitmap.PixelHeight - 1; i++)
            {
                for (int j = 1; j < bitmap.PixelWidth - 1; j++)
                {
                    int currentPixel = pixelData[i * w + j];
                    int[][] cluster = new int[][] {
                    new int[] { pixelData[(i-1) * w + j - 1], pixelData[(i - 1) * w + j], pixelData[(i - 1) * w + j + 1]},
                    new int[] { pixelData[i * w + j - 1], pixelData[i * w+ j], pixelData[i * w + j + 1] },
                    new int[] { pixelData[(i + 1) *w +  j - 1], pixelData[(i+1) *w + j], pixelData[(i+1) * w + j + 1] },
                    };
                    int[] values = new int[3];
                    // 0, 1,  2
                    // w , w + 1, w+ 2
                    // 2 w , 2w +1, w + 2
                    for (int c = 0; c < 3; c++)
                    {

                        int hValue = ApplyMaskOnCluster(cluster, hMask, c);
                        int vValue = ApplyMaskOnCluster(cluster, vMask, c);
                        values[c] = (int)Math.Sqrt(hValue * hValue + vValue * vValue);
                    }
                    int s = values.Sum() / 3;
                    modifiedData[i * w + j] = BitConverter.ToInt32(new byte[] { (byte)s, (byte)s, (byte)s, 0xff});
                }
            }
            modifiedImage.WritePixels(new Int32Rect(0, 0, w, h), modifiedData, widthInByte, 0);


            return modifiedImage;
        }

        private int ApplyMaskOnCluster(int[][] cluster, int[][] m, int c)
        {
            return Math.Abs( GetChannelByte(cluster[0][0], c) * m[0][0]
                            + GetChannelByte(cluster[0][1], c) * m[0][1]
                            + GetChannelByte(cluster[0][2], c) * m[0][2]
                            + GetChannelByte(cluster[1][0], c) * m[1][0]
                            + GetChannelByte(cluster[1][1], c) * m[1][1]
                            + GetChannelByte(cluster[1][2], c) * m[1][2]
                            + GetChannelByte(cluster[2][0], c) * m[2][0]
                            + GetChannelByte(cluster[2][1], c) * m[2][1]
                            + GetChannelByte(cluster[2][2], c) * m[2][2]);
        }

        private int GetChannelByte(int pixel, int channel)
        {
            return BitConverter.GetBytes(pixel)[channel];
        }

    }
}
