using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Timers;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;

namespace ReaderDemo
{
    public partial class MainForm : Form
    {
        Program Program = new Program();
        public int res, k, Detect, cmd;
        public int m_hScanner = -1, m_hSocket = -1, OK = 0;
        public int nBaudRate = 0, Interval=100, EPC_Word;
        public string szPort;
        public int HardVersion, SoftVersion;
        public int hwnd;
        public int nidEvent, mem, ptr, len;
        public int Read_times;
        public int m_antenna_sel, RS485Address = 0;
        public byte Mask;
        public byte[] AccessPassword = new byte[4];
        public byte[] mask = new byte[96];
        public byte[] IDTemp = new byte[12];
        public byte[,] TagBuffer = new byte[100, 130];
        public byte[] AccessPassWord = new byte[4];
        public byte[,] TagNumber = new byte[100, 80];
        public int ComMode;
        public string readerip;
        public uint readerport;
        public string hostip;
        public uint hostport;
        public byte connect_OK = 0;
        public int count_test = 0;
        public Socket NetSocket = null;
        public string ReaderIP;
        public int ReaderPort;
        public byte[] IPrecebuffer = new byte[64];

        public Socket EpcSocket = null;
        public int AutoPort;
        public byte[] Epcrecebuffer = new byte[256];
        public string[] EpcStr = new string[7];
        public int RssiOut = 1;

        //自动模式 标签是否只读一次
        bool isRead = false;
        List<string> epcOnly = new List<string>();

        //命令模式下过滤
        public int StartBit;//起始地址bit
        public int DataLenth;//数据长度bit
        public string Datastr;//过滤数据

        public delegate void NetInvoke();

        public MainForm()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            int i;

            string strtemp = "";
            double Freq = 0, jumpFreq = 0, temp = 0;

            comboBox19.SelectedIndex = 0;
            button27.Enabled = false;
            comboBox19.Enabled = false;
            button13.Enabled = false;

            IPAddress[] arrIPAddresses = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress ips in arrIPAddresses)
            {
                if (ips.AddressFamily.Equals(AddressFamily.InterNetwork))
                {
                    textBox37.Text = ips.ToString();
                }
            }

            Freq = 865.00;
            jumpFreq = 0.500;

            for (i = 0; i < 7; i++)
            {
                temp = Freq + i * jumpFreq;
                strtemp = string.Format("{0,0:D}{1,0:s}{2,7:F03}", i, ":", temp);
                comboBox14.Items.Add(strtemp);
                comboBox15.Items.Add(strtemp);
            }

            Freq = 902.00;
            jumpFreq = 0.500;

            for (i = 6; i < 59; i++)
            {
                temp = Freq + (i - 6) * jumpFreq;
                strtemp = string.Format("{0,0:D}{1,0:s}{2,7:F03}", i + 1, ":", temp);
                comboBox14.Items.Add(strtemp);
                comboBox15.Items.Add(strtemp);
            }

