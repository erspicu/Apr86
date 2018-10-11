using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;


using Apr8086.x86;

namespace Apr8086
{
    public partial class Apr8086_UI : Form
    {

        string firmware_path = Application.StartupPath + "/" + "firmware";

        Graphics grfx;
        public Apr8086_UI()
        {
            InitializeComponent();
            grfx = panel1.CreateGraphics();
        }


        #region for test
        Thread pc8086_t = null;
        Apr8086Core pc8086;
        bool runin = false;
        private void button1_Click(object sender, EventArgs e)
        {


            if (runin) return;
            runin = true;
            pc8086 = new Apr8086Core();
            byte[] bios_bytes = File.ReadAllBytes(firmware_path + "/pcxtbios.bin");
            byte[] vbios_bytes = File.ReadAllBytes(firmware_path + "/videorom.bin");
            pc8086.init(grfx , bios_bytes, vbios_bytes);
            pc8086_t = new Thread(pc8086.Run);
            pc8086_t.Start();
        }
        #endregion

    }
}
