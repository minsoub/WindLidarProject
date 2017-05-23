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

        private string m_stCode;
        private string m_stHost;
        private string m_stPort;
        private int m_st_rcv_port;
        private int m_ft_rcv_port;
        private int m_at_rcv_port;
        private int m_ltPort;
        private char[] delimiterChar = { ':' };
        private char[] arrSeparator = { '|' };
        private delegate void LogMessageCallback(String msg);
        private DateTime m_chkDate;
        LogMessageCallback log;
        public struct sTempInfo
        {
            public int readIndex;
            public string fileName;
            public string fullFileName;
            public string startTime;
            public string endTime;
        }

        private sTempInfo tmpInfo;

        public DataProcess(ObserverProcess m)
        {
            main = m;
            log = new LogMessageCallback(main.setMsg);
            sendInfo = new SndDataInfo();
            tmpInfo = new sTempInfo();

            clear();
        }

        public void clear()
        {
            sendInfo.lstInfo.Clear();
            sendInfo.staFileName = null;
            sendInfo.iniFileName = null;
            sendInfo.fileCount = 0;
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
            clear();

            string year = DateTime.Today.ToString("yyyy");
            string mon = DateTime.Today.ToString("MM");
            string dd = DateTime.Today.ToString("dd");
            string dataPath = Path.Combine(path, year, mon);
            bool firstRead = false;

            dataPath = Path.Combine(dataPath, m_data1, m_data2);   // "EOLID", "DATA");

            // 디렉토리 내에 파일이 존재하는지 체크한다.
            if (Directory.Exists(dataPath) == false)        // 현재달의 관측데이터가 있는지 체크한다.
            {
                Console.WriteLine("Directory not exist.... : {0}", path);
                log("Directory not exist.... : " + path);
                return false;
            }

            // tmp file check
            String tmpFileName = year + "_" + mon + "_" + dd + "_sendTmp.dat";
            String tmpFile = Path.Combine(path, tmpFileName);   // 상위폴더에 존재   dataPath, tmpFileName);

            if (File.Exists(tmpFile))
            {
                using (StreamReader sr = File.OpenText(tmpFile))
                {
                    string readData = sr.ReadLine();
                    // readIndex|fileFullName|fileName|from|to
                    string[] arr = readData.Split(arrSeparator);
                    tmpInfo.readIndex = Convert.ToInt32(arr[0]);
                    tmpInfo.fileName = arr[1];
                    tmpInfo.fullFileName = arr[2];
                    tmpInfo.startTime = arr[3];
                    tmpInfo.endTime = arr[4];
                }
            }
            else
            {
                tmpInfo.readIndex = -1;  // tmp 파일이 없다.
                firstRead = true;
            }

            DirectoryInfo dir = new DirectoryInfo(dataPath);
            int cnt = 0;
            sendInfo.path = dataPath;
            string fileEndDt = "";
            string fileStDt = "";
            // STA 파일만 검색
            foreach (FileInfo fi in dir.GetFiles("*.sta").OrderBy(fi => fi.CreationTime))      // 날짜순 정렬
            {
                string file = fi.FullName;
                string ext = Path.GetExtension(file);

                if (ext == ".sta")
                {
                    Console.WriteLine("[HasWritePermissionOnDir] " + file);
                    if (FileLocked(file) == false)     // if File not lock
                    {
                        // sta 파일에서 60분간의 데이터를 읽어서 파일을 생성해야 한다.
                        using (StreamReader sr = new StreamReader(file))
                        {
                            string line;
                            string head = "";
                            string body = "";
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
                                    // body read
                                    if (firstRead == true)        // first read
                                    {
                                        body += line;
                                        if (readCount == 0)
                                        {
                                            fileStDt = line.Substring(0, 19);
                                        }
                                        readCount++;
                                        tmpInfo.readIndex++;
                                        fileEndDt = line.Substring(0, 19);  // 2017-05-18 20:00:00 (start date)
                                        if (readCount == 6)
                                            break;
                                    }
                                    else
                                    {
                                        // index 다음을 읽는다.
                                        if (idx == (tmpInfo.readIndex))
                                        {
                                            body += line;
                                            if (readCount == 0)
                                            {
                                                fileStDt = line.Substring(0, 19);
                                            }
                                            readCount++;
                                            tmpInfo.readIndex++;
                                            fileEndDt = line.Substring(0, 19);  // 2017-05-18 20:00:00 (start date)
                                            if (readCount == 6)
                                                break;
                                        }
                                    }
                                }
                                idx++;
                            }  // while loop end

                            if (body == "")
                            {
                                // 파일의 끝
                                tmpInfo.readIndex = 0;
                                // sta file move
                                FileMoveProcess(file);
                                continue;       // next sta
                            }
                            // head, body
                            // sta file create

                            // sta file 명 구하기
                            string fileDt = fileEndDt;   //  body.Substring(0, 19);  // 2017-05-18 20:00:00 (start date)
                            string dd1 = fileDt.Substring(8, 2);
                            string hh1 = fileDt.Substring(11, 2);
                            string mi1 = fileDt.Substring(14, 2);
                            string staSaveName = dd1 + "_" + hh1 + "_" + mi1 + ".snd";
                            String saveName = Path.Combine(dataPath, staSaveName);
                            // sta file create
                            using (StreamWriter sw = File.CreateText(saveName))
                            {
                                sw.WriteLine(head);
                                sw.WriteLine(body);
                            }
                            tmpInfo.fileName = staSaveName;
                            tmpInfo.fullFileName = saveName;
                            // start 시간을 구한다. start 시간은 마지막 시간에서 10분전 데이터
                            if (firstRead == true)        // first
                            {
                                tmpInfo.endTime = fileDt;
                                //DateTime endDt = DateTime.ParseExact(fileDt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                                //DateTime stDt = endDt.AddMinutes(-10);
                                tmpInfo.startTime = fileStDt;  //  stDt.ToString("yyyy-MM-dd HH:mm:ss");
                            }
                            else
                            {
                                tmpInfo.startTime = fileStDt;   //  tmpInfo.endTime;
                                tmpInfo.endTime = fileDt;
                            }
                            //tmpInfo.readIndex = idx;

                            cnt = 1;
                        }
                    }

                    break;   // sta 파일 하나 읽으면 루프 종료.
                }
            }
            if (cnt == 0)
            {
                log("Upload data does not exists");
                return false;
            }

            // snd(std), raw, ini, rtd 파일 전송
            // raw, ini, rtd 파일은 endTime 이하에 속하는 파일을 전송한다. 
            //DateTime sDt = DateTime.ParseExact(tmpInfo.startTime, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            DateTime eDt = DateTime.ParseExact(tmpInfo.endTime, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            SndDataInfo.sFileInfo sf = new SndDataInfo.sFileInfo();
            sf.fileName = tmpInfo.fileName;
            sf.fullFileName = tmpInfo.fullFileName;
            sf.startTime = tmpInfo.startTime;
            sf.endTime = tmpInfo.endTime;
            sendInfo.fileCount++;
            sendInfo.lstInfo.Add(sf);
            m_chkDate = eDt;

            return true;
        }

        /**
         * FTP Server에 데이터를 업로드 할 수 있는지 체크한다.
         * 라이다에서 데이터를 쓰고 있으면 FTP Server에 데이터를 전송하면 안된다.
         */
        public bool HasWritePermissionOnDir(string path)
        {
            clear();

            string year = DateTime.Today.ToString("yyyy");
            string mon = DateTime.Today.ToString("MM");
            string dd = DateTime.Today.ToString("dd");
            string dataPath = Path.Combine(path, year, mon);

            dataPath = Path.Combine(dataPath, m_data1, m_data2);   //"EOLID", "DATA");

            Console.WriteLine("dataPath : {0}", dataPath);
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
            SndDataInfo.sFileInfo sf = new SndDataInfo.sFileInfo();

           // sf.startTime = tmpInfo.startTime;
           // sf.endTime = tmpInfo.endTime;

            foreach (FileInfo fi in dir.GetFiles().OrderBy(fi => fi.CreationTime))      // 날짜순 정렬
            {
                string file = fi.FullName;
                string ext = Path.GetExtension(file);

                Console.WriteLine("ext : {0}", ext);
                if (ext == ".raw")
                {
                    DateTime sd = convertTimeExtract(fi.Name);
                    sf.startTime = sd.ToString("yyyy-MM-dd HH:mm:ss");
                    sf.endTime = sf.startTime;
                    m_chkDate = sd;
                }

                if (ext == ".raw" || ext == ".ini" || ext == ".rtd")
                {
                    DateTime rtdDt = convertTimeExtract(fi.Name);
                    
                        sendInfo.fileCount++;

                        if (ext == ".raw")
                        {
                            sf.rawFullName = fi.FullName;
                            sf.rawFile = fi.Name;
                        }
                        else if(ext == ".ini")
                        {
                            sf.iniFullName = fi.FullName;
                            sf.iniFile = fi.Name;
                        }
                        else if(ext == ".rtd")
                        {
                            sf.rtdFullName = fi.FullName;
                            sf.rtdFile = fi.Name;
                        }

                        if (sendInfo.fileCount == 3) break;
                }
            }
            sendInfo.lstInfo.Add(sf);

            return true;
        }

        /**
         * yyyy_mm_dd_sendTmp.dat 파일을 업데이트 한다.
         * read index | filename | full file name | start time | end time
         */
        public void tmpSave(string path)
        {
            string year = DateTime.Today.ToString("yyyy");
            string mon = DateTime.Today.ToString("MM");
            string dd = DateTime.Today.ToString("dd");
            string dataPath = Path.Combine(path, year, mon);
            dataPath = Path.Combine(dataPath, m_data1, m_data2);   // "EOLID", "DATA");

            String tmpFileName = year + "_" + mon + "_" + dd + "_sendTmp.dat";
            String tmpFile = Path.Combine(dataPath, tmpFileName);

            string data = tmpInfo.readIndex + "|" + tmpInfo.fileName + "|" + tmpInfo.fullFileName + "|" + tmpInfo.startTime + "|" + tmpInfo.endTime;
            using (StreamWriter sw = File.CreateText(tmpFile))
            {
                sw.WriteLine(data);
            }
        }

        /**
          * STA 관측 데이터 상태 시작 정보를 전송한다.
          */
        public bool startStaStatusSendData()
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


                // AT : 관측소 ID : IP ADDR : 시작시각 : 종료시각 : 파일개수 : sta파일명 : S
                string msg = "AT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + sendInfo.fileCount + ":" + info.fileName + ":S" ;
                byte[] buf = Encoding.ASCII.GetBytes(msg);
                int stPort = System.Convert.ToInt32(m_stPort);          // 10001

                using (UdpClient c = new UdpClient(m_at_rcv_port))      // 10004 : AT_RCV_PORT
                {
                    c.Send(buf, buf.Length, m_stHost, stPort);
                    //log.Log("File status data send (startStatusSendData :" + m_stHost + "[" + stPort + "]) " + msg);
                    Console.WriteLine("File data(STA) send msg : " + msg);
                    main.setMsg("File data(STA) send start (startStaStatusSendData :" + m_stHost + "[" + stPort + "])" + msg);

                    c.Client.ReceiveTimeout = 2000;     // 2 second
                    IPEndPoint ipepLocal = new IPEndPoint(IPAddress.Any, m_ft_rcv_port);     // 10001 + 2
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
                        main.setMsg("File data(STA) receive msg(startStaStatusSendData) : " + data);

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


                string msg = "FT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + sendInfo.fileCount + ":" 
                    + info.iniFile + ":" + info.rawFile + ":" + info.rtdFile + ":" +type+ ":"+p1+":"+p2+":"+p3+":"+p4+":"+p5+":S";
                byte[] buf = Encoding.ASCII.GetBytes(msg);
                int stPort = System.Convert.ToInt32(m_stPort);          // 10001

                using (UdpClient c = new UdpClient(m_ft_rcv_port))       // 10003
                {
                    c.Send(buf, buf.Length, m_stHost, stPort);
                    //log.Log("File status data send (startStatusSendData :" + m_stHost + "[" + stPort + "]) " + msg);
                    Console.WriteLine("File data send msg : " + msg);
                    main.setMsg("File data send start (startStatusSendData :" + m_stHost + "[" + stPort + "])" + msg);

                    c.Client.ReceiveTimeout = 2000;     // 2 second
                    IPEndPoint ipepLocal = new IPEndPoint(IPAddress.Any, m_ft_rcv_port);     // 10001 + 2
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


                string msg = "FT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + sendInfo.fileCount + ":" + info.fileName + ":E";
                byte[] buf = Encoding.ASCII.GetBytes(msg);
                int stPort = System.Convert.ToInt32(m_stPort);           // 10001

                using (UdpClient c = new UdpClient(m_ft_rcv_port))       // 10003
                {
                    c.Send(buf, buf.Length, m_stHost, stPort);
                    //log.Log("File status data send (startStatusSendData :" + m_stHost + "[" + stPort + "]) " + msg);
                    Console.WriteLine("File data send msg : " + msg);
                    main.setMsg("File data send end (startStatusSendData :" + m_stHost + "[" + stPort + "])" + msg);

                    c.Client.ReceiveTimeout = 2000;     // 2 second
                    IPEndPoint ipepLocal = new IPEndPoint(IPAddress.Any, m_ft_rcv_port);     // 10001 + 2
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
                        main.setMsg("File data receive msg(endStatusSendData) : " + data);

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
                // log.Log("Alarm data send error(endStatusSendData) : " + ex.ToString());
                main.setMsg("File data send error(endStatusSendData) : " + ex.ToString());
            }


            return result;
        }
        /**
         * STA 파일에 대해서 FTP에 전송한다.  
         */
        public bool StaftpSend(string ftp_uri, string host, string port, string user, string pass)
        {
            var enabled = true;

            log("[ FtpSend ] function called....");
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
                FileInfo file = new FileInfo(fileName);
                file.MoveTo(destFileName);

                result = true;
            }
            catch (IOException ex)
            {
                log(ex.ToString());
                Console.WriteLine(ex.ToString());
                result = false;
            }
            return result;
        }
        /**
         * STA 파일에 대해서 백업한다.
         */
        public bool FileStaMoveProcess()
        {
            var result = false;
            try
            {
                SndDataInfo.sFileInfo info = sendInfo.lstInfo[0];

                // sta 파일 이동
                string destFileName = info.fullFileName.Replace("DATA", "BACKUP");
                FileInfo staFile = new FileInfo(info.fullFileName);
                staFile.MoveTo(destFileName);
                result = true;
            }
            catch (IOException ex)
            {
                log(ex.ToString());
                Console.WriteLine(ex.ToString());
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


                // Ini 파일 이동            
                string destFileName = info.iniFullName.Replace("DATA", "BACKUP");
                FileInfo iniFile = new FileInfo(info.iniFullName);
                iniFile.MoveTo(destFileName);

                // rtd 파일 이동
                destFileName = info.rtdFullName.Replace("DATA", "BACKUP");
                FileInfo rtdFile = new FileInfo(info.rtdFullName);
                rtdFile.MoveTo(destFileName);

                // raw 파일 이동
                destFileName = info.rawFullName.Replace("DATA", "BACKUP");
                FileInfo rawFile = new FileInfo(info.rawFullName);
                rawFile.MoveTo(destFileName);

                // sta 파일 이동
                destFileName = info.fullFileName.Replace("DATA", "BACKUP");
                FileInfo staFile = new FileInfo(info.fullFileName);
                staFile.MoveTo(destFileName);
                result = true;
            }
            catch (IOException ex)
            {
                log(ex.ToString());
                Console.WriteLine(ex.ToString());
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
                Console.WriteLine(FileName);

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

        DateTime convertTimeExtract(string data)
        {
            // 10_09_00_58_356_0.rtd
            string year = DateTime.Today.ToString("yyyy");
            string mon = DateTime.Today.ToString("MM");
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
