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
        private string m_stCode;
        private string m_stHost;
        private string m_stPort;
        private int m_st_rcv_port;
        private int m_ft_rcv_port;
        private int m_ltPort;
        private char[] delimiterChar = { ':' };
        private delegate void LogMessageCallback(String msg);
        private DateTime m_chkDate;
        LogMessageCallback log;

        public DataProcess(ObserverProcess m)
        {
            main = m;
            log = new LogMessageCallback(main.setMsg);
            sendInfo = new SndDataInfo();
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
        public void setNetworkInfo(string stCode, string stHost, string stPort, int ltPort, int st_rcv_port, int ft_rcv_port)
        {
            m_stCode = stCode;
            m_stHost = stHost;
            m_stPort = stPort;
            m_ltPort = ltPort;
            m_st_rcv_port = st_rcv_port;
            m_ft_rcv_port = ft_rcv_port;
        }
        public void setPath(string sPath, string bPath)
        {
            m_sourcePath = sPath;
            m_backupPath = bPath;
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
            string dataPath = Path.Combine(path, year, mon);
            dataPath = Path.Combine(dataPath, "EOLID", "DATA");

            

            // 디렉토리 내에 파일이 존재하는지 체크한다.
            if (Directory.Exists(dataPath) == false)
            {
                Console.WriteLine("Directory not exist.... : {0}", path);
                log("Directory not exist.... : " + path);
                return false;
            }

            DirectoryInfo dir = new DirectoryInfo(dataPath);
            int cnt = 0;
            sendInfo.path = dataPath;
            foreach (FileInfo fi in dir.GetFiles().OrderBy(fi => fi.CreationTime))      // 날짜순 정렬
            {
                string file = fi.FullName;
                string ext = Path.GetExtension(file);
                

                if (ext == ".ini" || ext == ".sta")
                {
                    Console.WriteLine("[HasWritePermissionOnDir]" + file);
                    if (FileLocked(file) == false)       // File lock이 아니면
                    {
                        // INI 파일 체크
                        if (ext == ".ini")
                        {
                            sendInfo.iniFileName = fi.Name;
                            sendInfo.iniFullFileName = file;
                            sendInfo.fileCount++;
                            if (sendInfo.fileCount == 2) break;
                        }
                        // STS 파일 체크
                        if (ext == ".sta")
                        {
                            sendInfo.staFileName = fi.Name;
                            sendInfo.staFullFileName = file;
                            sendInfo.fileCount++;
                            cnt++;
                            if (sendInfo.fileCount == 2) break;
                        }                        
                    }
                }
            }            
            if (cnt == 0)
            {
                log("Upload data does not exists");
                return false;
            }

            string staName = Path.GetFileNameWithoutExtension(sendInfo.staFileName);  // 10_08_55_00_-_10_09_00_58
            DateTime[] arrDt = fromDateTimeExtract(staName);
            m_chkDate = arrDt[0];

            // rtd 파일 읽기
            foreach (FileInfo fi in dir.GetFiles().OrderBy(fi => fi.CreationTime))      // 날짜순 정렬
            {
                string file = fi.FullName;
                string ext = Path.GetExtension(file);
                if (ext == ".rtd")
                {
                    DateTime rtdDt = convertTimeExtract(fi.Name);

                    // to ~ from에 속하는 데이터인지 확인한다.
                    double s1 = (arrDt[0] - rtdDt).TotalSeconds;
                    double s2 = (arrDt[1] - rtdDt).TotalSeconds;

                    //Console.WriteLine(arrDt[0] + ", " + arrDt[1]);
                    //Console.WriteLine("staName : " + staName + ", rtd : " + file + ", s1 : " + s1 + ", s2 : " + s2);

                    if (s1 <= 0 && s2 >= 0)         // 해당 시간내에 속한 파일이다.
                    {
                        sendInfo.fileCount++;
                        SndDataInfo.sFileInfo sf = new SndDataInfo.sFileInfo();
                        sf.fileName = fi.Name;
                        sf.fullFileName = file;
                        sendInfo.lstInfo.Add(sf);                       
                    }
                }
            }

            return true;
        }

        /**
         * 관측 데이터 상태 시작 정보를 전송한다.
         */
        public bool startStatusSendData()
        {
            bool result = false;

            try
            {
                string stDt = null;
                string etDt = null;

                string year = DateTime.Today.ToString("yyyy");
                string mon = DateTime.Today.ToString("MM");

                string d1, d2, h1, h2, m1, m2, s1, s2;
                // 10_08_55_00_-_10_09_00_58.sta
                d1 = sendInfo.staFileName.Substring(0, 2);
                h1 = sendInfo.staFileName.Substring(3, 2);
                m1 = sendInfo.staFileName.Substring(6, 2);
                s1 = sendInfo.staFileName.Substring(9, 2);
                d2 = sendInfo.staFileName.Substring(14, 2);
                h2 = sendInfo.staFileName.Substring(17, 2);
                m2 = sendInfo.staFileName.Substring(20, 2);
                s2 = sendInfo.staFileName.Substring(23, 2);

                stDt = year + "_" + mon + "_" + d1 + "_" + h1 + "_" + m1 + "_" + s1;
                etDt = year + "_" + mon + "_" + d2 + "_" + h2 + "_" + m2 + "_" + s2;

                // Ini파일에서 type을 읽어 들인다.
                string iniFilePath = Path.Combine(sendInfo.path, sendInfo.iniFileName);
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
                sendInfo.m_year = year;
                sendInfo.m_mon = mon;
                sendInfo.m_day = d1;


                string msg = "FT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + sendInfo.fileCount + ":" + sendInfo.staFileName + ":"+type+ ":"+p1+":"+p2+":"+p3+":"+p4+":"+p5+":S";
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
                string stDt = null;
                string etDt = null;

                string year = DateTime.Today.ToString("yyyy");
                string mon = DateTime.Today.ToString("MM");

                string d1, d2, h1, h2, m1, m2, s1, s2;
                d1 = sendInfo.staFileName.Substring(0, 2);
                h1 = sendInfo.staFileName.Substring(3, 2);
                m1 = sendInfo.staFileName.Substring(6, 2);
                s1 = sendInfo.staFileName.Substring(9, 2);
                d2 = sendInfo.staFileName.Substring(14, 2);
                h2 = sendInfo.staFileName.Substring(17, 2);
                m2 = sendInfo.staFileName.Substring(20, 2);
                s2 = sendInfo.staFileName.Substring(23, 2);

                stDt = year + "_" + mon + "_" + d1 + "_" + h1 + "_" + m1 + "_" + s1;
                etDt = year + "_" + mon + "_" + d2 + "_" + h2 + "_" + m2 + "_" + s2;


                string msg = "FT:" + m_stCode + ":" + m_stHost + ":" + stDt + ":" + etDt + ":" + sendInfo.fileCount + ":" + sendInfo.staFileName + ":E";
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
         * 파일이 존재하면 이동할 때 삭제를 하고 이동할 것인지를 결정해야 한다.
         */
        public bool FileMoveProcess()
        {
            var result = false;
            try
            {
                // Ini 파일 이동            
                string destFileName = sendInfo.iniFullFileName.Replace("DATA", "BACKUP");
                FileInfo iniFile = new FileInfo(sendInfo.iniFullFileName);
                iniFile.MoveTo(destFileName);

                // rtd 파일 이동
                foreach (SndDataInfo.sFileInfo info in sendInfo.lstInfo)
                {
                    destFileName = info.fullFileName.Replace("DATA", "BACKUP");
                    FileInfo rtdFile = new FileInfo(info.fullFileName);
                    rtdFile.MoveTo(destFileName);
                }
                // sta 파일 이동
                destFileName = sendInfo.staFullFileName.Replace("DATA", "BACKUP");
                FileInfo staFile = new FileInfo(sendInfo.staFullFileName);
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

            Console.WriteLine("toDt : " + toDt);
            Console.WriteLine("fromDt : " + fromDt);

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
            //Console.WriteLine(dt);


            return DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

    }
}
