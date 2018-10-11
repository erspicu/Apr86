using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Apr8086.x86
{
    unsafe partial class Apr8086Core
    {
        //MAX 1Mbyte , 20bits address line
        byte[] PA_mem = new byte[0x100000];

        public void modrm_WriteTool(ushort val)
        {
            if (is_mod3)
            {
                if (is_W)
                    Table_WordRegsSet(target_RegIndex, val);
                else
                    Table_ByteRegsSet(target_RegIndex, (byte)val);
            }
            else
            {
                if (is_W)
                {
                    PA_Write(target_ea, (byte)val);
                    PA_Write((target_ea + 1), (byte)(val >> 8));
                }
                else
                    PA_Write(target_ea, (byte)val);
            }
        }

        public ushort modreg_ReadTool()
        {
            ushort Data = 0;
            if (is_mod3)
                Data = is_W ? Table_WordRegsGet(target_RegIndex) : Table_ByteRegsGet(target_RegIndex); //read from register              
            else
                Data = is_W ? (ushort)(PA_Read(target_ea) | (PA_Read(target_ea + 1) << 8)) : PA_Read(target_ea); //read from memory
            return Data;
        }


        public byte PA_Read(int add)
        {
            add &= 0xfffff;

            //if (add >= 0xc0000 && add <= 0xcffff){}
            //if (step_count >= 950000 && step_count <= 1000000) StepsLog.WriteLine(step_count.ToString("x4") + " " + add.ToString("x5") + " r " + PA_mem[add].ToString("x2"));

            if (add == 0x410) // hack patch
                return 0x41;

            return PA_mem[add];
        }

        public void PA_Write(int add, byte val)
        {
            add &= 0xfffff;
            PA_mem[add] = val;

            //if (step_count >= 950000 && step_count <= 1000000) StepsLog.WriteLine(step_count.ToString("x4") + " " + add.ToString("x5") + " w " + val.ToString("x2"));
            //if (add >= 0xc0000 && add <= 0xcffff) { }

            if ((add >= 0xA0000) && (add <= 0xBFFFF))
            {
                //Here
                //StepsLog.WriteLine(add.ToString("x5") + ":" + val.ToString("x2") + " " + (char)val);

                //StepsLog.WriteLine( step_count .ToString("x5")+ " " + add.ToString("x4")+ " "+ (char)val);
            }
        }

        public byte Mem_CS_r8(int address)
        {
            return PA_Read((Reg_CS << 4) + address);
        }

        public byte Mem_ES_r8(int address)
        {
            return PA_Read((Reg_ES << 4) + address);
        }

        public void Mem_ES_w8(int address, byte val)
        {
            PA_Write((Reg_ES << 4) + address, val);
        }

        public ushort Mem_ES_r16(int address)
        {
            return (ushort)(PA_Read((Reg_ES << 4) + address) | (PA_Read((Reg_ES << 4) + address + 1) << 8));
        }

        public void Mem_ES_w16(int address, ushort val)
        {
            PA_Write((Reg_ES << 4) + address, (byte)(val & 0xff));
            PA_Write((Reg_ES << 4) + address + 1, (byte)((val & 0xff00) >> 8));
        }

        public byte Mem_DS_r8(int address)
        {
            return PA_Read((Reg_DS << 4) + address);
        }

        public void Mem_DS_w8(int address, byte val)
        {
            PA_Write((Reg_DS << 4) + address, val);
        }

        public ushort Mem_DS_r16(int address)
        {
            return (ushort)(PA_Read((Reg_DS << 4) + address) | (PA_Read((Reg_DS << 4) + address + 1) << 8));
        }

        public void Mem_DS_w16(int address, ushort val)
        {
            PA_Write((Reg_DS << 4) + address, (byte)(val & 0xff));
            PA_Write((Reg_DS << 4) + address + 1, (byte)((val & 0xff00) >> 8));
        }

        public ushort Mem_SS_r16(int address)
        {
            return (ushort)(PA_Read((Reg_SS << 4) + address) | (PA_Read((Reg_SS << 4) + address + 1) << 8));
        }

        public void Mem_SS_w16(int address, ushort val)
        {
            PA_Write((Reg_SS << 4) + address, (byte)(val & 0xff));
            PA_Write((Reg_SS << 4) + address + 1, (byte)((val & 0xff00) >> 8));
        }
    }
}
