using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Apr8086.x86
{
    unsafe public partial class Apr8086Core
    {
        byte IO_read_byte(ushort port)
        {

            Console.WriteLine("IO read byte unfinish ! " + port.ToString("x4"));

            //由於尚未完整實作IO介面周邊功能,因此先以fakepc自行修改版專案輸出之io與step記錄來輸出.
            if (io_steps.ContainsKey((int)step_count))
                return (byte)io_steps[(int)step_count];
            else
            {
                Console.WriteLine("bad io step!");
                return 0;
            }
        }

        ushort IO_read_word(ushort port)
        {


            Console.WriteLine("IO read word unfinish ! " + port.ToString("x4"));

            //由於尚未完整實作IO介面周邊功能,因此先以fakepc自行修改版專案輸出之io與step記錄來輸出.
            if (io_steps.ContainsKey((int)step_count))
                return io_steps[(int)step_count];
            else
            {
                Console.WriteLine("bad io step!");
                return 0;
            }
        }


        void IO_write_byte(ushort port, byte val)
        {
            Console.WriteLine("IO write byte unfinish !" + port.ToString("x4"));
        }

        void IO_write_word(ushort port, ushort val)
        {
            Console.WriteLine("IO write word unfinish !" + port.ToString("x4"));
        }
    }
}
