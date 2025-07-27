using Accessibility;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AltairEmu
{
    public partial class AltairPanel : Form
    {

        public CancellationTokenSource executeCancelSource = new CancellationTokenSource();
        Intel8080 cpu;

        byte[] addressLights = new byte[16];
        byte statusByte;
        bool stop = false;
        bool step = false;
        bool SSW = false;
        byte swByte;

        Task cpuTask;

        public AltairPanel()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();
            cpu = new Intel8080(executeCancelSource.Token, this);
            cpu.SyncPulse += OnSync;
            
            // CODE FOR RUNNING 8080 TEST
            /*
            byte[] testFile = File.ReadAllBytes(".\\TST8080.COM");
            for (int i = 0; i < testFile.Length; i++)
            {
                cpu.memory[i + 0x100] = testFile[i];
            }
            cpu.memory[5] = 0xC9;
            cpu.Reset();
            cpu.PC = 0x100;
            cpu.READY = true;
            while (cpu.PC != 0)
            {
                if (cpu.PC >= 0x5)
                {
                    //MessageBox.Show("PC: " + cpu.PC.ToString("X") + " SP: " + cpu.SP.ToString("X"));
                }
                cpu.InstructionDecoder();
                if (cpu.PC == 0x05)
                {
                    if (cpu.registers[1] == 2)
                    {
                        Debug.Write((char)cpu.registers[3]);
                    } else if (cpu.registers[1] == 9)
                    {
                        byte currentChar = cpu.memory[(cpu.registers[2] << 8) | cpu.registers[3]];
                        int charIndex = 0;
                        while (currentChar != '$')
                        {
                            Debug.Write((char)currentChar);
                            charIndex++;
                            currentChar = cpu.memory[((cpu.registers[2] << 8) | cpu.registers[3]) + charIndex];
                        }
                    }
                }
            }*/
        }

        public byte GetData()
        {
            if (SSW)
            {
                return swByte;
            }
            else
            {
                return cpu.memory[cpu.addressBus];
            }
        }

        public byte GetSwitches(int start)
        {
            byte value = 0;
            for (int i = start; i < start + 8; i++)
            {
                if (((CheckBox)switchBox.Controls[i]).Checked)
                {
                    value |= (byte)(1 << (i - start));
                }
            }
            return value;
        }

        public void execute(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                cpu.InstructionDecoder();
            }
        }

        private void OnSync(Object sender, EventArgs e)
        {
            statusByte = cpu.dataBus;
            if ((statusByte == 0) || ((statusByte & 0b10) == 0))
            {
                cpu.dataBus = 0xFF;
            }
            else if ((statusByte == 0b01000010))
            {
                if (cpu.addressBus == 0xFFFF)
                {
                    cpu.dataBus = GetSwitches(8);
                }
                else
                {
                    cpu.dataBus = 0xFF;
                }
            }
            else
            {
                cpu.dataBus = cpu.memory[cpu.addressBus];
            }
            if (stop && ((statusByte & 0b00100000) > 0))
            {
                cpu.READY = false;
                stop = false;
            }
            if (step)
            {
                cpu.READY = false;
                step = false;
            }
        }

        private void updatePanel()
        {
            while (true)
            {
                for (int i = 0; i < 16; i++)
                {
                    if ((cpu.addressBus & (1 << i)) > 0)
                    {
                        addressLights[i] = 140;
                    }
                    else
                    {
                        if (addressLights[i] > 0)
                        {
                            addressLights[i]--;
                        }
                    }
                }
            }
        }

        
        private async void AltairPanel_Shown(object sender, EventArgs e)
        {

            cpu.READY = false;
            cpuTask = Task.Run(() => execute(executeCancelSource.Token), executeCancelSource.Token);
            Task.Run(updatePanel);
            int counter;
            while (true)
            {
                counter = 0;
                foreach (Control control in addressBox.Controls)
                {
                    if (control is CheckBox light)
                    {
                        if (addressLights[counter] > 0)
                        {
                            light.Checked = true;
                        }
                        else
                        {
                            light.Checked = false;
                        }
                        light.Refresh();
                        counter++;
                    }
                }
                counter = 0;
                foreach (Control control in dataBox.Controls)
                {
                    if (control is CheckBox light)
                    {
                        if ((cpu.dataBus & (1 << counter)) > 0)
                        {
                            light.Checked = true;
                        }
                        else
                        {
                            light.Checked = false;
                        }
                        light.Refresh();
                        counter++;
                    }
                }
                counter = 0;
                foreach (Control control in statusBox.Controls)
                {
                    if (control is CheckBox light)
                    {
                        if ((statusByte & (1 << counter)) > 0)
                        {
                            light.Checked = true;
                        }
                        else
                        {
                            light.Checked = false;
                        }
                        light.Refresh();
                        counter++;
                    }
                }
                inteLight.Checked = cpu.INTE;
                inteLight.Refresh();
                waitLight.Checked = cpu.WAIT;
                waitLight.Refresh();
                await Task.Delay(5);
            }
        }

        private void stopBtn_Click(object sender, EventArgs e)
        {
            stop = true;
        }

        private void runBtn_Click(object sender, EventArgs e)
        {
            cpu.READY = true;
        }

        private void stepBtn_Click(object sender, EventArgs e)
        {
            if (cpu.WAIT)
            {
                step = true;
                cpu.READY = true;
            }
        }

        private async void resetBtn_Click(object sender, EventArgs e)
        {
            cpu.READY = false;
            executeCancelSource.Cancel();
            await cpuTask;
            executeCancelSource = new CancellationTokenSource();
            step = false;
            stop = false;
            cpu.Reset();
            cpu.setToken(executeCancelSource.Token);
            cpuTask = Task.Run(() => execute(executeCancelSource.Token), executeCancelSource.Token);
        }

        private void depositBtn_Click(object sender, EventArgs e)
        {
            if (cpu.WAIT)
            {
                cpu.dataBus = GetSwitches(0);
                cpu.memory[cpu.addressBus] = cpu.dataBus;
            }
        }

        private void exmBtn_Click(object sender, EventArgs e)
        {
            if (cpu.WAIT)
            {
                swByte = 0xC3;
                SSW = true;
                step = true;
                cpu.READY = true;
                while (cpu.READY)
                {

                }
                swByte = GetSwitches(0);
                step = true;
                cpu.READY = true;
                while (cpu.READY)
                {

                }
                swByte = GetSwitches(8);
                step = true;
                cpu.READY = true;
                while (cpu.READY)
                {

                }
                cpu.dataBus = cpu.memory[cpu.addressBus];
                SSW = false;
            }
        }

        private void exmNxtBtn_Click(object sender, EventArgs e)
        {
            if (cpu.WAIT)
            {
                SSW = true;
                swByte = 0x00;
                step = true;
                cpu.READY = true;
                while (cpu.READY)
                {

                }
                cpu.dataBus = cpu.memory[cpu.addressBus];
                SSW = false;
            }
        }

        private void depNxtBtn_Click(object sender, EventArgs e)
        {
            if (cpu.WAIT)
            {
                SSW = true;
                swByte = 0x00;
                step = true;
                cpu.READY = true;
                while (cpu.READY)
                {

                }
                cpu.dataBus = GetSwitches(0);
                cpu.memory[cpu.addressBus] = cpu.dataBus;
                SSW = false;
            }
        }
    }
}
