using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Resources;

namespace WindLidarSystem
{
    public partial class WindLidarServer : Form
    {
        private ProcessReceiver process;
        protected StatusBar mainStatusBar;
        protected StatusBarPanel statusPanel;
        protected StatusBarPanel datetimePanel;
        private char[] delimiterChar = { ':' };
        private char[] delimiterChar2 = { '=' };
        private Point mousePoint;
        private LogCls log;
        public struct StsInfo
        {
            public string s_code;
            public string s_sts;
            public string s_lastDt;
        }

        public struct FtsInfo
        {
            public string s_code;
            public string s_sts;
            public string s_lastDt;
        }
        private List<StsInfo> stList;
        private List<FtsInfo> ftList;
        private const int cGrip = 16;      // Grip size
        private const int cCaption = 62;  // 2;   // Caption bar height;

        public WindLidarServer()
        {
            InitializeComponent();

            log = new LogCls();
            //this.Text = "";
            //this.ControlBox = false;

            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);

            process = null;

            btnStop.Enabled = false;
            
            txtIP.Text = ParamInitInfo.Instance.m_ftpIP;                     // UWA FTP IP Address
            txtPort.Text = ParamInitInfo.Instance.m_ftpPort;                 // UWA FTP Port
            txtID.Text = ParamInitInfo.Instance.m_ftpUser;                   // UWA FTP User
            txtPass.Text = ParamInitInfo.Instance.m_ftpPass;                 // UWA FTP User Password

            txtListenPort.Text = ParamInitInfo.Instance.m_listenPort;        // Listen Port
            txtClientRcvPort.Text = ParamInitInfo.Instance.m_clientRcvPort;  // Client Receive Port
            txtLocalSendPort.Text = ParamInitInfo.Instance.m_localPort;      // Local Port
            txtDestPort.Text = ParamInitInfo.Instance.m_dataclientport;      // 관측데이터에 대한 수신결과 전송 Port

            txtDBName.Text = ParamInitInfo.Instance.m_dbName;
            txtDBHost.Text = ParamInitInfo.Instance.m_dbHost;
            txtDBPort.Text = ParamInitInfo.Instance.m_dbPort;
            txtDBUserID.Text = ParamInitInfo.Instance.m_dbUser;
            txtDBUserPass.Text = ParamInitInfo.Instance.m_dbPass;

            txtFtpThreadTime.Text = ParamInitInfo.Instance.m_ftpThreadTime;
            txtStsThreadTime.Text = ParamInitInfo.Instance.m_stsThreadTime;


            createStatusBar();
            stList = new List<StsInfo>();
            ftList = new List<FtsInfo>();

            // 초기 세팅값
            defaultClear();


            // test image
            //ResourceManager rm = Properties.Resources.ResourceManager;
           // Bitmap myImage = (Bitmap)rm.GetObject("off");

            panel13211.BackgroundImage = Properties.Resources.btn_off;  //.off;
            panel13211.BackgroundImageLayout = ImageLayout.Stretch;

            panel13206.BackgroundImage = Properties.Resources.btn_off;
            panel13206.BackgroundImageLayout = ImageLayout.Stretch;

            panel13210.BackgroundImage = Properties.Resources.btn_off;
            panel13210.BackgroundImageLayout = ImageLayout.Stretch;

            panel1.BackgroundImage = Properties.Resources.ridar;
            panel1.BackgroundImageLayout = ImageLayout.Stretch;

            //panelClose.BackgroundImage = Properties.Resources.close;
            //panelClose.BackgroundImageLayout = ImageLayout.Stretch;

            //panelHide.BackgroundImage = Properties.Resources.hide;
            //panelHide.BackgroundImageLayout = ImageLayout.Stretch;


            lblSmall.BackColor = System.Drawing.ColorTranslator.FromHtml("#32709f");  //Color.DodgerBlue;
            lblSmall.ForeColor = Color.White;
            lblSmall.Cursor = Cursors.Hand;
            lblClose.BackColor = System.Drawing.ColorTranslator.FromHtml("#32709f");  //Color.DodgerBlue;
            lblClose.ForeColor = Color.White;
            lblClose.Cursor = Cursors.Hand;

            lblTitle.BackColor = System.Drawing.ColorTranslator.FromHtml("#32709f");  //Color.DodgerBlue;
            lblTitle.ForeColor = Color.White;

            lblTitle2.BackColor = System.Drawing.ColorTranslator.FromHtml("#32709f");  //Color.DodgerBlue;
            lblTitle2.ForeColor = Color.White;

