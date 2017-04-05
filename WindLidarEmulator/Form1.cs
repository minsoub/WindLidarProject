using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindLidarEmulator
{
    public partial class Form1 : Form
    {
        private FileThreadCls process;

        public Form1()
        {
            InitializeComponent();

            cboSite.Items.Add("13211");
            cboSite.Items.Add("13210");
            cboSite.Items.Add("13206");

            process = null;

            btnStop.Enabled = false;

            txtAvertTime.Text = "1000";
            txtSectorSize.Text = "360";
            txtScanningSpeed.Text = "1";
            cboSite.SelectedIndex = 0;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (process == null)
            {
                if (txtAvertTime.Text == "")
                {
                    MessageBox.Show("생성주기를 입력하지 않았습니다", "입력에러", MessageBoxButtons.OK);
                    return;
                }
                if (txtSectorSize.Text == "")
                {
                    MessageBox.Show("섹터 크기를 입력하지 않았습니다", "입력에러", MessageBoxButtons.OK);
                    return;
                }
                if (txtScanningSpeed.Text == "")
                {
                    MessageBox.Show("스캐닝 속도를 입력하지 않았습니다", "입력에러", MessageBoxButtons.OK);
                    return;
                }
                if (cboSite.Text == "")
                {
                    MessageBox.Show("사이트를 선택하지 않았습니다", "입력에러", MessageBoxButtons.OK);
                    return;
                }

                process = new FileThreadCls(this);
                process.setAvertTime(txtAvertTime.Text);
                process.setSectorSize(txtSectorSize.Text);
                process.setScanningSpeed(txtScanningSpeed.Text);
                process.setSite(cboSite.Text);
                process.start();

                btnStart.Enabled = false;
                btnStop.Enabled = true;
                txtAvertTime.Enabled = false;
                txtSectorSize.Enabled = false;
                txtScanningSpeed.Enabled = false;
                cboSite.Enabled = false;
                
            }
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            process.abort();
            process = null;

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            txtAvertTime.Enabled = true;
            txtSectorSize.Enabled = true;
            txtScanningSpeed.Enabled = true;
            cboSite.Enabled = true;
        }
        public void logMessage(string msg)
        {
            logMessage(lstLog, msg);
        }

        private void logMessage(ListBox _lstLog, string _msg)
        {
            if (_lstLog.InvokeRequired)     // 해당 컨트롤이 Invoke를 요구한다면
            {
                _lstLog.Invoke(new logMessageDelegate(logMessage), new object[] { _lstLog, _msg });
            }
            else
            {
                string sendTime = " [" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "]";

                if (lstLog.Items.Count == 5000)
                {
                    lstLog.Items.RemoveAt(0);
                }
                lstLog.Items.Add(sendTime + _msg); //  Add(msg); 
                lstLog.SelectedIndex = lstLog.Items.Count - 1;

                // lstLog.Items.Insert(0, sendTime + _msg); //  Add(msg); 
            }
        }

        private delegate void logMessageDelegate(ListBox _lstLog, string _msg);


    }
}
