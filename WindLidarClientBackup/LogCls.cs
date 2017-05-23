using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace WindLidarClientBackup
{
    public class LogCls
    {

        public LogCls( )
        {
        }

        public void Log(string msg)
        {
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string filePath = path + @"\Logs\Log" + DateTime.Today.ToString("yyyyMMdd") + ".log";
            string dirPath = path + @"\Logs";
            string tmp;

            DirectoryInfo di = new DirectoryInfo(dirPath);
            FileInfo fi = new FileInfo(filePath);

            try
            {
                if (di.Exists != true) Directory.CreateDirectory(dirPath);

                if (fi.Exists != true)
                {
                    using(StreamWriter sw = new StreamWriter(filePath))
                    {
                        tmp = string.Format("[{0}] : {1}", GetDateTime(), msg);
                        sw.WriteLine(tmp);
                        sw.Close();
                    }
                }else
                {
                    using(StreamWriter sw = File.AppendText(filePath))
                    {
                        tmp = string.Format("[{0}] : {1}", GetDateTime(), msg);
                        sw.WriteLine(tmp);
                        sw.Close();
                    }
                }
            }catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public string GetDateTime()
        {
            DateTime nDate = DateTime.Now;
            return nDate.ToString("yyyy-MM-dd HH:mm:ss") + ":" + nDate.Millisecond.ToString("000");
        }
    }
}
