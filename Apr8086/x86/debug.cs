using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace Apr8086.x86
{
    unsafe partial class Apr8086Core
    {
        long step_count = 0;

        public void debug()
        {
            //Console.WriteLine(
            //step_count.ToString("x4") + " AX:" + Reg_A.X.ToString("x4") + " BX:" + Reg_B.X.ToString("x4") + " CX:" + Reg_C.X.ToString("x4") + " DX:" + Reg_D.X.ToString("x4") + " CS:" + Reg_CS.ToString("x4") + " DS:" + Reg_DS.ToString("x4") + " ES:" + Reg_ES.ToString("x4") + " SS:" + Reg_SS.ToString("x4") +
            // " \nBP:" + Reg_BP.ToString("x4") + " IP:" + Reg_IP.ToString("x4") + " SP:" + Reg_SP.ToString("x4") + " SI:" + Reg_SI.ToString("x4") + " DI:" + Reg_DI.ToString("x4") + " opcde:" + ins_byte1.ToString("x2") + " flag:" + flag_v.ToString("x4")
            //);

          //  if (step_count >= 1000000 && step_count <= 1100000)
            //  StepsLog.WriteLine(step_count.ToString("x5") + " AX:" + Reg_A.X.ToString("x4") + " BX:" + Reg_B.X.ToString("x4") + " CX:" + Reg_C.X.ToString("x4") + " DX:" + Reg_D.X.ToString("x4") + " CS:" + Reg_CS.ToString("x4") + " DS:" + Reg_DS.ToString("x4") + " ES:" + Reg_ES.ToString("x4") + " SS:" + Reg_SS.ToString("x4") +
             //"\r\nBP:" + Reg_BP.ToString("x4") + " IP:" + Reg_IP.ToString("x4") + " SP:" + Reg_SP.ToString("x4") + " SI:" + Reg_SI.ToString("x4") + " DI:" + Reg_DI.ToString("x4") + " opcode:" + ins_byte1.ToString("x2") + " flag:" + GetFlag().ToString("x4"));

            step_count++;

            if (step_count > 1100000)
            {
                StepsLog.Close();

                TextModeDemo();

                Console.WriteLine("ok");
                Console.ReadLine();
            }
        }
    }
}
