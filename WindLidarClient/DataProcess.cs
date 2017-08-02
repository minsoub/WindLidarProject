using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace WindLidarClient
{
    /**
     * DataProcess  클래스는 관측 데이터 파일을 있는지 주기적으로 체크해서
     * 관측 데이터가 있으면 관측 데이터 상태 및 파일을 서버에 전송한다.
     */
    public class DataProcess
    {
        private ObserverProcess main;
        private SndDataInfo sendInfo;
        private string m_sourcePath;
        private string m_backupPath;
        private string m_data1;
        private string m_data2;
        private LogCls fsLog;

        private string m_stCode;
        private string m_stHost;
        private string m_stPort;
        private int m_st_rcv_port;
        private int m_ft_rcv_port;
        private int m_at_rcv_port;
        private int m_ltPort;
        private char[] delimiterChar = { ':' };
        private char[] arrSeparator = { '|' };
        private char[] sndSeparator = { '_' };
        private delegate void LogMessageCallback(String msg);
        private DateTime m_chkDate;
        private int m_mode;   // 0 : STA, 1 : DBS, 2: NOT DBS
        LogMessageCallback log;
        public struct sTempInfo
        {
            public int readIndex;
            public string fileName;
            public string fullFileName;
            public string startTime;
            public string endTime;
            public string lastTime;
        }

        private sTempInfo tmpInfo;

        public DataProcess(ObserverProcess m)
        {
            main = m;
            log = new LogMessageCallback(main.setMsg);
            sendInfo = new SndDataInfo();
            tmpInfo = new sTempInfo();
            fsLog = new LogCls();
            clear();
        }

        public void clear()
        {
            sendInfo.lstInfo.Clear();
            sendInfo.staFileName = null;
            sendInfo.iniFileName = null;
            sendInfo.fileCount = 0;
        }
        public void setMode(int mode)
        {
            m_mode = mode;
        }
        public int getSendFileCount()
        {
            return sendInfo.fileCount;
        }

        public DateTime getCheckDate()
        {

            return m_chkDate;
        }
        /**
         * 네트워크 정보를 설정한다.
         */
        public void setNetworkInfo(string stCode, string stHost, string stPort, int ltPort, int st_rcv_port, int ft_rcv_port, int at_rcv_port)
        {
            m_stCode = stCode;
            m_stHost = stHost;
            m_stPort = stPort;
            m_ltPort = ltPort;
            m_st_rcv_port = st_rcv_port;
            m_ft_rcv_port = ft_rcv_port;
            m_at_rcv_port = at_rcv_port;
        }
        public void setPath(string sPath, string bPath, string data1, string data2)
        {
            m_sourcePath = sPath;
            m_backupPath = bPath;
            m_data1 = data1;        // EOLID
            m_data2 = data2;        // DATA
        }

        /**
          * FTP Server에 데이터를 업로드 할 수 있는지 체크한다.
          * 라이다에서 데이터를 쓰고 있으면 FTP Server에 데이터를 전송하면 안된다.
          * STA 파일에 대해서만 체크한다.
          * 
          */
        public bool StaHasWritePermissionOnDir(string path)
        {
            int found = 0;
            clear();
            string[] monList = { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12" };

            //DateTime testDt = DateTime.Today;
            string year = DateTime.Today.ToString("yyyy");
            string mon = DateTime.Today.ToString("MM");
            string dd = DateTime.Today.ToString("dd");
            string dataPath = "";
            bool firstRead = false;
            string lindDt = "";
            string sndFile = "";
            DateTime startDt;
            DateTime endDt;
            string laststring = null;
            //log("1");
            // 과거 데이터를 보낼 수 있으니 달의 시작점을 찾아야 한다.
            foreach (string m in monList)
            {
                dataPath = Path.Combine(path, year, m);
                dataPath = Path.Combine(dataPath, m_data1, m_data2);  // 월 데이터 체크

                // 디렉토리 내에 파일이 존재하는지 체크한다.
                if (Directory.Exists(dataPath) == true)        // 현재달의 관측데이터가 있는지 체크한다.
                {
                    // 데이터가 존재하는지 체크한다.
                    DirectoryInfo checkDir = new DirectoryInfo(dataPath);
                    FileInfo[] fileCheck = checkDir.GetFiles("*.sta");

                    if (fileCheck.Count() > 0)
                    {
                        found = 1;
                        mon = m;
                        break;
                    }
                }
            }
            //log("2");
            if (found == 0)
            {
                log("[STA] no found file");
                Console.WriteLine("no found file............");
                return false;
            }
            found = 0;

            // _sendTmp.dat 파일을 읽어들인다.
            string tmpFile = Path.Combine(path, "_sendTmp.dat");
            if (File.Exists(tmpFile))
            {
                using (StreamReader sr = File.OpenText(tmpFile))
                {
                    string line = sr.ReadLine();
                    string[] arr = line.Split(arrSeparator);
                    laststring = arr[arr.Length - 1];
                }
            }
           // log("3");
            // snd 파일이 있는지 체크하고 있으면 1시간 데이터가 채워졌는지 체크한다.
            // 1시간 데이터가 채워지지 않았다면 10분 데이터(한줄)를 STA로 읽어서 넣는다
            DirectoryInfo sndCheckdir = new DirectoryInfo(dataPath);
            FileInfo[] sndCheckInfo = sndCheckdir.GetFiles("*.snd");
            if (sndCheckInfo.Count() == 0)  // snd 파일이 없다 => first read
            {
                // first read
                firstRead = true;
            }
            else
            {
                sndFile = sndCheckInfo[0].FullName;
                if (File.Exists(sndFile))
                {
                    using (StreamReader sr = File.OpenText(sndFile))
                    {
                        string line = "";
                        while ((line = sr.ReadLine()) != null) 
                            lindDt = line.Substring(0, 19);  // 2017-05-18 20:00:00 (start date)
                    }
                }
                sndFile = sndCheckInfo[0].Name;
            }

            //log("4");
            DirectoryInfo dir = new DirectoryInfo(dataPath);
            int cnt = 0;
            sendInfo.path = dataPath;
            string fileEndDt = "";
            string fileStDt = "";
            int hour = 0;
            // STA 파일만 검색
            FileInfo[] fileArray = dir.GetFiles("*.sta");
            // 날짜순대로 정렬 => 파일명으로 정렬
            for (int i = 0; i < fileArray.Length - 1; i++)
            {
                for (int j=0; j<fileArray.Length-1-i; j++)
                {
                    FileInfo a = fileArray[j];
                    FileInfo b = fileArray[j+1];

                    DateTime at = convertTimeExtract(a.Name, mon);
                    DateTime bt = convertTimeExtract(b.Name, mon);
                    
                    if (at > bt) // fileArray[j] > fileArray[j+1])
                    {
                        FileInfo temp = fileArray[j];
                        fileArray[j] = fileArray[j + 1];
                        fileArray[j] = temp;
                    }
                }
            }
            //log("5");
            foreach (FileInfo fi in fileArray)
            {
                string file = fi.FullName;
                string ext = Path.GetExtension(file);

                if (ext == ".sta")
                {
                    Console.WriteLine("[StaHasWritePermissionOnDir] " + file);
                    if (FileLocked(file) == false)     // if File not lock
                    {
                        List<string> body = new List<string>();
                        body.Clear();

                        using (StreamReader sr = new StreamReader(file))
                        {
                            string line;
                            string head = "";

                            int idx = 0;
                            int readCount = 0;
                            while ((line = sr.ReadLine()) != null)
                            {
                                // head read
                                if (idx == 0)
                                {
                                    head = line;
                                    if (firstRead == true) tmpInfo.readIndex++;
                                }
                                else
                                {
                                    if (firstRead == true)        // first read
                                    {
                                        log("[STA] fristRead == true");
                                        int sHour = 0;
                                        int sMin = 0;
                                        if (laststring != null)
                                        {
                                            string dtString1 = line.Substring(0, 19);
                                            DateTime dt1 = DateTime.ParseExact(dtString1, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                                            DateTime testDt = DateTime.ParseExact(laststring, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                                            var diffInSeconds = (testDt - dt1).TotalSeconds;

                                            // 읽은 날짜가 이전에 읽은 날짜보다 커야 한다.
                                            if (diffInSeconds >= 0)       // dt1 : 현재 읽은 날짜가 과거
                                            {
                                                continue;       // next read
                                            }
                                        }

                                        if (readCount == 0)
                                        {
                                            fileStDt = line.Substring(0, 19);
                                            hour = System.Convert.ToInt32(fileStDt.Substring(11, 2));
                                        }
                                        string fileTmpDt = line.Substring(0, 19);  // 2017-05-18 20:00:00 (start date)
                                        
                                        sHour = System.Convert.ToInt32(fileTmpDt.Substring(11, 2));
                                        sMin = System.Convert.ToInt32(fileTmpDt.Substring(14, 2));

                                        if (sMin == 0) // last time (매 1시간)
                                        {
                                            if (sHour == 0)     // 24 시
                                            {
                                                found = 2;
                                            }
                                            found = 1;
                                        }
                                        if (sMin != 0)
                                        {
                                            DateTime dt1 = DateTime.ParseExact(fileTmpDt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                                            dt1 = dt1.AddHours(1);  // 1 hour
                                            String tm1 = dt1.ToString("yyyy-MM-dd HH:mm:ss");
                                            sndFile = tm1.Substring(8, 2) + "_" + tm1.Substring(11, 2) + "_00_00.snd";
                                        }
                                        else
                                        {
                                            sndFile = fileTmpDt.Substring(8, 2) + "_" + fileTmpDt.Substring(11, 2) + "_00_00.snd";
                                        }

                                        body.Add(line);
                                        readCount++;
                                        tmpInfo.readIndex++;
                                        fileEndDt = fileTmpDt;

                                        break;
                                    }
                                    else
                                    {

                                        string dtString1 = line.Substring(0, 19);
                                        DateTime dt1    = DateTime.ParseExact(dtString1, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                                        DateTime testDt = DateTime.ParseExact(lindDt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                                        var diffInSeconds = (testDt - dt1).TotalSeconds;

                                        // 현재 읽은 라인의 날짜가 이전에 읽은 날짜보다 커야 한다.
                                        if (diffInSeconds >= 0)       // dt1 : 현재 읽은 날짜가 과거
                                        {
                                            continue;       // next read
                                        }
                                        int sHour = System.Convert.ToInt32(dtString1.Substring(11, 2));
                                        int sMin = System.Convert.ToInt32(dtString1.Substring(14, 2));

                                        if (sMin == 0) // last time (매 1시간)
                                        {
                                            if (sHour == 0)     // 24 시
                                            {
                                                found = 2;
                                            }
                                            found = 1;
                                        }

                                        body.Add(line);
                                        readCount++;
                                        tmpInfo.readIndex++;
                                        fileEndDt = dtString1;
                                        break;
                                    }
                                }
                                idx++;
                            }  // while loop end

                            //if (found == 0)
                            //{
                            //    // 1시간 데이터를 채우지 못해서 전송하면 안된다.
                            //    readCount = 0;
                            //    body.Clear();
                            //}

                            if (body.Count > 0)
                            {
                                string staSaveName = sndFile;
                                                               
                                String saveName = Path.Combine(dataPath, staSaveName);
                                tmpInfo.fullFileName = saveName;
                                tmpInfo.fileName = sndFile;

                                string sdt1 = year + "-" + mon + "-" + staSaveName.Substring(0, 2) + " " + staSaveName.Substring(3, 2) + ":00:00";


                                startDt = DateTime.ParseExact(sdt1, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                                endDt = startDt;
                                startDt = startDt.AddHours(-1);

                                tmpInfo.startTime = startDt.ToString("yyyy-MM-dd HH:mm:ss");
                                tmpInfo.endTime = endDt.ToString("yyyy-MM-dd HH:mm:ss");
                                tmpInfo.lastTime = line.Substring(0, 19);  // 2017-05-18 20:00:00 (start date)
                                Console.WriteLine("staSaveName : " + saveName);
                                // debug
                                log("[STA] Save name : " + saveName);

                                // sta file create
                                // 존재하면 append
                                if (File.Exists(saveName))
                                {
                                    using (StreamWriter sw = File.AppendText(saveName))
                                    {
                                        foreach (string bodyLine in body)
                                        {
                                            sw.WriteLine(bodyLine);
                                        }
                                    }
                                }
                                else
                                {
                                    using (StreamWriter sw = File.CreateText(saveName))
                                    {
                                        sw.WriteLine(head);
                                        foreach (string bodyLine in body)
                                        {
                                            sw.WriteLine(bodyLine);
                                        }
                                    }
                                }
                                cnt = 1;
                            }
                        }  // using end

                        if (body.Count == 0)
                        {
                            // 파일의 끝
                            tmpInfo.readIndex = 0;
                            // sta file move
                            log("[STA] end file : " + file);
                            string t = fi.Name.Substring(0, 2);    // day
                            int tt = DateTime.Today.Day;
                            if (tt != Convert.ToInt32(t))       // 이전 데이터로 모두 읽은 데이터이므로 이동
                            {
                                log("[STA] 전송완료된 데이터로서 백업이동");
                                FileMoveProcess(file);
                            }else
                            {
                                log("[STA] 관측데이터 수신중.......");
                            }
                            continue;       // next sta
                        }
                    }

                    break;   // sta 파일 하나 읽으면 루프 종료.
                }
            }  // foreach end
            if (cnt == 0)
            {
                log("Upload data does not exists");
                return false;
            }
           // log("6");
            // snd(std)  파일 전송
            //DateTime sDt = DateTime.ParseExact(tmpInfo.startTime, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            DateTime eDt = DateTime.ParseExact(tmpInfo.endTime, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            SndDataInfo.sFileInfo sf = new SndDataInfo.sFileInfo();
            sf.fileName = tmpInfo.fileName;
            sf.fullFileName = tmpInfo.fullFileName;
            sf.startTime = tmpInfo.startTime;
            sf.endTime = tmpInfo.endTime;
            sf.found = found;

            sendInfo.fileCount++;
            sendInfo.lstInfo.Add(sf);
            m_chkDate = eDt;

            return true;
        }

        /**
         * FTP Server에 데이터를 업로드 할 수 있는지 체크한다.
         * 라이다에서 데이터를 쓰고 있으면 FTP Server에 데이터를 전송하면 안된다.
         * 현재 파일의 경우 최소 10분 전의 데이터를 읽는다. 
         */
        public bool HasWritePermissionOnDir(string path)
        {
            clear();
            string[] monList = { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12" };
            bool result = false;
            string year = DateTime.Today.ToString("yyyy");
            string mon = DateTime.Today.ToString("MM");
            string dd = DateTime.Today.ToString("dd");
            string dataPath = "";
            int found = 0;
            // 과거 데이터를 보낼 수 있으니 달의 시작점을 찾아야 한다.
            foreach (string m in monList)
            {
                dataPath = Path.Combine(path, year, m);
                dataPath = Path.Combine(dataPath, m_data1, m_data2);  // 월 데이터 체크

                // 디렉토리 내에 파일이 존재하는지 체크한다.
                if (Directory.Exists(dataPath) == true)        // 현재달의 관측데이터가 있는지 체크한다.
                {
                    // 데이터가 존재하는지 체크한다.
                    DirectoryInfo checkDir = new DirectoryInfo(dataPath);
                    FileInfo[] fileCheck = checkDir.GetFiles("*.raw");

                    if (fileCheck.Count() > 0)
                    {
                        found = 1;
                        mon = m;
                        break;
                    }
                }
            }
            if (found == 0)
            {
                dataPath = Path.Combine(path, year, mon);
                dataPath = Path.Combine(dataPath, m_data1, m_data2);   //"EOLID", "DATA");
            }

            Console.WriteLine("dataPath : {0}", dataPath);

            log("dataPath : " + dataPath);

            // 디렉토리 내에 파일이 존재하는지 체크한다.
            if (Directory.Exists(dataPath) == false)        // 현재달의 관측데이터가 있는지 체크한다.
            {
                Console.WriteLine("Directory not exist.... : {0}", path);
                log("Directory not exist.... : " + path);
                return false;
            }

            DirectoryInfo dir = new DirectoryInfo(dataPath);
            sendInfo.path = dataPath;

            // raw, ini, rtd 파일 전송
            SndDataInfo.sFileInfo sfDBS = getSendData(dir, "DBS", mon);
            if (sfDBS.fileCnt > 0)
            {
                sendInfo.lstInfo.Add(sfDBS);
                result = true;
            }

            SndDataInfo.sFileInfo sfPPI = getSendData(dir, "PPI", mon);
            if (sfPPI.fileCnt > 0)
            {
                result = true;
                sendInfo.lstInfo.Add(sfPPI);
            }

            SndDataInfo.sFileInfo sfRHI = getSendData(dir, "RHI", mon);
            if (sfRHI.fileCnt > 0)
            {
                result = true;
                sendInfo.lstInfo.Add(sfRHI);
            }

            SndDataInfo.sFileInfo sfLOS = getSendData(dir, "LOS", mon);
            if (sfLOS.fileCnt > 0)
            {
                result = true;
                sendInfo.lstInfo.Add(sfLOS);
            }

            return result;
        }

        /**
         * 관측데이터를 디렉토리에서 검색해서 구조체에 담아서 리턴한다.
         */ 
        private SndDataInfo.sFileInfo getSendData(DirectoryInfo dir, string mode, string mon)
        {
            // mode : DBS, PPI, RHI, LOS
            SndDataInfo.sFileInfo sf = new SndDataInfo.sFileInfo();
            sf.fileCnt = 0;
            sf.iniFile = "";
            sf.rawFile = "";
            sf.rtdFile = "";

            DateTime curDt = DateTime.Now;
            curDt = curDt.AddMinutes(-10);  // -10 minutes
            sf.rawFile = "";
            int idx = 0;
            int firstChk = 0;
            string tmpDt2 = "";
            foreach (FileInfo fi in dir.GetFiles("*_"+mode+"*").OrderBy(fi => fi.Name))      // 날짜순 정렬
            {
                string file = fi.FullName;
                string ext = Path.GetExtension(file);

                Console.WriteLine("ext : {0}", ext);
                if (ext == ".raw" || ext == ".ini" || ext == ".rtd")
                {
                    if (firstChk == 0)
                    {
                        DateTime sd = convertTimeExtract(fi.Name, mon);
                        sf.startTime = sd.ToString("yyyy-MM-dd HH:mm:ss");
                        sf.endTime = sf.startTime;
                        m_chkDate = sd;
                    }
                    firstChk = 1;
                }

                if (ext == ".raw" || ext == ".ini" || ext == ".rtd")
                {
                    DateTime rtdDt = convertTimeExtract(fi.Name, mon);
                    idx++;
                    string tmpDt = rtdDt.ToString("yyyy-MM-dd HH:mm");

                    string pDt = tmpDt + ":00";  // second 포함
                    DateTime pD = DateTime.ParseExact(pDt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                    //Console.WriteLine("curDt : " + curDt.ToString("yyyy-MM-dd HH:mm:ss"));
                    //Console.WriteLine("pD : " + pD.ToString("yyyy-MM-dd HH:mm:ss"));
                    // 디버그용
                    fsLog.Log("SndDataInfo.sFileInfo getSendData pD : " + pD.ToString("yyyy-MM-dd HH:mm:ss"));

                    if (pD > curDt)     // 데이터 파일 날짜가 현재 날짜보다 10분 이전내에 포함되면 아직 파일 생성중이다.
                    {
                        sf.rawFile = "";
                        fsLog.Log("[getSendData()] : 데이터 파일 날짜가 현재 날짜보다 10분 이전 내에 포함되어 있어 전송대상에 제외됩니다["+pDt+", "+curDt.ToString("yyyy-MM-dd HH:mm:ss")+"]");
                        sf.fileCnt = 0;
                        break;
                    }

                    if (idx == 1)
                    {
                        tmpDt2 = tmpDt;
                    }
                    else
                    {
                        if (tmpDt2 != tmpDt) break;   // 파일 3개의 날짜가 다르면 종료
                    }

                    sendInfo.fileCount++;
                    

                    if (ext == ".raw")
                    {
                        sf.rawFullName = fi.FullName;
                        sf.rawFile = fi.Name;
                        sf.fileCnt++;
                    }
                    else if (ext == ".ini")
                    {
                        sf.iniFullName = fi.FullName;
                        sf.iniFile = fi.Name;
                        sf.fileCnt++;
                    }
                    else if (ext == ".rtd")
                    {
                        sf.rtdFullName = fi.FullName;
                        sf.rtdFile = fi.Name;
                        sf.fileCnt++;
                    }
                }
            }

            return sf;

        }

        /**
         * yyyy_mm_dd_sendTmp.dat 파일을 업데이트 한다.
         * read index | filename | full file name | start time | end time
         */
        public bool tmpSave(string path)
        {
            try
            {
                string year = DateTime.Today.ToString("yyyy");
                string mon = DateTime.Today.ToString("MM");
                string dd = DateTime.Today.ToString("dd");
                string dataPath = path;  //  Path.Combine(path, year, mon);
                //dataPath = Path.Combine(dataPath, m_data1, m_data2);   // "EOLID", "DATA");

                String tmpFileName = "_sendTmp.dat";
                String tmpFile = Path.Combine(dataPath, tmpFileName);

                string data = tmpInfo.readIndex + "|" + tmpInfo.fileName + "|" + tmpInfo.fullFileName + "|" + tmpInfo.startTime + "|" + tmpInfo.lastTime;
                using (StreamWriter sw = File.CreateText(tmpFile))
                {
                    sw.WriteLine(data);
                }
                return true;
            }catch(Exception ex)
            {
                main.setMsg("[STA] temp save error : " + ex.ToString());
                return false;
            }
        }

        /**
          * STA 관측 데이터 상태 시작 정보를 전송한다.
          */
        public bool startStaStatusSendData()
        {
            bool result = false;
            Console.WriteLine("startStaStatusSendData => m_mode : {0}", m_mode);
            try
            {
                SndDataInfo.sFileInfo info = sendInfo.lstInfo[0];
                string sD = null;
                string eD = null;
                string stDt = null;
                string etDt = null;
                sD = info.startTime;
                eD = info.endTime;

                string y1, y2, mm1, mm2, d1, d2, h1, h2, m1, m2, s1, s2;
                // 2017-02-09 14:32:11
                y1 = sD.Substring(0, 4);
                mm1 = sD.Substring(5, 2);
                d1 = sD.Substring(8, 2);
                h1 = sD.Substring(11, 2);
                m1 = sD.Substring(14, 2);
                s1 = sD.Substring(17, 2);
                y2 = eD.Substring(0, 4);
                mm2 = eD.Substring(5, 2);
                d2 = eD.Substring(8, 2);
                h2 = eD.Substring(11, 2);
                m2 = eD.Substring(14, 2);
                s2 = eD.Substring(17, 2);

                stDt = y1 + "_" + mm1 + "_" + d1 + "_" + h1 + "_" + m1 + "_" + s1;
                etDt = y2 + "_" + mm2 + "_" + d2 + "_" + h2 + "_" + m2 + "_" + s2;

                // type save
                //sendInfo.type = type;
                sendInfo.m_year = y2;
                sendInfo.m_mon = mm2;
                sendInfo.m_day = d2;

                // AT : 관측소 ID : IP ADDR : 시작시각 : 종료시각 : 파일개수 : sta파일명 : S
                string msg = "AT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + sendInfo.fileCount + ":" + info.fileName + ":S" ;
                byte[] buf = Encoding.ASCII.GetBytes(msg);
                int stPort = System.Convert.ToInt32(m_stPort);          // 10001

                using (UdpClient c = new UdpClient(m_at_rcv_port))      // 10004 : AT_RCV_PORT
                {
                    c.Send(buf, buf.Length, m_stHost, stPort);
                    //log.Log("File status data send (startStatusSendData :" + m_stHost + "[" + stPort + "]) " + msg);
                    Console.WriteLine("File data(STA) send msg : " + msg);
                    main.setMsg("STA send start :" + m_stHost + "[" + stPort + "])" + msg);

                    c.Client.ReceiveTimeout = 2000;     // 2 second
                    IPEndPoint ipepLocal = new IPEndPoint(IPAddress.Any, m_at_rcv_port+5);     // 10001 + 2
                    EndPoint remote = (EndPoint)ipepLocal;

                    byte[] rcvBuf = c.Receive(ref ipepLocal);

                    if (rcvBuf == null)
                    {
                        result = false;
                    }
                    else
                    {
                        string data = Encoding.UTF8.GetString(rcvBuf);
                        string[] msgArr = data.Split(delimiterChar);
                        //log.Log("Alarm receive msg(almDataSend) : " + data);
                        Console.WriteLine("File data(STA) get msg : " + data);
                        main.setMsg("STA receive msg : " + data);

                        if (msgArr[3] == "ok")
                        {
                            result = true;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                }
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                result = false;
                Console.WriteLine(ex.ToString());
                // log.Log("Alarm data send error(startStaStatusSendData) : " + ex.ToString());
                main.setMsg("File data send error(startStaStatusSendData) : " + ex.ToString());
                fsLog.Log("[startStaStatusSendData()] : File data send error => " + ex.ToString());
            }

            return result;
        }

        /**
         * 관측 데이터 상태 시작 정보를 전송한다.
         */
        public bool startStatusSendData()
        {
            bool result = false;

            try
            {
                SndDataInfo.sFileInfo info = sendInfo.lstInfo[0];
                string sD = null;
                string eD = null;
                string stDt = null;
                string etDt = null;
                sD = info.startTime;
                eD = info.endTime;

                string y1, y2, mm1, mm2,  d1, d2, h1, h2, m1, m2, s1, s2;
                // 2017-02-09 14:32:11
                y1 = sD.Substring(0, 4);
                mm1 = sD.Substring(5, 2);
                d1 = sD.Substring(8, 2);
                h1 = sD.Substring(11, 2);
                m1 = sD.Substring(14, 2);
                s1 = sD.Substring(17, 2);
                y2 = eD.Substring(0, 4);
                mm2 = eD.Substring(5, 2);
                d2 = eD.Substring(8, 2);
                h2 = eD.Substring(11, 2);
                m2 = eD.Substring(14, 2);
                s2 = eD.Substring(17, 2);

                stDt = y1 + "_" + mm1 + "_" + d1 + "_" + h1 + "_" + m1 + "_" + s1;
                etDt = y2 + "_" + mm2 + "_" + d2 + "_" + h2 + "_" + m2 + "_" + s2;

                // Ini파일에서 type을 읽어 들인다.
                string iniFilePath = info.iniFullName;
                Console.WriteLine("initFile : " + iniFilePath);

                IniFile iniFile = new IniFile(iniFilePath);
                string type = iniFile.Read("TYPE", "PARAMS");
                string p1 = iniFile.Read("PARAM1", "PARAMS");
                string p2 = iniFile.Read("PARAM2", "PARAMS");
                string p3 = iniFile.Read("PARAM3", "PARAMS");
                string p4 = iniFile.Read("PARAM4", "PARAMS");
                string p5 = iniFile.Read("AVERTIME", "PARAMS");

                // type save
                sendInfo.type = type;
                sendInfo.m_year = y1;
                sendInfo.m_mon = mm1;
                sendInfo.m_day = d1;

                string msg = "FT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + info.fileCnt + ":" 
                    + info.iniFile + ":" + info.rawFile + ":" + info.rtdFile + ":" +type+ ":"+p1+":"+p2+":"+p3+":"+p4+":"+p5+":S";
                //string msg = "FT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + sendInfo.fileCount + ":"
                //    + info.iniFile + ":" + info.rawFile + ":" + info.rtdFile + ":" + type + ":" + p1 + ":" + p2 + ":" + p3 + ":" + p4 + ":" + p5 + ":S";
                // Debug
                fsLog.Log(msg);

                byte[] buf = Encoding.ASCII.GetBytes(msg);
                int stPort = System.Convert.ToInt32(m_stPort);          // 10001
                Console.WriteLine("File data send msg : " + msg);
                using (UdpClient c = new UdpClient(m_ft_rcv_port))       // 10003
                {
                    c.Send(buf, buf.Length, m_stHost, stPort);
                    //log.Log("File status data send (startStatusSendData :" + m_stHost + "[" + stPort + "]) " + msg);
                   // Console.WriteLine("File data send msg : " + msg);
                    main.setMsg("File data send start (startStatusSendData :" + m_stHost + "[" + stPort + "])" + msg);

                    c.Client.ReceiveTimeout = 2000;     // 2 second
                    IPEndPoint ipepLocal = new IPEndPoint(IPAddress.Any, m_ft_rcv_port+5);     // 10001 + 2
                    EndPoint remote = (EndPoint)ipepLocal;

                    byte[] rcvBuf = c.Receive(ref ipepLocal);

                    if (rcvBuf == null)
                    {
                        result = false;
                    }
                    else
                    {
                        string data = Encoding.UTF8.GetString(rcvBuf);
                        string[] msgArr = data.Split(delimiterChar);
                        //log.Log("Alarm receive msg(almDataSend) : " + data);
                        Console.WriteLine("File data get msg : " + data);
                        main.setMsg("File data receive msg(startStatusSendData) : " + data);

                        if (msgArr[3] == "ok")
                        {
                            result = true;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                }
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                result = false;
                Console.WriteLine(ex.ToString());
                // log.Log("Alarm data send error(startStatusSendData) : " + ex.ToString());
                main.setMsg("File data send error(startStatusSendData) : " + ex.ToString());
                fsLog.Log("[startStatusSendData()] : File data send error => " + ex.ToString());
            }


            return result;
        }

        /**
         * 관측 데이터 전송 완료 메시지를 전송한다.
         * 전송된 개수를 포함해야 한다.
         * FT:관측소ID:IP:시작시각:종료시각:총개수:파일명:E
         */
        public bool endStatusSendData()
        {
            bool result = false;

            try
            {
                SndDataInfo.sFileInfo info = sendInfo.lstInfo[0];
                string sD = null;
                string eD = null;
                string stDt = null;
                string etDt = null;
                sD = info.startTime;
                eD = info.endTime;

                string y1, y2, mm1, mm2, d1, d2, h1, h2, m1, m2, s1, s2;
                // 2017-02-09 14:32:11
                y1 = sD.Substring(0, 4);
                mm1 = sD.Substring(5, 2);
                d1 = sD.Substring(8, 2);
                h1 = sD.Substring(11, 2);
                m1 = sD.Substring(14, 2);
                s1 = sD.Substring(17, 2);
                y2 = eD.Substring(0, 4);
                mm2 = eD.Substring(5, 2);
                d2 = eD.Substring(8, 2);
                h2 = eD.Substring(11, 2);
                m2 = eD.Substring(14, 2);
                s2 = eD.Substring(17, 2);

                stDt = y1 + "_" + mm1 + "_" + d1 + "_" + h1 + "_" + m1 + "_" + s1;
                etDt = y2 + "_" + mm2 + "_" + d2 + "_" + h2 + "_" + m2 + "_" + s2;

                string msg = "";
                int rcvPort = -1;
                Console.WriteLine("m_mode : " + m_mode);
                if (m_mode == 0)
                {  // STA
                    msg = "AT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + sendInfo.fileCount + ":" + info.fileName + ":E";
                    rcvPort = m_at_rcv_port;
                }
                else    // NOT STA
                {
                    string fff = "";
                    if (info.rawFile != "" && info.rawFile != null)
                    {
                        fff = info.rawFile;
                    }
                    else if(info.iniFile != "" && info.iniFile != null)
                    {
                        fff = info.iniFile;
                    }
                    else if(info.rtdFile != "" && info.rtdFile != null)
                    {
                        fff = info.rtdFile;
                    }
                    msg = "FT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + info.fileCnt + ":" + fff + ":E";
                    rcvPort = m_ft_rcv_port;
                }
               
                byte[] buf = Encoding.ASCII.GetBytes(msg);
                int stPort = System.Convert.ToInt32(m_stPort);           // 10001


                using (UdpClient c = new UdpClient(rcvPort))       // 10003(FT), 10004(AT)
                {
                    c.Send(buf, buf.Length, m_stHost, stPort);
                    //log.Log("File status data send (startStatusSendData :" + m_stHost + "[" + stPort + "]) " + msg);
                    Console.WriteLine("File data send msg : " + msg);
                    main.setMsg("End Snd Msg (" + m_stHost + "[" + stPort + "])" + msg);
                    //fsLog.Log("File data send end (startStatusSendData :" + m_stHost + "[" + stPort + "])" + msg);

                    c.Client.ReceiveTimeout = 2000;     // 2 second
                    IPEndPoint ipepLocal = new IPEndPoint(IPAddress.Any, rcvPort+5);     // 10001 + 2
                    EndPoint remote = (EndPoint)ipepLocal;

                    byte[] rcvBuf = c.Receive(ref ipepLocal);

                    if (rcvBuf == null)
                    {
                        result = false;
                    }
                    else
                    {
                        string data = Encoding.UTF8.GetString(rcvBuf);
                        string[] msgArr = data.Split(delimiterChar);
                        //log.Log("Alarm receive msg(almDataSend) : " + data);
                        Console.WriteLine("File data get msg : " + data);
                        main.setMsg("End Rcv msg : " + data);
                        // fsLog.Log("File data receive msg(endStatusSendData) : " + data);

                        if (msgArr[3] == "ok")
                        {
                            result = true;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                }
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                result = false;
                Console.WriteLine(ex.ToString());

                main.setMsg("File data send error(endStatusSendData) : " + ex.ToString());
                fsLog.Log("[endStatusSendData()] : File data send error => " + ex.ToString());
            }


            return result;
        }
        /**
         * STA 파일에 대해서 FTP에 전송한다.  
         */
        public bool StaftpSend(string ftp_uri, string host, string port, string user, string pass)
        {
            var enabled = true;
            Console.WriteLine("StaftpSend => m_mode : {0}", m_mode);
            //log("[ FtpSend ] function called....");
            FtpModuleLib ftpClient = new FtpModuleLib(this);
            ftpClient.setFtpInfo(m_stCode, ftp_uri, host, port, user, pass);
            ftpClient.setSendData(sendInfo);

            int sendCount = ftpClient.sendStaDataToFtpServer();    // 보낸 파일 개수를 리턴한다.
            sendInfo.sendCount = sendCount;

            if (sendCount == 0)
            {
                enabled = false;
            }

            return enabled;
        }

        /**
          * FTP Server에 데이터를 전송한다.
          */
        public bool ftpSend(string ftp_uri, string host, string port, string user, string pass)
        {
            var enabled = true;

            log("[ FtpSend ] function called....");
            FtpModuleLib ftpClient = new FtpModuleLib(this);
            ftpClient.setFtpInfo(m_stCode, ftp_uri, host, port, user, pass);
            ftpClient.setSendData(sendInfo);

            int sendCount = ftpClient.sendDataToFtpServer();    // 보낸 파일 개수를 리턴한다.
            sendInfo.sendCount = sendCount;

            if (sendCount == 0) 
            {
                enabled = false;
            }

            return enabled;
        }

        /**
         * 입력된 파일명으로 BACKUP 디렉토리로 파일을 이동한다.
         */
        public bool FileMoveProcess(string fileName)
        {
            var result = false;

            try
            {
                string destFileName = fileName.Replace("DATA", "BACKUP");
                FileInfo backFile = new FileInfo(destFileName);
                string dirBackup = backFile.Directory.FullName;

                log("FielMoveProcess : " + fileName + " => " + destFileName);
                if (!Directory.Exists(dirBackup))
                {
                    Directory.CreateDirectory(dirBackup);
                }
                if (File.Exists(destFileName))
                {
                    File.Delete(destFileName);
                }
                FileInfo file = new FileInfo(fileName);
                file.MoveTo(destFileName);

                result = true;
            }
            catch (IOException ex)
            {
                log("[FileMoveProcess] : " + ex.ToString() + "=>" + fileName);
                Console.WriteLine("[FileMoveProcess] : " + ex.ToString() + "=>" + fileName);
                fsLog.Log("[FileMoveProcess] : " + ex.ToString() + "=>" + fileName);

                result = false;
            }
            return result;
        }
        /**
         * STA 파일에 대해서 백업한다.
         */
        public bool FileStaMoveProcess(string path)
        {
            var result = false;
            SndDataInfo.sFileInfo info = sendInfo.lstInfo[0]; ;
            string destFileName = "";
            try
            {
                //info = sendInfo.lstInfo[0];

                // sta 파일 이동
                if (info.found > 0)
                {
                    destFileName = info.fullFileName.Replace("DATA", "BACKUP");
                    FileInfo backFile = new FileInfo(destFileName);
                    string dirBackup = backFile.Directory.FullName;

                    log("FileStaMoveProcess : " + info.fullFileName + " => " + destFileName);

                    if (!Directory.Exists(dirBackup))
                    {
                        Directory.CreateDirectory(dirBackup);
                    }
                    if (File.Exists(destFileName))
                    {
                        File.Delete(destFileName);
                    }

                    FileInfo staFile = new FileInfo(info.fullFileName);
                    staFile.MoveTo(destFileName);

                    tmpSave(path);
                }
                result = true;
            }
            catch (IOException ex)
            {
                log("[FileStaMoveProcess] : " + ex.ToString());
                Console.WriteLine("[FileStaMoveProcess] : " + ex.ToString());
                Console.WriteLine("FileStaMoveProcess : " + info.fullFileName + " => " + destFileName);

                fsLog.Log("FileStaMoveProcess : " + info.fullFileName + " => " + destFileName);
                fsLog.Log(ex.ToString());

                result = false;
            }

            return result;
        }

        /**
         * 파일이 존재하면 이동할 때 삭제를 하고 이동할 것인지를 결정해야 한다.
         */
        public bool FileMoveProcess()
        {
            var result = false;
            try
            {
                SndDataInfo.sFileInfo info = sendInfo.lstInfo[0];
                string destFileName = "";
                // Ini 파일 이동  
                if (info.iniFullName != null)
                {
                    destFileName = info.iniFullName.Replace("DATA", "BACKUP");
                    FileInfo backFile = new FileInfo(destFileName);
                    string dirBackup = backFile.Directory.FullName;
                    if (!Directory.Exists(dirBackup))
                    {
                        Directory.CreateDirectory(dirBackup);
                    }
                    if (File.Exists(destFileName))
                    {
                        File.Delete(destFileName);
                    }

                    FileInfo iniFile = new FileInfo(info.iniFullName);
                    iniFile.MoveTo(destFileName);

                }

                // rtd 파일 이동
                if (info.rtdFullName != null)
                {
                    destFileName = info.rtdFullName.Replace("DATA", "BACKUP");
                    if (File.Exists(destFileName))
                    {
                        File.Delete(destFileName);
                    }
                    FileInfo rtdFile = new FileInfo(info.rtdFullName);
                    rtdFile.MoveTo(destFileName);
                }
                if (info.rawFullName != null)
                {
                    // raw 파일 이동
                    destFileName = info.rawFullName.Replace("DATA", "BACKUP");
                    if (File.Exists(destFileName))
                    {
                        File.Delete(destFileName);
                    }
                    FileInfo rawFile = new FileInfo(info.rawFullName);
                    rawFile.MoveTo(destFileName);
                }

                result = true;

            }
            catch (IOException ex)
            {
                log("[FileMoveProcess] : " + ex.ToString());
                Console.WriteLine("[FileMoveProcess] : " + ex.ToString());

                fsLog.Log("FileMoveProcess : " + ex.ToString());

                result = false;
            }

            return result;
        }

        /**
         *  파일이 Lock이 걸려 있는지 체크한다.
         *  Lock이 걸려 있으면 파일이 사용중이므로 
         *  파일을 전송하지 않는다.
         */
        public static bool FileLocked(string FileName)
        {
            FileStream fs = null;
            try
            {
                Console.WriteLine(" FileLocked : " + FileName);

                // NOTE: This doesn't handle situations where file is opened for writing by another process but put into write shared mode, it will not throw an exception and won't show it as write locked
                fs = File.Open(FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None); // If we can't open file for reading and writing then it's locked by another process for writing
            }
            catch (UnauthorizedAccessException) // https://msdn.microsoft.com/en-us/library/y973b725(v=vs.110).aspx
            {
                // This is because the file is Read-Only and we tried to open in ReadWrite mode, now try to open in Read only mode
                try
                {
                    fs = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.None);
                }
                catch (Exception)
                {
                    return true; // This file has been locked, we can't even open it to read
                }
            }
            catch (Exception)
            {
                return true; // This file has been locked
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }
            return false;
        }
        /**
         * STA 파일이름을 받아서 from, to로 분리해서 리턴한다.
         * 파일 형식 : 10_10_19_00_-_10_10_20_09.sta
         */
        DateTime[] fromDateTimeExtract(string data)
        {
            // sta format : 10_10_19_00_-_10_10_20_09.sta
            string toDt = null;
            string fromDt = null;

            string year = DateTime.Today.ToString("yyyy");
            string mon = DateTime.Today.ToString("MM");

            string d1, d2, h1, h2, m1, m2, s1, s2;
            d1 = data.Substring(0, 2);
            h1 = data.Substring(3, 2);
            m1 = data.Substring(6, 2);
            s1 = data.Substring(9, 2);

            d2 = data.Substring(14, 2);
            h2 = data.Substring(17, 2);
            m2 = data.Substring(20, 2);
            s2 = data.Substring(23, 2);

            toDt = year + "-" + mon + "-" + d1 + " " + h1 + ":" + m1 + ":" + s1;
            fromDt = year + "-" + mon + "-" + d2 + " " + h2 + ":" + m2 + ":" + s2;

           // Console.WriteLine("toDt : " + toDt);
           // Console.WriteLine("fromDt : " + fromDt);

            DateTime[] arr = new DateTime[2];
            arr[0] =  DateTime.ParseExact(toDt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            arr[1] = DateTime.ParseExact(fromDt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

            return arr;
        }

        DateTime convertTimeExtract(string data, string mon)
        {
            // 10_09_00_58_356_0.rtd
            string year = DateTime.Today.ToString("yyyy");
            //string mon = DateTime.Today.ToString("MM");
            string dt = null;

            string d1, h1, m1, s1;
            d1 = data.Substring(0, 2);
            h1 = data.Substring(3, 2);
            m1 = data.Substring(6, 2);
            s1 = data.Substring(9, 2);
            dt = year + "-" + mon + "-" + d1 + " " + h1 + ":" + m1 + ":" + s1;
           // Console.WriteLine("convertTimeExtract : " +dt);


            return DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

    }
}
