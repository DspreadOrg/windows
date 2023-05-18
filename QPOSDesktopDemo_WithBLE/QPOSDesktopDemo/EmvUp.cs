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
        private void UpdateEmv_from_xml()
        {
            string sStuName = string.Empty;
            string emv_app_str = "";
            string emv_capk_str = "";
            bool app_start = false;
            bool capk_start = false;
            string Filename = "emv_profile_tlv.xml";
            FileStream fs = new FileStream(Filename, FileMode.Open);
           
            StreamReader reader = new StreamReader(fs, UnicodeEncoding.GetEncoding("GB2312"));
            while ((sStuName = reader.ReadLine()) != null)
            {
                //更新APP
                if (app_start)
                {
                    emv_app_str += QPOSDesktopLib.Emv.format_tlv_value(sStuName);

                }
                else if (capk_start)
                {
                    emv_capk_str += QPOSDesktopLib.Emv.format_tlv_value(sStuName);
                }
                if (sStuName.Contains("<app>"))
                {
                    app_start = true;
                    Tip.d("app start-----------------\r\n");
                }
                else if (sStuName.Contains("</app>"))
                {
                    app_start = false;
                    //pos.updateEmv_AppConfig(emv_app_str);
                    emv_app_str += ",";
                }
                if (sStuName.Contains("<capk>"))
                {
                    capk_start = true;
                    app_start = false;
                    Tip.d("capk start-----------------\r\n");
                }
                else if (sStuName.Contains("</capk>"))
                {
                    capk_start = false;
                    emv_capk_str += ",";
                }

            }
            fs.Close();
            pos.CustomUpdateEmvConfig(emv_app_str, emv_capk_str);

        }

        private void UpdateEmv_from_bin()
        {
            string emvAppCfg = "";
            string emvCapkCfg = "";
            //读文件
            string file_app = "emv_app.bin";
            string file_capk = "emv_capk.bin";
            System.IO.FileStream fs = new FileStream(file_app, FileMode.Open);
            long size = fs.Length;
            fs.Seek(0, SeekOrigin.Begin);
            byte[] array = new byte[size];

            if (size != 0)
            {
                fs.Read(array, 0, array.Length);
                emvAppCfg = Util.byteArray2Hex(array);
                Tip.d("emvAPPcfg=  " + emvAppCfg + "\r\n");
                fs.Close();
            }
            System.IO.FileStream fs_capk = new FileStream(file_capk, FileMode.Open);
            long size_capk = fs_capk.Length;
            fs_capk.Seek(0, SeekOrigin.Begin);
            byte[] array_capk = new byte[size_capk];
            if (size_capk != 0)
            {
                fs_capk.Read(array_capk, 0, array_capk.Length);
                emvCapkCfg = Util.byteArray2Hex(array_capk);
                Tip.d("emvCapkCfg=  " + emvCapkCfg + "\r\n");
                fs_capk.Close();
            }
            pos.updateEmvConfig(emvAppCfg, emvCapkCfg);

        }
    }
}
