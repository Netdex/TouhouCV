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
        private const int DETECTION_RADIUS = 60;

        private DxScreenCapture _capture;
        private Rectangle _screenRegion;
        private Process _process;
        private IntPtr hWnd;
        private IntPtr hndl;

        private Image<Gray, byte> _imgPower;

        private void TouhouCV_Load(object sender, EventArgs e)
        {
            _capture = new DxScreenCapture();
            _imgPower = new Image<Gray, byte>(Resources.Power);

            Process[] processes = Process.GetProcessesByName("th11e");
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
            this.Location = new Point(topLeft.X + 680, topLeft.Y);

            Win32.SetForegroundWindow(hWnd);

            new Thread(() =>
            {
                while (!this.IsDisposed)
                {
                    ProcessCapture();
                    DoPlayerMovement(_force);
                    Thread.Sleep(7);
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

            _force = new Vec2();

            Image<Bgr, byte> imageToShow = scrn.Copy().Convert<Bgr, byte>();

            // Read player position from memory
            _playerPos = GetPlayerPosition();
            imageToShow.Draw(
                new Rectangle((int)(_playerPos.X - 3), (int)(_playerPos.Y - 3), 6, 6),
                new Bgr(Color.Lime),
                2);
            imageToShow.Draw(new CircleF(_playerPos, DETECTION_RADIUS), new Bgr(Color.Aqua), 1);
            // Look for power
            scrn.ROI = new Rectangle(
                (int)Math.Max(_playerPos.X - 100, 0),
                (int)Math.Max(_playerPos.Y - 50, 0),
                (int)Math.Min(BOX_SIZE.Width - (_playerPos.X - 100), 200),
                (int)Math.Min(BOX_SIZE.Height - (_playerPos.Y - 50), 100));
            imageToShow.Draw(scrn.ROI, new Bgr(Color.Yellow), 1);
            using (Image<Gray, float> result = scrn.MatchTemplate(_imgPower, TemplateMatchingType.SqdiffNormed))
            {
                double minDistSq = double.MaxValue;
                int minX = 0, minY = 0;
                for (int y = 0; y < result.Height; y++)
                {
                    for (int x = 0; x < result.Width; x++)
                    {
                        if (result.Data[y, x, 0] < 0.20)
                        {
                            double dist =
                                (x - _playerPos.X) * (x - _playerPos.X) +
                                (y - _playerPos.Y) * (y - _playerPos.Y);
                            if (dist < minDistSq)
                            {
                                minDistSq = dist;
                                minX = x;
                                minY = y;
                            }
                        }
                    }
                }
                if (minDistSq != double.MaxValue)
                {
                    Rectangle match = new Rectangle(minX + scrn.ROI.X, minY + scrn.ROI.Y, _imgPower.Width, _imgPower.Height);
                    imageToShow.Draw(match, new Bgr(Color.Yellow), 2);
                    imageToShow.Draw(new LineSegment2DF(match.Location, _playerPos), new Bgr(Color.Yellow), 1);
                    Vec2 acc = Vec2.CalculateForce(
                        new Vec2(match.X + _imgPower.Width / 2.0, match.Y + _imgPower.Height / 2.0),
                        new Vec2(_playerPos.X, _playerPos.Y), 4000);
                    _force += acc;
                }
            }
            scrn.ROI = new Rectangle(
                (int)Math.Max(_playerPos.X - DETECTION_RADIUS, 0),
                (int)Math.Max(_playerPos.Y - DETECTION_RADIUS, 0),
                (int)Math.Min(BOX_SIZE.Width - (_playerPos.X - DETECTION_RADIUS), DETECTION_RADIUS * 2),
                (int)Math.Min(BOX_SIZE.Height - (_playerPos.Y - DETECTION_RADIUS), DETECTION_RADIUS * 2));
            imageToShow.Draw(scrn.ROI, new Bgr(Color.Red), 1);

            var binthresh = scrn.SmoothBlur(3, 3).ThresholdBinary(new Gray(248), new Gray(255));
            // Detect blobs (bullets) on screen
            CvBlobs resultingImgBlobs = new CvBlobs();
            CvBlobDetector bDetect = new CvBlobDetector();
            uint noBlobs = bDetect.Detect(binthresh, resultingImgBlobs);
            int blobCount = 0;
            foreach (CvBlob targetBlob in resultingImgBlobs.Values)
            {
                if (targetBlob.Area > 5)
                {
                    imageToShow.ROI = scrn.ROI;
                    imageToShow.FillConvexPoly(targetBlob.GetContour(), new Bgr(Color.Red));

                    // Find closest point on blob contour to player
                    Point minPoint = targetBlob.GetContour()[0];
                    double minDist = double.MaxValue;
                    foreach (var point in targetBlob.GetContour())
                    {
                        Point adj = new Point(point.X + scrn.ROI.X, point.Y + scrn.ROI.Y);
                        double dist =
                                Math.Pow(adj.X - _playerPos.X, 2) +
                                Math.Pow(adj.Y - _playerPos.Y, 2);

                        if (dist < minDist)
                        {
                            minPoint = adj;
                            minDist = dist;
                        }
                    }
                    // Ensure the bullet is in the correct range
                    if (minDist < DETECTION_RADIUS * DETECTION_RADIUS)
                    {
                        imageToShow.ROI = Rectangle.Empty;
                        // Calculate forces
                        Vec2 acc = Vec2.CalculateForce(new Vec2(minPoint.X, minPoint.Y),
                            new Vec2(_playerPos.X, _playerPos.Y), -5000);
                        _force += acc;
                        imageToShow.Draw(new LineSegment2DF(_playerPos, minPoint), new Bgr(Color.Cyan), 1);
                        blobCount++;
                    }
                }
            }
            scrn.ROI = Rectangle.Empty;
            imageToShow.ROI = Rectangle.Empty;

            // Account for border force, to prevent cornering
            //if (BOX_SIZE.Width - _playerPos.X < 120)
            _force += new Vec2(Vec2.CalculateForce(BOX_SIZE.Width - _playerPos.X, -4000), 0);
            //if (_playerPos.X < 120)
            _force += new Vec2(Vec2.CalculateForce(_playerPos.X, 4000), 0);
            if (BOX_SIZE.Height - _playerPos.Y < 50)
                _force += new Vec2(0, Vec2.CalculateForce(BOX_SIZE.Height - _playerPos.Y, -2000));
            if (_playerPos.Y < 100)
                _force += new Vec2(0, Vec2.CalculateForce(_playerPos.Y, 5000));

            imageToShow.Draw("BLOB_COUNT: " + blobCount, new Point(10, 20), FontFace.HersheyPlain, 1, new Bgr(Color.White), 1);
            // Draw force vector
            imageToShow.Draw(
                new LineSegment2DF(_playerPos,
                new PointF((float)(_playerPos.X + _force.X), (float)(_playerPos.Y + _force.Y))),
                new Bgr(Color.Orange), 5);
            imageBox.Image = imageToShow;
        }

        public void DoPlayerMovement(Vec2 force)
        {
            if (force.X > 1000 || force.Y > 1000)
                Bomb();
            // Spam Z
            DInput.SendKey(0x2C, DInput.KEYEVENTF_SCANCODE);
            // Ensure the force is large enough to worry about
            if (Math.Abs(force.X) > 0.05)
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
            if (Math.Abs(force.Y) > 0.05)
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

        public void Bomb()
        {
            DInput.SendKey(0x4B, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
            DInput.SendKey(0x4D, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
            DInput.SendKey(0x2C, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
            Thread.Sleep(20);
            DInput.SendKey(0x2D, DInput.KEYEVENTF_SCANCODE);
            Thread.Sleep(20);
            DInput.SendKey(0x2D, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
        }

        private void TouhouCV_FormClosing(object sender, FormClosingEventArgs e)
        {
            DInput.SendKey(0x2C, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
            DInput.SendKey(0x4D, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
            DInput.SendKey(0x4B, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
            DInput.SendKey(0x50, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
            DInput.SendKey(0x48, DInput.KEYEVENTF_KEYUP | DInput.KEYEVENTF_SCANCODE);
        }

        public PointF GetPlayerPosition()
        {
            IntPtr baseAddr = new IntPtr(0x400000);
            /*
            MoF: 0x00077834
            SA: 0x000A8EB4
            LoLK: 0x000E9BB8
            */
            IntPtr ptrAddr = IntPtr.Add(baseAddr, 0x000A8EB4);
            int addr;
            Win32.ReadMemoryInt32(hndl, ptrAddr, out addr);
            /*
            EOSD: 0x006CAA68
            IN: 0x017D6110
            MoF: 0x354
            SA: 0x3FC
            LoLK: 0x508
            */
            addr += 0x3FC;
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
