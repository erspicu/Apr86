using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

//ref 8086-datasheet.pdf
namespace Apr8086.x86
{
    unsafe public partial class Apr8086Core
    {
        static RegWord Reg_A = new RegWord(), Reg_B = new RegWord(), Reg_C = new RegWord(), Reg_D = new RegWord();
        ushort Reg_SP = 0, Reg_BP = 0, Reg_SI = 0, Reg_DI = 0, Reg_CS = 0, Reg_DS = 0, Reg_SS = 0, Reg_ES = 0, Reg_IP = 0;
        bool flag_D, flag_I, flag_T, flag_S, flag_O, flag_Z, flag_A, flag_P, flag_C;

        bool tmp_flag = false;
        bool Halt = false, SegOverride = false;

        bool[] Table_Paritys = new bool[256];

        byte ins_byte1, ins_byte2, tmp_b1, tmp_b2, tmp_b3;
        ushort tmp_w1, tmp_w2, tmp_w3, disp16;
        short disp8;//byet signed extend to 16byte ,so use short
        ushort tmp_dst_ip, tmp_dst_cs;

        uint tmp_dw1;

        int tmp_idw1;

        int _i, cnt; // for loop counter




        enum SegReg
        {
            ES, CS, SS, DS
        }

        SegReg SegRegUse;

        [StructLayout(LayoutKind.Explicit, Size = 2)]
        struct RegWord
        {
            [FieldOffset(1)]
            public byte H; //heigh byte

            [FieldOffset(0)]
            public byte L; //low byte

            [FieldOffset(0)]
            public ushort X; //word
        }

        int target_ea = 0; //effective Address
        int target_RegIndex = 0;
        int RegIndex = 0;

        bool is_mod3 = false; //是否使用register 

        bool is_W = false;
        bool is_D = false;
        bool is_V = false;
        bool is_S = false;
        bool is_Z = false;

        //for debug info
        public void opcode_inf(bool subopcode)
        {
            string inf = "unkow opcode or editing : " + ins_byte1.ToString("x2");
            if (subopcode)
                inf += " " + ins_byte2.ToString("x2");
            //Console.WriteLine(inf);
            MessageBox.Show(inf);
        }

        ushort Table_SegRegsGet(int reg)
        {
            switch (reg)
            {
                case 0: return Reg_ES;
                case 1: return Reg_CS;
                case 2: return Reg_SS;
                case 3: return Reg_DS;
            }
            return 0;
        }

        void Table_SegRegsSet(int reg, ushort val)
        {
            switch (reg)
            {
                case 0: Reg_ES = val; break;
                case 1: Reg_CS = val; break;
                case 2: Reg_SS = val; break;
                case 3: Reg_DS = val; break;
            }
        }

        ushort Table_WordRegsGet(int reg)
        {
            switch (reg)
            {
                case 0: return Reg_A.X;
                case 1: return Reg_C.X;
                case 2: return Reg_D.X;
                case 3: return Reg_B.X;
                case 4: return Reg_SP;
                case 5: return Reg_BP;
                case 6: return Reg_SI;
                case 7: return Reg_DI;
            }
            return 0;
        }

        void Table_WordRegsSet(int reg, ushort val)
        {
            switch (reg)
            {
                case 0: Reg_A.X = val; break;
                case 1: Reg_C.X = val; break;
                case 2: Reg_D.X = val; break;
                case 3: Reg_B.X = val; break;
                case 4: Reg_SP = val; break;
                case 5: Reg_BP = val; break;
                case 6: Reg_SI = val; break;
                case 7: Reg_DI = val; break;
            }
        }

        byte Table_ByteRegsGet(int reg)
        {
            switch (reg)
            {
                case 0: return Reg_A.L;
                case 1: return Reg_C.L;
                case 2: return Reg_D.L;
                case 3: return Reg_B.L;
                case 4: return Reg_A.H;
                case 5: return Reg_C.H;
                case 6: return Reg_D.H;
                case 7: return Reg_B.H;
            }
            return 0;
        }

        void Table_ByteRegsSet(int reg, byte val)
        {
            switch (reg)
            {
                case 0: Reg_A.L = val; break;
                case 1: Reg_C.L = val; break;
                case 2: Reg_D.L = val; break;
                case 3: Reg_B.L = val; break;
                case 4: Reg_A.H = val; break;
                case 5: Reg_C.H = val; break;
                case 6: Reg_D.H = val; break;
                case 7: Reg_B.H = val; break;
            }
        }

        public ushort pop()
        {
            tmp_w1 = Mem_SS_r16(Reg_SP);
            Reg_SP += 2;
            return tmp_w1;
        }

        public ushort GetFlag()
        {
            return (ushort)(((flag_O) ? 0x800 : 0) | ((flag_D) ? 0x400 : 0) | ((flag_I) ? 0x200 : 0) | ((flag_T) ? 0x100 : 0) | ((flag_S) ? 0x80 : 0) |
                ((flag_Z) ? 0x40 : 0) | ((flag_A) ? 0x10 : 0) | ((flag_P) ? 4 : 0) | ((flag_C) ? 1 : 0) | 2);
        }

        public void flag_decode(ushort val)
        {
            flag_O = ((val & 0x800) > 0) ? true : false;
            flag_D = ((val & 0x400) > 0) ? true : false;
            flag_I = ((val & 0x200) > 0) ? true : false;
            flag_T = ((val & 0x100) > 0) ? true : false;
            flag_S = ((val & 0x80) > 0) ? true : false;
            flag_Z = ((val & 0x40) > 0) ? true : false;
            flag_A = ((val & 0x10) > 0) ? true : false;
            flag_P = ((val & 4) > 0) ? true : false;
            flag_C = ((val & 1) > 0) ? true : false;
        }

        //解析出使用register或是memory , 以及 effective address
        public void modregrm_parse()
        {
            ins_byte2 = Mem_CS_r8(Reg_IP++);
            is_mod3 = false;
            target_ea = 0;

            //pare mod
            switch (ins_byte2 & 0xc0)
            {
                case 0:
                    {
                        //check r/m
                        switch (ins_byte2 & 7)
                        {
                            case 0://[bx] + [si] 
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_A.X + Reg_SI;
                                break;
                            case 1://[bx] + [di] 
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_B.X + Reg_DI;
                                break;
                            case 2://[bp] + [si] 
                                if (!SegOverride) SegRegUse = SegReg.SS;
                                target_ea = Reg_BP + Reg_SI;
                                break;
                            case 3://[bp] + [di] 
                                if (!SegOverride) SegRegUse = SegReg.SS;
                                target_ea = Reg_BP + Reg_DI;
                                break;
                            case 4://[si]
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_SI;
                                break;
                            case 5://[di] 
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_DI;
                                break;
                            case 6://Direct Addressing with disp16
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                                break;
                            case 7://[bx]
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_B.X;
                                break;
                        }
                        target_ea &= 0xffff;
                    }
                    break;

                case 0x40: // MOD 01
                    {
                        disp8 = (short)((sbyte)Mem_CS_r8(Reg_IP++));

                        //check r/m
                        switch (ins_byte2 & 7)
                        {
                            case 0://[bx] + [si] + d8
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_B.X + Reg_SI + disp8;
                                break;
                            case 1://[bx] + [di] + d8
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_B.X + Reg_DI + disp8;
                                break;
                            case 2://[bp] + [si] + d8
                                if (!SegOverride) SegRegUse = SegReg.SS;
                                target_ea = Reg_BP + Reg_SI + disp8;
                                break;
                            case 3://[bp] + [di] + d8
                                if (!SegOverride) SegRegUse = SegReg.SS;
                                target_ea = Reg_BP + Reg_DI + disp8;
                                break;
                            case 4://[si] + d8
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_SI + disp8;
                                break;
                            case 5://[di] + d8
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_DI + disp8;
                                break;
                            case 6://[bp] + d8
                                if (!SegOverride) SegRegUse = SegReg.SS;
                                target_ea = Reg_BP + disp8;
                                break;
                            case 7://[bx] + d8
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_B.X + disp8;
                                break;
                        }
                        target_ea &= 0xffff;
                    }
                    break;

                case 0x80: //MOD 10
                    {
                        disp16 = (ushort)(Mem_CS_r8(Reg_IP++) | Mem_CS_r8(Reg_IP++) << 8);
                        //check r/m
                        switch (ins_byte2 & 7)
                        {
                            case 0://[bx] + [si] + d16
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_B.X + Reg_SI + disp16;
                                break;
                            case 1://[bx] + [di] + d16
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_B.X + Reg_DI + disp16;
                                break;
                            case 2://[bp] + [si] + d16
                                if (!SegOverride) SegRegUse = SegReg.SS;
                                target_ea = Reg_BP + Reg_SI + disp16;
                                break;
                            case 3://[bp] + [di] + d16
                                if (!SegOverride) SegRegUse = SegReg.SS;
                                target_ea = Reg_BP + Reg_DI + disp16;
                                break;
                            case 4://[si] + d16
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_SI + disp16;
                                break;
                            case 5://[di] + d16
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_DI + disp16;
                                break;
                            case 6://[bp] + d16
                                if (!SegOverride) SegRegUse = SegReg.SS;
                                target_ea = Reg_BP + disp16;
                                break;
                            case 7://[bx] + d16
                                if (!SegOverride) SegRegUse = SegReg.DS;
                                target_ea = Reg_B.X + disp16;
                                break;
                        }
                        target_ea &= 0xffff;
                    }
                    break;

                //as reg
                case 0xc0:
                    is_mod3 = true;
                    target_RegIndex = ins_byte2 & 7;
                    break;
            }

            target_ea += (Table_SegRegsGet((int)SegRegUse) << 4);//fixed
            SegOverride = false;
        }

        int prefix_effect_step = 0;
        int retype = 0;

