using System.Diagnostics;

namespace AltairEmu
{
    public class Intel8080
    {

        public Intel8080(CancellationToken token, AltairPanel panel)
        {
            this.token = token;
            this.panel = panel;
        }

        CancellationToken token;
        AltairPanel panel;

        public byte[] registers = new byte[8]; // Order: B, C, D, E, H, L, Memory, A
        private static string[] regList = { "B", "C", "D", "E", "H", "L", "Memory", "A" };
        private static string[] pairList = { "BC", "DE", "HL", "SP" };
        private static string[] conditionList = { "NZ", "Z", "NC", "C", "PO", "PE", "P", "M" };

        public byte W; // W and Z are internal CPU registers
        public byte Z;
        public byte TMP; // ALU temporary register

        public byte SW = 0b00000010; // Processor status word

        public ushort PC; // Program Counter
        public ushort SP; // Stack Pointer

        public bool INTE; // Interrupt Enable Output
        public bool DBIN; // Data Byte In Output
        public bool WR; // Write BAR output
        public bool WAIT; // Wait Output
        public bool READY; // Ready Input

        public byte[] memory = new byte[65536];

        public ushort addressBus;
        public byte dataBus;

        private void setSign() { SW = (byte)(SW | 0b10000000); }
        private void clearSign() { SW = (byte)(SW & 0b01111111); }
        private bool getSign() => (SW & 0b10000000) != 0;
        private void setZero() { SW = (byte)(SW | 0b01000000); }
        private void clearZero() { SW = (byte)(SW & 0b10111111); }
        private bool getZero() => (SW & 0b01000000) != 0;
        private void setAux() { SW = (byte)(SW | 0b00010000); }
        private void clearAux() { SW = (byte)(SW & 0b11101111); }
        private bool getAux() => (SW & 0b00010000) != 0;
        private void setParity() { SW = (byte)(SW | 0b00000100); }
        private void clearParity() { SW = (byte)(SW & 0b11111011); }
        private bool getParity() => (SW & 0b00000100) != 0;
        private void setCarry() { SW = (byte)(SW | 0b00000001); }
        private void clearCarry() { SW = (byte)(SW & 0b11111110); }
        private bool getCarry() => (SW & 0b00000001) != 0;

        public event EventHandler SyncPulse;

        public void setToken(CancellationToken token)
        {
            this.token = token;
        }

        private void updateFlags(byte result, bool zero, bool sign, bool parity)
        {
            if (zero)
            {
                if (result == 0)
                {
                    setZero();
                }
                else
                {
                    clearZero();
                }
            }
            if (sign)
            {
                if ((result & 0b10000000) != 0)
                {
                    setSign();
                }
                else
                {
                    clearSign();
                }
            }
            if (parity)
            {
                for (int i = 1; i < 8; i++)
                {
                    result ^= (byte)((result >> i) & 0x01);
                }
                result &= 0x01;
                if (result == 0)
                {
                    setParity();
                }
                else
                {
                    clearParity();
                }
            }
        }


        Stopwatch timer = new Stopwatch();

        private void StartState()
        {
            timer = Stopwatch.StartNew();
        }

        private void EndState()
        {
            while (timer.ElapsedTicks < 5)
            {

            }
            timer.Stop();
        }

        private void WaitState()
        {
            WAIT = true;
            while (!READY && !token.IsCancellationRequested)
            {
                timer = Stopwatch.StartNew();
                while (timer.ElapsedTicks < 5)
                {

                }
                timer.Stop();
            }
            WAIT = false;
        }

        public void Reset()
        {
            this.PC = 0;
            this.WAIT = false;
        }

        protected virtual void OnSync()
        {
            SyncPulse?.Invoke(this, EventArgs.Empty);
        }

        private void MOV_RR(int dst, int src)
        {
            TMP = registers[src];
            EndState();
            StartState();
            registers[dst] = TMP;
            EndState();
        }

        private void MOV_RM(int reg)
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            registers[reg] = dataBus;
            DBIN = false;
            EndState();
        }

        private void MOV_MR(int reg)
        {
            TMP = registers[reg];
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b00000000; // MEMORY WRITE STATUS BYTE
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = TMP;
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
        }

