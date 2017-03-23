using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindLidarSystem
{
    public sealed class ParamInitInfo
    {
        private static readonly ParamInitInfo instance = new ParamInitInfo();

        public string m_ftpIP;
        public string m_ftpPort;
        public string m_ftpUser;
        public string m_ftpPass;

        public string m_listenPort;
        public string m_clientRcvPort;
        public string m_dataclientport;
        public string m_localPort;

        public string m_dbName;
        public string m_dbUser;
        public string m_dbPass;
        public string m_dbHost;
        public string m_dbPort;
        public string m_stsThreadTime;
        public string m_ftpThreadTime;      
        public string m_sourceDir;
        public string m_backupDir;

        private IniFile iniFS;

        private ParamInitInfo() 
        {
            iniFS = new IniFile(@"D:\WindLidarServer.ini");

            m_ftpIP = iniFS.Read("UWA_FTP_IP");
            m_ftpPort = iniFS.Read("UWA_FTP_PORT");
            m_ftpUser = iniFS.Read("UWA_FTP_ID");
            m_ftpPass = iniFS.Read("UWA_FTP_PASS");

            m_listenPort = iniFS.Read("ListenPort");
            m_clientRcvPort = iniFS.Read("ClientPort");
            m_dataclientport = iniFS.Read("DataClientPort");
            m_localPort = iniFS.Read("LocalPort");

            m_dbName = iniFS.Read("DB_NAME");
            m_dbUser = iniFS.Read("DB_USER");
            m_dbPass = iniFS.Read("DB_PASS");
            m_dbHost = iniFS.Read("DB_HOST");
            m_dbPort = iniFS.Read("DB_PORT");
            m_ftpThreadTime = iniFS.Read("FTP_THREAD_TIME");
            m_stsThreadTime = iniFS.Read("STS_THREAD_TIME");
            m_sourceDir = iniFS.Read("SOURCE_PATH");
            m_backupDir = iniFS.Read("BACKUP_PATH");
        }

        public static ParamInitInfo Instance
        {
            get
            {
                return instance;
            }
        }
    }
}
