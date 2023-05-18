using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using SDK_LIB;

namespace QPOSDesktopDemo
{
    public partial class MainWindow : Window
    {
        private void update_firmware()
        {
            string file_name = "upgrader.asc";
            if (!File.Exists(file_name))
            {
                Tip.d(file_name + " not find");
                return;
            }
            System.IO.FileStream fs = new FileStream(file_name, FileMode.Open);
            long size = fs.Length;
            fs.Seek(0, SeekOrigin.Begin);
            byte[] array = new byte[size];

            if (size != 0)
            {
                fs.Read(array, 0, array.Length);
                fs.Close();
            }
            Tip.d("read upgrader.asc finish");

            pos.updatePosFirmware(array, "");

        }
    }
}
