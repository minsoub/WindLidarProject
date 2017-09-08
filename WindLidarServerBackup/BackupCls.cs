using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace WindLidarServerBackup
{
    public class BackupCls
    {
        private LogCls log;
        private IniFile myIniFile;
        private string[] s_code_arr = {"13211", "13210", "13206"};
        public BackupCls()
        {
             log = new LogCls();
        }

        /**
         * Backup start
         */
        public void process()
        {
            log.Log("Backup deletion start.............");

            if (backupCheck() == false)
            {
                log.Log("Backup data isn't exist!!!");
            }
            else
            {
                log.Log("Backup data is deleted....");
            }
            log.Log("Backup deletion end");
        }

        /**
         * Backup 가능 여부를 체크한다.
         */
        public bool backupCheck()
        {
            bool result = false;

            DateTime chkDt = DateTime.Today.AddMonths(-3);      // before 3 months

            string chYear = chkDt.ToString("yyyy");
            string chMon = chkDt.ToString("MM");

            myIniFile = new IniFile(@"D:\\WindLidarServer.ini");

            string backupDir = myIniFile.Read("BACKUP_PATH", "WindLidarSystem");
            Console.WriteLine("init dir : " + backupDir);
            // path
            for (int i = 0; i < s_code_arr.Length; i++)
            {
                string dataPath = Path.Combine(backupDir, s_code_arr[i], chYear, chMon);
                Console.WriteLine("directory : " + dataPath);

                log.Log("Target Directory : " + dataPath);
                if (Directory.Exists(dataPath) == false)
                {
                    //result = false;
                }
                else
                {
                    // data delete
                    Directory.Delete(dataPath, true);

                    result = true;
                    log.Log("The delete is complated.");
                }
            }

            string logDir = myIniFile.Read("LOG_DIR", "WindLidarSystem");
            log.Log("Log dir : " + logDir);

            // 디렉토리를 읽어서 chkDt보다 늦은 파일들은 모두 삭제한다.
            if (Directory.Exists(logDir) == false)        // 현재달의 관측데이터가 있는지 체크한다.
            {
                Console.WriteLine("Directory not exist.... : {0}", logDir);
                log.Log("Directory not exist.... : " + logDir);

                return false;
            }
            DirectoryInfo dir = new DirectoryInfo(logDir);
            FileInfo[] fileArray = dir.GetFiles();
            foreach (FileInfo fi in fileArray)
            {
                DateTime rtdDt = convertTimeExtract(fi.Name);

                if (chkDt >= rtdDt)
                {
                    fi.Delete();
                    result = true;
                }
            }
            return result;
        }

        DateTime convertTimeExtract(string data)
        {
            // Log20170602.log
            string dt = null;

            string y1, m1, d1;
            y1 = data.Substring(3, 4);
            m1 = data.Substring(7, 2);
            d1 = data.Substring(9, 2);

            dt = y1 + "-" + m1 + "-" + d1;

            return DateTime.ParseExact(dt, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
