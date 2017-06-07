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
using System.Net.Sockets;

namespace WindLidarClient
{
    public class ObserverProcess
    {
        private WindClientForm main;
        private Thread stsThread;
        private Thread staThread;   // STA 파일 처리 Thread
        private Thread almThread;
        private Thread fileThread;
        private LogCls fsLog;

        private ManualResetEvent waitHandle;
        private ManualResetEvent staHandle;
        private ManualResetEvent almHandle;
        private bool isShutdown;
        private AlarmProcess alarmProcess;

        const int ERROR_SHARING_VIOLATION = 32;
        const int ERROR_LOCK_VIOLATION = 33;
        const string FTP_URI = "ftp://";

        private string m_id;
        private string m_pass;
        private string m_host;
        private string m_port;
        private string m_stHost;
        private string m_stPort;
        private string m_stCode;
        protected IniFile myIniFile;

        // 메인에서 상태 로그를 출력하도록 delegate를 선언한다.
        private delegate void LogMessageCallback(String msg);
        LogMessageCallback log;

        // 메인에서 프로그래스바의 상태를 변경하도록 delegate를 선언한다.
        private delegate void StartPointCallback(int stPoint);
        private delegate void EndPointCallback(int etPoint);
        private delegate void ProgressIngCallback(int data);

        StartPointCallback startProgress;
        EndPointCallback   endProgress;
        ProgressIngCallback ingProgress;

        

        protected string m_sourcePath;
        protected string m_backupPath;
        protected int m_cstLocalPort;
        protected int m_sts_sleep_time;
        protected int m_file_sleep_time;
        protected int m_sta_sleep_time;
        protected int m_alm_sleep_time;
        protected int m_st_rcv_port;        
        protected int m_ft_rcv_port;
        protected int m_at_rcv_port;
        protected string m_data1;
        protected string m_data2;

        

        public ObserverProcess(object cls)
        {
            main = (WindClientForm) cls;
            isShutdown = true;

            myIniFile = new IniFile(@"D:\WindLidarClient.ini");
            m_sourcePath = myIniFile.Read("SOURCE_PATH");
            m_backupPath = myIniFile.Read("BACKUP_PATH");
            m_sts_sleep_time = System.Convert.ToInt32(myIniFile.Read("STS_SLEEP_TIME"));
            m_file_sleep_time = System.Convert.ToInt32(myIniFile.Read("FILE_SLEEP_TIME"));
            m_sta_sleep_time = System.Convert.ToInt32(myIniFile.Read("STA_SLEEP_TIME"));
            m_alm_sleep_time = System.Convert.ToInt32(myIniFile.Read("ALM_SLEEP_TIME"));
            m_data1 = myIniFile.Read("DATA1");
            m_data2 = myIniFile.Read("DATA2");

            waitHandle = new ManualResetEvent(false);
            almHandle = new ManualResetEvent(false);
            staHandle = new ManualResetEvent(false);

            log = new LogMessageCallback(main.logMessage);
            

            startProgress = new StartPointCallback(main.StartPointProgress);
            endProgress = new EndPointCallback(main.EndPointProgress);
            ingProgress = new ProgressIngCallback(main.IngProgress);

            alarmProcess = new AlarmProcess(this);
            fsLog = new LogCls();

        }

        public void setMsg(string msg)
        {
            log(msg);
        }

        public void setSndLocalPort(string sndLocalPort, int  st_rcv_port, int ft_rcv_port, int at_rcv_port)
        {
            m_cstLocalPort = System.Convert.ToInt32(sndLocalPort);
            m_st_rcv_port = st_rcv_port;
            m_ft_rcv_port = ft_rcv_port;
            m_at_rcv_port = at_rcv_port;
        }

        public void setID(string id)
        {
            m_id = id;
        }
        public void setPass(string pass)
        {
            m_pass = pass;
        }
        public void setHost(string host)
        {
            m_host = host;
        }
        public void setPort(string port)
        {
            m_port = port;
        }
        public void setStHost(string stHost)
        {
            m_stHost = stHost;
        }
        public void setStPort(string stPort)
        {
            m_stPort = stPort;
        }
        public void setStCode(string stCode)
        {
            m_stCode = stCode;
        }



