using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace WindLidarClientBackup
{
    public class BackupCls
    {
        private LogCls log;
        private IniFile myIniFile;
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

            DateTime chkDt = DateTime.Today.AddMonths(-3);

            string chYear = chkDt.ToString("yyyy");
            string chMon = chkDt.ToString("MM");

            myIniFile = new IniFile(@"D:\\WindLidarClient.ini");

            string backupDir = myIniFile.Read("BACKUP_PATH", "WindLidarClient");
            Console.WriteLine("init dir : " + backupDir);
            // path
            string dataPath = Path.Combine(backupDir, chYear, chMon);
            Console.WriteLine("directory : " + dataPath);

            log.Log("Target Directory : " + dataPath);
            if (Directory.Exists(dataPath) == false)
            {
                result = false;
            }
            else
            {
                // data delete
                Directory.Delete(dataPath, true);

                result = true;
                log.Log("The delete is complated.");
            }

            return result;
        }
    }
}
