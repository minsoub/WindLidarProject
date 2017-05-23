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
using MySql.Data.MySqlClient;

namespace WindLidarSystem
{
    public class ProcessReceiver
    {
        private WindLidarServer main;

        private Thread processThread;
        private Thread stsThread;
        private Thread staThread;

        private ManualResetEvent waitHandle;
        private ManualResetEvent stsHandle;
        private ManualResetEvent staHandle;
        
        private StatusProcess stsProcess;
        private FileProcess ftsProcess;
        private AlarmProcess almProcess;
        private bool isShutdown;

        const string FTP_URI = "ftp://";

        private delegate void LogMessageCallback(String msg);
        LogMessageCallback log;

        private delegate void StsMessageCallback(String msg);
        StsMessageCallback stsMessage;

        private delegate void FtsMessageCallback(String msg);
        FtsMessageCallback ftsMessage;

        private delegate void StsMessageDBCallback(string msg);
        StsMessageDBCallback stsDB;


        private UdpClient server;
        private int ListenPort;
        private char[] delimiterChar = { ':' };

        public ProcessReceiver(object obj)
        {
            main = (WindLidarServer)obj;
            isShutdown = true;

            waitHandle = new ManualResetEvent(false);
            stsHandle = new ManualResetEvent(false);
            staHandle = new ManualResetEvent(false);

            log = new LogMessageCallback(main.logMessage);
            stsMessage = new StsMessageCallback(main.stsMessage);
            ftsMessage = new FtsMessageCallback(main.ftsMessage);
            stsDB = new StsMessageDBCallback(main.stsDB);
            stsProcess = new StatusProcess(this);
            ftsProcess = new FileProcess(this);
            almProcess = new AlarmProcess(this);
            server = null;
            
        }
        public void setLogMessage(string msg)
        {
            log(msg);
        }
 
        /**
         * Start 버튼을 클릭했을 때 호출되는 메소드로 데이터베이스에 연결하고
         * 쓰레드를 생성해서 가동시킨다.
         */
        public void start()
        {
            isShutdown = false;

            if (server != null) server.Close();

            ftsProcess.setDir(ParamInitInfo.Instance.m_sourceDir, ParamInitInfo.Instance.m_backupDir);

            ListenPort = System.Convert.ToInt32(ParamInitInfo.Instance.m_listenPort);
            server = new UdpClient(ListenPort);
            // data 처리
            processThread = new Thread(new ThreadStart(WindLidarDataProcess));
            processThread.IsBackground = true;
            processThread.Start();

            stsThread = new Thread(new ThreadStart(StatusThreadProcess));
            stsThread.Start();

            staThread = new Thread(new ThreadStart(StaThreadProcess));
            staThread.Start();

            // socket async
            server.BeginReceive(ReceiverClient, null);
        }

        public void abort()
        {
            isShutdown = true;
            Thread.Sleep(1000);
            if (processThread != null) processThread.Abort();
            if (stsThread != null) stsThread.Abort();
            if (staThread != null) staThread.Abort();
            // socket
            if (server != null)
            {
                server.Close();
            }
            server = null;
            processThread = null;
            stsThread = null;
            stsProcess = null;
            ftsProcess = null;
            almProcess = null;
        }

        public void StaThreadProcess()
        {
            while (!isShutdown)
            {
                if (isShutdown == false)
                {
                    staHandle.Reset();
                    // 데이터베이스에 접속해서 데이터를 가져온다.
                    try
                    {
                        //if (oCon != null)
                        //{
                        // 하나의 Row 가져오기
                        StsInfo fileData = ftsProcess.getStaRcvDataInfo();
                        if (fileData != null)
                        {
                            // FTP 전송 - need module
                            ftsProcess.setFtpInfo(fileData.s_code, FTP_URI, ParamInitInfo.Instance.m_ftpIP, ParamInitInfo.Instance.m_ftpPort,
                                ParamInitInfo.Instance.m_ftpUser, ParamInitInfo.Instance.m_ftpPass);
                            bool sts = ftsProcess.ftpSendData(fileData);

                            if (sts == false)
                            {
                                Console.WriteLine("ftsProcess ftpSendData false...........[" + fileData.s_code + "]");
                                ftsProcess.ftpFailUpdate(fileData);
                            }
                        }
                        else
                        {
                            Console.WriteLine("The transfer data is not found .......");
                        }
                        //}
                    }
                    catch (MySqlException e)
                    {
                        log("[ ProcessReceiver::windDataProcess(error) ] Error : " + e.Message);
                    }
                    staHandle.WaitOne(1000 * 60 * 10);   // 10 minute .. hour  .. System.Convert.ToInt16(ParamInitInfo.Instance.m_ftpThreadTime));  // 1 minute ( need setup)
                }
            }
        }
        /**
         * 데이터베이스에 주기적으로 접속해서 데이터를 가져와서
         * FDP전송하고 데이터베이스에 다시 업데이트를 수행한다.
         * 쓰레드 함수
         */
        public void WindLidarDataProcess()
        {
            while(!isShutdown)
            {
                if (isShutdown == false)
                {
                    waitHandle.Reset();
                    // 데이터베이스에 접속해서 데이터를 가져온다.
                    try
                    {
                        //if (oCon != null)
                        //{
                            // 하나의 Row 가져오기
                            StsInfo fileData = ftsProcess.getRcvDataInfo();
                            if (fileData != null)
                            {
                                // FTP 전송 - need module
                                ftsProcess.setFtpInfo(fileData.s_code, FTP_URI, ParamInitInfo.Instance.m_ftpIP, ParamInitInfo.Instance.m_ftpPort,
                                    ParamInitInfo.Instance.m_ftpUser, ParamInitInfo.Instance.m_ftpPass); 
                                bool sts = ftsProcess.ftpStaSendData(fileData);

                                if (sts == false)
                                {
                                    Console.WriteLine("ftsProcess ftpSendData false...........["+fileData.s_code+"]");
                                    ftsProcess.ftpFailUpdate(fileData);
                                }
                            }else
                            {
                                Console.WriteLine("The transfer data is not found .......");
                            }
                        //}
                    }
                    catch (MySqlException e)
                    {
                        log("[ ProcessReceiver::windDataProcess(error) ] Error : " + e.Message);
                    }
                   waitHandle.WaitOne( 1000 * System.Convert.ToInt16(ParamInitInfo.Instance.m_ftpThreadTime) );  // 1 minute ( need setup)
                }
            }
        }

