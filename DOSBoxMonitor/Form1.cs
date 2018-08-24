using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

//monIgnore_

namespace DOSBoxMonitor
{
    public partial class Form1 : Form
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
          int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);
        [DllImport("kernel32.dll")]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);
        [DllImport("kernel32.dll")]
        static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        public Form1()
        {
            InitializeComponent();
        }

        int debugCOMSize;
        int debugGlobalShift = -1;
        List<Variable> debugVars = new List<Variable>();

        const int AX_OFFSET = 0x0074B1C0, BX_OFFSET = 0x0074B1CC, CX_OFFSET = 0x0074B1C4, DX_OFFSET = 0x0074B1C8, SI_OFFSET = 0x0074B1D8, DI_OFFSET = 0x0074B1DC;

        private void monitor_Tick(object sender, EventArgs e)
        {
            int bytesRead = 0;

            short AX, BX, CX, DX, SI, DI;
            byte[] buffer = new byte[2];
            //получаем значения регистров
            ReadProcessMemory((int)processHandle, AX_OFFSET, buffer, 2, ref bytesRead);
            AX = (short)(buffer[0] + ((int)buffer[1]) * 256);
            ReadProcessMemory((int)processHandle, BX_OFFSET, buffer, 2, ref bytesRead);
            BX = (short)(buffer[0] + ((int)buffer[1]) * 256);
            ReadProcessMemory((int)processHandle, CX_OFFSET, buffer, 2, ref bytesRead);
            CX = (short)(buffer[0] + ((int)buffer[1]) * 256);
            ReadProcessMemory((int)processHandle, DX_OFFSET, buffer, 2, ref bytesRead);
            DX = (short)(buffer[0] + ((int)buffer[1]) * 256);
            ReadProcessMemory((int)processHandle, SI_OFFSET, buffer, 2, ref bytesRead);
            SI = (short)(buffer[0] + ((int)buffer[1]) * 256);
            ReadProcessMemory((int)processHandle, DI_OFFSET, buffer, 2, ref bytesRead);
            DI = (short)(buffer[0] + ((int)buffer[1]) * 256);

            //заполняем таблицу
            int to = 10;
            if (hex)
                to = 16;
            dataGridView1.Rows[0].Cells[1].Value = ToSSAnsSign(AX, to, false);
            dataGridView1.Rows[0].Cells[2].Value = ToSSAnsSign((short)(AX / 256), to, true);
            dataGridView1.Rows[0].Cells[3].Value = ToSSAnsSign((short)(AX % 256), to, true);

            dataGridView1.Rows[1].Cells[1].Value = ToSSAnsSign(BX, to, false);
            dataGridView1.Rows[1].Cells[2].Value = ToSSAnsSign((short)(BX / 256), to, true);
            dataGridView1.Rows[1].Cells[3].Value = ToSSAnsSign((short)(BX % 256), to, true);

            dataGridView1.Rows[2].Cells[1].Value = ToSSAnsSign(CX, to, false);
            dataGridView1.Rows[2].Cells[2].Value = ToSSAnsSign((short)(CX / 256), to, true);
            dataGridView1.Rows[2].Cells[3].Value = ToSSAnsSign((short)(CX % 256), to, true);

            dataGridView1.Rows[3].Cells[1].Value = ToSSAnsSign(DX, to, false);
            dataGridView1.Rows[3].Cells[2].Value = ToSSAnsSign((short)(DX / 256), to, true);
            dataGridView1.Rows[3].Cells[3].Value = ToSSAnsSign((short)(DX % 256), to, true);

            dataGridView1.Rows[4].Cells[1].Value = ToSSAnsSign(SI, to, false);
            dataGridView1.Rows[4].Cells[2].Value = ToSSAnsSign((short)(SI / 256), to, true);
            dataGridView1.Rows[4].Cells[3].Value = ToSSAnsSign((short)(SI % 256), to, true);

            dataGridView1.Rows[5].Cells[1].Value = ToSSAnsSign(DI, to, false);
            dataGridView1.Rows[5].Cells[2].Value = ToSSAnsSign((short)(DI / 256), to, true);
            dataGridView1.Rows[5].Cells[3].Value = ToSSAnsSign((short)(DI % 256), to, true);

            if (debugGlobalShift != -1)//если известен адрес начала отлаживаемой програмы в памяти
            {
                int x = 0;
                foreach (Variable v in debugVars)
                {
                    int len = v.lenInE;
                    if (v.dword)
                        len *= 2;//dword - удваиваем длину
                    buffer = new byte[len];
                    ReadProcessMemory((int)processHandle, debugGlobalShift + v.shiftInMemory, buffer, len, ref bytesRead);
                    string toGrid = "";
                    if (v.isString)
                        toGrid = "\"";//если это строка, то выводим кавычки
                    for (int i = 0; i < buffer.Length; ++i)
                        if (v.isString)
                            toGrid += (char)buffer[i];
                        else
                        {
                            if (v.dword)
                            {
                                toGrid += ToSSAnsSign((short)(buffer[i] + buffer[i + 1] * 256), to, false) + ",";
                                ++i;//тк dword - 2 байта
                            }
                            else
                                toGrid += ToSSAnsSign(buffer[i], to, true) + ",";
                        }
                    if (v.isString)
                        toGrid += "\"";
                    dataGridView2.Rows[x].Cells[0].Value = v.name;
                    dataGridView2.Rows[x].Cells[1].Value = toGrid;
                    ++x;
                }
            }
        }

        string ToSSAnsSign(short n, int to, bool isByte)
        {
            int len = isByte ? 2 : 4;
            string output;
            if (!sgn)
                output = Convert.ToString((ushort)n, to).ToUpper();
            else
                output = Convert.ToString(n, to).ToUpper();
            if (hex)
                output = ("0000" + output).Substring(4 + output.Length - len) + "h";//дополняем ведущими нулями
            return output;
        }

        bool hex = false;
        bool sgn = false;
        SYSTEM_INFO SI;

        const int PROCESS_WM_READ = 0x0010;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        IntPtr processHandle;

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                Process process = Process.GetProcessesByName("DOSBox")[0];
                processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, process.Id);
                SI = new SYSTEM_INFO();
                GetSystemInfo(out SI);
                monitor.Enabled = true;
            }
            catch
            {
                Console.Beep();
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            hex = radioButton1.Checked;
        }

        bool monitorIsWorking = false;
        void ResetMon()
        {
            button2.Text = "Запустить монитор переменных";
            debugCOMSize = -1;
            debugGlobalShift = -1;
            debugVars.Clear();
            dataGridView2.Rows.Clear();
            textBox1.Enabled = true;
            comboBox1.Items.Clear();
            button2.Enabled = true;
        }
        string GenNNums(int num, int len)
        {
            string o = "";
            for (int i = 0; i < len - 1; ++i)
                o += num.ToString() + ",";
            return o + num.ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string targetASM = textBox1.Text;
            monitorIsWorking = !monitorIsWorking;

            try
            {
                Process process = Process.GetProcessesByName("DOSBox")[0];
                processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, process.Id);
                SI = new SYSTEM_INFO();
                GetSystemInfo(out SI);
                monitor.Enabled = true;
            }
            catch
            {
                if (monitorIsWorking)
                    Console.Beep();
                monitorIsWorking = false;
            }

            if (!monitorIsWorking)
            {
                ResetMon();
                return;
            }
            if (!File.Exists(targetASM))
            {
                Console.Beep();
                monitorIsWorking = false;
                return;
            }

            button2.Enabled = false;
            button2.Text = "Остановить монитор переменных";
            textBox1.Enabled = false;
            string orgContent = File.ReadAllText(targetASM).Replace("\t", " ").Replace(" DB ", " db ").Replace(" DW ", " dw ").Replace("dup (", "dup(");

            Regex dbSearch = new Regex(@"\s*[a-zA-Z0-9_-]+\s+(db)\s+[^;\r\n]+");
            Regex dwSearch = new Regex(@"\s*[a-zA-Z0-9_-]+\s+(dw)\s+[^;\r\n]+");//regex для поиска db и dw
            MatchCollection DBs = dbSearch.Matches(orgContent);
            MatchCollection DWs = dwSearch.Matches(orgContent);

            string matchValue, varName, varValue;
            bool containsString;
            int varLen;
            foreach (Match db in DBs)
            {
                containsString = false;
                matchValue = db.Value.Trim();
                varName = matchValue.Substring(0, matchValue.IndexOf(" db ")).TrimEnd();
                varValue = matchValue.Substring(matchValue.IndexOf(" db ") + 4).TrimStart();
                varLen = CountE(varValue, ref containsString);
                if (varLen <= 100 && !varName.StartsWith("monIgnore_"))//игнорируем слишком длинные переменные и переменные, помеченные как игнорируемые
                    debugVars.Add(new Variable(varName, db.Index, varValue, varLen, containsString));
            }
            foreach (Match dw in DWs)
            {
                containsString = false;
                matchValue = dw.Value.Trim();
                varName = matchValue.Substring(0, matchValue.IndexOf(" dw ")).TrimEnd();
                varValue = matchValue.Substring(matchValue.IndexOf(" dw ") + 4).TrimStart();
                varLen = CountE(varValue, ref containsString);
                if (varLen <= 100 && !varName.StartsWith("monIgnore_"))
                    debugVars.Add(new Variable(varName, dw.Index, varValue, varLen, containsString, true));
            }

            if (!useCompiled)
            {
                LogRT("Компилируем исходный код...");

                if (!CompileASM(targetASM))
                {
                    MessageBox.Show("Ошибка компиляции исходного кода");
                    monitorIsWorking = false;
                    ResetMon();
                    return;
                }
            }
            else
            {
                string filepath = Path.GetFileNameWithoutExtension(targetASM).Replace(" ", "");
                if (filepath.Length > 8)
                    filepath = filepath.Substring(0, 8);
                if (File.Exists("todbg.com"))
                    File.Delete("todbg.com");
                if (!File.Exists(filepath + ".COM"))
                {
                    MessageBox.Show(filepath + ".com не найден");
                    monitorIsWorking = false;
                    ResetMon();
                    return;
                }
                File.Copy(filepath + ".COM", "todbg.com");
            }

            if (File.Exists("original.com"))
                File.Delete("original.com");
            File.Move("todbg.com", "original.com");

            int dlt = 0, valuePos;
            string vpTemp;
            debugVars.Sort(delegate(Variable a, Variable b) { return a.dataPosInText.CompareTo(b.dataPosInText); });//сортируем по адресу начала
            debugVars = debugVars.GroupBy(o => o.name).Select(g => g.First()).ToList();//TODO: костыль, избавляющий от повторов. Понятия не имею почему они могут происходить

            string mdf1 = orgContent, mdf2 = orgContent;
            string prt1, prt2;
            foreach (Variable v in debugVars)
            {
                valuePos = v.dataPosInText + dlt;
                vpTemp = mdf1.Substring(valuePos);
                if (v.dword)
                    valuePos += vpTemp.IndexOf(" dw ") + 4;
                else
                    valuePos += vpTemp.IndexOf(" db ") + 4;
                while (mdf1[valuePos] == ' ')
                    valuePos++;

                prt1 = mdf1.Substring(0, valuePos);//заполяем нулями значение переменной
                prt2 = mdf1.Substring(valuePos + v.varData.Length);
                string repBy = GenNNums(0, v.lenInE);
                mdf1 = prt1 + repBy + prt2;
                dlt += repBy.Length - v.varData.Length;
            }
            dlt = 0;
            foreach (Variable v in debugVars)
            {
                valuePos = v.dataPosInText + dlt;
                vpTemp = mdf2.Substring(valuePos);
                if (v.dword)
                    valuePos += vpTemp.IndexOf(" dw ") + 4;
                else
                    valuePos += vpTemp.IndexOf(" db ") + 4;
                while (mdf2[valuePos] == ' ')
                    valuePos++;

                prt1 = mdf2.Substring(0, valuePos);
                prt2 = mdf2.Substring(valuePos + v.varData.Length);
                string repBy = GenNNums(1, v.lenInE);//заполняем единицами значение переменной
                mdf2 = prt1 + repBy + prt2;
                dlt += repBy.Length - v.varData.Length;
            }
            LogRT("Компилируем изменённый код(1)...");
            File.WriteAllText("mdf1.asm", mdf1);
            File.WriteAllText("mdf2.asm", mdf2);
            if (!CompileASM(Environment.CurrentDirectory + "\\mdf1.asm"))
            {
                MessageBox.Show("Ошибка компиляции изменённого кода");
                monitorIsWorking = false;
                ResetMon();
                return;
            }
            File.Move("todbg.com", "mdf1.com");
            LogRT("Компилируем изменённый код(2)...");
            CompileASM(Environment.CurrentDirectory + "\\mdf2.asm");
            File.Move("todbg.com", "mdf2.com");

            List<byte> orig = new List<byte>(File.ReadAllBytes("original.com"));
            List<byte> chng1 = new List<byte>(File.ReadAllBytes("mdf1.com"));
            List<byte> chng2 = new List<byte>(File.ReadAllBytes("mdf2.com"));

            debugCOMSize = orig.Count;

            if (orig.Count != chng1.Count)
            {
                MessageBox.Show("Изменённый код был изменён неверно, нарушены правила оформления?");
                monitorIsWorking = false;
                ResetMon();
                return;
            }

            for (int i = 0; i < orig.Count; ++i)
                if (orig[i] == chng1[i] && orig[i] == chng2[i])//смысл двух комплияций - точно выделить положение каждой переменной, тк при замене на какую-то константу новое значение может совпать со старым и тогда местоположение переменной в памяти будет определено неверено
                    chng1[i] = 255;

            int currVar = 0;
            for (int i = 0; i < chng1.Count; ++i)
                if (chng1[i] != 255)
                {
                    debugVars[currVar].shiftInMemory = i;
                    i += debugVars[currVar].lenInE * (debugVars[currVar].dword ? 2 : 1) - 1;//dword = 2bytes, перепрыгиваем эту переменную в памяти
                    currVar++;
                }
            LogRT("Ищем программу в памяти DOSBox...");


            debugGlobalShift = findInMem(orig.ToArray());
            srcState.Text = Convert.ToString(debugGlobalShift, 16).ToUpper();

            foreach (Variable v in debugVars)
            {
                comboBox1.Items.Add(v.name);
                dataGridView2.Rows.Add(v.name, "?");
            }
            comboBox1.SelectedIndex = 0;
            button2.Enabled = true;
            if (File.Exists("original.com"))
                File.Delete("original.com");
            if (File.Exists("mdf1.com"))
                File.Delete("mdf1.com");
            if (File.Exists("mdf2.com"))
                File.Delete("mdf2.com");
        }

        /// <summary>
        /// Поиск в памяти DOSBox заданного массива данных
        /// </summary>
        /// <param name="orig">искомый массив байт</param>
        /// <returns>сдвиг в памяти</returns>
        int findInMem(byte[] orig)
        {
            int minNotEq = orig.Length;
            int bytesRead = 0, bytesRead2 = 0, tempShift = -1;
            byte[] targetCom = new byte[orig.Length];
            IntPtr finder = SI.minimumApplicationAddress;
            IntPtr maxAddress = SI.maximumApplicationAddress;
            int finderI = (int)finder;
            int maxAddressI = (int)maxAddress;
            MEMORY_BASIC_INFORMATION memInfo = new MEMORY_BASIC_INFORMATION();
            int pageN = 0;
            while (finderI < maxAddressI)
            {
                if (pageN % 5 == 0)
                {
                    srcState.Text = Convert.ToString(finderI, 16).ToUpper();
                    Application.DoEvents();
                }

                VirtualQueryEx(processHandle, finder, out memInfo, 28);
                if (memInfo.Protect == 0x04 && memInfo.State == 0x00001000)//если страница памяти доступна для чтения
                {
                    byte[] page = new byte[memInfo.RegionSize];
                    ReadProcessMemory((int)processHandle, memInfo.BaseAddress, page, memInfo.RegionSize, ref bytesRead);
                    bytesRead = Math.Min(memInfo.RegionSize, bytesRead);
                    for (int i = 0; i < bytesRead; ++i)
                        if (page[i] == orig[0])//нашли предполагаемое начало
                        {
                            ReadProcessMemory((int)processHandle, memInfo.BaseAddress + i, targetCom, debugCOMSize, ref bytesRead2);
                            if (bytesRead2 == 0)
                                continue;//пробуем считать кусок памяти размером с программу, если ок, идём дальше
                            int notEq = 0;
                            for (int j = 0; j < debugCOMSize; ++j)
                                if (targetCom[j] != orig[j])
                                    notEq++;//накапливаем штрафные очки за несовпадения
                            if (notEq <= minNotEq)
                            {
                                tempShift = finderI + i;//минимум по различиям - скорее всего искомый массив в памяти
                                minNotEq = notEq;
                                break;
                            }
                        }
                }
                finderI += memInfo.RegionSize;
                finder = new IntPtr(finderI);
                pageN++;
            }
            return tempShift;
        }

        void LogRT(string what)
        {
            richTextBox1.Text = what + "\n" + richTextBox1.Text;
        }

        int ProcBlock(string block, ref bool containsString)
        {
            string tmp = block.Trim();
            if (tmp[0] == '\'')
                return tmp.Length - 2;//строка? возвращаем длину строки
            if (tmp.Contains("dup("))
            {
                string subDup = tmp.Substring(tmp.IndexOf("dup(") + 4);
                int delta = 1;
                int ptr = 0;
                while (delta != 0)
                {
                    if (subDup[ptr] == '(')
                        delta++;
                    if (subDup[ptr] == ')')
                        delta--;
                    ptr++;//ищем закрывающую скобку для этого dup
                }
                subDup = subDup.Substring(0, ptr - 1);
                string count = tmp.Split(' ')[0];
                if (count.Contains("*") || count.Contains("+") || count.Contains("-") || count.Contains("/"))//если это вычисляемое значение, игнорируем его
                    count = "150";
                int countI = 150;
                if (!int.TryParse(count, out countI))
                    countI = 150;
                return countI * CountE(subDup, ref containsString);
            }
            return 1;
        }

        int CountE(string data, ref bool containsString)//подсчёт тех колва тех самых необходимых констант для замены значения переменной
        {
            int len = 0;
            bool inString = false;
            for (int i = 0; i < data.Length; ++i)
            {
                if (data[i] == '\'')
                {
                    inString = !inString;//заменяем внутри строк управляющие символы ( ) , на пробелы, чтобы потом не путаться из-за них при парсинге выражения
                    containsString = true;
                }
                if (inString && (data[i] == ',' || data[i] == ')' || data[i] == '('))
                    data = data.Remove(i, 1).Insert(i, " ");
            }
            string[] blocks = data.Split(',');
            foreach (string block in blocks)
                len += ProcBlock(block, ref containsString);
            return len;
        }

        bool CompileASM(string argpath)
        {
            bool needToByCpyed = false;
            if (!Path.IsPathRooted(argpath))
                argpath = Environment.CurrentDirectory + "\\" + argpath;
            string filepath = Path.GetFileNameWithoutExtension(argpath);

            if (filepath.Length > 8)
            {
                filepath = filepath.Substring(0, 8);
                needToByCpyed = true;
            }

            if (filepath.Contains(" "))
            {
                filepath = filepath.Replace(" ", "");
                needToByCpyed = true;
            }

            if (Path.GetDirectoryName(argpath) != Environment.CurrentDirectory)
            {
                needToByCpyed = true;
            }
            if (needToByCpyed)
            {
                File.Copy(argpath, Environment.CurrentDirectory + "\\" + filepath + ".asm", true);
            }

            string content = File.ReadAllText(filepath + ".asm");

            File.WriteAllText("compile.bat", "@echo off\ntasm.exe [x].asm /l/zi\ntlink.exe [x].obj /tdc\nexit".Replace("[x]", filepath));

            ProcessStartInfo psi = new ProcessStartInfo("DOSBox.lnk");
            psi.Arguments = Environment.CurrentDirectory + "\\compile.bat";
            Process pr = Process.Start(psi);

            while (!pr.HasExited)
                Application.DoEvents();

            if (needToByCpyed && File.Exists(filepath + ".asm"))
                File.Delete(filepath + ".asm");
            if (File.Exists(filepath + ".LST"))
                File.Delete(filepath + ".LST");
            if (File.Exists(filepath + ".MAP"))
                File.Delete(filepath + ".MAP");
            if (File.Exists(filepath + ".OBJ"))
                File.Delete(filepath + ".OBJ");
            if (File.Exists("compile.bat"))
                File.Delete("compile.bat");

            if (File.Exists("todbg.com"))
                File.Delete("todbg.com");
            if (File.Exists(filepath + ".COM"))
            {
                File.Move(filepath + ".COM", "todbg.com");
                return true;
            }
            return false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            debugVars[comboBox1.SelectedIndex].isString = !debugVars[comboBox1.SelectedIndex].isString;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            sgn = checkBox1.Checked;
        }

        bool useCompiled = true;
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            useCompiled = checkBox2.Checked;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (!File.Exists("TASM.EXE") || !File.Exists("TLINK.EXE") || !File.Exists("DOSBox.lnk"))
            {
                MessageBox.Show("Проверьте, все ли файлы на месте: TASM.EXE, TLINK.EXE, DOSBox.lnk");
                System.Threading.Thread.CurrentThread.Abort();
                return;
            }

            dataGridView1.Rows.Add("AX", 0, 0, 0, 0);
            dataGridView1.Rows.Add("BX", 0, 0, 0, 0);
            dataGridView1.Rows.Add("CX", 0, 0, 0, 0);
            dataGridView1.Rows.Add("DX", 0, 0, 0, 0);
            dataGridView1.Rows.Add("SI", 0, 0, 0, 0);
            dataGridView1.Rows.Add("DI", 0, 0, 0, 0);
        }
    }

    class Variable
    {
        public string name;
        public int dataPosInText;
        public int lenInE;//длина в количестве констант, перечисляемых через запятую. Ну, |"varname db 0,0,0,0,0"| = 5
        public string varData;
        public int shiftInMemory = -1;
        public bool isString = false;
        public bool dword = false;

        public Variable(string name, int dataPosInText, string data, int len, bool isString, bool dwr = false)
        {
            this.isString = isString;
            dword = dwr;
            varData = data;
            this.name = name;
            this.dataPosInText = dataPosInText;
            lenInE = len;
        }
    }

    public struct MEMORY_BASIC_INFORMATION
    {
        public int BaseAddress;
        public int AllocationBase;
        public int AllocationProtect;
        public int RegionSize;
        public int State;
        public int Protect;
        public int lType;
    }

    public struct SYSTEM_INFO
    {
        public ushort processorArchitecture;
        ushort reserved;
        public uint pageSize;
        public IntPtr minimumApplicationAddress;
        public IntPtr maximumApplicationAddress;
        public IntPtr activeProcessorMask;
        public uint numberOfProcessors;
        public uint processorType;
        public uint allocationGranularity;
        public ushort processorLevel;
        public ushort processorRevision;
    }
}
