using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindLidarSystem
{
    public class SndDataInfo
    {
        public string path;
        public string staFileName;
        public string staFullFileName;
        public string iniFileName;
        public string iniFullFileName;
        public string rawFileName;
        public string rawFullFileName;
        public string rtdFileName;
        public string rtdFullFileName;
        public int fileCount;
        public int sendCount;
        public string type;
        public List<sFileInfo> lstInfo = new List<sFileInfo>();
        public string m_year;
        public string m_mon;
        public string m_day;
        public int mode;
        public struct sFileInfo
        {
            public string fileName;
            public string fullFileName;
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
            rtdFileName = "";
            rawFileName = "";
            fileCount = 0;
            sendCount = 0;
            type = "";
            mode = -1;
        }
    }
}
