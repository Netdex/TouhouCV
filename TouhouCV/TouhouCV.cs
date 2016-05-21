using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Cvb;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.Util;
using Emgu.CV.VideoSurveillance;
using SlimDX.Direct3D9;
using TouhouCV.Properties;

namespace TouhouCV
{
    public partial class TouhouCV : Form
    {
        public TouhouCV()
        {
            InitializeComponent();
        }

        private static readonly Rectangle BOX_SIZE = new Rectangle(33, 16, 384, 448);

        private DxScreenCapture _capture;
        private Rectangle _screenRegion;
        private Process _process;
        private IntPtr hWnd;
        private IntPtr hndl;

        private void TouhouCV_Load(object sender, EventArgs e)
        {
            _capture = new DxScreenCapture();

            Process[] processes = Process.GetProcessesByName("th10e");
            if (processes.Length < 1)
            {
                Console.WriteLine("Process 'th10.exe' is not running!");
                Environment.Exit(0);
            }
            _process = processes[0];
            hndl = Win32.GetProcessHandle(_process);
            hWnd = _process.MainWindowHandle;
            var topLeft = new Win32.POINT(0, 0);
            Win32.ClientToScreen(hWnd, ref topLeft);
            _screenRegion = new Rectangle(
                new Point(topLeft.X + BOX_SIZE.X, topLeft.Y + BOX_SIZE.Y),
                new Size(BOX_SIZE.Width, BOX_SIZE.Height));

            Win32.SetForegroundWindow(hWnd);
            new Thread(() =>
            {
                while (!this.IsDisposed)
                {
                    DoPlayerMovement(_force);
                    Thread.Sleep(1);
                }
            }).Start();

            new Thread(() =>
            {
                while (!this.IsDisposed)
                {
                    ProcessCapture();
                    Thread.Sleep(20);
                }
            }).Start();
        }

        private PointF _playerPos;
        private Vec2 _force;

        private void ProcessCapture()
        {
            // Create screen capture with DirectX surfaces
            var surface = _capture.CaptureScreen(); ;
            var surfaceStream = Surface.ToStream(surface, ImageFileFormat.Bmp, _screenRegion);
            Bitmap bmp = new Bitmap(surfaceStream);
            surface.Dispose();
            surfaceStream.Dispose();
            var scrn = new Image<Gray, byte>(bmp);
            bmp.Dispose();

            Image<Bgr, byte> imageToShow = scrn.Copy().Convert<Bgr, byte>();
            var binthresh = scrn.SmoothBlur(3, 3).ThresholdBinary(new Gray(248), new Gray(255));

            // Read player position from memory
            _playerPos = GetPlayerPosition();
            imageToShow.Draw(
                new Rectangle((int)(_playerPos.X - 3), (int)(_playerPos.Y - 3), 6, 6),
                new Bgr(Color.Lime),
                2);

            CvBlobs resultingImgBlobs = new CvBlobs();
            CvBlobDetector bDetect = new CvBlobDetector();
            uint noBlobs = bDetect.Detect(binthresh, resultingImgBlobs);

            // Detect blobs (bullets) on screen
            _force = new Vec2();
            foreach (CvBlob targetBlob in resultingImgBlobs.Values)
            {
                if (targetBlob.Area > 5)
                {
                    imageToShow.FillConvexPoly(targetBlob.GetContour(), new Bgr(Color.Red));

                    // Find closest point on blob contour to player
                    Point minPoint = targetBlob.GetContour()[0];
                    double minDist = double.MaxValue;
                    foreach (var point in targetBlob.GetContour())
                    {
                        double dist =
                            Math.Sqrt(Math.Pow(point.X - _playerPos.X, 2) + Math.Pow(point.Y - _playerPos.Y, 2));

                        if (dist < minDist)
                        {
                            minPoint = point;
                            minDist = dist;
                        }
                    }
                    // Ensure the bullet is in the correct range
                    if (minDist < 150)
                    {
                        // Calculate forces
                        Vec2 acc = Vec2.CalculateForce(new Vec2(minPoint.X, minPoint.Y),
                            new Vec2(_playerPos.X, _playerPos.Y), -5000);
                        _force += acc;
                        imageToShow.Draw(new LineSegment2DF(_playerPos, minPoint), new Bgr(Color.Cyan), 1);
                    }
                }
            }
            // Account for border force, to prevent cornering
            if (BOX_SIZE.Width - _playerPos.X < 60)
                _force += new Vec2(Vec2.CalculateForce(BOX_SIZE.Width - _playerPos.X, -5000), 0);
            if (_playerPos.X < 60)
                _force += new Vec2(Vec2.CalculateForce(_playerPos.X, 5000), 0);
            if (BOX_SIZE.Height - _playerPos.Y < 60)
                _force += new Vec2(0, Vec2.CalculateForce(BOX_SIZE.Height - _playerPos.Y, -5000));
            if (_playerPos.Y < 50)
                _force += new Vec2(0, Vec2.CalculateForce(_playerPos.Y, 20000));

            // Draw force vector
            imageToShow.Draw(
                new LineSegment2DF(_playerPos,
                new PointF((float)(_playerPos.X + _force.X), (float)(_playerPos.Y + _force.Y))),
                new Bgr(Color.Orange), 5);
            imageBox.Image = imageToShow;
        }

        public void DoPlayerMovement(Vec2 force)
        {
            // Spam Z
            DInput.SendKey(0x2C, DInput.KEYEVENTF_SCANCODE);
            // Ensure the force is large enough to worry about
            if (Math.Abs(force.X) > 1)
            {
                if (force.X < 0)
                {
                    DInput.SendKey(0x4B, DInput.KEYEVENTF_SCANCODE);
                    DInput.SendKey(0x4D, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
                }
                else
                {
                    DInput.SendKey(0x4D, DInput.KEYEVENTF_SCANCODE);
                    DInput.SendKey(0x4B, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
                }
            }
            else
            {
                DInput.SendKey(0x4D, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
                DInput.SendKey(0x4B, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
            }
            if (Math.Abs(force.Y) > 1)
            {
                if (force.Y < 0)
                {
                    DInput.SendKey(0x48, DInput.KEYEVENTF_SCANCODE);
                    DInput.SendKey(0x50, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
                }
                else
                {
                    DInput.SendKey(0x50, DInput.KEYEVENTF_SCANCODE);
                    DInput.SendKey(0x48, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
                }
            }
            else
            {
                DInput.SendKey(0x50, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
                DInput.SendKey(0x48, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
            }
        }
        public PointF GetPlayerPosition()
        {
            IntPtr baseAddr = new IntPtr(0x400000);
            /*
            MoF: 0x00077834
            LoLK: 0x000E9BB8
            */
            IntPtr ptrAddr = IntPtr.Add(baseAddr, 0x00077834);
            int addr;
            Win32.ReadMemoryInt32(hndl, ptrAddr, out addr);
            /*
            MoF: 0x354
            LoLK: 0x508
            IN: 0x017D6110
            EOSD: 0x006CAA68
            */
            addr += 0x354;
            float x, y;
            Win32.ReadMemoryFloat(hndl, new IntPtr(addr), out x);
            Win32.ReadMemoryFloat(hndl, new IntPtr(addr + 4), out y);
            /*
             MoF:    0,    0 - BOX_SIZE.X - BOX_SIZE.Y
            LoLK: - 80, + 20
            */
            return new PointF(x - BOX_SIZE.X, y - BOX_SIZE.Y);
        }
    }
}