        public void start()
        {
            isShutdown = false;

            // 상태 데이터 전송
            stsThread = new Thread(new ThreadStart(StatusSender));
            stsThread.Start();  

            // 관측데이터 전송 : RAW, INI, RTD
            fileThread = new Thread(new ThreadStart(fileCheckProcess));
            fileThread.Start();

            // 관측데이터 전송 : STA (1시간마다)
            staThread = new Thread(new ThreadStart(StaCheckProcess));
            staThread.Start();

            // 알람데이터 전송
            almThread = new Thread(new ThreadStart(AlarmCheckProcess));            
            almThread.Start();

        }

        public void abort()
        {
            isShutdown = true;
            if (stsThread != null) stsThread.Abort();

            if (almThread != null) almThread.Abort();

            if (fileThread != null) fileThread.Abort();

            if (staThread != null) staThread.Abort();
            // socket
        }

        /**
         * 클라이언트 프로그램의 상태를 서버에 전송한다.
         */
        public void StatusSender()
        {
            // 자신의 상태 정보를 서버에 전송한다. 
            // 주기적으로 전송
            while(!isShutdown)
            {
                if (isShutdown == false)
                {
                    waitHandle.Reset();

                    string msg = "ST:" + m_stCode + ":1";
                    byte[] buf = Encoding.ASCII.GetBytes(msg);
                    int stPort = System.Convert.ToInt32(m_stPort);
                    try
                    {
                        using (UdpClient c = new UdpClient(m_cstLocalPort))  // source port
                        {
                            c.Send(buf, buf.Length, m_stHost, stPort);
                            log("[ StatusSender ] " + msg);
                        }
                    }catch(Exception ex)
                    {
                        log("[ StatusSender error ] : " + ex.ToString());
                    }

                    waitHandle.WaitOne(1000 * m_sts_sleep_time);  // 60 second
                }
            }
        }

        /**
         * Alerm 파일이 있는지 주기적으로 체크해서 알람정보를 전송하고
         * 읽은 파일을 백업 디렉토리 이동시킨다.
         * 마지막 최신 파일내용만 서버에 전송하고 나머지는 모두 백업 디렉토리로 이동시킨다.
         * m_sourcePath : D\\KoreaLidar
         * m_backupPath : D\\KoreaLidar
         */
        public void AlarmCheckProcess()
        {
            alarmProcess.setData(m_stCode, m_stPort, m_cstLocalPort, m_stHost);

            while(!isShutdown)
            {
                if (isShutdown == false)
                {
                    almHandle.Reset();
                    try
                    {
                        alarmProcess.clear();
                        // 파일 존재 여부 체크
                        bool sts = alarmProcess.almDataRead(m_sourcePath);
                        if (sts == true)
                        {
                            sts = alarmProcess.almDataSend();
                        }
                        if (sts == true)
                        {
                            sts = alarmProcess.almDataBackup();
                        }
                    }catch(Exception ex)
                    {
                        log("[ AlarmCheckProcess error ] : " + ex.ToString());
                    }
                    almHandle.WaitOne(1000 * m_alm_sleep_time);   // 60 second
                }
            }
        }

