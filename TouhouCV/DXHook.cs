using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Capture;
using Capture.Hook;
using Capture.Interface;
using SharpDX.Direct3D9;

namespace TouhouCV
{
    [Serializable]
    public class DXHook
    {
        public int ProcessID = 0;
        public Process Process;
        public CaptureProcess CaptureProcess;

        public Action<Device> OverlayAction { get; set; }

        public DXHook()
        {
            
        }

        public Screenshot Capture()
        {
            CaptureProcess.BringProcessWindowToFront();
            var scrn = CaptureProcess.CaptureInterface.GetScreenshot(new Rectangle(0, 0, 0, 0), new TimeSpan(0, 0, 1), null, ImageFormat.PixelData);
            return scrn;
        }

        public void AttachProcess(string proc)
        {
            string exeName = Path.GetFileNameWithoutExtension(proc);

            Process[] processes = Process.GetProcessesByName(exeName);
            foreach (Process process in processes)
            {
                // Simply attach to the first one found.

                // If the process doesn't have a mainwindowhandle yet, skip it (we need to be able to get the hwnd to set foreground etc)
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                // Skip if the process is already hooked (and we want to hook multiple applications)
                if (HookManager.IsHooked(process.Id))
                {
                    continue;
                }

                Direct3DVersion direct3DVersion = Direct3DVersion.AutoDetect;
                

                CaptureConfig cc = new CaptureConfig()
                {
                    Direct3DVersion = direct3DVersion,
                    ShowOverlay = true
                };

                ProcessID = process.Id;
                Process = process;

                var captureInterface = new CaptureInterface();
                CaptureProcess = new CaptureProcess(process, cc, captureInterface);
                break;
            }
            Thread.Sleep(10);

            if (CaptureProcess == null)
            {
                MessageBox.Show("No executable found matching: '" + exeName + "'");
                Environment.Exit(0);
            }
        }
    }
}
