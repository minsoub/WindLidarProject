using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace WindLidarClient
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
        private DataProcess main;
        private SndDataInfo mData;
        public FtpModuleLib(DataProcess m)
        {
            main = m;
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

        public void setSendData(SndDataInfo data)
        {
            //mData.clear();
            mData = data;
        }

        /**
         * FTP Server에 데이터를 전송한다.
         * 원하는 디렉토리를 생성해서 데이터를 전송한다.
         */
        public int sendDataToFtpServer()
        {

            // 데이터 날짜 체크
            SndDataInfo.sFileInfo info = mData.lstInfo[0];
            string ftp_url = ftpUri + ftpHost + ":" + ftpPort + "/" + m_stCode + "/" + mData.m_year + "/" + mData.m_mon + "/" + mData.m_day;  // + "/" + info.s_hour;
            if (FtpDirectoryExists(ftp_url) == false)
            {
                log.Log("FTP Server[sendDataToFtpServer] : Directory create error......[" + ftp_url + "]");
                return 0;
            }

            // Ini 파일 전송
            string ftpPath = ftp_url + "/" + info.iniFile;   //  mData.iniFileName;
            if (sendData(ftpPath, info.iniFullName))   //  mData.iniFullFileName))
            {
                mData.sendCount++;
            }
            
            // rtd 파일 전송
            //foreach (SndDataInfo.sFileInfo sInfo in mData.lstInfo)
            //{
            ftpPath = ftp_url + "/" + info.rtdFile;  //  sInfo.fileName;
                if (sendData(ftpPath, info.rtdFullName))   // sInfo.fullFileName))
                {
                    mData.sendCount++;
                }
            //}
            // raw 파일 전송
            ftpPath = ftp_url + "/" + info.rawFile;   //  mData.iniFileName;
            if (sendData(ftpPath, info.rawFullName))   //  mData.iniFullFileName))
            {
                mData.sendCount++;
            }

            log.Log("[ FtpSend ] FTP URI[sendDataToFtpServer] : " + ftpPath);

            return mData.sendCount;
        }

        /**
         * FTP Server에 STA 데이터를 전송한다.
         * 원하는 디렉토리를 생성해서 데이터를 전송한다.
         */
        public int sendStaDataToFtpServer()
        {

            // 데이터 날짜 체크
            SndDataInfo.sFileInfo info = mData.lstInfo[0];
            string ftp_url = ftpUri + ftpHost + ":" + ftpPort + "/" + m_stCode + "/" + mData.m_year + "/" + mData.m_mon + "/" + mData.m_day;  // + "/" + info.s_hour;
            if (FtpDirectoryExists(ftp_url) == false)
            {
                log.Log("FTP Server[sendStaDataToFtpServer] : Directory create error......[" + ftp_url + "]");
                return 0;
            }
            // sta 파일 전송
            string sndName = info.fileName.Replace("snd", "sta");
            string ftpPath = ftp_url + "/" + sndName;  //  확장자 변경해야 된다.

            if (sendData(ftpPath, info.fullFileName))  // mData.staFullFileName))
            {
                mData.sendCount++;
            }

            log.Log("[ FtpSend ] FTP URI[sendStaDataToFtpServer] : " + ftpPath);

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
            using (StreamReader reader = new StreamReader(inputFile))
            {
                data = Encoding.UTF8.GetBytes(reader.ReadToEnd());
            }

            try
            {
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
                Console.WriteLine("[ FtpSend ] WebException Error : " + ex.ToString());
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