        public void cpu_exec()
        {
            if (prefix_effect_step == 0 && retype != 0) retype = 0;
            else if (prefix_effect_step == 1) prefix_effect_step = 0;
            ins_byte1 = Mem_CS_r8(Reg_IP++);

            debug();

            switch (ins_byte1)
            {
                //MOV : Register/Memory to/from Register
                case 0x88:
                case 0x89:
                case 0x8A:
                case 0x8B:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if ((ins_byte1 & 2) > 0)
                    {
                        if (is_W)
                            Table_WordRegsSet((ins_byte2 & 0x38) >> 3, modreg_ReadTool());
                        else
                            Table_ByteRegsSet((ins_byte2 & 0x38) >> 3, (byte)modreg_ReadTool());
                    }
                    else
                    {
                        if (is_W)
                            modrm_WriteTool(Table_WordRegsGet((ins_byte2 & 0x38) >> 3));
                        else
                            modrm_WriteTool(Table_ByteRegsGet((ins_byte2 & 0x38) >> 3));
                    }
                    break;

                //MOV : Immediate to Register/Memory
                case 0xC6:
                case 0xC7:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                        modrm_WriteTool((ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));
                    else
                        modrm_WriteTool(Mem_CS_r8(Reg_IP++));
                    break;

                //MOV : Immediate to Register
                case 0xB0:
                case 0xB1:
                case 0xB2:
                case 0xB3:
                case 0xB4:
                case 0xB5:
                case 0xB6:
                case 0xB7:
                case 0xB8:
                case 0xB9:
                case 0xBA:
                case 0xBB:
                case 0xBC:
                case 0xBD:
                case 0xBE:
                case 0xBF:
                    {
                        if ((ins_byte1 & 8) > 0)
                            Table_WordRegsSet(ins_byte1 & 0x7, (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8))); //WORD
                        else
                            Table_ByteRegsSet(ins_byte1 & 0x7, Mem_CS_r8(Reg_IP++)); //BYTE   
                    }
                    break;

                //MOV : Memory to Accumulator
                case 0xA0:
                case 0xA1:
                    if (SegOverride == true)
                    {
                        tmp_w1 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        if ((ins_byte1 & 1) > 0)
                            Reg_A.X = (ushort)(PA_Read((Table_SegRegsGet((int)SegRegUse) << 4) + tmp_w1) | (PA_Read((Table_SegRegsGet((int)SegRegUse) << 4) + tmp_w1) << 8));
                        else
                            Reg_A.L = PA_Read((Table_SegRegsGet((int)SegRegUse) << 4) + tmp_w1);

                        SegOverride = false;
                        break;
                    }
                    if ((ins_byte1 & 1) > 0)
                        Reg_A.X = Mem_DS_r16((ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));
                    else
                        Reg_A.L = Mem_DS_r8((ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));
                    break;

                //MOV : Accumulator to Memory
                case 0xA3:
                case 0xA2:
                    if ((ins_byte1 & 1) > 0)
                        Mem_DS_w16((ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)), Reg_A.X);
                    else
                        Mem_DS_w8((ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)), Reg_A.L);
                    break;

                //MOV : Register/Memory to Segment Register
                case 0x8E:
                    modregrm_parse();
                    is_W = true;
                    Table_SegRegsSet((ins_byte2 & 0x18) >> 3, modreg_ReadTool());
                    break;

                //MOV : Segment Register to Register/Memory
                case 0x8C:
                    modregrm_parse();
                    is_W = true;
                    modrm_WriteTool(Table_SegRegsGet((ins_byte2 & 0x18) >> 3));
                    break;

                //has sub opcode
                case 0xfe:
                case 0xff:
                    {
                        is_W = ((ins_byte1 & 1) > 0) ? true : false;
                        modregrm_parse();
                        switch (ins_byte2 & 0x38)
                        {
                            //inc
                            case 0:
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool(); ;
                                    tmp_w2 = 1;
                                    tmp_dw1 = (uint)(tmp_w1 + tmp_w2);
                                    tmp_w3 = (ushort)tmp_dw1;
                                    modrm_WriteTool(tmp_w3);

                                    flag_Z = (tmp_w3 == 0) ? true : false;
                                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w3 & 0xff];

                                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = 1;
                                    tmp_w1 = (ushort)(tmp_b1 + tmp_b2);
                                    tmp_b3 = (byte)tmp_w1;
                                    modrm_WriteTool(tmp_b3);

                                    flag_Z = (tmp_b3 == 0) ? true : false;
                                    flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b3];

