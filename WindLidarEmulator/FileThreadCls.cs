using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security;
using System.Runtime.InteropServices;
using System.Net;

namespace WindLidarEmulator
{
    class FileThreadCls
    {
        private Form1 main;
        private Thread stsThread;
        private ManualResetEvent waitHandle;
        private bool isShutdown;
        private string m_abertime;
        private string m_sector_size;
        private string m_scanning_speed;
        private string m_site;
        private string sourcePath = "D:\\KoreaLidar";
        private delegate void LogMessageCallback(String msg);
        private static int sector_unit = 0;
        private string st_tm;
        private string et_tm;
        LogMessageCallback log;

        public FileThreadCls(object m)
        {
            main = (Form1)m;

            isShutdown = true;

            waitHandle = new ManualResetEvent(false);
            log = new LogMessageCallback(main.logMessage);
        }

        public void setAvertTime(string m)
        {
            m_abertime = m;
        }
        public void setSectorSize(string m)
        {
            m_sector_size = m;
        }
        public void setScanningSpeed(string m)
        {
            m_scanning_speed = m;
        }
        public void setSite(string site)
        {
            m_site = site;
        }

        public void start()
        {
            isShutdown = false;

            stsThread = new Thread(new ThreadStart(FileCreateProcess));
            stsThread.Start();
        }

        public void abort()
        {
            isShutdown = true;
            if (stsThread != null) stsThread.Abort();
        }

        public void FileCreateProcess()
        {
            int index = 0;
            while (!isShutdown)
            {
                if (isShutdown == false)
                {
                    waitHandle.Reset();
                    sector_unit = sector_unit + 1;
                    string year = DateTime.Now.ToString("yyyy");
                    string mon = DateTime.Now.ToString("MM");

                    // file create
                    // DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")                    
                    string rtdFileName = DateTime.Now.ToString("dd_HH_mm_ss")+"_"+index+"_0.rtd";                 
                    log("File creation start...........");
                    if (sector_unit == 1)       // first
                    {
                        // WINDCONFIG.INI 파일 생성
                        st_tm = DateTime.Now.ToString("dd_HH_mm_ss");
                        string iniFile = DateTime.Now.ToString("dd_HH_mm_ss") + "_WindConfig.ini";

                        if (IniFileProcess(iniFile, "ini") == true)
                        {
                            log("INI : " + iniFile + " file is created....");
                        }
                    }

                    if (FileProcess(rtdFileName, "rtd") == true)
                    {
                        log("RTD : " + rtdFileName + " file is created....");
                    }

                    if (sector_unit == 360)     // last scan
                    {
                        // STA 파일 생성
                        et_tm = DateTime.Now.ToString("dd_HH_mm_ss");
                        string staFileName = st_tm + "-" + et_tm + ".sta";  
                        
                        // st_tm, et_tm 사용해서 파일 생성
                        if (StsFileProcess(staFileName, "sts") == true)
                        {
                            log("STA : " + staFileName + " file is created....");
                        }
                        sector_unit = 0;
                    }

                    // Alerm 파일 생성
                    // 30초에 하나씩??
                    if (sector_unit % 30 == 0)
                    {
                        string alarmName = DateTime.Now.ToString("dd_HH_mm_ss") + ".alm";

                        if (AlarmFileProcess(alarmName, "alm") == true)
                        {
                            log("ALM : " + alarmName + " file is created....");
                        }
                    }

                    index++;
                    log("File creation end...........");
                    //waitHandle.WaitOne(1000 * Convert.ToInt16(m_abertime) + 13);  // unit : second + 13 second
                    waitHandle.WaitOne(Convert.ToInt16(m_abertime));  
                }
            }
        }

