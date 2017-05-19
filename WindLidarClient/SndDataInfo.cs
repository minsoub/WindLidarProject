using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindLidarClient
{
    public class SndDataInfo
    {
        public string path;
        public string staFileName;
        public string staFullFileName;
        public string iniFileName;
        public string iniFullFileName;
        public int fileCount;
        public int sendCount;
        public string type;
        public List<sFileInfo> lstInfo = new List<sFileInfo>();
        public string m_year;
        public string m_mon;
        public string m_day;

        public struct sFileInfo
        {
            public string fileName;
            public string fullFileName;
            public string startTime;
            public string endTime;

            public string iniFile;
            public string iniFullName;
            public string rawFile;
            public string rawFullName;
            public string rtdFile;
            public string rtdFullName;
        }

        public SndDataInfo()
        {
            clear();
        }

        public void clear()
        {
            lstInfo.Clear();
            path = "";
            staFileName = "";
            iniFileName = "";
            fileCount = 0;
            sendCount = 0;
            type = "";
        }
    }
}