                                    flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;
                                    flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                                }
                                break;

                            //dec
                            case 0x8:
                                if (is_W)
                                {

                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w2 = 1;

                                    tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                                    tmp_w3 = (ushort)tmp_dw1;
                                    modrm_WriteTool(tmp_w3);

                                    flag_Z = (tmp_w3 == 0) ? true : false;
                                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w3 & 0xff];

                                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                                }
                                else
                                {

                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = 1;
                                    tmp_w1 = (ushort)(tmp_b1 - tmp_b2);

                                    tmp_b3 = (byte)tmp_w1;
                                    modrm_WriteTool(tmp_b3);

                                    flag_Z = (tmp_b3 == 0) ? true : false;
                                    flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b3];

                                    flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;
                                    flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;

                                }
                                break;

                            //call : indirect with sehment
                            case 0x10:
                                tmp_w1 = modreg_ReadTool();
                                Mem_SS_w16((Reg_SP -= 2), Reg_IP);
                                Reg_IP = tmp_w1;
                                break;

                            //call : indirect intersegment
                            case 0x18:
                                Mem_SS_w16((Reg_SP -= 2), Reg_CS);
                                Mem_SS_w16((Reg_SP -= 2), Reg_IP);
                                Reg_IP = (ushort)(PA_Read(target_ea) | (PA_Read(target_ea + 1) << 8));
                                Reg_CS = (ushort)(PA_Read(target_ea + 2) | (PA_Read(target_ea + 3) << 8));
                                break;

                            //JMP : inderict within segment
                            case 0x20:
                                is_W = true; //fixed
                                Reg_IP = modreg_ReadTool();
                                break;

                            //JMP : inderect intersegment
                            case 0x28:
                                MessageBox.Show("0x28 need check!");
                                Reg_IP = (ushort)(Mem_DS_r16(target_ea) | (Mem_DS_r16(target_ea + 1) << 8));
                                Reg_CS = (ushort)(Mem_DS_r16(target_ea + 1) | (Mem_DS_r16(target_ea + 2) << 8));
                                break;

                            //PUSH :  register/memory
                            case 0x30:
                                Mem_SS_w16((Reg_SP -= 2), modreg_ReadTool());
                                break;

                            case 0x38:
                                opcode_inf(true);
                                break;
                        }
                    }
                    break;

                //PUSH : register
                case 0x50:
                case 0x51:
                case 0x52:
                case 0x53:
                case 0x54:
                case 0x55:
                case 0x56:
                case 0x57:
                    Mem_SS_w16((Reg_SP -= 2), Table_WordRegsGet(ins_byte1 & 7));
                    break;

                //PUSH : segment register
                case 0x6:
                case 0xe:
                case 0x16:
                case 0x1e:
                        Mem_SS_w16((Reg_SP -= 2), Table_SegRegsGet((ins_byte1 & 0x18) >> 3));//fixed 2016.09.05                    
                    break;

                //POP : register/memory
                case 0x8f:
                    modregrm_parse();
                    is_W = true;
                    modrm_WriteTool(pop());
                    break;

                //POP : Register
                case 0x58:
                case 0x59:
                case 0x5a:
                case 0x5b:
                case 0x5d:
                case 0x5e:
                case 0x5f:
                    Table_WordRegsSet(ins_byte1 & 0x7, pop());
                    break;

                //POP : Segment Register
                case 0x7:
                case 0xf:
                case 0x17:
                case 0x1f:
                    Table_SegRegsSet((ins_byte1 & 0x18) >> 3, pop());
                    break;

                //XCHG : Register/memory with register
                case 0x86:
                case 0x87:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    modregrm_parse();
                    RegIndex = (ins_byte2 & 0x38) >> 3;
                    if (is_W)
                    {
                        tmp_w1 = modreg_ReadTool();
                        modrm_WriteTool(Table_WordRegsGet(RegIndex));
                        Table_WordRegsSet(RegIndex, tmp_w1);
                    }
                    else
                    {
                        tmp_b1 = (byte)modreg_ReadTool();
                        modrm_WriteTool(Table_ByteRegsGet(RegIndex));
                        Table_ByteRegsSet(RegIndex, tmp_b1);
                    }
                    break;

                //in : fixed port
                case 0xe4:
                case 0xe5:
                    if ((ins_byte1 & 1) > 0)
                        Reg_A.X = IO_read_word(Mem_CS_r8(Reg_IP++));
                    else
                        Reg_A.L = IO_read_byte(Mem_CS_r8(Reg_IP++));
                    break;

                //in : variable port
                case 0xec:
                case 0xed:
                    if ((ins_byte1 & 1) > 0)
                        Reg_A.X = IO_read_word(Reg_D.X);
                    else
                        Reg_A.L = IO_read_byte(Reg_D.X);
                    break;

                //out : fixed port
                case 0xe6:
                case 0xe7:
                    if ((ins_byte1 & 1) > 0)
                        IO_write_word(Mem_CS_r8(Reg_IP++), Reg_A.X);
                    else
                        IO_write_byte(Mem_CS_r8(Reg_IP++), Reg_A.L);
                    break;

                //out : variable port
                case 0xee:
                case 0xef:
                    if ((ins_byte1 & 1) > 0)
                        IO_write_word(Reg_D.X, Reg_A.X);
                    else
                        IO_write_byte(Reg_D.X, Reg_A.L);
                    break;

                //NOP (XCHG AX <--> AX )
                case 0x90:
                    break;//do nothing

                //XCGH : Register with accumulator
                case 0x91: //CX
                case 0x92: //DX
                case 0x93: //BX
                case 0x94: //SP
                case 0x95: //BP
                case 0x96: //SI
                case 0x97: //DI                    
                    tmp_w1 = Table_WordRegsGet(ins_byte1 & 7);
                    Table_WordRegsSet(ins_byte1 & 7, Reg_A.X);
                    Reg_A.X = tmp_w1;
                    break;

                //XLAT
                case 0xd7:
                    Reg_A.L = PA_Read((Reg_DS << 4) + Reg_B.X + Reg_A.L);
                    break;

                //LEA ??
                case 0x8d:
                    modregrm_parse();
                    Table_WordRegsSet((ins_byte2 & 0x38) >> 3, (ushort)target_ea);
                    break;

                //LDS
                case 0xc5:
                    modregrm_parse();
                    Table_WordRegsSet((ins_byte2 & 0x38) >> 3, (ushort)(PA_Read(target_ea) | (PA_Read(target_ea + 1) << 8)));
                    Reg_DS = (ushort)(PA_Read(target_ea + 2) | (PA_Read(target_ea + 3) << 8));
                    break;

                //LES
                case 0xc4:
                    modregrm_parse();
                    Table_WordRegsSet((ins_byte2 & 0x38) >> 3, (ushort)(PA_Read(target_ea) | (PA_Read(target_ea + 1) << 8)));
                    Reg_ES = (ushort)(PA_Read(target_ea + 2) | (PA_Read(target_ea + 3) << 8));
                    break;

                //LAHF flag -> ah   (S,7)  (Z,6)  (A,4)  (P,2) (C,0)
                case 0x9f:
                    Reg_A.H = (byte)(((flag_S) ? 0x80 : 0) | ((flag_Z) ? 0x40 : 0) | ((flag_A) ? 0x10 : 0) | ((flag_P) ? 4 : 0) | ((flag_C) ? 1 : 0));
                    break;

                //SAHF ah->flag
                case 0x9e:
                    flag_S = ((Reg_A.H & 0x80) > 0) ? true : false;
                    flag_Z = ((Reg_A.H & 0x40) > 0) ? true : false;
                    flag_A = ((Reg_A.H & 0x10) > 0) ? true : false;
                    flag_P = ((Reg_A.H & 4) > 0) ? true : false;
                    flag_C = ((Reg_A.H & 1) > 0) ? true : false;
                    break;

                //PUSHF
                case 0x9c:
                    //tmp_w1 = (ushort) (0xf800 | GetFlag());
                    Mem_SS_w16((Reg_SP -= 2), (ushort)(0xf800 | GetFlag()));
                    break;

                //POPF
                case 0x9d:
                    flag_decode(pop());
                    break;


                #region string manipulation
                //REP
                case 0xf2:
                case 0xf3:
                    retype = ((ins_byte1 & 1) > 0) ? 1 : 2;
                    prefix_effect_step = 1;
                    break;

                //MOVS
                case 0xa4:
                case 0xa5:
                    Console.WriteLine("MOVS! need fix");
                    //MessageBox.Show("MOVS! need fix");
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = 1;
                        if (retype != 0)
                            tmp_w1 = Reg_C.X;
                        for (_i = 0; _i < tmp_w1; _i++)
                        {
                            Mem_ES_w16(Reg_DI, Mem_DS_r16(Reg_SI));
                            if (flag_D)
                            {
                                Reg_SI -= 2;
                                Reg_DI -= 2;
                            }
                            else
                            {
                                Reg_SI += 2;
                                Reg_DI += 2;
                            }
                            if (retype != 0) Reg_C.X--;
                        }
                    }
                    else
                    {
                        tmp_w1 = 1;
                        if (retype != 0)
                            tmp_w1 = Reg_C.X;

                        for (_i = 0; _i < tmp_w1; _i++)
                        {
                            Mem_ES_w8(Reg_DI, Mem_DS_r8(Reg_SI));
                            if (flag_D)
                            {
                                Reg_SI -= 1;
                                Reg_DI -= 1;
                            }
                            else
                            {
                                Reg_SI += 1;
                                Reg_DI += 1;
                            }
                            if (retype != 0) Reg_C.X--;
                        }
                    }
                    retype = 0;
                    break;

                //CMPS
                case 0xa6:
                case 0xa7:
                    MessageBox.Show("CMPS! need fix");
                    retype = 0;
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = 1;
                        if (retype != 0)
                            tmp_w1 = Reg_C.X;

                        for (_i = 0; _i < tmp_w1; _i++)
                        {
                            tmp_w1 = Mem_DS_r16(Reg_SI);
                            tmp_w2 = Mem_ES_r16(Reg_DI);
                            tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                            tmp_w3 = (ushort)tmp_dw1;
                            Reg_A.X = tmp_w3;

                            flag_Z = (tmp_w3 == 0) ? true : false;
                            flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                            flag_P = Table_Paritys[tmp_w3 & 0xff];

                            flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                            flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                            flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;

                            if (flag_D)
                            {
                                Reg_SI -= 2;
                                Reg_DI -= 2;
                            }
                            else
                            {
                                Reg_SI += 2;
                                Reg_DI += 2;
                            }
                            if (retype != 0)
                            {
                                Reg_C.X--;
                                if (retype == 1 && !flag_Z) break;
                                if (retype == 2 && flag_Z) break;
                            }
                        }
                    }
                    else
                    {
                        tmp_w1 = 1;
                        if (retype != 0)
                            tmp_w1 = Reg_C.X;

                        for (_i = 0; _i < tmp_w1; _i++)
                        {
                            tmp_b1 = Mem_DS_r8(Reg_SI);
                            tmp_b2 = Mem_ES_r8(Reg_DI);
                            tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                            tmp_b3 = (byte)tmp_w1;
                            Reg_A.L = tmp_b3;

                            flag_Z = (tmp_b3 == 0) ? true : false;
                            flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                            flag_P = Table_Paritys[tmp_b3];

                            flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                            flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;
                            flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;

                            if (flag_D)
                            {
                                Reg_SI -= 1;
                                Reg_DI -= 1;
                            }
                            else
                            {
                                Reg_SI += 1;
                                Reg_DI += 1;
                            }
                            if (retype != 0)
                            {
                                Reg_C.X--;
                                if (retype == 1 && !flag_Z) break;
                                if (retype == 2 && flag_Z) break;
                            }
                        }
                    }
                    retype = 0;
                    break;

                //SCAS
                case 0xae:
                case 0xaf:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        if (retype != 0)
                        {
                            if (retype == 1 && !flag_Z) break;
                            if (retype == 2 && flag_Z) break;

                            if (Reg_C.X == 0)
                            {
                                retype = 0;
                                break;
                            }

                            tmp_w1 = Reg_A.X;
                            tmp_w2 = Mem_ES_r16(Reg_DI);
                            tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                            tmp_w3 = (ushort)tmp_dw1;

                            flag_Z = (tmp_w3 == 0) ? true : false;
                            flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                            flag_P = Table_Paritys[tmp_w3 & 0xff];

                            flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                            flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_w1 ^ tmp_w2) & 0x8000) != 0) ? true : false; //fixed
                            flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;


                            Reg_IP -= 2;
                            Reg_C.X--;

                            if (flag_D)
                                Reg_DI -= 2;
                            else
                                Reg_DI += 2;

                        }
                        else
                        {

                            tmp_w1 = Reg_A.X;
                            tmp_w2 = Mem_ES_r16(Reg_DI);
                            tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                            tmp_w3 = (ushort)tmp_dw1;

                            flag_Z = (tmp_w3 == 0) ? true : false;
                            flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                            flag_P = Table_Paritys[tmp_w3 & 0xff];

                            flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                            flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_w1 ^ tmp_w2) & 0x8000) != 0) ? true : false; //fixed
                            flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;

                            if (flag_D)
                                Reg_DI -= 2;
                            else
                                Reg_DI += 2;
                        }

                    }
                    else
                    {
                        if (retype != 0)
                        {
                            if (retype == 1 && !flag_Z) break;
                            if (retype == 2 && flag_Z) break;

                            if (Reg_C.X == 0)
                            {
                                retype = 0;
                                break;
                            }

                            tmp_b1 = Reg_A.L;
                            tmp_b2 = Mem_ES_r8(Reg_DI);
                            tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                            tmp_b3 = (byte)tmp_w1;

                            flag_Z = (tmp_b3 == 0) ? true : false;
                            flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                            flag_P = Table_Paritys[tmp_b3];

                            flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                            flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) != 0) ? true : false;
                            flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;

                            Reg_IP -= 2;
                            Reg_C.X--;

                            if (flag_D)
                                Reg_DI -= 1;
                            else
                                Reg_DI += 1;

                        }
                        else
                        {

                            tmp_b1 = Reg_A.L;
                            tmp_b2 = Mem_ES_r8(Reg_DI);
                            tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                            tmp_b3 = (byte)tmp_w1;


                            flag_Z = (tmp_b3 == 0) ? true : false;
                            flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                            flag_P = Table_Paritys[tmp_b3];

                            flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                            flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) != 0) ? true : false;
                            flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;


                            if (flag_D)
                                Reg_DI -= 1;
                            else
                                Reg_DI += 1;
                        }
                    }
                    retype = 0;
                    break;

                //LODS
                case 0xac:
                case 0xad:
                    //MessageBox.Show("LODS! need fix");
                    Console.WriteLine("LODS! need fix");
                    retype = 0;
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = 1;
                        if (retype != 0)
                            tmp_w1 = Reg_C.X;

                        for (_i = 0; _i < tmp_w1; _i++)
                        {
                            Reg_A.X = Mem_DS_r16(Reg_SI);

                            if (flag_D)
                                Reg_SI -= 2;
                            else
                                Reg_SI += 2;

                            if (retype != 0)
                                Reg_C.X--;
                        }
                    }
                    else
                    {
                        tmp_w1 = 1;
                        if (retype != 0)
                            tmp_w1 = Reg_C.X;

                        for (_i = 0; _i < tmp_w1; _i++)
                        {
                            Reg_A.L = Mem_DS_r8(Reg_SI);

                            if (flag_D)
                                Reg_SI -= 1;
                            else
                                Reg_SI += 1;

                            if (retype != 0)
                                Reg_C.X--;
                        }
                    }
                    retype = 0;
                    break;

                //STOS
                case 0xaa:
                case 0xab:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        //正確範本
                        if (retype != 0)
                        {
                            if (Reg_C.X == 0)
                            {
                                retype = 0;
                                break;
                            }

                            Mem_ES_w16(Reg_DI, Reg_A.X);

                            Reg_IP -= 2;
                            Reg_C.X--;

                            if (flag_D)
                                Reg_DI -= 2;
                            else
                                Reg_DI += 2;
                        }
                        else
                        {
                            Mem_ES_w16(Reg_DI, Reg_A.X);
                            if (flag_D)
                                Reg_DI -= 2;
                            else
                                Reg_DI += 2;
                        }
                    }
                    else
                    {
                        if (retype != 0)
                        {
                            if (Reg_C.X == 0)
                            {
                                retype = 0;
                                //trace = true;
                                break;
                            }

                            Mem_ES_w8(Reg_DI, Reg_A.L);

                            Reg_IP -= 2;
                            Reg_C.X--;

                            if (flag_D)
                                Reg_DI -= 1;
                            else
                                Reg_DI += 1;

                        }
                        else
                        {
                            Mem_ES_w8(Reg_DI, Reg_A.L);

                            if (flag_D)
                                Reg_DI -= 1;
                            else
                                Reg_DI += 1;
                        }
                    }
                    break;

                #endregion

                #region Arithmetic

                //INC  : register
                case 0x40:
                case 0x41:
                case 0x42:
                case 0x43:
                case 0x44:
                case 0x45:
                case 0x46:
                case 0x47:
                    tmp_w1 = Table_WordRegsGet(ins_byte1 & 7); //fix
                    tmp_w2 = 1;
                    tmp_dw1 = (uint)(tmp_w1 + tmp_w2);
                    tmp_w3 = (ushort)tmp_dw1;

                    Table_WordRegsSet(ins_byte1 & 7, tmp_w3);//fix

                    flag_Z = (tmp_w3 == 0) ? true : false;
                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                    flag_P = Table_Paritys[tmp_w3 & 0xff];

                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    break;

                //DEC : register
                case 0x48:
                case 0x49:
                case 0x4a:
                case 0x4b:
                case 0x4c:
                case 0x4d:
                case 0x4e:
                case 0x4f:
                    tmp_w1 = Table_WordRegsGet(ins_byte1 & 7);
                    tmp_w2 = 1;
                    tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                    tmp_w3 = (ushort)tmp_dw1;

                    Table_WordRegsSet(ins_byte1 & 7, tmp_w3);//fix

                    flag_Z = (tmp_w3 == 0) ? true : false;
                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                    flag_P = Table_Paritys[tmp_w3 & 0xff];
                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    break;

                //AAA
                case 0x37:
                    if (((Reg_A.L & 0xf) > 9) || (flag_A == true))
                    {
                        Reg_A.L = (byte)((Reg_A.L + 6) & 0xf);
                        flag_A = true;
                    }
                    else
                        flag_A = false;
                    break;

                //DAA
                case 0x27:
                    if (((Reg_A.L & 0xf) > 9) || (flag_A == true))
                    {
                        Reg_A.L = (byte)(Reg_A.L + 6);
                        flag_A = true;
                    }
                    else
                        flag_A = false;
                    if ((Reg_A.L > 0x9f) || (flag_C == true))
                    {
                        Reg_A.L = (byte)(Reg_A.L + 0x60);
                        flag_C = true;
                    }
                    else
                        flag_C = false;
                    break;

                //AAS
                case 0x3f:

                    if (((Reg_A.L & 0xf) > 9) || (flag_A == true))
                    {
                        Reg_A.L = (byte)((Reg_A.L - 6) & 0xf);
                        Reg_A.H--;
                        flag_A = flag_C = true;
                    }
                    else
                        flag_A = flag_C = false;
                    break;

                //DAS
                case 0x2f:
                    if (((Reg_A.L & 0xf) > 9) || (flag_A == true))
                    {
                        Reg_A.L = (byte)(Reg_A.L - 6);
                        flag_A = true;
                    }
                    else
                        flag_A = false;
                    if ((Reg_A.L > 0x9f) || (flag_C == true))
                    {
                        Reg_A.L = (byte)(Reg_A.L - 0x60);
                        flag_C = true;
                    }
                    else
                        flag_C = false;
                    break;

                //AAM
                case 0xd4:
                    //tmp_b1 = Mem_CS_r8(Reg_IP++); alwasy 10 , so ignore
                    Reg_IP++;
                    Reg_A.H = (byte)(Reg_A.L / 10);
                    Reg_A.L = (byte)(Reg_A.L % 10);
                    flag_Z = (Reg_A.X == 0) ? true : false;
                    flag_S = ((Reg_A.X & 0x8000) > 0) ? true : false;
                    flag_P = Table_Paritys[Reg_A.L];
                    break;

                //AAD
                case 0xd5:
                    //tmp_b1 = Mem_CS_r8(Reg_IP++); always 10 , so ignore
                    Reg_IP++;
                    Reg_A.L = (byte)(Reg_A.H * 10 + Reg_A.L);
                    Reg_A.H = 0;
                    tmp_w1 = (ushort)(Reg_A.H * 10 + Reg_A.L);
                    flag_Z = (tmp_w1 == 0) ? true : false;
                    flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                    flag_P = Table_Paritys[tmp_w1 & 0xff];
                    break;

                //CBW
                case 0x98:
                    Reg_A.X = (ushort)((short)((sbyte)Reg_A.L));
                    break;

                //CWD
                case 0x99:
                    Reg_D.X = (ushort)(((Reg_A.H & 0x80) > 0) ? 0xffff : 0);
                    break;

                //has sub opcode
                case 0xd0:
                case 0xd1:
                case 0xd2:
                case 0xd3:
                    {
                        modregrm_parse();
                        is_W = ((ins_byte1 & 1) > 0) ? true : false;
                        is_V = ((ins_byte1 & 2) > 0) ? true : false;
                        cnt = ((is_V) ? Reg_C.L : 1) & 0x1f;
                        switch (ins_byte2 & 0x38)
                        {
                            case 0: //ROL
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        flag_C = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                        tmp_w1 <<= 1;
                                        tmp_w1 |= (ushort)((flag_C) ? 1 : 0);
                                    }
                                    if (!is_V)
                                        flag_O = (((tmp_w1 & 0x8000) > 0) && flag_C) ? true : false;
                                    else
                                        flag_O = false;
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        flag_C = ((tmp_w1 & 0x80) > 0) ? true : false;
                                        tmp_b1 <<= 1;
                                        tmp_b1 |= (byte)((flag_C) ? 1 : 0);
                                    }
                                    if (!is_V)
                                        flag_O = (((tmp_b1 & 0x80) > 0) && flag_C) ? true : false;
                                    else
                                        flag_O = false;
                                }
                                break;

                            case 8://ROR

                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        flag_C = ((tmp_w1 & 1) > 0) ? true : false;
                                        tmp_w1 = (ushort)((tmp_w1 >> 1) | (((flag_C) ? 1 : 0) << 15));
                                    }
                                    modrm_WriteTool(tmp_w1);
                                    if (!is_V) flag_O = (((tmp_b1 >> 15) ^ ((tmp_b1 >> 14) & 1)) > 0) ? true : false;
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        flag_C = ((tmp_b1 & 1) > 0) ? true : false;
                                        tmp_b1 = (byte)((tmp_b1 >> 1) | (((flag_C) ? 1 : 0) << 7)); //fix
                                    }
                                    modrm_WriteTool(tmp_b1);
                                    if (!is_V) flag_O = (((tmp_b1 >> 7) ^ ((tmp_b1 >> 6) & 1)) > 0) ? true : false;
                                }
                                break;

                            case 0x10://RCL
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        tmp_flag = flag_C;
                                        flag_C = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                        tmp_w1 <<= 1;
                                        tmp_w1 |= (ushort)((tmp_flag) ? 1 : 0);
                                    }
                                    modrm_WriteTool(tmp_w1);
                                    if (!is_V) flag_O = (((flag_C) ? 1 : 0) ^ ((tmp_w1 >> 15) & 1)) > 0 ? true : false;
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        tmp_flag = flag_C;
                                        flag_C = ((tmp_b1 & 0x80) > 0) ? true : false;
                                        tmp_b1 <<= 1;
                                        tmp_b1 |= (byte)((tmp_flag) ? 1 : 0);
                                    }
                                    modrm_WriteTool(tmp_b1);
                                    if (!is_V) flag_O = (((flag_C) ? 1 : 0) ^ ((tmp_b1 >> 7) & 1)) > 0 ? true : false;
                                }
                                break;

                            case 0x18://RCR
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        tmp_flag = flag_C;
                                        flag_C = ((tmp_w1 & 1) > 0) ? true : false;
                                        tmp_w1 = (byte)((tmp_w1 >> 1) | (((flag_C) ? 1 : 0) << 15));
                                    }

                                    modrm_WriteTool(tmp_w1);

                                    if (!is_V) flag_O = (((tmp_w1 >> 15) ^ ((tmp_w1 >> 14) & 1)) > 0) ? true : false;
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        tmp_flag = flag_C;
                                        flag_C = ((tmp_b1 & 1) > 0) ? true : false;
                                        tmp_b1 = (byte)((tmp_b1 >> 1) | (((flag_C) ? 1 : 0) << 7));
                                    }

                                    modrm_WriteTool(tmp_b1);

                                    if (!is_V) flag_O = (((tmp_b1 >> 7) ^ ((tmp_b1 >> 6) & 1)) > 0) ? true : false;
                                }
                                break;

                            case 0x20:// SAL SHL 4
                                if (is_W)
                                {

                                    tmp_w1 = modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        flag_C = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                        tmp_w1 = (ushort)((tmp_w1 << 1) & 0xFFFF);
                                    }
                                    modrm_WriteTool(tmp_w1);

                                    flag_O = ((!is_V) && (((flag_C) ? 1 : 0) == (tmp_w1 >> 15))) ? false : true; //!!! false : true 

                                    flag_Z = (tmp_w1 == 0) ? true : false;
                                    flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w1 & 0xff];
                                }
                                else
                                {

                                    tmp_b1 = (byte)modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        flag_C = ((tmp_b1 & 0x80) > 0) ? true : false;
                                        tmp_b1 = (byte)((tmp_b1 << 1) & 0xFF);
                                    }

                                    modrm_WriteTool(tmp_b1);

                                    flag_O = ((!is_V) && (((flag_C) ? 1 : 0) == (tmp_b1 >> 7))) ? false : true; //fixed

                                    flag_Z = (tmp_b1 == 0) ? true : false;
                                    flag_S = ((tmp_b1 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b1];
                                }
                                break;

                            case 0x28://SHR 5
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    flag_O = ((!is_V) && ((tmp_w1 & 0x8000) > 0)) ? true : false;

                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        flag_C = ((tmp_w1 & 1) > 0) ? true : false;
                                        tmp_w1 >>= 1;
                                    }
                                    modrm_WriteTool(tmp_w1);
                                    flag_Z = (tmp_w1 == 0) ? true : false;
                                    flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w1 & 0xff];
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();

                                    flag_O = ((!is_V) && ((tmp_b1 & 0x80) > 0)) ? true : false;

                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        flag_C = ((tmp_b1 & 1) > 0) ? true : false;
                                        tmp_b1 >>= 1;
                                    }


                                    modrm_WriteTool(tmp_b1);


                                    flag_Z = (tmp_b1 == 0) ? true : false;
                                    flag_S = ((tmp_b1 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b1];
                                }
                                break;

                            case 0x30: //6
                                opcode_inf(true);
                                break;

                            case 0x38://SAR 7


                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        tmp_w2 = (byte)(tmp_w1 & 0x80);
                                        flag_C = ((tmp_w1 & 1) > 0) ? true : false;
                                        tmp_w1 = (byte)((tmp_w1 >> 1) | tmp_w2);
                                    }

                                    modrm_WriteTool(tmp_w1);
                                    flag_O = false;
                                    flag_Z = (tmp_w1 == 0) ? true : false;
                                    flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w1 & 0xff];
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    for (_i = 0; _i < cnt; _i++)
                                    {
                                        tmp_b2 = (byte)(tmp_b1 & 0x80);
                                        flag_C = ((tmp_b1 & 1) > 0) ? true : false;
                                        tmp_b1 = (byte)((tmp_b1 >> 1) | tmp_b2);
                                    }

                                    modrm_WriteTool(tmp_b1);

                                    flag_O = false;
                                    flag_Z = (tmp_b1 == 0) ? true : false;
                                    flag_S = ((tmp_b1 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b1];
                                }


                                break;
                        }
                    }
                    break;

                //has sub opcode
                case 0xf6:
                case 0xf7:
                    {

                        modregrm_parse();
                        is_W = ((ins_byte1 & 1) > 0) ? true : false;



                        switch (ins_byte2 & 0x38)
                        {
                            case 0: //TEST
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w1 &= (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                                    flag_C = flag_O = false;
                                    flag_Z = (tmp_w1 == 0) ? true : false;
                                    flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w1 & 0xff];
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b1 &= Mem_CS_r8(Reg_IP++);
                                    flag_C = flag_O = false;
                                    flag_Z = (tmp_b1 == 0) ? true : false;
                                    flag_S = ((tmp_b1 & 0x80) > 0) ? true : false;

                                    flag_P = Table_Paritys[tmp_b1 & 0xff]; //fixed
                                }
                                break;

                            case 8://
                                opcode_inf(true);
                                break;

                            case 0x10://NOT
                                modrm_WriteTool((ushort)~modreg_ReadTool());
                                break;

                            case 0x18://NEG
                                if (is_W)
                                {
                                    tmp_w1 = 0;
                                    tmp_w2 = (ushort)modreg_ReadTool();

                                    tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                                    tmp_w3 = (ushort)tmp_dw1;

                                    modrm_WriteTool(tmp_w3);

                                    flag_Z = (tmp_w3 == 0) ? true : false;
                                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w3 & 0xff];

                                    flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                                }
                                else
                                {
                                    tmp_b1 = 0;
                                    tmp_b2 = (byte)~modreg_ReadTool();

                                    tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                                    tmp_b3 = (byte)tmp_w1;

                                    modrm_WriteTool(tmp_b3);

                                    flag_Z = (tmp_b3 == 0) ? true : false;
                                    flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b3];

                                    flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                                    flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;
                                    flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                                }
                                break;

                            case 0x20://MUL
                                if (is_W)
                                {
                                    tmp_dw1 = (uint)Reg_A.X * modreg_ReadTool();
                                    Reg_A.X = (ushort)tmp_dw1;
                                    Reg_D.X = (ushort)(tmp_dw1 >> 16);

                                    flag_Z = (Reg_A.X == 0) ? true : false;
                                    flag_S = ((Reg_A.X & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[Reg_A.X & 0xff];

                                    if (Reg_D.X > 0)
                                        flag_C = flag_O = true;
                                    else
                                        flag_C = flag_O = false;
                                }
                                else
                                {

                                    Reg_A.X = (ushort)(Reg_A.L * modreg_ReadTool());

                                    flag_Z = (Reg_A.L == 0) ? true : false;
                                    flag_S = ((Reg_A.L & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[Reg_A.L];

                                    if (Reg_A.H > 0)
                                        flag_C = flag_O = true;
                                    else
                                        flag_C = flag_O = false;
                                }
                                break;

                            case 0x28://IMUL
                                if (is_W)
                                {
                                    tmp_idw1 = (short)Reg_A.X * (short)modreg_ReadTool();
                                    Reg_A.X = (ushort)tmp_idw1;
                                    Reg_D.X = (ushort)(tmp_idw1 >> 16);

                                    flag_Z = (Reg_A.X == 0) ? true : false;
                                    flag_S = ((Reg_A.X & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[Reg_A.X & 0xff];

                                    if (Reg_D.X > 0)
                                        flag_C = flag_O = true;
                                    else
                                        flag_C = flag_O = false;
                                }
                                else
                                {
                                    Reg_A.X = (ushort)((sbyte)Reg_A.L * (short)modreg_ReadTool());

                                    flag_Z = (Reg_A.L == 0) ? true : false;
                                    flag_S = ((Reg_A.L & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[Reg_A.L];

                                    if (Reg_A.H != 0)
                                        flag_C = flag_O = true;
                                    else
                                        flag_C = flag_O = false;
                                }
                                break;

                            case 0x30://DIV
                                if (is_W)
                                {
                                    tmp_dw1 = (uint)((Reg_D.X << 16) | Reg_A.X);
                                    tmp_w1 = modreg_ReadTool();
                                    if (tmp_w1 == 0)
                                    {
                                        interrupt(0);
                                        return;
                                    }
                                    Reg_A.X = (ushort)(tmp_dw1 / tmp_w1);
                                    Reg_D.X = (ushort)(tmp_dw1 % tmp_w1);
                                }
                                else
                                {
                                    tmp_w1 = Reg_A.X;
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    if (tmp_b1 == 0)
                                    {
                                        interrupt(0);
                                        return;
                                    }
                                    Reg_A.L = (byte)(Reg_A.X / tmp_b1);
                                    Reg_A.H = (byte)(tmp_w1 / tmp_b1);
                                }
                                break;

                            case 0x38://IDVI
                                if (is_W)
                                {
                                    tmp_idw1 = ((Reg_D.X << 16) | Reg_A.X);
                                    tmp_w1 = modreg_ReadTool();
                                    if (tmp_w1 == 0)
                                    {
                                        interrupt(0);
                                        return;
                                    }
                                    Reg_A.X = (ushort)(tmp_idw1 / tmp_w1);
                                    Reg_D.X = (ushort)(tmp_idw1 % tmp_w1);
                                }
                                else
                                {
                                    tmp_w1 = Reg_A.X;
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    if (tmp_b1 == 0)
                                    {
                                        interrupt(0);
                                        return;
                                    }
                                    Reg_A.L = (byte)(((sbyte)Reg_A.X) / tmp_b1);
                                    Reg_A.H = (byte)(((sbyte)tmp_w1) / tmp_b1);
                                }
                                break;

                        }
                    }
                    break;

                //ADD : immediate to Accumulator
                case 0x4:
                case 0x5:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = Reg_A.X;
                        tmp_w2 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        tmp_dw1 = (uint)(tmp_w1 + tmp_w2);
                        tmp_w3 = (ushort)tmp_dw1;
                        Reg_A.X = tmp_w3;

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        tmp_b1 = Reg_A.L;
                        tmp_b2 = Mem_CS_r8(Reg_IP++);
                        tmp_w1 = (ushort)(tmp_b1 + tmp_b2);
                        tmp_b3 = (byte)tmp_w1;
                        Reg_A.L = tmp_b3;

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;

                //ADC : immediate to Accumulator
                case 0x14:
                case 0x15:
                    if (is_W)
                    {
                        tmp_w1 = Reg_A.X;
                        tmp_w2 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        tmp_dw1 = (uint)(tmp_w1 + tmp_w2 + ((flag_C) ? 1 : 0));
                        tmp_w3 = (ushort)tmp_dw1;
                        Reg_A.X = tmp_w3;

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        tmp_b1 = Reg_A.L;
                        tmp_b2 = Mem_CS_r8(Reg_IP++);
                        tmp_w1 = (ushort)(tmp_b1 + tmp_b2 + ((flag_C) ? 1 : 0));
                        tmp_b3 = (byte)tmp_w1;
                        Reg_A.L = tmp_b3;

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;

                //SUB : immediate to Accumulator
                case 0x2c:
                case 0x2d:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = Reg_A.X;
                        tmp_w2 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                        tmp_w3 = (ushort)tmp_dw1;
                        Reg_A.X = tmp_w3;

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        tmp_b1 = Reg_A.L;
                        tmp_b2 = Mem_CS_r8(Reg_IP++);
                        tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                        tmp_b3 = (byte)tmp_w1;
                        Reg_A.L = tmp_b3;

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;

                //SBB : immediate to Accumulator
                case 0x1c:
                case 0x1d:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = Reg_A.X;
                        tmp_w2 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        tmp_dw1 = (uint)(tmp_w1 - (tmp_w2 + ((flag_C) ? 1 : 0)));
                        tmp_w3 = (ushort)tmp_dw1;
                        Reg_A.X = tmp_w3;

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        tmp_b1 = Reg_A.L;
                        tmp_b2 = Mem_CS_r8(Reg_IP++);
                        tmp_w1 = (ushort)(tmp_b1 - (tmp_b2 + ((flag_C) ? 1 : 0)));
                        tmp_b3 = (byte)tmp_w1;
                        Reg_A.L = tmp_b3;

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;

                //AND : immediate to Accumulator
                case 0x24:
                case 0x25:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {

                        tmp_w1 = Reg_A.X;
                        tmp_w2 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        Reg_A.X = (ushort)(tmp_w1 & tmp_w2);
                        flag_C = flag_O = false;
                        flag_Z = (Reg_A.X == 0) ? true : false;
                        flag_S = ((Reg_A.X & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[Reg_A.X & 0xff];
                    }
                    else
                    {

                        tmp_b1 = Reg_A.L;
                        tmp_b2 = Mem_CS_r8(Reg_IP++);
                        Reg_A.L = (byte)(tmp_b1 & tmp_b2);
                        flag_C = flag_O = false;
                        flag_Z = (Reg_A.L == 0) ? true : false;
                        flag_S = ((Reg_A.L & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[Reg_A.L];
                    }

                    break;

                //OR : immediate to Accumulator
                case 0xc:
                case 0xd:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {

                        tmp_w1 = Reg_A.X;
                        tmp_w2 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        Reg_A.X = (ushort)(tmp_w1 | tmp_w2);
                        flag_C = flag_O = false;
                        flag_Z = (Reg_A.X == 0) ? true : false;
                        flag_S = ((Reg_A.X & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[Reg_A.X & 0xff];
                    }
                    else
                    {

                        tmp_b1 = Reg_A.L;
                        tmp_b2 = Mem_CS_r8(Reg_IP++);
                        Reg_A.L = (byte)(tmp_b1 | tmp_b2);
                        flag_C = flag_O = false;
                        flag_Z = (Reg_A.L == 0) ? true : false;
                        flag_S = ((Reg_A.L & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[Reg_A.L];
                    }
                    break;

                //XOR : immediate to Accumulator
                case 0x34:
                case 0x35:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = Reg_A.X;
                        tmp_w2 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        Reg_A.X = (ushort)(tmp_w1 ^ tmp_w2);
                        flag_C = flag_O = false;
                        flag_Z = (Reg_A.X == 0) ? true : false;
                        flag_S = ((Reg_A.X & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[Reg_A.X & 0xff];
                    }
                    else
                    {

                        tmp_b1 = Reg_A.L;
                        tmp_b2 = Mem_CS_r8(Reg_IP++);
                        Reg_A.L = (byte)(tmp_b1 ^ tmp_b2);
                        flag_C = flag_O = false;
                        flag_Z = (Reg_A.L == 0) ? true : false;
                        flag_S = ((Reg_A.L & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[Reg_A.L];
                    }

                    break;

                //CMP : immediate to Accumulator
                case 0x3c:
                case 0x3d:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = Reg_A.X;
                        tmp_w2 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                        tmp_w3 = (ushort)tmp_dw1;

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_w1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;//fixed
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        tmp_b1 = Reg_A.L;
                        tmp_b2 = Mem_CS_r8(Reg_IP++);
                        tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                        tmp_b3 = (byte)tmp_w1;

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) == 0x80) ? true : false; //fixed
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;

                //ADD : Reg/memory and register either
                case 0x0:
                case 0x1:
                case 0x2:
                case 0x3:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    is_D = ((ins_byte1 & 2) > 0) ? true : false;
                    if (is_W)
                    {
                        if (is_D) //fixed
                        {
                            tmp_w1 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_w2 = modreg_ReadTool();

                        }
                        else
                        {
                            tmp_w1 = modreg_ReadTool();
                            tmp_w2 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);

                        }
                        tmp_dw1 = (uint)(tmp_w1 + tmp_w2);
                        tmp_w3 = (ushort)tmp_dw1;

                        if (is_D)
                            Table_WordRegsSet((ins_byte2 & 0x38) >> 3, tmp_w3);
                        else
                            modrm_WriteTool(tmp_w3);

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        if (((ins_byte1 & 2) > 0))//fixed
                        {
                            tmp_b1 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_b2 = (byte)modreg_ReadTool();

                        }
                        else
                        {
                            tmp_b1 = (byte)modreg_ReadTool();
                            tmp_b2 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);

                        }

                        tmp_w1 = (ushort)(tmp_b1 + tmp_b2);
                        tmp_b3 = (byte)tmp_w1;

                        if (is_D)
                            Table_ByteRegsSet((ins_byte2 & 0x38) >> 3, tmp_b3);
                        else
                            modrm_WriteTool(tmp_b3);

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;

                //ADC : Reg/memory and register either
                case 0x10:
                case 0x11:
                case 0x12:
                case 0x13:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    is_D = ((ins_byte1 & 2) > 0) ? true : false;
                    if (is_W)
                    {
                        if (is_D) //fixed
                        {
                            tmp_w1 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_w2 = modreg_ReadTool();

                        }
                        else
                        {
                            tmp_w1 = modreg_ReadTool();
                            tmp_w2 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);

                        }
                        tmp_dw1 = (uint)(tmp_w1 + tmp_w2 + ((flag_C) ? 1 : 0));
                        tmp_w3 = (ushort)tmp_dw1;

                        if (is_D)
                            Table_WordRegsSet((ins_byte2 & 0x38) >> 3, tmp_w3);
                        else
                            modrm_WriteTool(tmp_w3);

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_w1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;//fix
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        if (((ins_byte1 & 2) > 0))//fixed
                        {
                            tmp_b1 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_b2 = (byte)modreg_ReadTool();

                        }
                        else
                        {
                            tmp_b1 = (byte)modreg_ReadTool();
                            tmp_b2 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);

                        }

                        tmp_w1 = (ushort)(tmp_b1 + tmp_b2 + ((flag_C) ? 1 : 0));
                        tmp_b3 = (byte)tmp_w1;

                        if (is_D)
                            Table_ByteRegsSet((ins_byte2 & 0x38) >> 3, tmp_b3);
                        else
                            modrm_WriteTool(tmp_b3);

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) == 0x80) ? true : false; //fix
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;

                //SUB : Reg/memory and register either
                case 0x28:
                case 0x29:
                case 0x2a:
                case 0x2b:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    is_D = ((ins_byte1 & 2) > 0) ? true : false;
                    if (is_W)
                    {
                        if (is_D) //fixed
                        {
                            tmp_w1 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_w2 = modreg_ReadTool();
                        }
                        else
                        {
                            tmp_w1 = modreg_ReadTool();
                            tmp_w2 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                        }
                        tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                        tmp_w3 = (ushort)tmp_dw1;
                        if (is_D)
                            Table_WordRegsSet((ins_byte2 & 0x38) >> 3, tmp_w3);
                        else
                            modrm_WriteTool(tmp_w3);

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_w1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false; //fixed
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        if (((ins_byte1 & 2) > 0))//fixed
                        {
                            tmp_b1 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_b2 = (byte)modreg_ReadTool();
                        }
                        else
                        {
                            tmp_b1 = (byte)modreg_ReadTool();
                            tmp_b2 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                        }

                        tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                        tmp_b3 = (byte)tmp_w1;

                        if (is_D)
                            Table_ByteRegsSet((ins_byte2 & 0x38) >> 3, tmp_b3);
                        else
                            modrm_WriteTool(tmp_b3);

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;//fixed
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;

                //SBB : Reg/memory and register either
                case 0x18:
                case 0x19:
                case 0x1a:
                case 0x1b:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    is_D = ((ins_byte1 & 2) > 0) ? true : false;
                    if (is_W)
                    {
                        if (is_D) //fixed
                        {
                            tmp_w1 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_w2 = modreg_ReadTool();

                        }
                        else
                        {
                            tmp_w1 = modreg_ReadTool();
                            tmp_w2 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);

                        }
                        tmp_dw1 = (uint)(tmp_w1 - (tmp_w2 + ((flag_C) ? 1 : 0)));
                        tmp_w3 = (ushort)tmp_dw1;

                        if (is_D)
                            Table_WordRegsSet((ins_byte2 & 0x38) >> 3, tmp_w3);
                        else
                            modrm_WriteTool(tmp_w3);

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_w1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false; //fix
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        if (((ins_byte1 & 2) > 0))//fixed
                        {
                            tmp_b1 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_b2 = (byte)modreg_ReadTool();

                        }
                        else
                        {
                            tmp_b1 = (byte)modreg_ReadTool();
                            tmp_b2 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);

                        }

                        tmp_w1 = (ushort)(tmp_b1 - (tmp_b2 + ((flag_C) ? 1 : 0)));
                        tmp_b3 = (byte)tmp_w1;

                        if (is_D)
                            Table_ByteRegsSet((ins_byte2 & 0x38) >> 3, tmp_b3);
                        else
                            modrm_WriteTool(tmp_b3);

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) == 0x80) ? true : false; //fix
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;

                //AND : Reg/memory and register either
                case 0x20:
                case 0x21:
                case 0x22:
                case 0x23:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    is_D = ((ins_byte1 & 2) > 0) ? true : false;
                    if (is_W)
                    {
                        if (is_D) //fixed
                        {
                            tmp_w1 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_w2 = modreg_ReadTool();

                        }
                        else
                        {
                            tmp_w1 = modreg_ReadTool();
                            tmp_w2 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);

                        }
                        tmp_dw1 = (uint)(tmp_w1 & tmp_w2);
                        tmp_w3 = (ushort)tmp_dw1;

                        if (is_D)
                            Table_WordRegsSet((ins_byte2 & 0x38) >> 3, tmp_w3);
                        else
                            modrm_WriteTool(tmp_w3);

                        flag_C = flag_O = false;
                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];
                    }
                    else
                    {
                        if (((ins_byte1 & 2) > 0))//fixed
                        {
                            tmp_b1 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_b2 = (byte)modreg_ReadTool();

                        }
                        else
                        {
                            tmp_b1 = (byte)modreg_ReadTool();
                            tmp_b2 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);

                        }

                        tmp_w1 = (ushort)(tmp_b1 & tmp_b2);
                        tmp_b3 = (byte)tmp_w1;

                        if (is_D)
                            Table_ByteRegsSet((ins_byte2 & 0x38) >> 3, tmp_b3);
                        else
                            modrm_WriteTool(tmp_b3);

                        flag_C = flag_O = false;
                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];
                    }
                    break;

                //OR : Reg/memory and register either
                case 0x8:
                case 0x9:
                case 0xa:
                case 0xb:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    is_D = ((ins_byte1 & 2) > 0) ? true : false;
                    if (is_W)
                    {
                        if (is_D) //fixed
                        {
                            tmp_w1 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_w2 = modreg_ReadTool();

                        }
                        else
                        {
                            tmp_w1 = modreg_ReadTool();
                            tmp_w2 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);

                        }
                        tmp_dw1 = (uint)(tmp_w1 | tmp_w2);
                        tmp_w3 = (ushort)tmp_dw1;

                        if (is_D)
                            Table_WordRegsSet((ins_byte2 & 0x38) >> 3, tmp_w3);
                        else
                            modrm_WriteTool(tmp_w3);

                        flag_C = flag_O = false;
                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];
                    }
                    else
                    {
                        if (((ins_byte1 & 2) > 0))//fixed
                        {
                            tmp_b1 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_b2 = (byte)modreg_ReadTool();

                        }
                        else
                        {
                            tmp_b1 = (byte)modreg_ReadTool();
                            tmp_b2 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);

                        }

                        tmp_w1 = (ushort)(tmp_b1 | tmp_b2);
                        tmp_b3 = (byte)tmp_w1;

                        if (is_D)
                            Table_ByteRegsSet((ins_byte2 & 0x38) >> 3, tmp_b3);
                        else
                            modrm_WriteTool(tmp_b3);

                        flag_C = flag_O = false;
                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];
                    }
                    break;

                //XOR : Reg/memory and register either
                case 0x30:
                case 0x31:
                case 0x32:
                case 0x33:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    is_D = ((ins_byte1 & 2) > 0) ? true : false;
                    if (is_W)
                    {
                        if (is_D) //fixed
                        {
                            tmp_w1 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_w2 = modreg_ReadTool();
                        }
                        else
                        {
                            tmp_w1 = modreg_ReadTool();
                            tmp_w2 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                        }
                        tmp_dw1 = (uint)(tmp_w1 ^ tmp_w2);
                        tmp_w3 = (ushort)tmp_dw1;

                        if (is_D)
                            Table_WordRegsSet((ins_byte2 & 0x38) >> 3, tmp_w3);
                        else
                            modrm_WriteTool(tmp_w3);

                        flag_C = flag_O = false;
                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];
                    }
                    else
                    {
                        if (((ins_byte1 & 2) > 0))//fixed
                        {
                            tmp_b1 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_b2 = (byte)modreg_ReadTool();
                        }
                        else
                        {
                            tmp_b1 = (byte)modreg_ReadTool();
                            tmp_b2 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                        }

                        tmp_w1 = (ushort)(tmp_b1 ^ tmp_b2);
                        tmp_b3 = (byte)tmp_w1;

                        if (is_D)
                            Table_ByteRegsSet((ins_byte2 & 0x38) >> 3, tmp_b3);
                        else
                            modrm_WriteTool(tmp_b3);

                        flag_C = flag_O = false;
                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];
                    }
                    break;


                //CMP : Reg/memory and register either
                case 0x38:
                case 0x39:
                case 0x3a:
                case 0x3b:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    is_D = ((ins_byte1 & 2) > 0) ? true : false;
                    if (is_W)
                    {
                        if (is_D) //fixed
                        {
                            tmp_w1 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_w2 = modreg_ReadTool();

                        }
                        else
                        {
                            tmp_w1 = modreg_ReadTool();
                            tmp_w2 = Table_WordRegsGet((ins_byte2 & 0x38) >> 3);

                        }
                        tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                        tmp_w3 = (ushort)tmp_dw1;

                        flag_Z = (tmp_w3 == 0) ? true : false;
                        flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w3 & 0xff];

                        flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                        flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_w1 ^ tmp_w2) & 0x8000) == 0x8000) ? true : false;//fixed
                        flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                    }
                    else
                    {
                        if (((ins_byte1 & 2) > 0))//fixed
                        {
                            tmp_b1 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                            tmp_b2 = (byte)modreg_ReadTool();

                        }
                        else
                        {
                            tmp_b1 = (byte)modreg_ReadTool();
                            tmp_b2 = Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);

                        }

                        tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                        tmp_b3 = (byte)tmp_w1;

                        flag_Z = (tmp_b3 == 0) ? true : false;
                        flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b3];

                        flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                        flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) == 0x80) ? true : false;//fixed
                        flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                    }
                    break;


                //TEST : Register/memory and register
                case 0x84:
                case 0x85:
                    modregrm_parse();
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = modreg_ReadTool();
                        tmp_w1 &= Table_WordRegsGet((ins_byte2 & 0x38) >> 3);
                        flag_C = flag_O = false;
                        flag_Z = (tmp_w1 == 0) ? true : false;
                        flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w1 & 0xff];
                    }
                    else
                    {
                        tmp_b1 = (byte)modreg_ReadTool();
                        tmp_b1 &= Table_ByteRegsGet((ins_byte2 & 0x38) >> 3);
                        flag_C = flag_O = false;
                        flag_Z = (tmp_b1 == 0) ? true : false;
                        flag_S = ((tmp_b1 & 0x80) > 0) ? true : false;
                    }
                    break;

                //TEST : Immediated and accumulator
                case 0xa8:
                case 0xa9:
                    is_W = ((ins_byte1 & 1) > 0) ? true : false;
                    if (is_W)
                    {
                        tmp_w1 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                        tmp_w1 &= Reg_A.X;
                        flag_C = flag_O = false;
                        flag_Z = (tmp_w1 == 0) ? true : false;
                        flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_w1 & 0xff];
                    }
                    else
                    {
                        tmp_b1 = Mem_CS_r8(Reg_IP++);
                        tmp_b1 &= Reg_A.L;
                        flag_C = flag_O = false;
                        flag_Z = (tmp_b1 == 0) ? true : false;
                        flag_S = ((tmp_b1 & 0x80) > 0) ? true : false;
                        flag_P = Table_Paritys[tmp_b1];
                    }
                    break;

                case 0x80:
                case 0x81:
                case 0x82:
                case 0x83:
                    {
                        modregrm_parse();
                        is_W = ((ins_byte1 & 1) > 0) ? true : false;
                        switch (ins_byte2 & 0x38)
                        {
                            case 0://ADD
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w2 = (ushort)(((ins_byte1 & 2) > 0) ? (short)((sbyte)Mem_CS_r8(Reg_IP++)) : (Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));//fixed

                                    tmp_dw1 = (uint)(tmp_w1 + tmp_w2);
                                    tmp_w3 = (ushort)tmp_dw1;
                                    modrm_WriteTool(tmp_w3);

                                    flag_Z = (tmp_w3 == 0) ? true : false;
                                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w3 & 0xff];

                                    flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;

                                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) != 0) ? true : false; //fixed
                                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;

                                }
                                else
                                {

                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = Mem_CS_r8(Reg_IP++);
                                    tmp_w1 = (ushort)(tmp_b1 + tmp_b2);

                                    tmp_b3 = (byte)tmp_w1;
                                    modrm_WriteTool(tmp_b3);
                                    flag_Z = (tmp_b3 == 0) ? true : false;
                                    flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b3];
                                    flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false; //fixed
                                    flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) != 0) ? true : false;//fixed
                                    flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                                }
                                break;

                            case 8://OR
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w2 = (ushort)(((ins_byte1 & 2) > 0) ? (short)((sbyte)Mem_CS_r8(Reg_IP++)) : (Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));//fixed
                                    tmp_w1 |= tmp_w2;
                                    modrm_WriteTool(tmp_w1);

                                    flag_C = flag_O = false;
                                    flag_Z = (tmp_w1 == 0) ? true : false;
                                    flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w1 & 0xff];
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = Mem_CS_r8(Reg_IP++);
                                    tmp_b1 |= tmp_b2;
                                    modrm_WriteTool(tmp_b1);

                                    flag_C = flag_O = false;
                                    flag_Z = (tmp_b1 == 0) ? true : false;
                                    flag_S = ((tmp_b1 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b1];
                                }
                                break;

                            case 0x10://ADC
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w2 = (ushort)(((ins_byte1 & 2) > 0) ? (short)((sbyte)Mem_CS_r8(Reg_IP++)) : (Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));//fixed
                                    tmp_dw1 = (uint)(tmp_w1 + tmp_w2 + ((flag_C) ? 1 : 0));
                                    tmp_w3 = (ushort)tmp_dw1;
                                    modrm_WriteTool(tmp_w3);

                                    flag_Z = (tmp_w3 == 0) ? true : false;
                                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w3 & 0xff];

                                    flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) != 0) ? true : false; //fixed
                                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = Mem_CS_r8(Reg_IP++);
                                    tmp_w1 = (ushort)(tmp_b1 + tmp_b2 + ((flag_C) ? 1 : 0));
                                    tmp_b3 = (byte)tmp_w1;
                                    modrm_WriteTool(tmp_b3);

                                    flag_Z = (tmp_b3 == 0) ? true : false;
                                    flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b3];

                                    flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                                    flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_w1 ^ tmp_b2) & 0x80) != 0) ? true : false; //fixed
                                    flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                                }
                                break;

                            case 0x18://SBB
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w2 = (ushort)(((ins_byte1 & 2) > 0) ? (short)((sbyte)Mem_CS_r8(Reg_IP++)) : (Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));//fixed
                                    tmp_dw1 = (uint)(tmp_w1 - (tmp_w2 + ((flag_C) ? 1 : 0)));
                                    tmp_w3 = (ushort)tmp_dw1;
                                    modrm_WriteTool(tmp_w3);

                                    flag_Z = (tmp_w3 == 0) ? true : false;
                                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w3 & 0xff];

                                    flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_dw1 ^ tmp_w2) & 0x8000) != 0) ? true : false;//fixed
                                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = Mem_CS_r8(Reg_IP++);
                                    tmp_w1 = (ushort)(tmp_b1 - (tmp_b2 + ((flag_C) ? 1 : 0)));
                                    tmp_b3 = (byte)tmp_w1;
                                    modrm_WriteTool(tmp_b3);

                                    flag_Z = (tmp_b3 == 0) ? true : false;
                                    flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b3];

                                    flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                                    flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) != 0) ? true : false;//fixed
                                    flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                                }
                                break;

                            case 0x20://AND
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w2 = (ushort)(((ins_byte1 & 2) > 0) ? (short)((sbyte)Mem_CS_r8(Reg_IP++)) : (Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));//fixed
                                    tmp_w1 &= tmp_w2;
                                    modrm_WriteTool(tmp_w1);
                                    flag_C = flag_O = false;
                                    flag_Z = (tmp_w1 == 0) ? true : false;
                                    flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w1 & 0xff];
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = Mem_CS_r8(Reg_IP++);
                                    tmp_b1 &= tmp_b2;
                                    modrm_WriteTool(tmp_b1);
                                    flag_C = flag_O = false;
                                    flag_Z = (tmp_b1 == 0) ? true : false;
                                    flag_S = ((tmp_b1 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b1];
                                }
                                break;

                            case 0x28://SUB
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w2 = (ushort)(((ins_byte1 & 2) > 0) ? (short)((sbyte)Mem_CS_r8(Reg_IP++)) : (Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));//fixed
                                    tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                                    tmp_w3 = (ushort)tmp_dw1;
                                    modrm_WriteTool(tmp_w3);

                                    flag_Z = (tmp_w3 == 0) ? true : false;
                                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w3 & 0xff];

                                    flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_w1 ^ tmp_w2) & 0x8000) != 0) ? true : false;//fixed
                                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = Mem_CS_r8(Reg_IP++);
                                    tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                                    tmp_b3 = (byte)tmp_w1;
                                    modrm_WriteTool(tmp_b3);

                                    flag_Z = (tmp_b3 == 0) ? true : false;
                                    flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b3];

                                    flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false;
                                    flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) != 0) ? true : false;//fixed
                                    flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                                }
                                break;

                            case 0x30://XOR
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w2 = (ushort)(((ins_byte1 & 2) > 0) ? (short)((sbyte)Mem_CS_r8(Reg_IP++)) : (Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));//fixed
                                    tmp_w1 ^= tmp_w2;
                                    modrm_WriteTool(tmp_w1);

                                    flag_C = flag_O = false;
                                    flag_Z = (tmp_w1 == 0) ? true : false;
                                    flag_S = ((tmp_w1 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w1 & 0xff];
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = Mem_CS_r8(Reg_IP++);
                                    tmp_b1 ^= tmp_b2;
                                    modrm_WriteTool(tmp_b1);
                                    flag_C = flag_O = false;
                                    flag_Z = (tmp_b1 == 0) ? true : false;
                                    flag_S = ((tmp_b1 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b1];
                                }
                                break;

                            case 0x38://CMP
                                if (is_W)
                                {
                                    tmp_w1 = modreg_ReadTool();
                                    tmp_w2 = (ushort)(((ins_byte1 & 2) > 0) ? (short)((sbyte)Mem_CS_r8(Reg_IP++)) : (Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8)));//fixed
                                    tmp_dw1 = (uint)(tmp_w1 - tmp_w2);
                                    tmp_w3 = (ushort)tmp_dw1;

                                    flag_Z = (tmp_w3 == 0) ? true : false;
                                    flag_S = ((tmp_w3 & 0x8000) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_w3 & 0xff];

                                    flag_C = ((tmp_dw1 & 0xFFFF0000) > 0) ? true : false;
                                    flag_O = (((tmp_dw1 ^ tmp_w1) & (tmp_w1 ^ tmp_w2) & 0x8000) != 0) ? true : false;//fixed
                                    flag_A = (((tmp_w2 ^ tmp_w1 ^ tmp_dw1) & 0x10) == 0x10) ? true : false;
                                }
                                else
                                {
                                    tmp_b1 = (byte)modreg_ReadTool();
                                    tmp_b2 = Mem_CS_r8(Reg_IP++);
                                    tmp_w1 = (ushort)(tmp_b1 - tmp_b2);
                                    tmp_b3 = (byte)tmp_w1;

                                    flag_Z = (tmp_b3 == 0) ? true : false;
                                    flag_S = ((tmp_b3 & 0x80) > 0) ? true : false;
                                    flag_P = Table_Paritys[tmp_b3];

                                    flag_C = ((tmp_w1 & 0xFF00) > 0) ? true : false; //fixed
                                    flag_O = (((tmp_w1 ^ tmp_b1) & (tmp_b1 ^ tmp_b2) & 0x80) != 0) ? true : false; //fixed
                                    flag_A = (((tmp_b2 ^ tmp_b1 ^ tmp_w1) & 0x10) == 0x10) ? true : false;
                                }
                                break;
                        }
                    }
                    break;
                #endregion

                //Call : direct with segment
                case 0xe8:
                    tmp_w1 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                    Mem_SS_w16((Reg_SP -= 2), Reg_IP);
                    Reg_IP += tmp_w1;
                    break;

                //call : direct intersegment
                case 0x9a:
                    tmp_dst_ip = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                    tmp_dst_cs = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                    Mem_SS_w16((Reg_SP -= 2), Reg_CS);
                    Mem_SS_w16((Reg_SP -= 2), Reg_IP);
                    Reg_IP = tmp_dst_ip;
                    Reg_CS = tmp_dst_cs;
                    break;

                //JMP : direct within segment
                case 0xe9:
                    tmp_w1 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                    Reg_IP += tmp_w1;
                    break;

                //JMP : direct within segment-short
                case 0xeb:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JMP : direct intersegment
                case 0xea:
                    tmp_dst_ip = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                    tmp_dst_cs = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                    Reg_IP = tmp_dst_ip;
                    Reg_CS = tmp_dst_cs;
                    break;

                //RET : within segment
                case 0xc3:
                    Reg_IP = pop();
                    break;

                //RET : within seg adding immed to sp
                case 0xc2:
                    tmp_w1 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                    Reg_IP = pop();
                    Reg_SP += tmp_w1;
                    break;

                //RET : intersegment
                case 0xcb:
                    Reg_IP = pop();
                    Reg_CS = pop();
                    break;

                //RET : intersegment adding immediate to sp
                case 0xca:
                    tmp_w1 = (ushort)(Mem_CS_r8(Reg_IP++) | (Mem_CS_r8(Reg_IP++) << 8));
                    Reg_IP = pop();
                    Reg_CS = pop();
                    Reg_SP += tmp_w1;
                    break;

                //JO : jump on overflow
                case 0x70:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (flag_O) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JNO : jump on not overflow
                case 0x71:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (!flag_O) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JB/JNAE : Jump on Below/Not Above or Equal
                case 0x72:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (flag_C) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JNB/JAE : Jump on Not Below/Above or Equal
                case 0x73:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (!flag_C) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JE/JZ : Jump on Equal/Zero
                case 0x74:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    //if (step_count == 0xcd361)
                    // {
                    //   MessageBox.Show("!!!! " + tmp_b1.ToString("x2") );
                    //}
                    if (flag_Z) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JNE/JNZ : Jump on Not Equal/Not Zero
                case 0x75:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (!flag_Z) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JBE/JNA : Jump on Below or Equal/ Not Above
                case 0x76:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (flag_C || flag_Z) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JNBE/JA : Jump on Not Below or qual/Above
                case 0x77:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (!flag_C && !flag_Z) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JS : Jump on Sign
                case 0x78:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (flag_S) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JNS : Jump on Not Sign
                case 0x79:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (!flag_S) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JP/JPE : Jump on Parity/Parity Even
                case 0x7a:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (flag_P) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JNP/JPO : Jump on Not Par/Par Odd
                case 0x7b:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (!flag_P) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JL/JNGE : Jump on Less/Not Greater or Equal
                case 0x7c:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (flag_S != flag_O) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JNL/JGE : Jump on Not Less/Greater or Equal
                case 0x7d:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (flag_S == flag_O) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JLE/JNG : Jump on Less or Equal/ Not Greater
                case 0x7e:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if ((flag_S != flag_O) || flag_Z) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JNLE/JG : Jump on Not Less or Equal/Greater
                case 0x7f:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if ((!flag_Z && (flag_S == flag_O))) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //LOOPNZ/LOOPNE : Loop While Not Zero/Equal
                case 0xe0:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if ((--Reg_C.X != 0) && !flag_Z) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //LOOPZ/LOOPE : Loop While Zero/Equal
                case 0xe1:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if ((--Reg_C.X != 0) && flag_Z) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //LOOP : Loop CX Times
                case 0xe2:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (--Reg_C.X != 0) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //JCXZ : Jump on CX Zero
                case 0xe3:
                    tmp_b1 = Mem_CS_r8(Reg_IP++);
                    if (Reg_C.X == 0) Reg_IP = (ushort)(Reg_IP + (short)((sbyte)tmp_b1));
                    break;

                //INT : Type specified
                case 0xcd:
                    interrupt(Mem_CS_r8(Reg_IP++));
                    break;

                //INT : Type 3
                case 0xcc:
                    interrupt(3);
                    break;

                //INTO : interrupt on overflow
                case 0xce:
                    if (flag_O) interrupt(4);
                    break;

                //IRET : interrupt return
                case 0xcf:
                    Reg_IP = pop();
                    Reg_CS = pop();
                    flag_decode(pop());
                    break;

                //CLC
                case 0xf8:
                    flag_C = false;
                    break;

                //CMC
                case 0xf5:
                    flag_C = !flag_C;
                    break;

                //STC
                case 0xf9:
                    flag_C = true;
                    break;

                //CLD
                case 0xfc:
                    flag_D = false;
                    break;

                //STD
                case 0xfd:
                    flag_D = true;
                    break;

                //CLI
                case 0xfa:
                    flag_I = false;
                    break;

                //STI
                case 0xfb:
                    flag_I = true;
                    break;

                //HLT
                case 0xF4:
                    Halt = true;
                    break;

                //WAIT
                case 0x9B:
                    break;

                //ESC : escape (to external device) 
                case 0xD8:
                case 0xD9:
                case 0xDA:
                case 0xDB:
                case 0xDC:
                case 0xDE:
                case 0xDD:
                case 0xDF:
                    modregrm_parse();
                    Console.WriteLine("ESC editing..");
                    //MessageBox.Show("ESC editing..");
                    break;

                //LOCK
                case 0xF0:
                    break;

                //SEGMENT =override prefix
                case 0x26:
                case 0x2e:
                case 0x36:
                case 0x3e:
                    SegRegUse = (SegReg)((ins_byte1 & 0x18) >> 3);
                    SegOverride = true;
                    break;

                default:
                    opcode_inf(false);
                    break;
            }
        }
    }
}
