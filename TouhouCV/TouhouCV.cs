using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Capture;
using Capture.Interface;
using Emgu.CV;
using Emgu.CV.Cvb;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.CV.Util;
using Emgu.Util;
using Emgu.CV.VideoSurveillance;
using Emgu.CV.XFeatures2D;
using SharpDX;
using SharpDX.Direct3D9;
using TouhouCV.Properties;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace TouhouCV
{
    public class TouhouCV
    {
        private static TouhouCV _instance;

        public static TouhouCV Instance => _instance ?? (_instance = new TouhouCV());

        private TouhouCV()
        {

        }

        public void Load()
        {
            string proc = "th10e";
            _capture = new DXHook();
            _capture.AttachProcess(proc);
            _imgPower = new Image<Gray, byte>(Resources.Power);

            // Start visualization form
            _form = new THViz();
            Thread controlThread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.Run(_form);
            });
            controlThread.SetApartmentState(ApartmentState.STA);
            controlThread.Start();

            _process = _capture.Process;
            _hndl = Win32.GetProcessHandle(_process);
            _hWnd = _process.MainWindowHandle;
            Win32.SetForegroundWindow(_hWnd);

            //_bgSub = new BackgroundSubtractorMOG2(100, 64, false);
            //LoadEtama();

            new Thread(() =>
            {
                while (true)
                {
                    ProcessCapture();
                    if (DoMovement && !TEST_MODE)
                        DoPlayerMovement(_force);
                    Thread.Sleep(1);
                }
            }).Start();
        }

        public static readonly Rectangle BOX_SIZE = new Rectangle(32, 16, 384, 448);
        private const bool TEST_MODE = false;

        private const int INITIAL_DETECTION_RADIUS = 70;
        private float _detectionRadius = INITIAL_DETECTION_RADIUS;
        public bool DoMovement { get; set; } = true;
        private DXHook _capture;
        private Process _process;
        public IntPtr _hWnd;
        private IntPtr _hndl;
        private THViz _form;
        private Image<Gray, byte> _imgPower;
        private List<Image<Gray, byte>> _etama;

        private PointF _playerPos;
        private Vec2 _force;
        public bool ShouldAssist = false;
        public Point AssistPoint = Point.Empty;

        private void LoadEtama()
        {
            _etama = new List<Image<Gray, byte>>();
            Image<Gray, byte> etama = new Image<Gray, byte>(Properties.Resources.bullet1);
            for (int i = 0; i < 12; i++)
            {
                _etama.Add(etama.Copy(new Rectangle(0, i * 16, 16, 16)));
            }
            _etama.Add(etama.Copy(new Rectangle(4 * 16, 12 * 16, 8, 8)));
            _etama.Add(etama.Copy(new Rectangle(0, 15 * 16, 8, 8)));
            _etama.Add(etama.Copy(new Rectangle(4 * 16, 15 * 16, 16, 16)));
        }

        private BackgroundSubtractorMOG2 _bgSub;
        private unsafe void ProcessCapture()
        {
            try
            {
                Screenshot ssht = _capture.Capture();
                Image<Bgra, byte> imageToShow;
                Image<Gray, byte> scrn;

                fixed (byte* p = ssht.Data)
                {
                    IntPtr ptr = (IntPtr)p;
                    imageToShow = new Image<Bgra, byte>(ssht.Width, ssht.Height, ssht.Stride, ptr) { ROI = BOX_SIZE };
                    scrn = imageToShow.Convert<Gray, byte>();
                }
                ssht.Dispose();

                _force = new Vec2();

                // Read player position from memory
                _playerPos = GetPlayerPosition();
                imageToShow.Draw(
                    new Rectangle((int)(_playerPos.X - 3), (int)(_playerPos.Y - 3), 6, 6),
                    new Bgra(0, 255, 0, 255),
                    2);

                imageToShow.Draw(new CircleF(_playerPos, _detectionRadius), new Bgra(0, 255, 255, 255), 1);
                // Look for power
                scrn.ROI = new Rectangle(
                    (int)Math.Max(_playerPos.X - 100, 0),
                    (int)Math.Max(_playerPos.Y - 75, 0),
                    (int)Math.Min(BOX_SIZE.Width - (_playerPos.X - 100), 200),
                    (int)Math.Min(BOX_SIZE.Height - (_playerPos.Y - 75), 100));
                imageToShow.Draw(scrn.ROI, new Bgra(255, 255, 0, 255), 1);
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
                        Rectangle match = new Rectangle(minX + scrn.ROI.X, minY + scrn.ROI.Y, _imgPower.Width,
                            _imgPower.Height);
                        imageToShow.Draw(match, new Bgra(0, 255, 255, 255), 2);
                        imageToShow.Draw(new LineSegment2DF(match.Location, _playerPos), new Bgra(0, 255, 255, 255), 1);
                        Vec2 acc = Vec2.CalculateForce(
                            new Vec2(match.X + _imgPower.Width / 2.0, match.Y + _imgPower.Height / 2.0),
                            new Vec2(_playerPos.X, _playerPos.Y), 4000);
                        _force += acc;
                    }
                }

                // Processing bounding box
                scrn.ROI = new Rectangle(
                    (int)Math.Max(_playerPos.X - INITIAL_DETECTION_RADIUS, 0),
                    (int)Math.Max(_playerPos.Y - INITIAL_DETECTION_RADIUS, 0),
                    (int)Math.Min(BOX_SIZE.Width - ((int)_playerPos.X - INITIAL_DETECTION_RADIUS), INITIAL_DETECTION_RADIUS * 2),
                    (int)Math.Min(BOX_SIZE.Height - ((int)_playerPos.Y - INITIAL_DETECTION_RADIUS), INITIAL_DETECTION_RADIUS * 2));
                imageToShow.Draw(scrn.ROI, new Bgra(0, 0, 255, 255), 1);

                if (TEST_MODE)
                {

                    return;
                }
                Vec2 _playerVec = new Vec2(_playerPos.X, _playerPos.Y);

                var binthresh = scrn.SmoothBlur(3, 3).ThresholdBinary(new Gray(240), new Gray(255)); //220
                // Detect blobs (bullets) on screen
                CvBlobs resultingImgBlobs = new CvBlobs();
                CvBlobDetector bDetect = new CvBlobDetector();
                uint noBlobs = bDetect.Detect(binthresh, resultingImgBlobs);
                int blobCount = 0;
                resultingImgBlobs.FilterByArea(10, 500);
                foreach (CvBlob targetBlob in resultingImgBlobs.Values)
                {
                    imageToShow.ROI = new Rectangle(scrn.ROI.X + BOX_SIZE.X, scrn.ROI.Y + BOX_SIZE.Y, scrn.ROI.Width,
                        scrn.ROI.Height);
                    imageToShow.FillConvexPoly(targetBlob.GetContour(), new Bgra(0, 0, 255, 255));

                    // Find closest point on blob contour to player
                    Point minPoint = targetBlob.GetContour()[0];
                    double minDist = double.MaxValue;
                    foreach (var point in targetBlob.GetContour())
                    {
                        Point adj = new Point(point.X + scrn.ROI.X, point.Y + scrn.ROI.Y);
                        double dist =
                            (adj.X - _playerPos.X) * (adj.X - _playerPos.X) +
                            (adj.Y - _playerPos.Y) * (adj.Y - _playerPos.Y);

                        if (dist < minDist)
                        {
                            minPoint = adj;
                            minDist = dist;
                        }
                    }
                    // Ensure the bullet is in the correct range
                    if (minDist < _detectionRadius * _detectionRadius)
                    {
                        imageToShow.ROI = BOX_SIZE;
                        // Calculate forces
                        Vec2 acc = Vec2.CalculateForce(new Vec2(minPoint.X, minPoint.Y),
                            _playerVec, -5000);
                        _force += acc;
                        imageToShow.Draw(new LineSegment2DF(_playerPos, minPoint), new Bgra(0, 255, 128, 255), 1);
                        blobCount++;
                    }
                }
                imageToShow.ROI = BOX_SIZE;
                scrn.ROI = Rectangle.Empty;

                // Calculate new detection orb radius
                //float nRad = Math.Max(20.0f, INITIAL_DETECTION_RADIUS/(1 + blobCount*0.3f));
                if (blobCount >= 1)
                    _detectionRadius = (_detectionRadius * 19 + 20.0f) / 20.0f;
                else
                    _detectionRadius = (_detectionRadius * 59 + INITIAL_DETECTION_RADIUS) / 60.0f;


                // Account for border force, to prevent cornering
                //if (BOX_SIZE.Width - _playerPos.X < 120)
                _force += new Vec2(Vec2.CalculateForce(BOX_SIZE.Width - _playerPos.X, -4000), 0);
                //if (_playerPos.X < 120)
                _force += new Vec2(Vec2.CalculateForce(_playerPos.X, 4000), 0);
                if (BOX_SIZE.Height - _playerPos.Y < 50)
                    _force += new Vec2(0, Vec2.CalculateForce(BOX_SIZE.Height - _playerPos.Y, -2000));
                if (_playerPos.Y < 200)
                    _force += new Vec2(0, Vec2.CalculateForce(_playerPos.Y, 2000));
                // Corners are the devil
                _force += Vec2.CalculateForce(new Vec2(BOX_SIZE.Width, BOX_SIZE.Height),
                    _playerVec, -2000);
                _force += Vec2.CalculateForce(new Vec2(0, BOX_SIZE.Height),
                    _playerVec, -2000);
                _force += Vec2.CalculateForce(new Vec2(0, 0),
                    _playerVec, -2000);
                _force += Vec2.CalculateForce(new Vec2(BOX_SIZE.Width, 0),
                    _playerVec, -2000);

                // Assist force
                if (ShouldAssist)
                {
                    Vec2 sub = new Vec2(AssistPoint.X, AssistPoint.Y) - _playerVec;
                    double dist = sub.Length();
                    _force += new Vec2(sub.X / dist * 2, sub.Y / dist * 2);
                    imageToShow.Draw(
                        new LineSegment2DF(_playerPos, AssistPoint),
                        new Bgra(128, 0, 255, 255), 2);
                    imageToShow.Draw("ASSIST", new Point(10, 40), FontFace.HersheyPlain, 1, new Bgra(0, 255, 0, 255), 1);
                }
                //imageToShow.Draw("BLOB_AREA: " + percBlob, new Point(10, 20), FontFace.HersheyPlain, 1, new Bgra(255, 255, 255, 255), 1);
                // Draw force vector
                imageToShow.Draw(
                    new LineSegment2DF(_playerPos,
                        new PointF((float)(_playerPos.X + _force.X), (float)(_playerPos.Y + _force.Y))),
                    new Bgra(0, 128, 255, 255), 5);

                if(DoMovement)
                    imageToShow.Draw("DO_MOVEMENT", new Point(10, 20), FontFace.HersheyPlain, 1, new Bgra(0, 255, 0, 255), 1);
                _form.imageBox.Image = imageToShow;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void DoPlayerMovement(Vec2 force)
        {
            if (Math.Abs(force.X) > 3000 || Math.Abs(force.Y) > 3000)
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

        public PointF GetPlayerPosition()
        {
            IntPtr baseAddr = new IntPtr(0x400000);
            /*
            MoF: 0x00077834
            SA: 0x000A8EB4
            UFO: 0x000B4514
            LoLK: 0x000E9BB8
            */
            IntPtr ptrAddr = IntPtr.Add(baseAddr, 0x00077834);
            int addr;
            Win32.ReadMemoryInt32(_hndl, ptrAddr, out addr);
            /*
            EOSD: 0x006CAA68
            IN: 0x017D6110
            MoF: 0x354
            SA: 0x3FC
            UFO: 0x444
            LoLK: 0x508
            */
            addr += 0x354;
            float x, y;
            Win32.ReadMemoryFloat(_hndl, new IntPtr(addr), out x);
            Win32.ReadMemoryFloat(_hndl, new IntPtr(addr + 4), out y);
            /*
             MoF:    0,    0 - BOX_SIZE.X - BOX_SIZE.Y
            LoLK: - 80, + 20
            */
            return new PointF(x - BOX_SIZE.X, y - BOX_SIZE.Y);
        }
    }
}