            comboBox14.SelectedIndex = 7;
            comboBox15.SelectedIndex = 59;
            comboBox13.SelectedIndex = 3;

        }

        #region EPCC1G2
        #region Timer
        unsafe private void timer1_Tick(object sender, EventArgs e)
        {
            int i, j, nCounter = 0, ID_len = 0, ID_len_temp = 0, be_antenna, success;
            string str, strtemp;
            byte[] DB = new byte[128];
            byte[] IDBuffer = new byte[7680];
            byte[,] chArray = new byte[100, 2600];
            string RSSI = "";
            byte[] temp = new byte[10 * 260];

            Read_times++;
            Thread.Sleep(Interval);
            be_antenna = 1;
                        switch (ComMode)
                        {
                            case 0:
                                res = Program.SetAntenna(m_hScanner, be_antenna, RS485Address);
                                break;
                            case 1:
                                res = Program.Net_SetAntenna(m_hSocket, be_antenna);
                                break;
                        }

            ListViewItem item = new ListViewItem();
            switch (nidEvent)
            {
                case 1:
                    if (be_antenna != 0)
                    {
                        Array.Clear(TagBuffer, 0, TagBuffer.Length);
                        count_test++;
                        switch (ComMode)
                        {
                            case 0:
                                if (2 == mem)
                                {
                                    unsafe
                                    {
                                        int row = chArray.GetUpperBound(0) + 1;
                                        int col = chArray.GetUpperBound(1) + 1;

                                        fixed (byte* fp = chArray)
                                        {
                                            byte*[] farr = new byte*[row];
                                            for (i = 0; i < row; i++)
                                            {
                                                farr[i] = fp + i * col;
                                            }

                                            fixed (byte** fpp = farr)
                                            {
                                                res = Program.EPC1G2_ReadLabelTID(m_hScanner, mem, ptr, len, mask, fpp, ref nCounter, RS485Address);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (RssiOut == 1)
                                    {
                                        res = Program.EPC1G2_ReadLabelIDRSSI(m_hScanner, mem, ptr, len, mask, IDBuffer, ref nCounter, RS485Address);
                                    }
                                    else
                                    {
                                        res = Program.EPC1G2_ReadLabelID(m_hScanner, mem, ptr, len, mask, IDBuffer, ref nCounter, RS485Address);
                                    }
                                }
                                break;

                            case 1:
                                if (2 == mem)
                                {
                                    //res = Program.Net_EPC1G2_ReadLabelTID(m_hSocket, mem, ptr, len, mask, chArray, ref nCounter);
                                }
                                else
                                {
                                    res = Program.Net_EPC1G2_ReadLabelID(m_hSocket, mem, ptr, len, mask, IDBuffer, ref nCounter);
                                }
                                break;

                        }
                        if (res == OK)
                        {
                            if (RssiOut == 1)
                            {
                                for (i = 0; i < nCounter; i++)
                                {
                                    ID_len_temp = IDBuffer[ID_len] * 2 + 2;

                                    for (j = 0; j < ID_len_temp; j++)
                                    {
                                        TagBuffer[i, j] = IDBuffer[ID_len + j];
                                    }

                                    ID_len += ID_len_temp;
                                }
                                for (i = 0; i < nCounter; i++)
                                {
                                    str = "";
                                    strtemp = "";
                                    ID_len = TagBuffer[i, 0] * 2;

                                    for (j = 0; j < ID_len; j++)
                                    {
                                        strtemp = TagBuffer[i, j + 1].ToString("X2");
                                        str += strtemp;
                                        RSSI = "-" + TagBuffer[i, ID_len + 1].ToString();
                                    }

                                    for (j = 0; j < k; j++)
                                    {
                                        ID_len_temp = Convert.ToInt32(strtemp, 16) * 2;

                                        if (ID_len == ID_len_temp)
                                        {
                                        }
                                    }
                                    if (j == k)
                                    {
                                        if (str.Length > StartBit)
                                        {
                                            if (str.Substring(StartBit, DataLenth) == Datastr)
                                            {
                                                item.SubItems.Add(str);
                                                item.SubItems.Add(RSSI.ToString());
                                                item.SubItems.Add(TagBuffer[i, 0].ToString("X2"));
                                                success = 1;
                                                item.SubItems.Add(success.ToString());
                                                item.SubItems.Add(count_test.ToString());
                                                k++;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                int kk = 0;
                                for (i = 0; i < nCounter; i++)
                                {
                                    ID_len_temp = IDBuffer[ID_len] * 2 + 1;
                                    if (2 == mem)
                                    {
                                        //如果是TID
                                        ID_len_temp = IDBuffer[ID_len];
                                        for (kk = 0; kk < 1024; kk++)
                                        {
                                            temp[kk] = chArray[i, kk];
                                        }

                                        if (temp[2] == (char)0xEF)
                                        {

                                            if (0 <= temp[1] - 2 && temp[1] - 2 < 1024)
                                            {
                                                int ireLen = (byte)(temp[temp[1] - 2]);
                                                int immlen = (byte)(temp[1] - ireLen - 2);

                                                if (0 <= temp[1] - ireLen - 2 && temp[1] - ireLen - 2 < 1024)
                                                {
                                                    IDBuffer[0] = (byte)ireLen;
                                                    if (0 <= immlen && immlen < (int)(1024 - ireLen))
                                                    {
                                                        for (kk = 0; kk < ireLen; kk++)
                                                        {
                                                            IDBuffer[kk + 1] = temp[temp[1] - ireLen - 2 + kk];
                                                        }

                                                        ID_len_temp = ireLen + 1;

                                                        for (j = 0; j < ID_len_temp; j++)
                                                        {
                                                            TagBuffer[i, j] = IDBuffer[j];
                                                        }
                                                    }
                                                    else
                                                    {
                                                        kk = 0;//ERROR3
                                                    }
                                                }
                                                else
                                                {
                                                    kk = 0;//ERROR2
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                kk = 0;//ERROR1

                                                continue;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        for (j = 0; j < ID_len_temp; j++)
                                        {
                                            TagBuffer[i, j] = IDBuffer[ID_len + j];
                                        }
                                    }
                                    ID_len += ID_len_temp;
                                }
                                for (i = 0; i < nCounter; i++)
                                {
                                    str = "";
                                    strtemp = "";
                                    ID_len = TagBuffer[i, 0] * 2;
                                    if (2 == mem)
                                    {
                                        ID_len = TagBuffer[i, 0];
                                    }
                                    for (j = 0; j < ID_len; j++)
                                    {
                                        strtemp = TagBuffer[i, j + 1].ToString("X2");
                                        str += strtemp;
                                    }

                                    for (j = 0; j < k; j++)
                                    {
                                        ID_len_temp = Convert.ToInt32(strtemp, 16) * 2;
                                        if (2 == mem)
                                        {
                                            ID_len_temp = Convert.ToInt32(strtemp, 16);
                                        }
                                    }
                                    if (j == k)
                                    {
                                        if (str.Length > StartBit)
                                        {
                                            if (str.Substring(StartBit, DataLenth) == Datastr)
                                            {
                                                item.SubItems.Add(str);
                                                item.SubItems.Add(RSSI.ToString());
                                                item.SubItems.Add(TagBuffer[i, 0].ToString("X2"));
                                                success = 1;
                                                item.SubItems.Add(success.ToString());
                                                item.SubItems.Add(count_test.ToString());
                                                k++;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                    }
                    break;
                case 2:
                    if (be_antenna != 0)
                    {
                        switch (ComMode)
                        {
                            case 0:
                                res = Program.EPC1G2_ReadWordBlock(m_hScanner, Convert.ToByte(EPC_Word), IDTemp, Convert.ToByte(mem), Convert.ToByte(0), Convert.ToByte(1), DB, AccessPassWord, RS485Address);
                                break;
                            case 1:
                                res = Program.Net_EPC1G2_ReadWordBlock(m_hSocket, Convert.ToByte(EPC_Word), IDTemp, Convert.ToByte(mem), Convert.ToByte(0), Convert.ToByte(1), DB, AccessPassWord);
                                break;
                        }
                        if (res == OK)
                        {
                            str = "";
                            for (i = 0; i < Convert.ToByte(1) * 2; i++)
                            {
                                strtemp = DB[i].ToString("X2");
                                str += strtemp;
                            }
                            microPost(str);
                        }
                    }
                    break;
            }
        }
        #endregion
        #region post methods from all over internet
        public void microPost(string dataa)
        {
            WebRequest request = WebRequest.Create("http://127.0.0.1:3000/");
            request.Method = "Post";
            string postData = "id=" + dataa;
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;
            Stream dataStream;
            try
            {
                dataStream = request.GetRequestStream();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            WebResponse response = request.GetResponse();
            using (dataStream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();
                var user = new JavaScriptSerializer();
                List<userss> users = user.Deserialize<List<userss>>(responseFromServer);
                foreach (userss i in users)
                {
                    listView2.Items.Add(i.user_name);
                }
            }
            response.Close();
        }
        public class userss
        {
            public int? id { get; set; }
            public string user_name { get; set; }
            public string user_email { get; set; }
            public string user_phone { get; set; }
        }
        #endregion
        #region ErrorInformation
        public void ReportError(ref string temp)
        {
            switch (res)
            {
                case 1:
                    temp = "Connect antenna fail!";
                    break;
                case 2:
                    temp = "No Tag!";
                    break;
                case 3:
                    temp = "Illegal Tag!";
                    break;
                case 4:
                    temp = "Power is not enough!";
                    break;
                case 5:
                    temp = "The memory has been protected!";
                    break;
                case 6:
                    temp = "Check sum error!";
                    break;
                case 7:
                    temp = "Parameter error!";
                    break;
                case 8:
                    temp = "The memory don't exist!";
                    break;
                case 9:
                    temp = "The Access Password is error!";
                    break;
                case 10:
                    temp = "The Kill Password cannot be 000000!";
                    break;
                case 14:
                    temp = "Locked Tags in the field!";
                    break;
                case 30:
                    temp = "Invalid Command!";
                    break;
                case 31:
                    temp = "Other Error!";
                    break;
                default:
                    temp = "Unbeknown Error!";
                    break;
            }
        }
        #endregion

        #region Read
        private void button5_Click(object sender, EventArgs e)
        {
            if (connect_OK == 0)
                return;
            int i;
            string str;
            nidEvent = 2;
            
            str = "00000000";
            for (i = 0; i < 4; i++)
            {
                AccessPassWord[i] = Convert.ToByte(str.Substring(i * 2, 2), 16);
            }

            EPC_Word = TagBuffer[1, 0];
            for (i = 0; i < TagBuffer[1, 0] * 2; i++)
            {
                IDTemp[i] = TagBuffer[1, i + 1];
            }            
                mem = 3;
                m_antenna_sel = 1;
                Read_times = 0;
                k = 0;
                timer1.Interval = Interval;
                timer1.Enabled = true;
        }
        #endregion
        #endregion

        private void button24_Click(object sender, EventArgs e)
        {
            if (connect_OK == 0)
                return;
            int i, Ant, WorkMode = 0, TagType = 0, port;
            string[] ipinfo = new string[4];
            byte[] readerip = new byte[4];
            byte[] mask = new byte[4];
            byte[] gateway = new byte[4];
            byte[] MAC = new byte[6];
            string str;

            Program.ReaderBasicParam Param = new Program.ReaderBasicParam();
            Program.ReaderAutoParam AutoParam = new Program.ReaderAutoParam();

            port = Convert.ToInt16(textBox40.Text);
            ipinfo = textBox28.Text.Split(new Char[] { '.' });
            for (i = 0; i < 4; i++)
            {
                readerip[i] = Convert.ToByte(ipinfo[i]);
            }
            ipinfo = textBox41.Text.Split(new Char[] { '.' });
            for (i = 0; i < 4; i++)
            {
                mask[i] = Convert.ToByte(ipinfo[i]);
            }
            ipinfo = textBox42.Text.Split(new Char[] { '.' });
            for (i = 0; i < 4; i++)
            {
                gateway[i] = Convert.ToByte(ipinfo[i]);
            }

            str = "";
            str = textBox16.Text;
            for (i = 0; i < 6; i++)
            {
                MAC[i] = (byte)Convert.ToInt16((str[i * 2].ToString() + str[i * 2 + 1].ToString()), 16);
            }

            if (comboBox14.SelectedIndex > comboBox15.SelectedIndex)
            {
                MessageBox.Show("The Min of Frequency can't be greater than the Max of Frequency!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (Convert.ToInt32(textBox27.Text) > 30 || Convert.ToInt32(textBox27.Text) < 20)
            {
                MessageBox.Show("Please input RF power between 20 and 30!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                textBox27.Focus();
                textBox27.SelectAll();
                return;
            }
            if (Convert.ToInt32(textBox29.Text) > 100 || Convert.ToInt32(textBox29.Text) < 0)
            {
                MessageBox.Show("Please input Max. tags of once Reading between 1 and 100!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                textBox29.Focus();
                textBox29.SelectAll();
                return;
            }

            if (Convert.ToInt32(textBox34.Text) > 254 || Convert.ToInt32(textBox34.Text) < 1)
            {
                MessageBox.Show("Please input reader address between 1 and 254 !", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                textBox34.Focus();
                textBox34.SelectAll();
                return;
            }

            if (Convert.ToInt32(textBox31.Text) < 1 || Convert.ToInt32(textBox31.Text) > 255)
            {
                MessageBox.Show("Please input pulse width between 1 and 255!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                textBox31.Focus();
                textBox31.SelectAll();
                return;
            }
            if (Convert.ToInt32(textBox30.Text) < 1 || Convert.ToInt32(textBox30.Text) > 255)
            {
                MessageBox.Show("Please input pulse interval between 1 and 255!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                textBox30.Focus();
                textBox30.SelectAll();
                return;
            }

            if (Convert.ToInt32(textBox33.Text) < 0 || Convert.ToInt32(textBox33.Text) > 8)
            {
                MessageBox.Show("Please input interval of reading for standard between 0 and 8!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                textBox33.Focus();
                textBox33.SelectAll();
                return;
            }

            if (radioButton29.Checked == true)
            {
                WorkMode = 0;
            }
            if (radioButton31.Checked == true)
            {
                WorkMode = 1;
            }


            if (radioButton36.Checked)
            {
                TagType = 1;
            }

            if (radioButton46.Checked)
            {
                TagType = 4;
            }

            int Enablebuzzer = 0;

            if (checkBox13.Checked == true)
                Enablebuzzer += 1;
            if (checkBox20.Checked == true)
                Enablebuzzer += 8;

            Param.BaudRate = (byte)(comboBox13.SelectedIndex + 4);
            Param.Min_Frequence = (byte)(comboBox14.SelectedIndex);
            Param.Max_Frequence = (byte)(comboBox15.SelectedIndex);
            Param.Power = Convert.ToByte(textBox27.Text);
            Param.WorkMode = (byte)WorkMode;
            Param.ReaderAddress = Convert.ToByte(textBox34.Text);
            Param.NumofCard = Convert.ToByte(textBox29.Text);
            Param.TagType = (byte)TagType;
            Param.ReadTimes = 1;
            Param.EnableBuzzer = (byte)Enablebuzzer;
            Param.IP1 = readerip[0];
            Param.IP2 = readerip[1];
            Param.IP3 = readerip[2];
            Param.IP4 = readerip[3];
            Param.Port1 = (byte)((port >> 8) & 0xFF);
            Param.Port2 = (byte)(port & 0xFF);
            Param.Gateway1 = gateway[0];
            Param.Gateway2 = gateway[1];
            Param.Gateway3 = gateway[2];
            Param.Gateway4 = gateway[3];
            Param.Mask1 = mask[0];
            Param.Mask2 = mask[1];
            Param.Mask3 = mask[2];
            Param.Mask4 = mask[3];
            Param.MAC1 = MAC[0];
            Param.MAC2 = MAC[1];
            Param.MAC3 = MAC[2];
            Param.MAC4 = MAC[3];
            Param.MAC5 = MAC[4];
            Param.MAC6 = MAC[5];

            for (i = 0; i < 3; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.WriteBasicParam(m_hScanner, ref Param, RS485Address);
                        break;
                    case 1:
                        res = Program.Net_WriteBasicParam(m_hSocket, ref Param);
                        break;
                }
                if (res == OK)
                    break;
            }
            if (res != OK)
            {
                MessageBox.Show("Fail to update Reader BasicParameter!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Ant = 0;
            if (checkBox14.Checked == true)
                Ant += 1;
            if (checkBox15.Checked == true)
                Ant += 2;
            if (checkBox16.Checked == true)
                Ant += 4;
            if (checkBox17.Checked == true)
                Ant += 8;

            int OutInterface = 0, OutMode = 0;

            if (radioButton35.Checked == true)
                OutInterface = 0;
            if (radioButton34.Checked == true)
                OutInterface = 1;
            if (radioButton37.Checked == true)
                OutInterface = 2;
            if (radioButton32.Checked == true)
                OutInterface = 3;
            if (radioButton33.Checked == true)
                OutInterface = 4;

            if (radioButton40.Checked == true)
                OutMode = 0;
            if (radioButton41.Checked == true)
                OutMode = 1;

            int AutoMode = 0;
            if (radioButton23.Checked == true)
                AutoMode = 0;
            if (radioButton30.Checked == true)
                AutoMode = 1;

            int PersistenceTime = 0, LenofList = 0, ReportCondition = 0, TriggerMode = 0, IDPosition = 0;
            if (Convert.ToInt32(textBox10.Text) > 255)
            {
                textBox10.Text = "255";
            }
            PersistenceTime = Convert.ToByte(textBox10.Text);

            if (Convert.ToInt32(textBox11.Text) > 255)
            {
                textBox11.Text = "255";
            }
            LenofList = Convert.ToByte(textBox11.Text);

            if (radioButton26.Checked == true)
                ReportCondition = 0;
            if (radioButton28.Checked == true)
                ReportCondition = 1;
            if (radioButton27.Checked == true)
                ReportCondition = 2;
            if (radioButton45.Checked == true)
                ReportCondition = 3;
            if (radioButton44.Checked == true)
                ReportCondition = 4;

            if (radioButton24.Checked == true)
                TriggerMode = 0;
            if (radioButton25.Checked == true)
                TriggerMode = 1;

            if (radioButton47.Checked == true)
                IDPosition = 0;
            if (radioButton48.Checked == true)
                IDPosition = 1;

            AutoParam.AutoMode = (byte)AutoMode;
            AutoParam.TimeH = (byte)(PersistenceTime >> 8);
            AutoParam.TimeL = (byte)PersistenceTime;
            AutoParam.Interval = (byte)comboBox18.SelectedIndex;
            AutoParam.NumH = (byte)(LenofList >> 8);
            AutoParam.NumL = (byte)LenofList;
            AutoParam.OutInterface = (byte)OutInterface;
            AutoParam.OutputManner = (byte)OutMode;
            AutoParam.Report_Interval = Convert.ToByte(textBox12.Text);
            AutoParam.Report_Condition = (byte)ReportCondition;
            AutoParam.Antenna = (byte)Ant;
            AutoParam.TriggerMode = (byte)TriggerMode;
            AutoParam.WiegandInterval = Convert.ToByte(textBox30.Text);
            AutoParam.WiegandWidth = Convert.ToByte(textBox31.Text);
            AutoParam.ID_Start = Convert.ToByte(textBox33.Text);
            AutoParam.IDPosition = (byte)IDPosition;
            port = Convert.ToInt32(textBox44.Text);
            ipinfo = textBox43.Text.Split(new Char[] { '.' });
            AutoParam.HostIP1 = Convert.ToByte(ipinfo[0]);
            AutoParam.HostIP2 = Convert.ToByte(ipinfo[1]);
            AutoParam.HostIP3 = Convert.ToByte(ipinfo[2]);
            AutoParam.HostIP4 = Convert.ToByte(ipinfo[3]);
            AutoParam.Port1 = (byte)((port >> 8) & 0xFF);
            AutoParam.Port2 = (byte)(port & 0xFF);
            AutoPort = port;

            if (checkBox21.Checked == true)
            {
                AutoParam.EnableRelay = 1;
            }
            else
            {
                AutoParam.EnableRelay = 0;
            }

            if (checkBox5.Checked == true)
            {
                AutoParam.Report_Output = 1;
            }
            else
            {
                AutoParam.Report_Output = 0;
            }

            if (checkBox6.Checked == true)
                AutoParam.Alarm = 1;
            else
                AutoParam.Alarm = 0;

            for (i = 0; i < 3; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.WriteAutoParam(m_hScanner, ref AutoParam, RS485Address);
                        break;
                    case 1:
                        res = Program.Net_WriteAutoParam(m_hSocket, ref AutoParam);
                        break;
                }
                if (res == OK)
                    break;
            }
            if (res != OK)
            {
                MessageBox.Show("Fail to update Reader AutoParameter!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            int str_len = 0, m = 0, filterLength = 0, filterAddress;
            byte[] FilterMask = new byte[64];

            str = "";
            str = textBox15.Text;
            str_len = str.Length;
            filterLength = Convert.ToByte(textBox14.Text);
            filterAddress = Convert.ToByte(textBox13.Text);

            if (filterLength == 0)
                m = 0;
            else
            {
                m = filterLength / 8;
                if (filterLength % 8 != 0)
                {
                    for (i = 0; i < ((m + 1) * 2 - str_len); i++)
                        str += "0";
                    m++;
                }
            }
            filterLength = m;

            for (i = 0; i < m; i++)
            {
                FilterMask[i] = (byte)Convert.ToInt16((str[i * 2].ToString() + str[i * 2 + 1].ToString()), 16);
            }

            if (filterAddress + filterLength > 96)
                filterLength = 8 - filterAddress;

            for (i = 0; i < 3; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.SetReportFilter(m_hScanner, filterAddress, filterLength, FilterMask, RS485Address);
                        break;
                    case 1:
                        res = Program.Net_SetReportFilter(m_hSocket, filterAddress, filterLength, FilterMask);
                        break;
                }
                if (res == OK)
                    break;
            }
            if (res != OK)
            {
                MessageBox.Show("Fail to update Reader Filter!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            int[] br = new int[] { 9600, 19200, 38400, 57600, 115200 };
            int baud = br[Param.BaudRate - 4];

            nBaudRate = baud;

            Program.DCB dcb = new Program.DCB();
            Program.GetCommState(m_hScanner, ref dcb);
            dcb.BaudRate = baud;
            Program.SetCommState(m_hScanner, ref dcb);

            Program.PurgeComm(m_hScanner, 0x04);
            Program.PurgeComm(m_hScanner, 0x08);

            Thread.Sleep(100);
            switch (ComMode)
            {
                case 0:
                    res = Program.Reboot(m_hScanner, RS485Address);
                    break;
                case 1:
                    res = Program.Net_Reboot(m_hSocket);
                    break;
            }

            if (res == OK)
            {
                MessageBox.Show("Update Reader Parameter successfully!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Fail to reboot reader!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        public int ReaderParam()
        {
            int i = 0;
            byte[] readerIP = new byte[4];
            byte[] mask = new byte[4];
            byte[] gateway = new byte[4];
            int port = 0;
            Program.ReaderBasicParam Param = new Program.ReaderBasicParam();
            Program.ReaderAutoParam AutoParam = new Program.ReaderAutoParam();
            for (i = 0; i < 5; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.ReadBasicParam(m_hScanner, ref Param, RS485Address);
                        break;
                    case 1:
                        res = Program.Net_ReadBasicParam(m_hSocket, ref Param);
                        break;
                }
                if (res == OK)
                    break;
            }

            if (res != OK)
            {
                MessageBox.Show("Fail to read parameter of reader!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return res;
            }

            int[] br = new int[] { 9600, 19200, 38400, 57600, 115200 };
            nBaudRate = br[Param.BaudRate - 4];

            comboBox13.SelectedIndex = Param.BaudRate - 4;
            textBox27.Text = Param.Power.ToString();
            comboBox14.SelectedIndex = Param.Min_Frequence;
            comboBox15.SelectedIndex = Param.Max_Frequence;
            textBox29.Text = Param.NumofCard.ToString();
            textBox34.Text = Param.ReaderAddress.ToString();

            switch (Param.WorkMode)
            {
                case 0:
                    radioButton29.Checked = true;
                    break;
                case 1:
                    radioButton31.Checked = true;
                    break;
            }

            switch (Param.TagType)
            {
                case 1:
                    radioButton36.Checked = true;
                    break;
                case 4:
                    radioButton46.Checked = true;
                    break;
            }

            if ((Param.EnableBuzzer & 0x01) == 1)
                checkBox13.Checked = true;
            else
                checkBox13.Checked = false;

            if (((Param.EnableBuzzer >> 3) & 0x01) == 1)
                checkBox20.Checked = true;
            else
                checkBox20.Checked = false;

            port = (int)(Param.Port1 << 8) + (int)Param.Port2;

            textBox28.Text = Param.IP1.ToString();
            textBox28.Text += ".";
            textBox28.Text += Param.IP2.ToString();
            textBox28.Text += ".";
            textBox28.Text += Param.IP3.ToString();
            textBox28.Text += ".";
            textBox28.Text += Param.IP4.ToString();
            textBox40.Text = port.ToString();
            textBox41.Text = Param.Mask1.ToString();
            textBox41.Text += ".";
            textBox41.Text += Param.Mask2.ToString();
            textBox41.Text += ".";
            textBox41.Text += Param.Mask3.ToString();
            textBox41.Text += ".";
            textBox41.Text += Param.Mask4.ToString();
            textBox42.Text = Param.Gateway1.ToString();
            textBox42.Text += ".";
            textBox42.Text += Param.Gateway2.ToString();
            textBox42.Text += ".";
            textBox42.Text += Param.Gateway3.ToString();
            textBox42.Text += ".";
            textBox42.Text += Param.Gateway4.ToString();
            string MACstr;
            MACstr = "";
            MACstr = Param.MAC1.ToString("X02");
            MACstr = MACstr + Param.MAC2.ToString("X02");
            MACstr = MACstr + Param.MAC3.ToString("X02");
            MACstr = MACstr + Param.MAC4.ToString("X02");
            MACstr = MACstr + Param.MAC5.ToString("X02");
            MACstr = MACstr + Param.MAC6.ToString("X02");
            textBox16.Text = MACstr;
            for (i = 0; i < 5; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.ReadAutoParam(m_hScanner, ref AutoParam, RS485Address);
                        break;
                    case 1:
                        res = Program.Net_ReadAutoParam(m_hSocket, ref AutoParam);
                        break;
                }
                if (res == OK)
                    break;
            }

            if (res != OK)
            {
                MessageBox.Show("Fail to read parameter of reader!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return res;
            }

            switch (AutoParam.AutoMode)
            {
                case 0:
                    radioButton23.Checked = true;
                    break;
                case 1:
                    radioButton30.Checked = true;
                    break;
            }

            int PersistenceTime;
            PersistenceTime = AutoParam.TimeH;
            PersistenceTime = (PersistenceTime << 8) + AutoParam.TimeL;
            textBox10.Text = PersistenceTime.ToString();
            comboBox18.SelectedIndex = AutoParam.Interval;
            int LenofList;
            LenofList = AutoParam.NumH;
            LenofList = (LenofList << 8) + AutoParam.NumL;
            textBox11.Text = LenofList.ToString();

            switch (AutoParam.OutputManner)
            {
                case 0:
                    radioButton40.Checked = true;
                    break;
                case 1:
                    radioButton41.Checked = true;
                    break;
            }

            switch (AutoParam.OutInterface)
            {
                case 0:
                    radioButton35.Checked = true;
                    break;
                case 1:
                    radioButton34.Checked = true;
                    break;
                case 2:
                    radioButton37.Checked = true;
                    break;
                case 3:
                    radioButton32.Checked = true;
                    break;
                case 4:
                    radioButton33.Checked = true;
                    break;
            }

            textBox31.Text = AutoParam.WiegandWidth.ToString();
            textBox30.Text = AutoParam.WiegandInterval.ToString();
            textBox33.Text = AutoParam.ID_Start.ToString();
            switch (AutoParam.IDPosition)
            {
                case 0:
                    radioButton47.Checked = true;
                    break;
                case 1:
                    radioButton48.Checked = true;
                    break;
            }

            textBox12.Text = AutoParam.Report_Interval.ToString();

            switch (AutoParam.Report_Condition)
            {
                case 0:
                    radioButton26.Checked = true;
                    break;
                case 1:
                    radioButton28.Checked = true;
                    break;
                case 2:
                    radioButton27.Checked = true;
                    break;
                case 3:
                    radioButton45.Checked = true;
                    break;
                case 4:
                    radioButton44.Checked = true;
                    break;
            }

            if ((AutoParam.Report_Output & 0x01) == 1)
            {
                checkBox5.Checked = true;
            }
            else
            {
                checkBox5.Checked = false;
            }

            if ((AutoParam.Antenna & 0x01) == 1)
                checkBox14.Checked = true;
            else
                checkBox14.Checked = false;

            if (((AutoParam.Antenna >> 1) & 0x01) == 1)
                checkBox15.Checked = true;
            else
                checkBox15.Checked = false;

            if (((AutoParam.Antenna >> 2) & 0x01) == 1)
                checkBox16.Checked = true;
            else
                checkBox16.Checked = false;

            if (((AutoParam.Antenna >> 3) & 0x01) == 1)
                checkBox17.Checked = true;
            else
                checkBox17.Checked = false;

            switch (AutoParam.TriggerMode)
            {
                case 0:
                    radioButton24.Checked = true;
                    break;
                case 1:
                    radioButton25.Checked = true;
                    break;
            }

            if (AutoParam.Alarm == 1)
            {
                checkBox6.Checked = true;
            }
            else
            {
                checkBox6.Checked = false;
            }

            if (AutoParam.EnableRelay == 1)
                checkBox21.Checked = true;
            else
                checkBox21.Checked = false;

            textBox43.Text = AutoParam.HostIP1.ToString();
            textBox43.Text += ".";
            textBox43.Text += AutoParam.HostIP2.ToString();
            textBox43.Text += ".";
            textBox43.Text += AutoParam.HostIP3.ToString();
            textBox43.Text += ".";
            textBox43.Text += AutoParam.HostIP4.ToString();
            port = (int)(AutoParam.Port1 << 8) + (int)AutoParam.Port2;
            textBox44.Text = port.ToString();
            AutoPort = port;

            int FilterAddress = 0;
            int FilterLength = 0;
            byte[] FilterMask = new byte[64];
            int l = 0;
            string str, str_temp;

            for (i = 0; i < 3; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.GetReportFilter(m_hScanner, ref FilterAddress, ref FilterLength, FilterMask, RS485Address);
                        break;
                    case 1:
                        res = Program.Net_GetReportFilter(m_hSocket, ref FilterAddress, ref FilterLength, FilterMask);
                        break;
                }
                if (res == OK) break;
            }
            if (res != OK)
            {
                MessageBox.Show("Read ReportFilter Parameter Fail!!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return res;
            }

            textBox13.Text = FilterAddress.ToString();
            textBox14.Text = FilterLength.ToString();

            if (FilterLength % 8 == 0)
            {
                l = FilterLength / 8;
            }
            else
            {
                l = FilterLength / 8 + 1;
            }

            str = "";
            for (i = 0; i < l; i++)
            {
                str_temp = FilterMask[i].ToString("X02");
                str += str_temp;
            }

            textBox15.Text = str;

            byte[] ReaderID = new byte[12];

            Thread.Sleep(200);
            switch (ComMode)
            {
                case 0:
                    res = Program.GetReaderID(m_hScanner, ReaderID, RS485Address);
                    break;
                case 1:
                    res = Program.Net_GetReaderID(m_hSocket, ReaderID);
                    break;
            }

            textBox39.Text = "";

            for (i = 0; i < 10; i++)
            {
                textBox39.Text += (char)ReaderID[i];
            }

            return OK;
        }
        private void button25_Click(object sender, EventArgs e)
        {
            if (connect_OK == 0)
                return;
            int Relay = 0;
            if (checkBox18.Checked == true)
                Relay += 1;
            if (checkBox19.Checked == true)
                Relay += 2;
            switch (ComMode)
            {
                case 0:
                    res = Program.SetRelay(m_hScanner, Relay, RS485Address);
                    break;
                case 1:
                    res = Program.Net_SetRelay(m_hSocket, Relay);
                    break;
            }
            if (res != OK)
            {
                MessageBox.Show("Set relay unsuccessfully! Please try again!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            else
                MessageBox.Show("Set relay successfully!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        #region connect reader
        private void button26_Click(object sender, EventArgs e)
        {
            int i;
            byte HardVer;
            szPort = comboBox19.Text;
            readerip = textBox35.Text;
            readerport = Convert.ToUInt16(textBox36.Text);
            hostip = textBox37.Text;
            hostport = Convert.ToUInt16(textBox38.Text);
            if (radioButton42.Checked == true)
                ComMode = 1;
            else
                ComMode = 0;
            for (i = 0; i < 3; i++)
            {
                res = Program.Net_ConnectScanner(ref m_hSocket, readerip, readerport, hostip, hostport);
                if (res == OK)
                    break;
            }

            if ((res != OK) && (ComMode == 0))
            {
                MessageBox.Show("None Reader connect to the COM!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if ((res != OK) && (ComMode == 1))
            {
                MessageBox.Show("None Reader connect to the RJ45!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            for (i = 0; i < 3; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.GetReaderVersion(m_hScanner, ref HardVersion, ref SoftVersion, RS485Address);
                        break;
                    case 1:
                        res = Program.Net_GetReaderVersion(m_hSocket, ref HardVersion, ref SoftVersion);
                        break;
                }
                if (res == OK)
                    break;
            }
            if (res != OK)
            {
                connect_OK = 0;
                MessageBox.Show("Can't get reader version!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            else
            {
                connect_OK = 1;
                MessageBox.Show("Connect reader success!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }

            HardVer = (byte)HardVersion;
            textBox25.Text = String.Format("{0,0:D02}{1,0:D02}", (byte)(HardVersion >> 8), (byte)HardVersion);
            textBox26.Text = String.Format("{0,0:D02}{1,0:D02}", (byte)(SoftVersion >> 8), (byte)SoftVersion);

            ReaderParam();
            button26.Enabled = false;
            button27.Enabled = true;
            this.tabControl1.SelectedIndex = 2;
        }
        #endregion
        private void button27_Click(object sender, EventArgs e)
        {
            switch (ComMode)
            {
                case 0:
                    Program.DisconnectScanner(m_hScanner);
                    break;
                case 1:
                    Program.Net_DisconnectScanner();
                    break;
            }
            button26.Enabled = true;
            button27.Enabled = false;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (connect_OK == 0)
                return;
            Program.ReaderDate time;
            DateTime dt = DateTime.Now;
            time.Year = (byte)((dt.Year - 2000) & 0x00FF);
            time.Month = (byte)dt.Month;
            time.Day = (byte)dt.Day;
            time.Hour = (byte)dt.Hour;
            time.Minute = (byte)dt.Minute;
            time.Second = (byte)dt.Second;
            switch (ComMode)
            {
                case 0:
                    res = Program.SetReaderTime(m_hScanner, time, RS485Address);
                    break;
                case 1:
                    res = Program.Net_SetReaderTime(m_hSocket, time);
                    break;
            }
            if (res != OK)
            {
                MessageBox.Show("Can't set reader time!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            else
            {
                textBox45.Text = "Set reader time successfully!";
            }
        }
        private void button28_Click(object sender, EventArgs e)
        {
            if (connect_OK == 0)
                return;
            int i;
            Program.ReaderDate time = new Program.ReaderDate();
            for (i = 0; i < 5; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.GetReaderTime(m_hScanner, ref time, RS485Address);
                        break;
                    case 1:
                        res = Program.Net_GetReaderTime(m_hSocket, ref time);
                        break;
                }
                if (res == OK)
                    break;
            }
            if (res != OK)
            {
                MessageBox.Show("Can't get reader time!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            textBox45.Text = "Reader time is ";
            textBox45.Text += time.Year.ToString("D2");
            textBox45.Text += "-";
            textBox45.Text += time.Month.ToString("D2");
            textBox45.Text += "-";
            textBox45.Text += time.Day.ToString("D2");
            textBox45.Text += " ";
            textBox45.Text += time.Hour.ToString("D2");
            textBox45.Text += ":";
            textBox45.Text += time.Minute.ToString("D2");
            textBox45.Text += ":";
            textBox45.Text += time.Second.ToString("D2");
        }
        private void button29_Click(object sender, EventArgs e)
        {
            if (connect_OK == 0)
                return;
            int i, j;
            j = (int)MessageBox.Show("Do you determine to recover the factory parameter?", "Information", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (j != 1)
                return;

            for (i = 0; i < 5; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.ReadFactoryParameter(m_hScanner);
                        break;
                    case 1:
                        res = Program.Net_ReadFactoryParameter(m_hSocket);
                        break;
                }
                if (res == OK)
                    break;
            }
            if (res != OK)
            {
                MessageBox.Show("Fail to recover default parameters!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            switch (ComMode)
            {
                case 0:
                    Program.Reboot(m_hScanner, RS485Address);
                    break;
                case 1:
                    Program.Net_Reboot(m_hSocket);
                    break;
            }
            if (ComMode == 0)
                Program.DisconnectScanner(m_hScanner);

            if (ComMode == 0)
            {
                for (i = 0; i < 5; i++)
                {
                    Thread.Sleep(100);
                    res = Program.ConnectScanner(ref m_hScanner, szPort, ref nBaudRate);
                    if (res == OK)
                        break;
                }
            }
            if (res != OK)
            {
                MessageBox.Show("Can't connect the reader!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            res = ReaderParam();
            if (res != OK)
            {
                return;
            }

            MessageBox.Show("Set default parameter successfully!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void button9_Click(object sender, EventArgs e)
        {
            int i = 0;
            int str_len = 0, m = 0, filterLength = 0, filterAddress;
            string str;
            byte[] FilterMask = new byte[64];

            str = textBox15.Text;
            str_len = str.Length;
            filterLength = Convert.ToByte(textBox14.Text);
            filterAddress = Convert.ToByte(textBox13.Text);

            if (filterLength == 0)
                m = 0;
            else
            {
                m = filterLength / 8;
                if (filterLength % 8 != 0)
                {
                    for (i = 0; i < ((m + 1) * 2 - str_len); i++)
                        str += "0";
                    m++;
                }
            }
            filterLength = Convert.ToByte(textBox14.Text);

            for (i = 0; i < m; i++)
            {
                FilterMask[i] = (byte)Convert.ToInt16((str[i * 2].ToString() + str[i * 2 + 1].ToString()), 16);
            }

            if (filterAddress + filterLength > 96)
                filterLength = 8 - filterAddress;

            for (i = 0; i < 3; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.SetReportFilter(m_hScanner, filterAddress, filterLength, FilterMask, RS485Address);
                        break;
                    case 1:
                        res = Program.Net_SetReportFilter(m_hSocket, filterAddress, filterLength, FilterMask);
                        break;
                }
                if (res == OK)
                    break;
            }
            if (res != OK)
            {
                MessageBox.Show("Fail to update Reader AutoParameter!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
        }

        private void tabControl1_Selected(object sender, TabControlEventArgs e)
        {
            button5_Click(null, null);
        }

        private void button10_Click(object sender, EventArgs e)
        {
            int i = 0;
            int FilterAddress = 0;
            int FilterLength = 0;
            byte[] FilterMask = new byte[64];
            int l = 0;
            string str, str_temp;

            for (i = 0; i < 3; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.GetReportFilter(m_hScanner, ref FilterAddress, ref FilterLength, FilterMask, RS485Address);

                        break;
                    case 1:
                        res = Program.Net_GetReportFilter(m_hSocket, ref FilterAddress, ref FilterLength, FilterMask);
                        break;
                }
                if (res == OK) break;
            }
            if (res != OK)
            {
                MessageBox.Show("Read ReportFilter Parameter Fail!!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            textBox13.Text = FilterAddress.ToString();
            textBox14.Text = FilterLength.ToString();

            if (FilterLength % 8 == 0)
            {
                l = FilterLength / 8;
            }
            else
            {
                l = FilterLength / 8 + 1;
            }

            str = "";
            for (i = 0; i < l; i++)
            {
                str_temp = FilterMask[i].ToString("X");
                str += str_temp;
            }
            textBox15.Text = str;
        }
        private void button11_Click(object sender, EventArgs e)
        {

            for (int i = 0; i < 5; i++)
            {
                switch (ComMode)
                {
                    case 0:
                        res = Program.AutoMode(m_hScanner, 0, RS485Address);

                        break;
                    case 1:
                        res = Program.Net_AutoMode(m_hSocket, 0);
                        break;
                }
                if (res == OK)
                    break;
            }

            if (res == OK)
            {
                MessageBox.Show("Stop Auto Mode successfully!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else
            {
                MessageBox.Show("Stop Auto Mode fail!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        private void button12_Click(object sender, EventArgs e)
        {
            button12.Enabled = false;
            button13.Enabled = true;
            try
            {
                IPEndPoint Info = new IPEndPoint(IPAddress.Any, 4444);
                NetSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                NetSocket.Bind(Info);

                NetSocket.BeginReceive(IPrecebuffer, 0, IPrecebuffer.Length, SocketFlags.None, new AsyncCallback(IPReceiveCallBack), null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        private void IPReceiveCallBack(IAsyncResult AR)
        {
            try
            {
                //结束挂起的异步读取，返回接收到的字节数。 AR，它存储此异步操作的状态信息以及所有用户定义数据
                int REnd = NetSocket.EndReceive(AR);

                if (REnd > 0)
                {

                    ReaderIP = "";
                    ReaderIP = IPrecebuffer[3].ToString() + "." + IPrecebuffer[4].ToString() + "." + IPrecebuffer[5].ToString() + "." + IPrecebuffer[6].ToString();

                    ReaderPort = IPrecebuffer[7];
                    ReaderPort = (ReaderPort << 8) + IPrecebuffer[8];

                    NetInvoke mi = new NetInvoke(UpdateListView);
                    this.BeginInvoke(mi);
                }

                NetSocket.BeginReceive(IPrecebuffer, 0, IPrecebuffer.Length, 0, new AsyncCallback(IPReceiveCallBack), null);
            }
            catch
            {

            }
        }
        private void UpdateListView()
        {
            int row = 0;
            int count = 0;
            string str;

            row = listView4.Items.Count;
            str = "";
            count = 0;
            for (int i = 0; i < row; i++)
            {
                str = listView4.Items[i].SubItems[1].Text;
                if (ReaderIP == str)
                {
                    count = 1;
                    break;
                }
            }
            if (count == 0)
            {
                ListViewItem myitem = new ListViewItem();
                myitem = listView4.Items.Add(row.ToString(), row);
                myitem.SubItems.Add(ReaderIP);
                myitem.SubItems.Add(ReaderPort.ToString());
            }
        }

        private void button13_Click(object sender, EventArgs e)
        {
            NetSocket.Close();
            button12.Enabled = true;
            button13.Enabled = false;
        }

        private void EpcReceiveCallBack(IAsyncResult AR)
        {
            try
            {
                //结束挂起的异步读取，返回接收到的字节数。 AR，它存储此异步操作的状态信息以及所有用户定义数据
                int REnd = EpcSocket.EndReceive(AR);

                if (REnd > 0)
                {
                    NetInvoke mi = new NetInvoke(UpdateEpcView);
                    this.BeginInvoke(mi);
                }

                EpcSocket.BeginReceive(Epcrecebuffer, 0, Epcrecebuffer.Length, 0, new AsyncCallback(EpcReceiveCallBack), null);
            }
            catch
            {

            }
        }

        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            try
            {
                string str = string.Empty;

                if (RS485Address != 0)
                {

                }
                else
                {
                    str = serialPort1.ReadLine();
                }

                if (str.Length > 0)
                {
                    Epcrecebuffer = Encoding.UTF8.GetBytes(str);
                    NetInvoke mi = new NetInvoke(UpdateEpcView);
                    this.BeginInvoke(mi);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void UpdateEpcView()
        {
            string str;
            int i;
            int row = 0;
            if (Epcrecebuffer[0] == 0x44)
            {
                //第一次读取的时间
                str = "";
                for (i = 0; i < 19; i++)
                {
                    str += (char)Epcrecebuffer[0x05 + i];
                }
                EpcStr[1] = str;

                //最后一次读取的时间
                str = "";
                for (i = 0; i < 19; i++)
                {
                    str += (char)Epcrecebuffer[0x1f + i];
                }
                EpcStr[2] = str;

                //读取的次数
                str = "";
                for (i = 0; i < 5; i++)
                {
                    str += (char)Epcrecebuffer[0x3a + i];
                }
                EpcStr[3] = str;

                //天线编号
                str = "";
                for (i = 0; i < 2; i++)
                {
                    str += (char)Epcrecebuffer[0x45 + i];
                }
                EpcStr[4] = str;

                //标签类型
                str = "";
                for (i = 0; i < 2; i++)
                {
                    str += (char)Epcrecebuffer[0x4e + i];
                }
                EpcStr[5] = str;

                //EPC
                str = "";
                for (i = 0; i < 74; i++)
                {
                    str += (char)Epcrecebuffer[0x56 + i];
                }
                EpcStr[6] = str.Trim();
            }
            else
            {
                str = "";

                //读取的次数
                for (i = 0; i < 5; i++)
                {
                    str += (char)Epcrecebuffer[i];
                }
                EpcStr[3] = str;

                //天线编号
                str = "";
                for (i = 0; i < 2; i++)
                {
                    str += (char)Epcrecebuffer[0x07 + i];
                }
                EpcStr[4] = str;

                //标签类型
                str = "";
                for (i = 0; i < 2; i++)
                {
                    str += (char)Epcrecebuffer[0x0b + i];
                }
                EpcStr[5] = str;

                //EPC
                str = "";
                for (i = 0; i < 74; i++)
                {
                    str += (char)Epcrecebuffer[0x0f + i];
                }
                EpcStr[6] = str.Trim();
            }

            EpcStr[0] = row.ToString();

            if (isRead)
            {
                if (!epcOnly.Contains(EpcStr[6]))
                {
                    epcOnly.Add(EpcStr[6]);
                }
            }
        }

        private void radioButton42_CheckedChanged(object sender, EventArgs e)
        {
            textBox35.Enabled = true;
            textBox36.Enabled = true;
            textBox37.Enabled = true;
            textBox38.Enabled = true;
            comboBox19.Enabled = false;
            txt485address.Enabled = false;
        }

        private void radioButton43_CheckedChanged(object sender, EventArgs e)
        {
            textBox35.Enabled = false;
            textBox36.Enabled = false;
            textBox37.Enabled = false;
            textBox38.Enabled = false;
            comboBox19.Enabled = true;
            txt485address.Enabled = false;
        }

        private void listView4_Click(object sender, EventArgs e)
        {
            textBox35.Text = listView4.SelectedItems[0].SubItems[1].Text;
            textBox36.Text = listView4.SelectedItems[0].SubItems[2].Text;
        }
    }
}