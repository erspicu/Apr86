using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Apr8086.x86
{
    unsafe public  partial class Apr8086Core
    {

        void interrupt(byte type)
        {

            Mem_SS_w16((Reg_SP -= 2), GetFlag() ) ;
            Mem_SS_w16((Reg_SP -= 2), Reg_CS );
            Mem_SS_w16((Reg_SP -= 2), Reg_IP );

            Reg_IP = (ushort)(PA_Read(type * 4) | (PA_Read(type * 4 + 1) << 8));
            Reg_CS = (ushort)(PA_Read(type * 4 + 2) | (PA_Read(type * 4 + 3) << 8));

          
            flag_I = false;//?
            flag_T = false;

            Console.WriteLine("interrupt unfinish ! - " + type.ToString("x2"));

        }
    }
}