            btnStart.Cursor = Cursors.Hand;
            btnStop.Cursor = Cursors.Hand;

            btnStart.Text = "";
            btnStop.Text = "";
            btnStart.BackgroundImage = Properties.Resources.btn_start_sm;
            btnStart.BackgroundImageLayout = ImageLayout.Stretch;
            btnStop.BackgroundImage = Properties.Resources.btn_stop_g;
            btnStop.BackgroundImageLayout = ImageLayout.Stretch;

        }


        private void btnStart_Click(object sender, EventArgs e)
        {
            if (process == null)
            {
                logMessage("[WindLidarServer:Start] Process starting...");
                process = new ProcessReceiver(this);
                process.start();

                btnStart.Enabled = false;
                btnStop.Enabled = true;
                statusPanel.Text = "Application started. Action Start.";

                btnStart.BackgroundImage = Properties.Resources.btn_start_g;
                btnStop.BackgroundImage = Properties.Resources.btn_stop_sm;
            }
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            logMessage("[WindLidarServer:Stop] Process stopping...");
            statusPanel.Text = "Application started. Action stopping.";
            process.abort();
            process = null;

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            statusPanel.Text = "Application started. Action Stop.";

            btnStart.BackgroundImage = Properties.Resources.btn_start_sm;
            btnStop.BackgroundImage = Properties.Resources.btn_stop_g;
        }

        private void createStatusBar()
        {
            mainStatusBar = new StatusBar();
            statusPanel = new StatusBarPanel();
            datetimePanel = new StatusBarPanel();

            // Set first panel properties and add to StatusBar
            statusPanel.BorderStyle = StatusBarPanelBorderStyle.Sunken;
            statusPanel.Text = "Application started. No action yet.";
            statusPanel.ToolTipText = "Last Activity";
            statusPanel.AutoSize = StatusBarPanelAutoSize.Spring;
            mainStatusBar.Panels.Add(statusPanel);

            // Set second panel properties and add to StatusBar
            string sendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            datetimePanel.BorderStyle = StatusBarPanelBorderStyle.Raised;
            datetimePanel.ToolTipText = "Application Start Time : " + sendTime;
            datetimePanel.Text = "Application Start Time : " + sendTime;
            datetimePanel.AutoSize = StatusBarPanelAutoSize.Contents;
            mainStatusBar.Panels.Add(datetimePanel);
            mainStatusBar.ShowPanels = true;

            Controls.Add(mainStatusBar);
        }

        public void logMessage(string msg)
        {
            logMessage(lstLog, msg);
        }

        private void logMessage(ListBox _lstLog, string _msg)
        {
            try
            {
                if (_lstLog.InvokeRequired)     // 해당 컨트롤이 Invoke를 요구한다면
                {
                    _lstLog.Invoke(new logMessageDelegate(logMessage), new object[] { _lstLog, _msg });
                }
                else
                {
                    string sendTime = " [" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "]";
                    // lstLog.Items.Insert(0, sendTime + _msg); //  Add(msg); 
                    if (lstLog.Items.Count == 5000)
                    {
                        lstLog.Items.RemoveAt(0);
                    }
                    lstLog.Items.Add(sendTime + _msg); //  Add(msg); 
                    lstLog.SelectedIndex = lstLog.Items.Count - 1;
                }
            }catch(Exception ex)
            {
                log.Log("[logMessage] error : " + ex.ToString());
            }
        }

        private delegate void logMessageDelegate(ListBox _lstLog, string _msg);
        //private delegate void stsMessageDelegate(ListBox _lstLog, string _msg);

        public void stsMessage(string msg)
        {
            try
            {
                string[] msgArr = msg.Split(delimiterChar);

                string stCode = msgArr[1];
                string stSts = msgArr[2];
                string rcvTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                StsInfo item = new StsInfo();
                item.s_code = stCode;
                item.s_sts = stSts;
                item.s_lastDt = rcvTime;
                if (stList.Count() == 0)
                {
                    stList.Add(item);
                }
                else
                {
                    int found = 0;
                    for (int i = 0; i < stList.Count(); i++)
                    {
                        if (stList[i].s_code == item.s_code)
                        {
                            stList[i] = item;
                            found = 1;
                        }
                    }
                    if (found == 0)
                    {
                        stList.Add(item);   // new
                    }
                }

                refreshWindow();
            }
            catch (Exception ex)
            {
                log.Log("[stsMessage] error : " + ex.ToString());
            }

        }

