using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Point = System.Windows.Point;

namespace NativeAreaScreenshot
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private Point startPoint;
        private System.Windows.Shapes.Rectangle selectionRect;

        // Path where screenshots will be saved. We'll make this configurable later.
        // For now, save to the user's My Pictures folder
        private string saveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AreaScreenshots");

        public MainWindow()
        {
            InitializeComponent();
            selectionRect = SelectionRectangle;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure window covers all screens (including multi-monitor setups)
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
            
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            isDragging = true;
            startPoint = e.GetPosition(OverlayCanvas);

            Canvas.SetLeft(selectionRect, startPoint.X);
            Canvas.SetTop(selectionRect, startPoint.Y);
            selectionRect.Width = 0;
            selectionRect.Height = 0;
            selectionRect.Visibility = Visibility.Visible;
            
            OverlayCanvas.CaptureMouse();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;

            var currentPoint = e.GetPosition(OverlayCanvas);

            var x = Math.Min(currentPoint.X, startPoint.X);
            var y = Math.Min(currentPoint.Y, startPoint.Y);
            var w = Math.Abs(currentPoint.X - startPoint.X);
            var h = Math.Abs(currentPoint.Y - startPoint.Y);

            Canvas.SetLeft(selectionRect, x);
            Canvas.SetTop(selectionRect, y);
            selectionRect.Width = w;
            selectionRect.Height = h;
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDragging) return;
            isDragging = false;
            OverlayCanvas.ReleaseMouseCapture();

            double x = Canvas.GetLeft(selectionRect);
            double y = Canvas.GetTop(selectionRect);
            double width = selectionRect.Width;
            double height = selectionRect.Height;

            // Hide the window so it doesn't appear in the screenshot
            this.Hide();

            // Small delay to allow window to hide completely
            System.Threading.Thread.Sleep(50);

            if (width > 5 && height > 5)
            {
                CaptureScreenArea((int)x, (int)y, (int)width, (int)height);
            }

            // Close application after capture (or after accidental click)
            Application.Current.Shutdown();
        }

        private void CaptureScreenArea(int x, int y, int width, int height)
        {
            try
            {
                // Note: In a multi-monitor setup with different DPIs, this coordinate mapping
                // might need adjustment using Graphics.FromHdc / Physical scales.
                // For a basic implementation, we assume 100% scaling standard coordinates relative to virtual screen.
                
                int screenX = (int)(SystemParameters.VirtualScreenLeft + x);
                int screenY = (int)(SystemParameters.VirtualScreenTop + y);

                using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(screenX, screenY, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
                    }

                    string filename = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HHmmss}.png";
                    string fullPath = Path.Combine(saveDirectory, filename);
                    
                    bmp.Save(fullPath, ImageFormat.Png);
                    
                    // Optional: could play a sound or show a notification toast here
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"スクリーンショットの保存に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}