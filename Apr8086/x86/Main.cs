using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using NativeWIN32API;

namespace Apr8086.x86
{
    unsafe public partial class Apr8086Core
    {

        StreamWriter StepsLog;
        Dictionary<int, ushort> io_steps = new Dictionary<int, ushort>();
        uint[] font = new uint[256 * 14 * 8];

        uint[] screen_buffer = new uint[640 * 350];

        Graphics device;

        public void init( Graphics _device , byte[] bios_bytes, byte[] vbios_bytes)
        {

            Stopwatch st = new Stopwatch();
            st.Restart();

            

            NativeGDI.initHighSpeed(_device , 640, 350, screen_buffer, 0, 0);



            for (int i = 0; i < 256; i++)
            {
                int c = 0;
                byte v = (byte)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((v & 1) > 0) c++;
                    v >>= 1;
                }
                Table_Paritys[i] = ((c % 2) == 0) ? true : false;
            }

            //init ram
            for (int i = 0; i < 0x100000; i++) PA_mem[i] = 0;

            //reset
            Reg_CS = 0xffff;
            Reg_IP = 0;

            //load system bios 
            Array.Copy(bios_bytes, 0, PA_mem, 0x100000 - bios_bytes.Count(), bios_bytes.Count());

            //load vga bios
            Array.Copy(vbios_bytes, 0, PA_mem, 0xc0000, vbios_bytes.Count());


            if (File.Exists(@"c:\log\log.txt"))
                File.Delete(@"c:\log\log.txt");

            if (!Directory.Exists(@"c:\log"))
                Directory.CreateDirectory((@"c:\log"));

            StepsLog = File.AppendText(@"c:\log\log.txt");

            List<string> lines = File.ReadAllLines(Application.StartupPath + @"\io_step.dat").ToList();
            foreach (string i in lines)
                io_steps.Add(Convert.ToInt32(i.Substring(0, 6).Replace(" ", ""), 16), (ushort)Convert.ToUInt16(i.Substring(7, 4).Replace(" ", ""), 16));

            //load font to array
            for (int i = 0; i < 256; i++)
            {
                Bitmap bmp = new Bitmap(Application.StartupPath + @"\ASII_FONT\" + i.ToString("x2") + ".png");
                int start = i * 8 * 14;
                for (int y = 0; y < 14; y++)
                    for (int x = 0; x < 8; x++)
                        font[start + x + (y << 3)] = (uint)(bmp.GetPixel(x, y).ToArgb());
                bmp.Dispose();
            }

            st.Stop();
            Console.WriteLine("init : " + st.ElapsedMilliseconds + " ms done!\n");
        }

        public bool run = true;
        public void Run()
        {
            while (run)
                cpu_exec();
        }
    }
}