        /**
         * 관측자료 송수신에 대한 최신 날짜를 업데이트 한다.
         */
        public void ftsMessage(string msg)
        {
            try
            {
                string[] msgArr = msg.Split(delimiterChar);

                string stCode = msgArr[1];
                string stSts = msgArr[2];
                string rcvTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                FtsInfo item = new FtsInfo();
                item.s_code = stCode;
                item.s_sts = stSts;
                item.s_lastDt = rcvTime;
                if (ftList.Count() == 0)
                {
                    ftList.Add(item);
                }
                else
                {
                    int found = 0;
                    for (int i = 0; i < ftList.Count(); i++)
                    {
                        if (ftList[i].s_code == item.s_code)
                        {
                            ftList[i] = item;
                            found = 1;
                        }
                    }
                    if (found == 0)
                    {
                        ftList.Add(item);   // new
                    }
                }

                refreshWindow();
            }
            catch (Exception ex)
            {
                log.Log("[ftsMessage] error : " + ex.ToString());
            }
        }


        private void refreshWindow()
        {
            try
            {
                for (int i = 0; i < stList.Count(); i++)
                {
                    StsInfo item = stList[i];

                    if (item.s_code == "13211")       // 일산
                    {
                        UpdateRadioStsA(item.s_sts);
                        UpdateLableStaLastTime(item.s_lastDt);
                    }
                    if (item.s_code == "13210")       // 송도
                    {
                        UpdateRadioStsB(item.s_sts);
                        UpdateLableStbLastTime(item.s_lastDt);
                    }
                    if (item.s_code == "13206")       //구로
                    {
                        UpdateRadioStsC(item.s_sts);
                        UpdateLableStcLastTime(item.s_lastDt);
                    }

                }
                for (int i = 0; i < ftList.Count(); i++)
                {
                    FtsInfo ftm = ftList[i];

                    if (ftm.s_code == "13211")       // 일산
                    {
                        UpdateLableFtaLastTime(ftm.s_lastDt);
                    }
                    if (ftm.s_code == "13210")       // 송도
                    {
                        UpdateLableFtbLastTime(ftm.s_lastDt);
                    }
                    if (ftm.s_code == "13206")       //구로
                    {
                        UpdateLableFtcLastTime(ftm.s_lastDt);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Log("[refreshWindow] error : " + ex.ToString());
            }
        }

        public void stsDB(string msg)
        {
            try
            {
                if (msg == null) return;
                string[] msgArr = msg.Split(delimiterChar2);

                string stCode = msgArr[1];
                string stSts = msgArr[2];
                string rcvTime = msgArr[3];
                StsInfo item = new StsInfo();
                item.s_code = stCode;
                item.s_sts = stSts;
                item.s_lastDt = rcvTime;
                if (stList.Count() == 0)
                {
                    stList.Add(item);
                }
                else
                {
                    int found = 0;
                    for (int i = 0; i < stList.Count(); i++)
                    {
                        if (stList[i].s_code == item.s_code)
                        {
                            stList[i] = item;
                            found = 1;
                        }
                    }
                    if (found == 0)
                    {
                        stList.Add(item);   // new
                    }
                }

                refreshWindow();
            }
            catch (Exception ex)
            {
                log.Log("[stsDB] error : " + ex.ToString());
            }
        }

        private void UpdateLableStaLastTime(string msg)
        {
            try
            {
                if (lblstaLastTime.InvokeRequired)
                {
                    lblstaLastTime.BeginInvoke(new Action(() => lblstaLastTime.Text = msg));
                }
                else
                {
                    lblstaLastTime.Text = msg;
                }
            }
            catch (Exception ex)
            {
                log.Log("[UpdateLableStaLastTime] error : " + ex.ToString());
            }
        }
        private void UpdateLableStbLastTime(string msg)
        {
            try
            {
                if (lblstbLastTime.InvokeRequired)
                {
                    lblstbLastTime.BeginInvoke(new Action(() => lblstbLastTime.Text = msg));
                }
                else
                {
                    lblstbLastTime.Text = msg;
                }
            }
            catch (Exception ex)
            {
                log.Log("[UpdateLableStbLastTime] error : " + ex.ToString());
            }
        }
        private void UpdateLableStcLastTime(string msg)
        {
            try
            {
                if (lblstcLastTime.InvokeRequired)
                {
                    lblstcLastTime.BeginInvoke(new Action(() => lblstcLastTime.Text = msg));
                }
                else
                {
                    lblstcLastTime.Text = msg;
                }
            }
            catch (Exception ex)
            {
                log.Log("[UpdateLableStcLastTime] error : " + ex.ToString());
            }
        }

        private void UpdateLableFtaLastTime(string msg)
        {
            try
            {
                if (lblstaDataLastTime.InvokeRequired)
                {
                    lblstaDataLastTime.BeginInvoke(new Action(() => lblstaDataLastTime.Text = msg));
                }
                else
                {
                    lblstaDataLastTime.Text = msg;
                }
            }
            catch (Exception ex)
            {
                log.Log("[UpdateLableFtaLastTime] error : " + ex.ToString());
            }
        }
        private void UpdateLableFtbLastTime(string msg)
        {
            try
            {
                if (lblstbDataLastTime.InvokeRequired)
                {
                    lblstbDataLastTime.BeginInvoke(new Action(() => lblstbDataLastTime.Text = msg));
                }
                else
                {
                    lblstbDataLastTime.Text = msg;
                }
            }
            catch (Exception ex)
            {
                log.Log("[UpdateLableFtbLastTime] error : " + ex.ToString());
            }
        }
        private void UpdateLableFtcLastTime(string msg)
        {
            try
            {
                if (lblstcDataLastTime.InvokeRequired)
                {
                    lblstcDataLastTime.BeginInvoke(new Action(() => lblstcDataLastTime.Text = msg));
                }
                else
                {
                    lblstcDataLastTime.Text = msg;
                }
            }
            catch (Exception ex)
            {
                log.Log("[UpdateLableFtcLastTime] error : " + ex.ToString());
            }
        }

        /**
         * 13211 : 일산
         */
        private void UpdateRadioStsA(string msg)
        {
            try
            {
                if (msg == "0")
                {
                    if (panel13211.InvokeRequired)
                    {
                        panel13211.BeginInvoke(new Action(() => panel13211.BackgroundImage = Properties.Resources.btn_off));
                    }
                    else
                    {
                        panel13211.BackgroundImage = Properties.Resources.btn_off;
                    }
                }
                else if (msg == "1")
                {
                    if (panel13211.InvokeRequired)
                    {
                        panel13211.BeginInvoke(new Action(() => panel13211.BackgroundImage = Properties.Resources.btn_on));
                    }
                    else
                    {
                        panel13211.BackgroundImage = Properties.Resources.btn_on;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Log("[UpdateRadioStsA] error : " + ex.ToString());
            }
        }                        
        /**
         * 13210 : 송도
         */
        private void UpdateRadioStsB(string msg)
        {
            try
            {
                if (msg == "0")
                {
                    if (panel13210.InvokeRequired)
                    {
                        panel13210.BeginInvoke(new Action(() => panel13210.BackgroundImage = Properties.Resources.btn_off));
                    }
                    else
                    {
                        panel13210.BackgroundImage = Properties.Resources.btn_off;
                    }
                }
                else if (msg == "1")
                {
                    if (panel13210.InvokeRequired)
                    {
                        panel13210.BeginInvoke(new Action(() => panel13210.BackgroundImage = Properties.Resources.btn_on));
                    }
                    else
                    {
                        panel13210.BackgroundImage = Properties.Resources.btn_on;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Log("[UpdateRadioStsB] error : " + ex.ToString());
            }
        }
        /**
         * 13206 : 구로
         */
        private void UpdateRadioStsC(string msg)
        {
            try
            {
                if (msg == "0")
                {
                    if (panel13206.InvokeRequired)
                    {
                        panel13206.BeginInvoke(new Action(() => panel13206.BackgroundImage = Properties.Resources.btn_off));
                    }
                    else
                    {
                        panel13206.BackgroundImage = Properties.Resources.btn_off;
                    }
                }
                else if (msg == "1")
                {
                    if (panel13206.InvokeRequired)
                    {
                        panel13206.BeginInvoke(new Action(() => panel13206.BackgroundImage = Properties.Resources.btn_on));
                    }
                    else
                    {
                        panel13206.BackgroundImage = Properties.Resources.btn_on;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Log("[UpdateRadioStsC] error : " + ex.ToString());
            }
               
        }

        private void lstLog_DrawItem(object sender, DrawItemEventArgs e)
        {
            try
            {
                Console.WriteLine("lstLog_DrawItem called...");
                if (lstLog.Items.Count == 0)
                {
                    return;
                }

                // owner draw를 사용하여 ListBox에 색칠하기
                Brush myBrush;
                string msg = lstLog.Items[e.Index].ToString();
                Console.WriteLine(msg);
                if (msg.Contains("error") == true) //<font color=red>") == true)
                {
                    //lstLog.Items[e.Index] = lstLog.Items[e.Index].ToString().Replace("<font color=red>", "");
                    myBrush = Brushes.Red;
                }
                else if (msg.Contains("info") == true) // <font color=blue>") == true)
                {
                    lstLog.Items[e.Index] = lstLog.Items[e.Index].ToString().Replace("<font color=blue>", "");
                    myBrush = Brushes.Blue;
                }
                else
                {
                    myBrush = Brushes.DarkGray;
                }
                e.Graphics.DrawString(lstLog.Items[e.Index].ToString(), e.Font, myBrush, e.Bounds, StringFormat.GenericDefault);

                e.DrawFocusRectangle();
            }catch(Exception ex)
            {
                log.Log("DrawItem error : " + ex.ToString());
            }
        }

        private void defaultClear()
        {
            lblstaLastTime.Text = "";
            lblstbLastTime.Text = "";
            lblstcLastTime.Text = "";

            lblstaDataLastTime.Text = "";
            lblstbDataLastTime.Text = "";
            lblstcDataLastTime.Text = "";

            txtListenPort.ReadOnly = true;
            txtDestPort.ReadOnly = true;
            txtClientRcvPort.ReadOnly = true;
            txtLocalSendPort.ReadOnly = true;


            txtIP.ReadOnly = true;
            txtPort.ReadOnly = true;
            txtID.ReadOnly = true;
            txtPass.ReadOnly = true;

            txtDBName.ReadOnly = true;
            txtDBHost.ReadOnly = true;
            txtDBPort.ReadOnly = true;
            txtDBUserID.ReadOnly = true;
            txtDBUserPass.ReadOnly = true;

            txtFtpThreadTime.ReadOnly = true;
            txtStsThreadTime.ReadOnly = true;
        }

  //      private void panelClose_Click(object sender, EventArgs e)
  //      {
  //          // 자원 해제
  //          if (btnStart.Enabled == false)
  //          {
  //              // 자원이 사용중이므로 자원을 해제할 수 있도록 한다.
  //              btnStop.PerformClick();                
  //          }
  //          Application.Exit();
  //      }
  //      private void panelHide_MouseClick(object sender, MouseEventArgs e)
  //      {
  //          this.WindowState = FormWindowState.Minimized;
  //      }



        private void WindLidarServer_MouseDown(object sender, MouseEventArgs e)
        {
            mousePoint = new Point(e.X, e.Y);
        }

        private void WindLidarServer_MouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                Location = new Point(this.Left - (mousePoint.X - e.X),
                    this.Top - (mousePoint.Y - e.Y));
            }
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle rc = new Rectangle(this.ClientSize.Width - cGrip, this.ClientSize.Height - cGrip, cGrip, cGrip);
            ControlPaint.DrawSizeGrip(e.Graphics, this.BackColor, rc);
            rc = new Rectangle(0, 0, this.ClientSize.Width, cCaption);
            Rectangle rc2 = new Rectangle(0, 0, this.ClientSize.Width, ClientSize.Height);
            //e.Graphics.FillRectangle(Brushes.White, rc2);
            //e.Graphics.FillRectangle(Brushes.DodgerBlue, rc);   // DarkBlue, rc);


            Color color = System.Drawing.ColorTranslator.FromHtml("#32709f");
            Brush b = new SolidBrush(color);
            e.Graphics.FillRectangle(b, rc);   // DarkBlue, rc);
            //e.Graphics.FillRectangle(Brushes.DodgerBlue, rc);   // DarkBlue, rc);


            
            //e.Graphics.DrawRectangle(Pens.Black, rc2);

            ControlPaint.DrawBorder(e.Graphics, this.ClientRectangle, Color.DarkGray, ButtonBorderStyle.Solid);

        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84)
            {  // Trap WM_NCHITTEST
                Point pos = new Point(m.LParam.ToInt32() & 0xffff, m.LParam.ToInt32() >> 16);
                pos = this.PointToClient(pos);
                if (pos.Y < cCaption)
                {
                    m.Result = (IntPtr)2;  // HTCAPTION
                    return;
                }
                if (pos.X >= this.ClientSize.Width - cGrip && pos.Y >= this.ClientSize.Height - cGrip)
                {
                    m.Result = (IntPtr)17; // HTBOTTOMRIGHT
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void lblSmall_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void lblClose_Click(object sender, EventArgs e)
        {
            // 자원 해제
            if (btnStart.Enabled == false)
            {
                 // 자원이 사용중이므로 자원을 해제할 수 있도록 한다.
                 btnStop.PerformClick();                
            }
            Application.Exit();
        }

    }
}
