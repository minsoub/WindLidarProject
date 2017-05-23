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
             myIniFile = new IniFile(@"D:\WindLidarClient.ini");
             log = new LogCls();
        }

        /**
         * Backup start
         */
        public void process()
        {
            log.Log("Backup start.............");

            if (backupCheck() == false)
            {
                log.Log("Backup data isn't exist!!!");
            }
            else
            {
                log.Log("Backup data is deleted....");
            }
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

            // path
            string dataPath = Path.Combine(myIniFile.Read("BACKUP_PATH"), chYear, chMon);

            if (Directory.Exists(dataPath) == false)
            {
                result = false;
            }
            else
            {
                // data delete
                Directory.Delete(dataPath, true);

                result = true;
            }

            return result;
        }
    }
}
