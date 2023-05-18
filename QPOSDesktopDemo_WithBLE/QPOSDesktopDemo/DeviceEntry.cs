using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace QPOSDesktopDemo
{
    public  class DeviceEntry
    {
        public string DeviceType { get; set; }
        public string Name { get; set; }

        public const int WM_DEVICECHANGE = 0x219;
        public const int WM_DEVICEARRIVAL = 0x8000;
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        public static IntPtr DeviceChanged(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            String[] PortName;
            Console.WriteLine("msg: "+msg);
            if (msg == WM_DEVICECHANGE)
            {
                switch (wParam.ToInt32())
                {
                    case WM_DEVICEARRIVAL://device insert
                        Console.WriteLine("device insert");
                        break;
                    case DBT_DEVICEREMOVECOMPLETE://device remove
                        Console.WriteLine("Device remove");
                        break;
                }
            }

            return IntPtr.Zero;
        }
            
    }
}