        private void ADD_R(int reg)
        {
            byte result = (byte)(registers[7] + registers[reg]);
            updateFlags(result, true, true, true);
            if ((registers[7] + registers[reg]) > 0xFF)
            {
                setCarry();
            } else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) + (registers[reg] & 0x0F)) > 0x0F)
            {
                setAux();
            } else
            {
                clearAux();
            }
            registers[7] = result;
            EndState();
        }

        private void ADD_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] + TMP);
            updateFlags(result, true, true, true);
            if ((registers[7] + TMP) > 0xFF)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) + (TMP & 0x0F)) > 0x0F)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void ADC_R(int reg)
        {
            byte result = (byte)(registers[7] + registers[reg] + (getCarry() ? 1 : 0));
            updateFlags(result, true, true, true);
            if ((registers[7] + registers[reg] + (getCarry() ? 1 : 0)) > 0xFF)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) + (registers[reg] & 0x0F) + (getCarry() ? 1 : 0)) > 0x0F)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            EndState();
        }

        private void ADC_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] + TMP + (getCarry() ? 1 : 0));
            updateFlags(result, true, true, true);
            if ((registers[7] + TMP + (getCarry() ? 1 : 0)) > 0xFF)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) + (TMP & 0x0F) + (getCarry() ? 1 : 0)) > 0x0F)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void SUB_R(int reg)
        {
            byte result = (byte)(registers[7] - registers[reg]);
            updateFlags(result, true, true, true);
            if ((registers[7] - registers[reg]) < 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if ((registers[7] & 0x0F) + ((~registers[reg]) & 0x0F) + (getCarry() ? 0 : 1) > 0x0F) {
                setAux();
            } else
            {
                clearAux();
            }
            registers[7] = result;
            EndState();
        }

        private void SUB_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] - TMP);
            updateFlags(result, true, true, true);
            if ((registers[7] - TMP) < 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) - (TMP & 0x0F)) < 0)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void SBB_R(int reg)
        {
            byte result = (byte)(registers[7] + ~registers[reg] + (getCarry() ? 0 : 1));
            updateFlags(result, true, true, true);
            if ((registers[7] - registers[reg] - (getCarry() ? 1 : 0)) < 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) - ((registers[reg]) & 0x0F) - (getCarry() ? 1 : 0)) < 0)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            EndState();
        }

        private void SBB_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] - TMP - (getCarry() ? 1 : 0));
            updateFlags(result, true, true, true);
            if ((registers[7] - TMP - (getCarry() ? 1 : 0)) < 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) - (TMP & 0x0F) - (getCarry() ? 1 : 0)) < 0)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void ANA_R(int reg)
        {
            byte result = (byte)(registers[7] & registers[reg]);
            updateFlags(result, true, true, true);
            clearCarry();
            if (((registers[7] & 0b00001000) | (registers[reg] & 0b00001000)) != 0)
            {
                setAux();
            } else
            {
                clearAux();
            }
            registers[7] = result;
            EndState();
        }

        private void ANA_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] & TMP);
            updateFlags(result, true, true, true);
            clearCarry();
            if (((registers[7] & 0b00001000) | (TMP & 0b00001000)) != 0)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void XRA_R(int reg)
        {
            byte result = (byte)(registers[7] ^ registers[reg]);
            updateFlags(result, true, true, true);
            clearCarry();
            clearAux();
            registers[7] = result;
            EndState();
        }

        private void XRA_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] ^ TMP);
            updateFlags(result, true, true, true);
            clearCarry();
            clearAux();
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void ORA_R(int reg)
        {
            byte result = (byte)(registers[7] | registers[reg]);
            updateFlags(result, true, true, true);
            clearCarry();
            clearAux();
            registers[7] = result;
            EndState();
        }

        private void ORA_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] | TMP);
            updateFlags(result, true, true, true);
            clearCarry();
            clearAux();
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void CMP_R(int reg)
        {
            byte result = (byte)(registers[7] - registers[reg]);
            updateFlags(result, true, true, true);
            if (registers[7] - registers[reg] < 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if ((registers[7] & 0x0F) + ((~registers[reg]) & 0x0F) + (getCarry() ? 0 : 1) > 0x0F)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            EndState();
        }

        private void CMP_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] - TMP);
            updateFlags(result, true, true, true);
            if (registers[7] - TMP < 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) + (TMP & 0x0F)) < 0)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            DBIN = false;
            EndState();
        }

        private void HLT()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10001010; // HALT ACKNOWLEDGE STATUS BYTE
            OnSync();
            EndState();
            WaitState();
        }

        private void NOP()
        {
            EndState();
        }

        private void LXI_D16(int pair)
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            if (pair == 3)
            {
                SP = (ushort)dataBus;
            } else
            {
                registers[pair * 2 + 1] = dataBus;
            }
            DBIN = false;
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            if (pair == 3)
            {
                SP |= (ushort)(dataBus << 8);
            } else
            {
                registers[pair * 2] = dataBus;
            }
            DBIN = false;
            EndState();
        }

        private void INX_RP(int pair)
        {
            EndState();
            StartState();
            if (pair == 3)
            {
                SP++;
            }
            else
            {
                if ((registers[pair * 2 + 1] + 1) > 0xFF)
                {
                    registers[pair * 2]++;
                }
                registers[pair * 2 + 1]++;
            }
            EndState();
        }

        private void INR_R(int reg)
        {
            TMP = registers[reg];
            if (((TMP + 1) & 0x0F) == 0)
            {
                setAux();
            } else
            {
                clearAux();
            }
            EndState();
            StartState();
            registers[reg]++;
            updateFlags(registers[reg], true, true, true);
            EndState();
        }

        private void INR_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            if (((TMP + 1) & 0x0F) == 0)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            TMP++;
            updateFlags(TMP, true, true, true);
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b00000000; // MEMORY WRITE STATUS BYTE
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = TMP;
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
        }

        private void DCR_R(int reg)
        {
            TMP = registers[reg];
            if (!(((TMP-1) & 0xF) == 0xF))
            {
                setAux();
            } else
            {
                clearAux();
            }
            EndState();
            StartState();
            registers[reg]--;
            updateFlags(registers[reg], true, true, true);
            EndState();
        }

        private void DCR_M()
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            if (!(((TMP - 1) & 0xF) == 0xF))
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            TMP--;
            updateFlags(TMP, true, true, true);
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b00000000; // MEMORY WRITE STATUS BYTE
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = TMP;
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
        }

        private void MVI_R(int reg)
        {
            EndState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            PC++;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            registers[reg] = dataBus;
            DBIN = false;
            EndState();
        }

        private void MVI_M(int reg)
        {
            EndState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            PC++;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((registers[4] << 8) | registers[5]);
            dataBus = 0b00000000; // MEMORY WRITE STATUS BYTE
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = TMP;
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
        }

        private void DAD_RP(int pair)
        {
            EndState();
            StartState();
            if (pair == 3)
            {
                TMP = (byte)(SP & 0xFF);
            }
            else
            {
                TMP = registers[pair * 2 + 1];
            }
            EndState();
            StartState();
            if ((registers[5] + TMP) > 0xFF)
            {
                setCarry();
            } else
            {
                clearCarry();
            }
            EndState();
            StartState();
            registers[5] += TMP;
            EndState();
            StartState();
            if (pair == 3)
            {
                TMP = (byte)((SP >> 8) & 0xFF);
            } else
            {
                TMP = registers[pair * 2];
            }
            EndState();
            StartState();
            if ((registers[4]+TMP+(getCarry() ? 1 : 0)) > 0xFF)
            {
                registers[4] += (byte)(TMP + (getCarry() ? 1 : 0));
                setCarry();
            } else
            {
                registers[4] += (byte)(TMP + (getCarry() ? 1 : 0));
                clearCarry();
            }
            EndState();
            StartState();
            EndState();
        }

        private void DCX_RP(int pair)
        {
            EndState();
            StartState();
            if (pair == 3)
            {
                SP--;
            } else
            {
                if (registers[pair*2+1] == 0)
                {
                    registers[pair * 2 + 1] = 0xFF;
                    registers[pair * 2]--;
                } else
                {
                    registers[pair * 2 + 1]--;
                }
            }
            EndState();
        }

        private void STAX_RP(int pair)
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[pair * 2] << 8) | registers[pair * 2 + 1]);
            dataBus = 0b00000000; // MEMORY WRITE STATUS BYTE
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = registers[7];
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
        }

        private void SHLD_A16()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            PC++;
            EndState();
            StartState();
            dataBus = panel.GetData();
            Z = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            PC++;
            EndState();
            StartState();
            dataBus = panel.GetData();
            W = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((W << 8) | Z);
            dataBus = 0b00000000; // MEMORY WRITE STATUS BYTE
            EndState();
            StartState();
            if (Z == 0xFF)
            {
                W++;
            }
            Z++;
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = registers[5];
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
            StartState();
            addressBus = (ushort)((W << 8) | Z);
            dataBus = 0b00000000; // MEMORY WRITE STATUS BYTE
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = registers[4];
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
        }

        private void STA_A16()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            Z = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            W = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((W << 8) | Z);
            dataBus = 0b00000000; // MEMORY WRITE STATUS BYTE
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = registers[7];
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
        }

        private void LDAX_RP(int pair)
        {
            EndState();
            StartState();
            addressBus = (ushort)((registers[pair * 2] << 8) | registers[pair * 2 + 1]);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            registers[7] = dataBus;
            DBIN = false;
            EndState();
        }

        private void LHLD_A16()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            Z = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            W = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((W << 8) | Z);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            if (Z == 0xFF)
            {
                W++;
            }
            Z++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            registers[5] = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((W << 8) | Z);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            registers[4] = dataBus;
            DBIN = false;
            EndState();
        }

        private void LDA_A16()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            Z = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            W = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((W << 8) | Z);
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            registers[7] = dataBus;
            DBIN = false;
            EndState();
        }

        private void RLC()
        {
            if ((registers[7] & 0b10000000) > 0)
            {
                setCarry();
            } else
            {
                clearCarry();
            }
            registers[7] = (byte)(registers[7] << 1);
            registers[7] |= (byte)(getCarry() ? 1 : 0);
            EndState();
        }

        private void RAL()
        {
            bool oldCarry = getCarry();
            if ((registers[7] & 0b10000000) > 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            registers[7] = (byte)(registers[7] << 1);
            registers[7] |= (byte)(oldCarry ? 1 : 0);
            EndState();
        }

        private void DAA()
        {
            if (((registers[7] & 0x0F) > 9) || (getAux()))
            {
                if ((registers[7] & 0x0F) + 6 > 0x0F)
                {
                    setAux();
                } else
                {
                    clearAux();
                }
                registers[7] += 6;
            } else
            {
                clearAux();
            }
            if (((registers[7] & 0xF0) > 0x90) || (getCarry()))
            {
                if ((registers[7] + 0x60) > 0xFF)
                {
                    setCarry();
                }
                else
                {
                    clearCarry();
                }
                registers[7] += 0x60;
            }
            updateFlags(registers[7], true, true, true);
            EndState();
        }

        private void STC()
        {
            setCarry();
            EndState();
        }

        private void RRC()
        {
            if ((registers[7] & 1) > 0)
            {
                setCarry();
            } else
            {
                clearCarry();
            }
            registers[7] = (byte)(registers[7] >> 1);
            registers[7] |= (byte)((getCarry() ? 1 : 0) << 7);
            EndState();
        }

        private void RAR()
        {
            bool oldCarry = getCarry();
            if ((registers[7] & 1) > 0)
            {
                setCarry();
            } else
            {
                clearCarry();
            }
            registers[7] = (byte)(registers[7] >> 1);
            if (oldCarry)
            {
                registers[7] |= 0b10000000;
            }
            EndState();
        }

        private void CMA()
        {
            registers[7] = (byte)(~registers[7]);
            EndState();
        }

        private void CMC()
        {
            if (getCarry())
            {
                clearCarry();
            } else
            {
                setCarry();
            }
            EndState();
        }

        private void RST(int vector)
        {
            EndState();
            StartState();
            SP--;
            EndState();
            StartState();
            addressBus = SP;
            dataBus = 0b00000100; // STACK WRITE status byte
            OnSync();
            EndState();
            StartState();
            SP--;
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = (byte)(PC >> 8);
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
            StartState();
            addressBus = SP;
            dataBus = 0b00000100; // STACK WRITE status byte
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = (byte)(PC & 0xFF);
            memory[addressBus] = dataBus;
            PC = (ushort)(vector * 8);
            EndState();
            WR = true;
        }

        private void POP_RP(int pair)
        {
            EndState();
            StartState();
            addressBus = SP;
            dataBus = 0b10000110; // STACK READ status byte
            OnSync();
            EndState();
            StartState();
            SP++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            if (pair == 3)
            {
                dataBus &= 0b11010111;
                dataBus |= 0b10;
                SW = dataBus;
            }
            else
            {
                registers[pair * 2 + 1] = dataBus;
            }
            DBIN = false;
            EndState();
            StartState();
            addressBus = SP;
            dataBus = 0b10000110; // STACK READ status byte
            OnSync();
            EndState();
            StartState();
            SP++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            if (pair == 3)
            {
                registers[7] = dataBus;
            }
            else
            {
                registers[pair * 2] = dataBus;
            }
            DBIN = false;
            EndState();
        }

        private void PUSH_RP(int pair)
        {
            EndState();
            StartState();
            SP--;
            EndState();
            StartState();
            addressBus = SP;
            dataBus = 0b00000100; // STACK WRITE status byte
            OnSync();
            EndState();
            StartState();
            SP--;
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            if (pair == 3)
            {
                dataBus = registers[7];
            }
            else
            {
                dataBus = registers[pair * 2];
            }
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
            StartState();
            addressBus = SP;
            dataBus = 0b00000100; // STACK WRITE status byte
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            if (pair == 3)
            {
                byte tmp = SW;
                tmp &= 0b11010111;
                tmp |= 0b10;
                dataBus = tmp;
            } else
            {
                dataBus = registers[pair * 2 + 1];
            }
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
        }

        private void CALL_A16(bool condition)
        {
            EndState();
            StartState();
            if (condition)
            {
                SP--;
            }
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            PC++;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            Z = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            PC++;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            W = dataBus;
            DBIN = false;
            EndState();
            if (condition)
            {
                StartState();
                addressBus = SP;
                dataBus = 0b00000100; // STACK WRITE status byte
                OnSync();
                EndState();
                StartState();
                SP--;
                EndState();
                WR = false;
                if (!READY)
                {
                    WaitState();
                }
                StartState();
                dataBus = (byte)(PC >> 8);
                memory[addressBus] = dataBus;
                EndState();
                WR = true;
                StartState();
                addressBus = SP;
                dataBus = 0b00000100; // STACK WRITE status byte
                OnSync();
                EndState();
                StartState();
                EndState();
                WR = false;
                if (!READY)
                {
                    WaitState();
                }
                StartState();
                dataBus = (byte)(PC & 0xFF);
                memory[addressBus] = dataBus;
                EndState();
                WR = true;
                PC = (ushort)((W << 8) | Z);
            }
        }

        private void RET_A16(bool condition)
        {
            EndState();
            if (condition)
            {
                StartState();
                EndState();
                StartState();
                addressBus = SP;
                dataBus = 0b10000110; // STACK READ status byte
                OnSync();
                EndState();
                StartState();
                SP++;
                DBIN = true;
                EndState();
                if (!READY)
                {
                    WaitState();
                }
                StartState();
                dataBus = panel.GetData();
                Z = dataBus;
                DBIN = false;
                EndState();
                StartState();
                addressBus = SP;
                dataBus = 0b10000110; // STACK READ status byte
                OnSync();
                EndState();
                StartState();
                SP++;
                DBIN = true;
                EndState();
                if (!READY)
                {
                    WaitState();
                }
                StartState();
                dataBus = panel.GetData();
                W = dataBus;
                DBIN = false;
                EndState();
                PC = (ushort)((W << 8) | Z);
            }
        }

        private void JMP_A16(bool condition)
        {
            EndState();
            if (condition)
            {
                StartState();
                addressBus = PC;
                dataBus = 0b10000010; // MEMORY READ STATUS BYTE
                OnSync();
                EndState();
                StartState();
                PC++;
                DBIN = true;
                EndState();
                if (!READY)
                {
                    WaitState();
                }
                StartState();
                dataBus = panel.GetData();
                Z = dataBus;
                DBIN = false;
                EndState();
                StartState();
                addressBus = PC;
                dataBus = 0b10000010; // MEMORY READ STATUS BYTE
                OnSync();
                EndState();
                StartState();
                PC++;
                DBIN = true;
                EndState();
                if (!READY)
                {
                    WaitState();
                }
                StartState();
                dataBus = panel.GetData();
                W = dataBus;
                DBIN = false;
                PC = (ushort)((W << 8) | Z);
                EndState();
            } else
            {
                PC++;
                PC++;
                StartState();
                EndState();
            }
        }

        private void PCHL()
        {
            PC = (ushort)(registers[4] << 8);
            EndState();
            StartState();
            PC |= (byte)(registers[5]);
            EndState();
        }

        private void SPHL()
        {
            SP = (ushort)(registers[4] << 8);
            EndState();
            StartState();
            SP |= registers[5];
            EndState();
        }

        private void OUT_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            Z = dataBus;
            W = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((W << 8) | Z);
            dataBus = 0b00010000; // OUTPUT WRITE status byte
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = registers[7];
            // IO WRITE HERE
            EndState();
            WR = true;
        }

        private void XTHL()
        {
            EndState();
            StartState();
            addressBus = SP;
            dataBus = 0b10000110; // STACK READ status byte
            OnSync();
            EndState();
            StartState();
            SP++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            Z = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = SP;
            dataBus = 0b10000110; // STACK READ status byte
            OnSync();
            EndState();
            StartState();
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            W = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = SP;
            dataBus = 0b00000100; // STACK WRITE status byte
            OnSync();
            EndState();
            StartState();
            SP--;
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = registers[4];
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
            StartState();
            addressBus = SP;
            dataBus = 0b00000100; // STACK WRITE status byte
            OnSync();
            EndState();
            StartState();
            EndState();
            WR = false;
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = registers[5];
            memory[addressBus] = dataBus;
            EndState();
            WR = true;
            StartState();
            registers[4] = W;
            EndState();
            StartState();
            registers[5] = Z;
            EndState();
        }

        private void DI()
        {
            INTE = false;
            EndState();
        }

        private void IN_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            Z = dataBus;
            W = dataBus;
            DBIN = false;
            EndState();
            StartState();
            addressBus = (ushort)((W << 8) | Z);
            dataBus = 0b01000010; // INPUT READ status byte
            OnSync();
            EndState();
            StartState();
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            if (addressBus == 0xFFFF)
            {
                dataBus = panel.GetSwitches(8);
            } else
            {
                dataBus = 0xFF;
            }
            registers[7] = dataBus;
            EndState();
        }

        private void XCHG()
        {
            byte tmpH = registers[4];
            byte tmpL = registers[5];
            registers[4] = registers[2];
            registers[5] = registers[3];
            registers[2] = tmpH;
            registers[3] = tmpL;
            EndState();
        }

        private void EI()
        {
            INTE = true;
            EndState();
        }

        private void ADI_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] + TMP);
            updateFlags(result, true, true, true);
            if ((registers[7] + TMP) > 0xFF)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) + (TMP & 0x0F)) > 0x0F)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void SUI_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] - TMP);
            updateFlags(result, true, true, true);
            if ((registers[7] - TMP) < 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if ((registers[7] & 0x0F) + ((~TMP) & 0x0F) > 0x0F)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void ANI_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] & TMP);
            updateFlags(result, true, true, true);
            clearCarry();
            if (((registers[7] & 0b00001000) | (TMP & 0b00001000)) != 0)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void ORI_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] | TMP);
            updateFlags(result, true, true, true);
            clearCarry();
            clearAux();
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void ACI_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] + TMP + (getCarry() ? 1 : 0));
            updateFlags(result, true, true, true);
            if ((registers[7] + TMP + (getCarry() ? 1 : 0)) > 0xFF)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if (((registers[7] & 0x0F) + (TMP & 0x0F) + (getCarry() ? 1 : 0)) > 0x0F)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void SBI_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] - TMP - (getCarry() ? 1 : 0));
            updateFlags(result, true, true, true);
            if ((registers[7] - TMP - (getCarry() ? 1 : 0)) < 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if ((registers[7] & 0x0F) + ((~TMP) & 0x0F) + (getCarry() ? 0 : 1) > 0x0F)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            registers[7] = result;
            DBIN = false;
            EndState();
        }

        private void XRI_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] ^ TMP);
            updateFlags(result, true, true, true);
            clearCarry();
            clearAux();
            registers[7] = result;
            DBIN = false;
            EndState();
        }
        private void CPI_D8()
        {
            EndState();
            StartState();
            addressBus = PC;
            dataBus = 0b10000010; // MEMORY READ STATUS BYTE
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            dataBus = panel.GetData();
            TMP = dataBus;
            byte result = (byte)(registers[7] - TMP);
            updateFlags(result, true, true, true);
            if ((registers[7] - TMP) < 0)
            {
                setCarry();
            }
            else
            {
                clearCarry();
            }
            if ((registers[7] & 0x0F) + ((~TMP) & 0x0F) < 0x0F)
            {
                setAux();
            }
            else
            {
                clearAux();
            }
            DBIN = false;
            EndState();
        }

        public void InstructionDecoder()
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            StartState();
            DBIN = false;
            WR = true;
            addressBus = PC;
            dataBus = 0b10100010; // Status word for FETCH machine cycle
            OnSync();
            EndState();
            StartState();
            PC++;
            DBIN = true;
            EndState();
            if (!READY)
            {
                WaitState();
            }
            StartState();
            byte instruction = panel.GetData();
            DBIN = false;
            EndState();
            StartState();
            int dstReg = ((instruction >> 3) & 0b111);
            int srcReg = (instruction & 0b111);
            int pair = ((instruction >> 4) & 0b11);
            bool condition = ((dstReg % 2) == 0);
            if (dstReg <= 1)
            {
                condition ^= getZero();
            } else if (dstReg <= 3)
            {
                condition ^= getCarry();
            } else if (dstReg <= 5)
            {
                condition ^= getParity();
            } else
            {
                condition ^= getSign();
            }
            if ((instruction >= 0x40) && (instruction <= 0x7F))
            {
                if ((srcReg == 6) && (dstReg == 6))
                {
                    //MessageBox.Show("HLT");
                    HLT();
                    return;
                }
                else if (srcReg == 6)
                {
                    //MessageBox.Show("MOV " + regList[dstReg] + ", M");
                    MOV_RM(dstReg);
                    return;
                }
                else if (dstReg == 6)
                {
                    //MessageBox.Show("MOV M, " + regList[srcReg]);
                    MOV_MR(srcReg);
                    return;
                }
                else
                {
                    //MessageBox.Show("MOV " + regList[dstReg] + ", " + regList[srcReg]);
                    MOV_RR(dstReg, srcReg);
                    return;
                }
            }
            else if (instruction <= 0x3F)
            {
                if (((instruction & 0x0F) == 0) || ((instruction & 0x0F) == 8))
                {
                    //MessageBox.Show("NOP");
                    NOP();
                    return;
                }
                else if ((instruction & 0x0F) == 1)
                {
                    //MessageBox.Show("LXI " + pairList[pair]);
                    LXI_D16(pair);
                    return;
                }
                else if ((instruction & 0x0F) == 2)
                {
                    if (instruction == 0x22)
                    {
                        //MessageBox.Show("SHLD A16");
                        SHLD_A16();
                        return;
                    }
                    else if (instruction == 0x32)
                    {
                        //MessageBox.Show("STA A16");
                        STA_A16();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("STAX " + pairList[pair]);
                        STAX_RP(pair);
                        return;
                    }
                }
                else if ((instruction & 0x0F) == 3)
                {
                    //MessageBox.Show("INX " + pairList[pair]);
                    INX_RP(pair);
                    return;
                }
                else if (((instruction & 0x0F) == 4) || ((instruction & 0x0F) == 0xC))
                {
                    if (dstReg == 6)
                    {
                        //MessageBox.Show("INR M");
                        INR_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("INR " + regList[dstReg]);
                        INR_R(dstReg);
                        return;
                    }
                }
                else if (((instruction & 0x0F) == 5) || ((instruction & 0x0F) == 0xD))
                {
                    if (dstReg == 6)
                    {
                        //MessageBox.Show("DCR M");
                        DCR_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("DCR " + regList[dstReg]);
                        DCR_R(dstReg);
                        return;
                    }
                }
                else if (((instruction & 0x0F) == 6) || ((instruction & 0x0F) == 0xE))
                {
                    if (dstReg == 6)
                    {
                        //MessageBox.Show("MVI M");
                        MVI_M(dstReg);
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("MVI " + regList[dstReg]);
                        MVI_R(dstReg);
                        return;
                    }
                }
                else if ((instruction & 0x0F) == 7)
                {
                    if (instruction == 0x07)
                    {
                        //MessageBox.Show("RLC");
                        RLC();
                        return;
                    }
                    else if (instruction == 0x17)
                    {
                        //MessageBox.Show("RAL");
                        RAL();
                        return;
                    }
                    else if (instruction == 0x27)
                    {
                        //MessageBox.Show("DAA");
                        DAA();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("STC");
                        STC();
                        return;
                    }
                }
                else if ((instruction & 0x0F) == 9)
                {
                    //MessageBox.Show("DAD " + pairList[pair]);
                    DAD_RP(pair);
                    return;
                }
                else if ((instruction & 0x0F) == 0xA)
                {
                    if (instruction == 0x2A)
                    {
                        //MessageBox.Show("LHLD A16");
                        LHLD_A16();
                        return;
                    }
                    else if (instruction == 0x3A)
                    {
                        //MessageBox.Show("LDA A16");
                        LDA_A16();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("LDAX " + pairList[pair]);
                        LDAX_RP(pair);
                        return;
                    }
                }
                else if ((instruction & 0x0F) == 0xB)
                {
                    //MessageBox.Show("DCX " + pairList[pair]);
                    DCX_RP(pair);
                    return;
                }
                else
                {
                    if (instruction == 0x0F)
                    {
                        //MessageBox.Show("RRC");
                        RRC();
                        return;
                    }
                    else if (instruction == 0x1F)
                    {
                        //MessageBox.Show("RAR");
                        RAR();
                        return;
                    }
                    else if (instruction == 0x2F)
                    {
                        //MessageBox.Show("CMA");
                        CMA();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("CMC");
                        CMC();
                        return;
                    }
                }
            }
            else
            {
                if (instruction <= 0x87)
                {
                    if (srcReg == 6)
                    {
                        //MessageBox.Show("ADD M");
                        ADD_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("ADD " + regList[srcReg]);
                        ADD_R(srcReg);
                        return;
                    }
                }
                else if (instruction <= 0x8F)
                {
                    if (srcReg == 6)
                    {
                        //MessageBox.Show("ADC M");
                        ADC_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("ADC " + regList[srcReg]);
                        ADC_R(srcReg);
                        return;
                    }
                }
                else if (instruction <= 0x97)
                {
                    if (srcReg == 6)
                    {
                        //MessageBox.Show("SUB M");
                        SUB_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("SUB " + regList[srcReg]);
                        SUB_R(srcReg);
                        return;
                    }
                }
                else if (instruction <= 0x9F)
                {
                    if (srcReg == 6)
                    {
                        //MessageBox.Show("SBB M");
                        SBB_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("SBB " + regList[srcReg]);
                        SBB_R(srcReg);
                        return;
                    }
                }
                else if (instruction <= 0xA7)
                {
                    if (srcReg == 6)
                    {
                        //MessageBox.Show("ANA M");
                        ANA_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("ANA " + regList[srcReg]);
                        ANA_R(srcReg);
                        return;
                    }
                }
                else if (instruction <= 0xAF)
                {
                    if (srcReg == 6)
                    {
                        //MessageBox.Show("XRA M");
                        XRA_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("XRA " + regList[srcReg]);
                        XRA_R(srcReg);
                        return;
                    }
                }
                else if (instruction <= 0xB7)
                {
                    if (srcReg == 6)
                    {
                        //MessageBox.Show("ORA M");
                        ORA_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("ORA " + regList[srcReg]);
                        ORA_R(srcReg);
                        return;
                    }
                }
                else if (instruction <= 0xBF)
                {
                    if (srcReg == 6)
                    {
                        //MessageBox.Show("CMP M");
                        CMP_M();
                        return;
                    }
                    else
                    {
                        //MessageBox.Show("CMP " + regList[srcReg]);
                        CMP_R(srcReg);
                        return;
                    }
                } else if (((instruction & 0x0F) == 0) || ((instruction & 0x0F) == 8))
                {
                    //MessageBox.Show("RET " + conditionList[dstReg] + " A16");
                    RET_A16(condition);
                    return;
                }
                else if ((instruction & 0x0F) == 1)
                {
                    //MessageBox.Show("POP " + pairList[pair]);
                    POP_RP(pair);
                    return;
                } else if (((instruction & 0x0F) == 2) || ((instruction & 0x0F) == 0xA)) {
                    //MessageBox.Show("JMP " + conditionList[dstReg] + " A16");
                    JMP_A16(condition);
                    return;
                } else if ((instruction & 0x0F) == 3)
                {
                    if (instruction == 0xC3)
                    {
                        //MessageBox.Show("JMP A16");
                        JMP_A16(true);
                        return;
                    } else if (instruction == 0xD3)
                    {
                        //MessageBox.Show("OUT D8");
                        OUT_D8();
                        return;
                    } else if (instruction == 0xE3)
                    {
                        //MessageBox.Show("XTHL");
                        XTHL();
                        return;
                    } else
                    {
                        //MessageBox.Show("DI");
                        DI();
                        return;
                    }
                }
                else if (((instruction & 0x0F) == 4) || ((instruction & 0x0F) == 0xC))
                {
                    //MessageBox.Show("CALL " + conditionList[dstReg] + " A16");
                    CALL_A16(condition);
                    return;
                }
                else if ((instruction & 0x0F) == 5)
                {
                    //MessageBox.Show("PUSH" + pairList[pair]);
                    PUSH_RP(pair);
                    return;
                } else if ((instruction & 0x0F) == 6)
                {
                    if (instruction == 0xC6)
                    {
                        //MessageBox.Show("ADI D8");
                        ADI_D8();
                        return;
                    } else if (instruction == 0xD6)
                    {
                        //MessageBox.Show("SUI D8");
                        SUI_D8();
                        return;
                    } else if (instruction == 0xE6)
                    {
                        //MessageBox.Show("ANI D8");
                        ANI_D8();
                        return;
                    } else
                    {
                        //MessageBox.Show("ORI D8");
                        ORI_D8();
                        return;
                    }
                }
                else if (((instruction & 0x0F) == 7) || ((instruction & 0x0F) == 0xF))
                {
                    //MessageBox.Show("RST " + (dstReg * 8).ToString());
                    RST(dstReg);
                    return;
                } else if ((instruction & 0x0F) == 9)
                {
                    if (instruction == 0xE9)
                    {
                        //MessageBox.Show("PCHL");
                        PCHL();
                        return;
                    } else if (instruction == 0xF9)
                    {
                        //MessageBox.Show("SPHL");
                        SPHL();
                        return;
                    } else
                    {
                        //MessageBox.Show("RET A16");
                        RET_A16(true);
                        return;
                    }
                } else if ((instruction & 0x0F) == 0xB)
                {
                    if (instruction == 0xCB)
                    {
                        //MessageBox.Show("JMP A16");
                        JMP_A16(true);
                        return;
                    } else if (instruction == 0xDB)
                    {
                        //MessageBox.Show("IN D8");
                        IN_D8();
                        return;
                    } else if (instruction == 0xEB)
                    {
                        //MessageBox.Show("XCHG");
                        XCHG();
                        return;
                    } else
                    {
                        //MessageBox.Show("EI");
                        EI();
                        return;
                    }
                }
                else if ((instruction & 0x0F) == 0xD)
                {
                    //MessageBox.Show("CALL A16");
                    CALL_A16(true);
                    return;
                } else if ((instruction & 0x0F) == 0xE)
                {
                    if (instruction == 0xCE)
                    {
                        //MessageBox.Show("ACI D8");
                        ACI_D8();
                        return;
                    } else if (instruction == 0xDE)
                    {
                        //MessageBox.Show("SBI D8");
                        SBI_D8();
                        return;
                    } else if (instruction == 0xEE)
                    {
                        //MessageBox.Show("XRI D8");
                        XRI_D8();
                        return;
                    } else
                    {
                        //MessageBox.Show("CPI D8");
                        CPI_D8();
                        return;
                    }
                }
            }
            //MessageBox.Show("Error: unimplemented instruction " + Convert.ToHexString([instruction]));
            throw new Exception("Unimplemented instruction" + Convert.ToHexString([instruction]));
        }
    }
}
