using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security;
using System.Runtime.InteropServices;
namespace test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            
        }

        private void button1_Click(object sender, EventArgs e)
        {
                        // 파일이 존재하면 접근가능한지 체크한다.
            DirectoryInfo dir = new DirectoryInfo("D:\\ftp_test");
            FileInfo[] fList = dir.GetFiles();

            for (int i = 0; i < fList.Length; i++)
            {
                string file = Path.Combine(fList[i].DirectoryName, fList[i].ToString());
                Console.WriteLine(file);
                // Open the stream and read it back.
                using (FileStream fs = fList[i].Open(FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    byte[] b = new byte[10];
                    UTF8Encoding temp = new UTF8Encoding(true);

                    while (fs.Read(b, 0, b.Length) > 0)
                    {
                        Console.WriteLine(temp.GetString(b));
                        break;
                    }
                    
                }
            }
        }
    }
}
