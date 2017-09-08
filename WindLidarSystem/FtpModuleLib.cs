using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace WindLidarSystem
{
    public class FtpModuleLib
    {
        private LogCls log;
        private string ftpUser;
        private string ftpPass;
        private string ftpHost;
        private string ftpPort;
        private string ftpUri;
        private string m_stCode;
        private SndDataInfo mData;
        public string m_sourceDir;
        public string m_backupDir;

        public FtpModuleLib()
        {
            log = new LogCls();
        }
        public void setFtpInfo(string stCode, string ftp_uri, string host, string port, string user, string pass)
        {
            m_stCode = stCode;
            ftpUri = ftp_uri;
            ftpHost = host;
            ftpPort = port;
            ftpUser = user;
            ftpPass = pass;
        }
        public void setDir(string sourceDir, string backupDir)
        {
            m_sourceDir = sourceDir;
            m_backupDir = backupDir;
        }
        public void setSendData(SndDataInfo data)
        {
           // mData.clear();
            mData = data;
        }
        /**
         * STA파일을 FDP에 전송한다.
         */
        public int sendStaDataToFtpServer()
        {

            // 데이터 날짜 체크
            string ftp_url = ftpUri + ftpHost + ":" + ftpPort + "/site" + m_stCode + "/" + mData.m_year + "/" + mData.m_mon + "/" + mData.m_day;  // + "/" + info.s_hour;
            if (FtpDirectoryExists(ftp_url) == false)
            {
                log.Log("FTP Server : Directory create error......" + ftp_url);
                return 0;
            }

            // sta 파일 전송
            // staFileName : 17_01_00_00.sta  => 13201_20170714010000.sta
            string tmpName1 = mData.staFileName.Replace("_", "");   // => 17010000.sta
            string ftpSaveFile1 = m_stCode + "_" + mData.m_year + mData.m_mon + mData.m_day + tmpName1.Substring(2);
            string ftpPath = ftp_url + "/" + ftpSaveFile1;   //  mData.staFileName;
            //ftpPath = ftp_url + "/" + mData.staFileName;
            if (sendData(ftpPath, mData.staFullFileName))
            {
                mData.sendCount++;
            }


            // sta 파일 전송
           // string ftpPath = ftp_url + "/" + mData.staFileName;
           // if (sendData(ftpPath, mData.staFullFileName))
           // {
           //     mData.sendCount++;
           // }

            log.Log("[ FtpSend ] FTP URI : " + ftpPath);

            return mData.sendCount;
        }
        /**
         * FTP Server에 데이터를 전송한다.
         * 원하는 디렉토리를 생성해서 데이터를 전송한다.
         */
        public int sendDataToFtpServer()
        {

            // 데이터 날짜 체크
            // FDP Directory : site13206, site13211, site13210
            string ftp_url = ftpUri + ftpHost + ":" + ftpPort + "/site" + m_stCode + "/" + mData.m_year + "/" + mData.m_mon + "/" + mData.m_day;  // + "/" + info.s_hour;
            //string ftp_url = ftpUri + ftpHost + ":" + ftpPort + "/" + m_stCode + "/" + mData.m_year + "/" + mData.m_mon + "/" + mData.m_day;  // + "/" + info.s_hour;
            if (FtpDirectoryExists(ftp_url) == false)
            {
                log.Log("FTP Server : Directory create error......" + ftp_url);
                return 0;
            }
            string ftpPath = "";
            if (mData.mode == 0)        // STA
            {
                // sta 파일 전송
                // staFileName : 17_01_00_00.sta  => site13201_20170714_010000.sta
                string tmpName1 = mData.staFileName.Replace("_", "");   // => 17010000.sta
                string ftpSaveFile1 =  m_stCode + "_" + mData.m_year + mData.m_mon + mData.m_day+ tmpName1.Substring(2);
                ftpPath = ftp_url + "/" + ftpSaveFile1;   //  mData.staFileName;
                //ftpPath = ftp_url + "/" + mData.staFileName;
                if (sendData(ftpPath, mData.staFullFileName))
                {
                    mData.sendCount++;
                }
            }
            else
            {
                // Ini 파일 전송
                if (mData.iniFileName != "")
                {
                    // iniFileName : 17_01_00_00_0_DBS.ini  => site13201_20170714_010000_DBS.ini
                    string tmp = mData.iniFileName.Replace("_", "");   // => 170100000DBS.ini
                    string tmpName2 = tmp.Substring(2, 6) + "_" + tmp.Substring(9);   // 010000_DBS.ini
                    string ftpSaveFile2 =  m_stCode + "_" + mData.m_year + mData.m_mon + mData.m_day + tmpName2;
                    ftpPath = ftp_url + "/" + ftpSaveFile2;   //  mData.iniFileName;
                    //ftpPath = ftp_url + "/" + mData.iniFileName;
                    if (sendData(ftpPath, mData.iniFullFileName))
                    {
                        mData.sendCount++;
                    }
                }


                if (mData.rtdFileName != "")
                {
                    // rtd 파일전송
                    // rtdFileName : 17_01_00_00_0_DBS.rtd  => site13201_20170714_010000_DBS.rtd
                    string tmp = mData.rtdFileName.Replace("_", "");   // => 170100000DBS.rtd
                    string tmpName2 = tmp.Substring(2, 6) + "_" + tmp.Substring(9);   // 010000_DBS.rtd
                    string ftpSaveFile2 =  m_stCode + "_" + mData.m_year + mData.m_mon + mData.m_day +  tmpName2;
                    ftpPath = ftp_url + "/" + ftpSaveFile2;  //  mData.rtdFileName;
                    //ftpPath = ftp_url + "/" +  mData.rtdFileName;
                    if (sendData(ftpPath, mData.rtdFullFileName))
                    {
                        mData.sendCount++;
                    }
                }
                if (mData.rawFileName != "")
                {
                    // raw 파일전송
                    // rawFileName : 17_01_00_00_0_DBS.raw  => site13201_20170714_010000_DBS.raw
                    string tmp = mData.rawFileName.Replace("_", "");   // => 170100000DBS.raw
                    string tmpName2 = tmp.Substring(2, 6) + "_" + tmp.Substring(9);   // 010000_DBS.raw
                    string ftpSaveFile2 =  m_stCode + "_" + mData.m_year + mData.m_mon + mData.m_day +  tmpName2;
                    ftpPath = ftp_url + "/" + ftpSaveFile2;  //  mData.rawFileName;
                    //ftpPath = ftp_url + "/" +  mData.rawFileName;
                    if (sendData(ftpPath, mData.rawFullFileName))
                    {
                        mData.sendCount++;
                    }
                }
            }

            log.Log("[ FtpSend ] FTP URI : " + ftpPath);

            return mData.sendCount;
        }

        /**
         * FTP Server에 데이터를 실제로 전송한다.
         */
        private bool sendData(string ftpPath, string inputFile)
        {
            bool result = false;

            // WebRequest.Create로 Http,Ftp,File Request 객체를 모두 생성할 수 있다.
            FtpWebRequest req = (FtpWebRequest)WebRequest.Create(ftpPath);
            // FTP 업로드한다는 것을 표시
            req.Method = WebRequestMethods.Ftp.UploadFile;
            // 쓰기 권한이 있는 FTP 사용자 로그인 지정
            req.Credentials = new NetworkCredential(ftpUser, ftpPass);

            // 입력파일을 바이트 배열로 읽음
            byte[] data;
            
            try
            {
                using (StreamReader reader = new StreamReader(inputFile))
                {
                    data = Encoding.UTF8.GetBytes(reader.ReadToEnd());
                }

                // RequestStream에 데이타를 쓴다
                req.ContentLength = data.Length;
                using (Stream reqStream = req.GetRequestStream())
                {
                    reqStream.Write(data, 0, data.Length);
                }

                // FTP Upload 실행
                using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse())
                {
                    // FTP 결과 상태 출력
                    Console.WriteLine("[ FtpSend ] Upload completed : {0}, {1}", resp.StatusCode, resp.StatusDescription);
                    string logMsg = String.Format("[ FtpSend ] Upload completed : {0}, {1}", resp.StatusCode, resp.StatusDescription);
                    log.Log(logMsg);
                    result = true;
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine("[ FtpSend ] WebException Error : " + ex.ToString() + "=>" + inputFile);
                log.Log("[ FtpSend ] WebException error : " + ex.ToString());
                result = false;

                FtpWebResponse response = (FtpWebResponse)ex.Response;
                Console.WriteLine("[ FtpSend ] Response : " + response.StatusCode);
                Console.WriteLine("[ FtpSend ] " + response.ToString());
            }

            return result;
        }

        /**
         * FTP Server에 해당 디렉토리가 존재하는지 체크한다.
         * 없으면 생성한다.
         */
        private bool FtpDirectoryExists(string directory)
        {

            try
            {
                //create the directory
                Console.WriteLine(directory);
                FtpWebRequest requestDir = (FtpWebRequest)FtpWebRequest.Create(new Uri(directory));
                requestDir.Method = WebRequestMethods.Ftp.MakeDirectory;
                requestDir.Credentials = new NetworkCredential(ftpUser, ftpPass);
                requestDir.UsePassive = true;
                requestDir.UseBinary = true;
                requestDir.KeepAlive = false;
                FtpWebResponse response = (FtpWebResponse)requestDir.GetResponse();
                Stream ftpStream = response.GetResponseStream();

                ftpStream.Close();
                response.Close();

                return true;
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    response.Close();
                    return true;
                }
                else
                {
                    response.Close();
                    return false;
                }
            }
        }
    }
}