        /**
         * 데이터베이스 주기적으로 클라이언트 프로그램 상태 정보를 조회해서
         * 상태 등록 시간을 체크해서 상태 정보가 주기적으로 전송되는지 체크한다.
         * 주기적으로 상태정보가 전송되지 않으면 상태를 0으로 업데이트 시켜서
         * 사용자가 웹에서 확인할 때 에러가 발생했음을 알 수 있도록 한다.
         */
        public void StatusThreadProcess()
        {
            while(!isShutdown)
            {
                if (isShutdown == false)
                {
                    waitHandle.Reset();
                    try
                    {
                        string[] stsMsg = stsProcess.getClientStatusInfo();

                        log("[ProcessReceiver::StatusThreadProcess(info)] : 데이터베이스에 의해서 상태 정보가 조회 되었습니다!!!");
                        var str = "";
                        foreach (string msg in stsMsg)
                        {
                            str += msg;
                            stsDB(msg);
                        }
                        log("-search value-" + str);
                    }
                    catch (MySqlException e)
                    {
                        log("[ ProcessReceiver::StatusThreadProcess(error) ] Error : " + e.Message);
                    }
                    waitHandle.WaitOne(1000 * System.Convert.ToInt16(ParamInitInfo.Instance.m_stsThreadTime));  // 3 minute
                }
            }
        }

        /**
         * 클라이언트로 부터 UDP 메시지를 받는 함수
         * 메시지 구분에 따라서 데이터를 처리한다.
         */
        private void ReceiverClient(IAsyncResult res)
        {
           // if (res == currentAsyncResult)
           // {
            if (server != null)
            {
                try
                {
                    IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, ListenPort);
                    byte[] received = server.EndReceive(res, ref RemoteIpEndPoint);
                    string msg = Encoding.UTF8.GetString(received);
                    string[] msgArr = msg.Split(delimiterChar);

                    if (msgArr[0] == "ST")      // Process Status check
                    {
                        // 데이터베이스에 상태 등록
                        bool bl = stsProcess.stsUpdate(msgArr);
                        if (bl == false)
                        {
                            log("[ ProcessReceiver::ReceiverClient(info) ] database update error ... [" + msg + "]");
                        }
                        // 수신 받은 프로세스 상태 체크 값 배열을 구조체에 업데이트 하고
                        // 메인 화면에 출력한다.
                        stsMessage(msg);
                    }
                    else if (msgArr[0] == "FT")   // FTP file start/end send check
                    {
                        string client_ip = RemoteIpEndPoint.Address.ToString();
                        bool bl = ftsProcess.fileStsUpdate(msg, client_ip);
                        if (bl == true)
                        {
                            log("[ ProcessReceiver::ReceiverClient(info) ] file data status update [ok]");
                            ftsMessage(msg);
                        }

                        //ftpMessage(msg);
                    }
                    else if (msgArr[0] == "AT")   // FILE file start/end send check(STA)
                    {
                        string client_ip = RemoteIpEndPoint.Address.ToString();
                        bool bl = ftsProcess.fileStaUpdate(msg, client_ip);
                        if (bl == true)
                        {
                            log("[ ProcessReceiver::ReceiverClient(info) ] file data status update [ok] => STA");
                            ftsMessage(msg);
                        }
                    }
                    else if (msgArr[0] == "AM")   // Alarm message
                    {

                        string client_ip = RemoteIpEndPoint.Address.ToString();

                        log("client ip : " + client_ip);
                        bool bl = almProcess.almMessage(msg, client_ip);

                    }
                    //Process codes
                    log("[ ProcessReceiver::ReceiverClient(info) ] received msg : " + Encoding.UTF8.GetString(received));
                    //server.BeginReceive(new AsyncCallback(ReceiverClient), null);
                    server.BeginReceive(ReceiverClient, null);
                    // }
                }catch(Exception ex)
                {
                    log("[ ProcessReceiver::ReceiverClient(error) ] " + ex.ToString());
                    server.BeginReceive(ReceiverClient, null);
                }
            }
        }
    }
}