        protected bool FileProcess(string fileName, string mode)
        {
            string year = DateTime.Now.ToString("yyyy");
            string mon = DateTime.Now.ToString("MM");
            
            //Console.WriteLine(fs);

            // 디렉토리 생성
            string path = sourcePath + "\\" + year;
            DirectoryInfo dir1 = new DirectoryInfo(path);
            if (dir1.Exists == false) dir1.Create();

            path = path + "\\" + mon;
            DirectoryInfo dir2 = new DirectoryInfo(path);
            if (dir2.Exists == false) dir2.Create();

            path = path + "\\EOLID";
            DirectoryInfo dir3 = new DirectoryInfo(path);
            if (dir3.Exists == false) dir3.Create();

            path = path + "\\DATA";
            DirectoryInfo dir4 = new DirectoryInfo(path);
            if (dir4.Exists == false) dir4.Create();

            // 파일 생성
            string fsName = Path.Combine(path, fileName);
            string[] lines = { "[Time] [Index] [Channel Index] [Window Size] [Average Time] [Local Azimuth] [Local Zenith] [Global Zenith] [Points Count] [Radial Speed 1] [Width 1] [SNR 1] [Beta 1] [Radial Speed 2] [Width 2] [SNR 2] [Beta 2] …", 
                                 "[Time] [Index] [Channel Index] [Window Size] [Average Time] [Local Azimuth] [Local Zenith] [Global Zenith] [Points Count] [Radial Speed 1] [Width 1] [SNR 1] [Beta 1] [Radial Speed 2] [Width 2] [SNR 2] [Beta 2] …", 
                                 "[Time] [Index] [Channel Index] [Window Size] [Average Time] [Local Azimuth] [Local Zenith] [Global Zenith] [Points Count] [Radial Speed 1] [Width 1] [SNR 1] [Beta 1] [Radial Speed 2] [Width 2] [SNR 2] [Beta 2] …" 
                             };

            using (StreamWriter outputFile = new StreamWriter(fsName))
            {
                foreach (string line in lines)
                    outputFile.WriteLine(line);
            }

            return true;
        }
        protected bool AlarmFileProcess(string fileName, string mode)
        {
            FileStream fs = null;
            string year = DateTime.Now.ToString("yyyy");
            string mon = DateTime.Now.ToString("MM");

            //Console.WriteLine(fs);

            // 디렉토리 생성
            string path = sourcePath + "\\" + year;
            DirectoryInfo dir1 = new DirectoryInfo(path);
            if (dir1.Exists == false) dir1.Create();

            path = path + "\\" + mon;
            DirectoryInfo dir2 = new DirectoryInfo(path);
            if (dir2.Exists == false) dir2.Create();

            path = path + "\\EOLID";
            DirectoryInfo dir3 = new DirectoryInfo(path);
            if (dir3.Exists == false) dir3.Create();


            path = path + "\\ALARM";
            DirectoryInfo dir4 = new DirectoryInfo(path);
            if (dir4.Exists == false) dir4.Create();

            // 파일 생성
            string fsName = Path.Combine(path, fileName);
            string lines = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Alarm><RemoteConnect Value=\"1\" /><LidarState Value=\"2\" /><State Name=\"Scaner State\" Value=\"1\" /><State Name=\"Processor State\" Value=\"1\" /><State Name=\"Pofile Processor State\" Value=\"1\" /><State Name=\"Detector 0 State\" Value=\"1\" /><State Name=\"Inclinometer State\" Value=\"1\" /><State Name=\"Compass State\" Value=\"1\" /><State Name=\"Gps State\" Value=\"1\" /></Alarm>";
                           

            using (StreamWriter outputFile = new StreamWriter(fsName))
            {
                //foreach (string line in lines)
                outputFile.WriteLine(lines);
            }

            return true;
        }
        protected bool StsFileProcess(string fileName, string mode)
        {
            FileStream fs = null;
            string year = DateTime.Now.ToString("yyyy");
            string mon = DateTime.Now.ToString("MM");

            //Console.WriteLine(fs);

            // 디렉토리 생성
            string path = sourcePath + "\\" + year;
            DirectoryInfo dir1 = new DirectoryInfo(path);
            if (dir1.Exists == false) dir1.Create();

            path = path + "\\" + mon;
            DirectoryInfo dir2 = new DirectoryInfo(path);
            if (dir2.Exists == false) dir2.Create();

            path = path + "\\EOLID";
            DirectoryInfo dir3 = new DirectoryInfo(path);
            if (dir3.Exists == false) dir3.Create();


            path = path + "\\DATA";
            DirectoryInfo dir4 = new DirectoryInfo(path);
            if (dir4.Exists == false) dir4.Create();

            // 파일 생성
            string fsName = Path.Combine(path, fileName);
            string[] lines = { "[Time] [Index] [Channel Index] [Window Size] [Average Time] [Local Azimuth] [Local Zenith] [Global Zenith] [Points Count] [Radial Speed 1] [Width 1] [SNR 1] [Beta 1] [Radial Speed 2] [Width 2] [SNR 2] [Beta 2] …", 
                                 "[Time] [Index] [Channel Index] [Window Size] [Average Time] [Local Azimuth] [Local Zenith] [Global Zenith] [Points Count] [Radial Speed 1] [Width 1] [SNR 1] [Beta 1] [Radial Speed 2] [Width 2] [SNR 2] [Beta 2] …", 
                                 "[Time] [Index] [Channel Index] [Window Size] [Average Time] [Local Azimuth] [Local Zenith] [Global Zenith] [Points Count] [Radial Speed 1] [Width 1] [SNR 1] [Beta 1] [Radial Speed 2] [Width 2] [SNR 2] [Beta 2] …" 
                             };

            using (StreamWriter outputFile = new StreamWriter(fsName))
            {
                foreach (string line in lines)
                    outputFile.WriteLine(line);
            }

            return true;
        }
        /**
         * [PARAMS]
            TYPE=0
            PARAM1=-180
            PARAM2=45
            PARAM3=360
            PARAM4=1
            AVERTIME=1000
         */
        protected bool IniFileProcess(string fileName, string mode)
        {
            FileStream fs = null;
            string year = DateTime.Now.ToString("yyyy");
            string mon = DateTime.Now.ToString("MM");

            //Console.WriteLine(fs);

            // 디렉토리 생성
            string path = sourcePath + "\\" + year;
            DirectoryInfo dir1 = new DirectoryInfo(path);
            if (dir1.Exists == false) dir1.Create();

            path = path + "\\" + mon;
            DirectoryInfo dir2 = new DirectoryInfo(path);
            if (dir2.Exists == false) dir2.Create();

            path = path + "\\EOLID";
            DirectoryInfo dir3 = new DirectoryInfo(path);
            if (dir3.Exists == false) dir3.Create();


            path = path + "\\DATA";
            DirectoryInfo dir4 = new DirectoryInfo(path);
            if (dir4.Exists == false) dir4.Create();

            // 파일 생성
            string fsName = Path.Combine(path, fileName);
            string[] lines = { "[PARAMS]", "TYPE=0", "PARAM1=-180", "PARAM2=45", "PARAM3=360", "PARAM4=1", "AVERTIME=1000" };

            using (StreamWriter outputFile = new StreamWriter(fsName))
            {
                foreach (string line in lines)
                    outputFile.WriteLine(line);
            }

            return true;
        }
    }
}
