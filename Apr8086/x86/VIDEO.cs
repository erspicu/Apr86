using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NativeWIN32API;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

namespace Apr8086.x86
{
    unsafe partial class Apr8086Core
    {

        byte[] char_buffer = new byte[80 * 25 * 2];

        // https://en.wikipedia.org/wiki/Color_Graphics_Adapter
        uint[] TableColor = new uint[16] { 
            0xff000000,
            0xff0000aa,
            0xff00aa00,
            0xff00aaaa,
            0xffaa0000,
            0xffaa00aa,
            0xffaa5500,
            0xffaaaaaa,
            
            0xff555555,
            0xff5555ff,
            0xff55ff55,
            0xff55ffff,
            0xffff5555,
            0xffff55ff,
            0xffffff55,
            0xffffffff
        };

        void TextModeDemo()
        {

            Console.WriteLine("print!!!");

            Parallel.For(0, 25, y =>
            {
                for (int x = 0; x < 80; x++)
                {

                    int char_start_add = ((PA_mem[0xb8000 + ((x + y * 80) << 1)]) << 3) * 14;

                    byte char_attr = PA_mem[0xb8000 + ((x + y * 80) << 1) + 1];

                    byte bg = (byte)((char_attr & 0xf0) >> 4);
                    byte fg = (byte)(char_attr & 0xf);



                    int start = (x << 3) + y * 8960; // 640 * 14;
                    for (int fy = 0; fy < 14; fy++)
                        for (int fx = 0; fx < 8; fx++)
                        {
                            if (font[char_start_add + fx + (fy << 3)] == 0xff000000)
                            {
                                screen_buffer[start + fx + fy * 640] = TableColor[bg];
                            }
                            else
                                screen_buffer[start + fx + fy * 640] = TableColor[fg];
                        }
                }
            });

            NativeGDI.DrawImageHighSpeedtoDevice();
        }



    }
}