        /**
         * STA 파일에 대해서만 체크하고 파일을 전송한다.
         * STA 파일저장 데이터베이스를 따로 관리한다.
         */
        public void StaCheckProcess()
        {
            Console.WriteLine("Thread start => StaCheckProcess called....");
            // 파일이 접근, 쓸 수 있는 권한을 체크한다.
            while (!isShutdown)
            {
                if (isShutdown == false)   // debug : true
                {
                    staHandle.Reset();
                    bool old_data = false;

                    try
                    {
                        // 파일이 존재하고 접근, 쓸 수 있는 권한이 있는지 체크한다.
                        // 작성 중일 때는 대기..
                        DataProcess dataProcess = new DataProcess(this);
                        dataProcess.clear();
                        dataProcess.setMode(0);   // STA mode
                        dataProcess.setPath(m_sourcePath, m_backupPath, m_data1, m_data2);
                        dataProcess.setNetworkInfo(m_stCode, m_stHost, m_stPort, m_cstLocalPort, m_st_rcv_port, m_ft_rcv_port, m_at_rcv_port);

                        // 전송할 파일이 있는지 체크한다.
                        bool sts = dataProcess.StaHasWritePermissionOnDir(m_sourcePath);

                        if (sts == true)
                        {
                            Console.WriteLine("[ StaCheckProcess ] Write enabled....");
                            int fntCnt = 0;
                            fntCnt = dataProcess.getSendFileCount();
                            Console.WriteLine("fileCnt : " + fntCnt);

                            startProgress(0);
                            endProgress(fntCnt);

                            // 보낼 파일 개수 상태 전송
                            if (fntCnt > 0)
                            {
                                // 상태 정보 업데이트 및 상태 정보 전송 (STA 파일에 대해서 상태를 전송한다 : START)
                                bool ok = dataProcess.startStaStatusSendData();

                                if (ok)
                                {
                                    // FTP 데이터 전송
                                    ok = dataProcess.StaftpSend(FTP_URI, m_host, m_port, m_id, m_pass);

                                    // 데이터 백업 처리
                                    if (ok == true)
                                    {
                                        log("[FileMoveProcess] called....");
                                        if (dataProcess.FileStaMoveProcess() == true)
                                        {
                                            // 전송 완료 메시지 전송 및 자료 처리 완료 수신
                                            ok = dataProcess.endStatusSendData();
                                            dataProcess.tmpSave(m_sourcePath);
                                            //double s1 = (DateTime.Today - dataProcess.getCheckDate()).TotalSeconds;
                                            double span = ((DateTime.Now).Subtract(dataProcess.getCheckDate())).TotalSeconds;

                                            Console.WriteLine("s1 : " + span + " > 60 * 60 => true[old data]...??? [" + DateTime.Now+", "+ dataProcess.getCheckDate() + "]");
                                            if (span > (60 * 60))        // 읽은 데이터가 현재보다 60분 이전 데이터이면 오래된 데이터이므로
                                            {
                                                log("old data found................");
                                                old_data = true;
                                            }
                                        }
                                        else
                                        {
                                            log("[ StaCheckProcess ] File Move fail....");
                                            fsLog.Log("[ StaCheckProcess ] File Move fail....");
                                        }
                                    }
                                    else
                                    {
                                        log("FTP 데이터 전송 에러 : 로그 파일 확인 요망");
                                        fsLog.Log("FTP 데이터 전송 에러 : 로그 파일 확인 요망");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("[ StaCheckProcess ] not found data...");
                            log("[ StaCheckProcess ] No job : not found data.......");
                        }
                    }
                    catch (Exception ex)
                    {
                        log("[ StaCheckProcess error ] : " + ex.ToString());
                    }
                    if (old_data == false)
                    {
                        staHandle.WaitOne(1000 * m_sta_sleep_time);  // 1 hour  
                    }
                    else
                    {
                        staHandle.WaitOne(1000 * 5);  // 10 second
                    }
                }
            }
        }
        /**         
         * 관측 데이터 송신 체크 쓰레드 함수
         * 전송 파일이 있는지 주기적으로 체크한다.
         * 이 쓰레드 함수는 개별적으로 돌아가는 함수이다.
         * 전송하는 데이터가 옛날 데이터라면 파이을 읽는 속도를 증가시켜
         * 이전 데이터에 대해서 서버에 빨리 전송할 수 있도록 타임아웃을 조절한다.
         * RAW, INI, RTD 파일에 대해서 적용한다. 
         */
        public void fileCheckProcess()
        {
            Console.WriteLine("fileCheckProcess called....");
            // 파일이 접근, 쓸 수 있는 권한을 체크한다.
            while(!isShutdown)
            {
                if (isShutdown == false)   // debug : true
                {
                    waitHandle.Reset();
                    bool old_data = false;

                    try
                    {
                        // 파일이 존재하고 접근, 쓸 수 있는 권한이 있는지 체크한다.
                        // 작성 중일 때는 대기..
                        DataProcess dataProcess = new DataProcess(this);
                        dataProcess.clear();
                        dataProcess.setMode(1);   // STA not mode
                        dataProcess.setPath(m_sourcePath, m_backupPath, m_data1, m_data2);
                        dataProcess.setNetworkInfo(m_stCode, m_stHost, m_stPort, m_cstLocalPort, m_st_rcv_port, m_ft_rcv_port, -1);

                        // 전송할 파일이 있는지 체크한다.
                        bool sts = dataProcess.HasWritePermissionOnDir(m_sourcePath);

                        if (sts == true)
                        {
                            log("[ fileCheckProcess ] sts == true ");
                            Console.WriteLine("[ fileCheckProcess ] Write enabled....");
                            int fntCnt = 0;
                            fntCnt = dataProcess.getSendFileCount();
                            Console.WriteLine("fileCnt : " + fntCnt);

                            startProgress(0);
                            endProgress(fntCnt);

                            // 보낼 파일 개수 상태 전송
                            if (fntCnt > 0)
                            {
                                // 상태 정보 업데이트 및 상태 정보 전송
                                bool ok = dataProcess.startStatusSendData();

                                if (ok)
                                {
                                    // FTP 데이터 전송
                                    ok = dataProcess.ftpSend(FTP_URI, m_host, m_port, m_id, m_pass);

                                    // 데이터 백업 처리
                                    if (ok == true)
                                    {
                                        log("[FileMoveProcess] called....");
                                        if (dataProcess.FileMoveProcess() == true)
                                        {
                                            // 전송 완료 메시지 전송 및 자료 처리 완료 수신
                                            ok = dataProcess.endStatusSendData();
                                            //dataProcess.tmpSave(m_sourcePath);
                                            
                                            DateTime sd = dataProcess.getCheckDate().AddMinutes(10);  // + 10 minute
                                            if (sd < DateTime.Now)
                                            {
                                                old_data = true;
                                            }

                                            //double span = ((DateTime.Now).Subtract(dataProcess.getCheckDate())).TotalSeconds;
                                            //if (span > (60 * 20))        // 읽은 데이터가 현재보다 20분 이전 데이터이면 오래된 데이터이므로
                                            //{
                                            //    old_data = true;
                                            //}
                                        }
                                        else
                                        {
                                            log("[ fileCheckProcess ] File Move fail....");
                                            fsLog.Log("[ fileCheckProcess ] File Move fail....");
                                        }
                                    }
                                    else
                                    {
                                        log("FTP 데이터 전송 에러 : 로그 파일 확인 요망");
                                        fsLog.Log("FTP 데이터 전송 에러 : 로그 파일 확인 요망");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("[ fileCheckProcess ] not found data...");
                            log("[ fileCheckProcess ] No job : not found data.......");
                        }
                    }catch(Exception ex)
                    {
                        log("[ fileCheckProcess error ] : " + ex.ToString());
                        fsLog.Log("[ fileCheckProcess error ] : " + ex.ToString());
                        old_data = true;
                    }
                    if (old_data == false)
                    {
                        waitHandle.WaitOne(1000 * m_file_sleep_time);  // 10 minute
                    }
                    else
                    {
                        waitHandle.WaitOne(1000 * 3);  // 3 second
                    }
                }
            }
        }


        protected bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;
            string fs = Path.Combine(file.DirectoryName, file.ToString());
            Console.WriteLine(fs);
            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Write, FileShare.None);
            }
            catch (IOException)
            {
                Console.WriteLine("File read error....");
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

 
 


        public string getArrangeMinute(string min)
        {
            int m = System.Convert.ToInt32(min);
            string r_min = "";
            if (m > 0 && m <= 10)
            {
                r_min = "10";
            }
            else if(m > 10 && m <= 20)
            {
                r_min = "20";
            }
            else if(m > 20 && m <= 30)
            {
                r_min = "30";
            }
            else if(m > 30 && m <= 40)
            {
                r_min = "40";
            }
            else if(m > 40 && m <= 50)
            {
                r_min = "50";
            }
            else if(m > 50 || m == 0)
            {
                r_min = "60";
            }
            return r_min;
        }

    }
}
