using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace WindLidarSystem
{
    public class FileProcess : FtpModuleLib
    {
        private ProcessReceiver main;
        private LogCls log;
        private SndDataInfo sendInfo;

        private delegate void LogMessageCallback(String msg);
        LogMessageCallback logMsg;


        public FileProcess(ProcessReceiver m)
        {
            main = m;
            log = new LogCls();
            logMsg = new LogMessageCallback(main.setLogMessage);
        }
        public void clear()
        {
            sendInfo.lstInfo.Clear();
            sendInfo.staFileName = null;
            sendInfo.iniFileName = null;
            sendInfo.fileCount = 0;
        }
        public bool fileStsUpdate(string msg, string client_ip)
        {
            bool result = false;
            //FT:관측소ID:IP ADDR:시작시각:종료시각:파일개수:파일명:P1:P2:P3:P4:P5:S  => START
            //FT:관측소ID:IP ADDR:시작시각:종료시각:총개수:파일명:E => END
            logMsg("[ FileProcess::fileStsUpdate ] received msg : " + msg);

            // Database에 등록한다.
            MySqlCommand oCmd = null;
            string sql = "";
            try
            {
                using (MySqlConnection conn = ConnectionPool.Instance.getConnection())
                {
                    char[] splitData = { ':' };
                    string[] arrMsg = msg.Split(splitData);

                    if (arrMsg[7] == "E")       // End Message
                    {
                        string st_time = "";
                        string et_time = "";
                        char[] splitSt = { '_' };


                        string[] arrTime1 = arrMsg[3].Split(splitSt);
                        st_time = String.Format("{0}-{1}-{2} {3}:{4}:{5}",
                            arrTime1[0], arrTime1[1], arrTime1[2], arrTime1[3], arrTime1[4], arrTime1[5]
                        );
                        string[] arrTime2 = arrMsg[4].Split(splitSt);
                        et_time = String.Format("{0}-{1}-{2} {3}:{4}:{5}",
                            arrTime2[0], arrTime2[1], arrTime2[2], arrTime2[3], arrTime2[4], arrTime2[5]
                        );


                        sql = String.Format("UPDATE T_RCV_STS set acc_file_cnt = {0} WHERE s_code = '{1}' and st_time='{2}' and et_time='{3}'",
                        arrMsg[5], arrMsg[1], st_time, et_time
                        );

                        logMsg(sql);
                        oCmd = new MySqlCommand(sql, conn);
                        oCmd.ExecuteNonQuery();

                        udpOkSend(arrMsg[1], arrMsg[2], client_ip);

                    }
                    else
                    {
                        if (arrMsg.Length == 14)        // start message
                        {
                            string st_time = "";
                            string et_time = "";
                            char[] splitSt = { '_' };


                            string[] arrTime1 = arrMsg[3].Split(splitSt);
                            st_time = String.Format("{0}-{1}-{2} {3}:{4}:{5}",
                                arrTime1[0], arrTime1[1], arrTime1[2], arrTime1[3], arrTime1[4], arrTime1[5]
                            );
                            string[] arrTime2 = arrMsg[4].Split(splitSt);
                            et_time = String.Format("{0}-{1}-{2} {3}:{4}:{5}",
                                arrTime2[0], arrTime2[1], arrTime2[2], arrTime2[3], arrTime2[4], arrTime2[5]
                            );

                            // sts insert
                            sql = String.Format("insert into T_RCV_STS (s_code, st_time, et_time, real_file_cnt, acc_file_cnt, err_chk, s_chk, srv_file_cnt, f_name, reg_dt) values"
                            + " ('{0}', '{1}', '{2}', '{3}', '{4}', 'N', 'N', 0,  '{5}', current_timestamp ) ",
                            arrMsg[1], st_time, et_time, arrMsg[5], 0, arrMsg[6]
                            );
                            oCmd = new MySqlCommand(sql, conn);
                            oCmd.ExecuteNonQuery();

                            // ini insert
                            sql = String.Format("insert into T_RCV_PARAM_INFO (s_code, st_time, et_time, p_type, p_pam1, p_pam2, p_pam3, p_pam4, avt_tm, reg_dt) values"
                            + " ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', current_timestamp ) ",
                            arrMsg[1], st_time, et_time, arrMsg[7], arrMsg[8], arrMsg[9], arrMsg[10], arrMsg[11], arrMsg[12]
                            );
                            oCmd = new MySqlCommand(sql, conn);
                            oCmd.ExecuteNonQuery();

                            udpOkSend(arrMsg[1], arrMsg[2], client_ip);
                        }
                        else
                        {
                            log.Log("[ FileProcess::fileStsUpdate ] received error msg : " + msg);
                            logMsg("[ FileProcess::fileStsUpdate ] received error msg : " + msg);
                            result = false;
                        }
                    }

                    result = true;
                }
            }
            catch (MySqlException e)
            {
                log.Log("[FileProcess::fileStsUpdate] error : " + e.Message);
                logMsg("[FileProcess::fileStsUpdate] error : " + e.Message);
                Console.WriteLine(e.Message);

                result = false;
            }

            return result;
        }

        /**
         * 데이터베이스를 조회해서 StsInfo 구조체에 데이터를 담아서 리턴한다.
         */
        public StsInfo getRcvDataInfo()
        {
            MySqlCommand oCmd = null;
            StsInfo stsInfo = null;
            string sql = "";
            try
            {
                using (MySqlConnection conn = ConnectionPool.Instance.getConnection())
                {
                    // 작업처리하지 않은건(s_chk='N')과 받은 파일 개수가 0개 이상인 건을 조회해서 작업을 수행한다.
                    sql = "select no, s_code, st_time, et_time, real_file_cnt, acc_file_cnt, err_chk, s_chk, srv_file_cnt, f_name, reg_dt from T_RCV_STS ";
                    sql += " where s_chk='N' and acc_file_cnt > 0 and err_chk = 'N' order by reg_dt asc limit 0, 1";

                    oCmd = new MySqlCommand(sql, conn);
                    MySqlDataReader rs = oCmd.ExecuteReader();
                    while (rs.Read())
                    {
                        stsInfo = new StsInfo();
                        stsInfo.no = rs.GetInt32("no");
                        stsInfo.s_code = rs.GetString("s_code");
                        stsInfo.st_time = rs.GetString("st_time");
                        stsInfo.et_time = rs.GetString("et_time");
                        stsInfo.read_file_cnt = rs.GetInt32("real_file_cnt");
                        stsInfo.acc_file_cnt = rs.GetInt32("acc_file_cnt");
                        stsInfo.err_chk = rs.GetString("err_chk");
                        stsInfo.s_chk = rs.GetString("s_chk");
                        stsInfo.srv_file_cnt = rs.GetInt32("srv_file_cnt");
                        stsInfo.f_name = rs.GetString("f_name");

                    }
                    rs.Close();
                    rs = null;
                    oCmd = null;
                }
            }
            catch (MySqlException e)
            {
                log.Log("[FileProcess::getRcvDataInfo] error : " + e.Message);
                logMsg("[FileProcess::getRcvDataInfo] error : " + e.Message);
            }

            return stsInfo;
        }



        public void udpOkSend(string s_code, string host, string client_ip)
        {
            // Client에 전송한다.
            int localPort = System.Convert.ToInt32(ParamInitInfo.Instance.m_localPort);        // 10004
            int sndPort = System.Convert.ToInt32(ParamInitInfo.Instance.m_dataclientport);

            string sndMsg = "FT:" + s_code + ":" + host + ":ok";
            byte[] buf = Encoding.ASCII.GetBytes(sndMsg);

            using (UdpClient c = new UdpClient(localPort+1))  // source port (로컬 포트에서 상태 포트를 하나 사용하므로 중복이 발생하므로 사용포트 - 1)
            {
                c.Send(buf, buf.Length, client_ip, sndPort);
                logMsg("[FileProcess::udpOkSend] Send Msg [host : " + client_ip + " : " + sndPort + " : " + sndMsg);
            }
        }

        /**
         * FTP Server에 데이터를 전송한다.
         */
        public bool ftpSendData(StsInfo info)
        {
            bool result = false;

            sendInfo = new SndDataInfo();

            // FTP 전송할 파일을 읽어 들인다.
            bool ok = HasWritePermissionOnDir(info);

            if (ok == true)
            {
                setSendData(sendInfo);

                int sendCount = sendDataToFtpServer();      // FTP에 전송하고 전송된 개수를 리턴 받는다.

                info.srv_file_cnt = sendCount;

                if (databaseSendUpdate(info) == true)
                {
                    logMsg("[ftpSendData] The data is successfully updated.["+info.s_code+"]");

                    if(FileMoveProcess(info) == true)
                    {
                        logMsg("[ftpSendData] The data is successfully moved in the backup directory.[" + info.s_code + "]");
                        result = true;
                    }else
                    {
                        logMsg("[ftpSendData] The job moving to the backup directory is not successed.[" + info.s_code + "]");
                        result = false;
                    }
                }
                else
                {
                    logMsg("[ftpSendData] The update is not successed.[" + info.s_code + "]");

                    result =  false;
                }
            }else
            {
                logMsg("[ftpSendData] File is not exists...[" + info.s_code + "]");
                result = false;
            }

            return result;

        }
        /**
         * 데이터베이스 전송 정보를 저장한다.
         */
        private bool databaseSendUpdate(StsInfo info)
        {
            bool result = false;
            logMsg("[ FileProcess::databaseSendUpdate ] database updated => " + info.s_code + "[" + info.srv_file_cnt + "]");

            // Database에 등록한다.
            MySqlCommand oCmd = null;
            string sql = "";
            try
            {
                using (MySqlConnection conn = ConnectionPool.Instance.getConnection())
                {
                    sql = String.Format("update T_RCV_STS set s_chk='Y', srv_file_cnt={0}, upt_dt=current_timestamp where no={1}", info.srv_file_cnt, info.no);
                    oCmd = new MySqlCommand(sql, conn);
                    oCmd.ExecuteNonQuery();
                    result = true;
                }
            }
            catch (MySqlException e)
            {
                logMsg("[FileProcess::databaseSendUpdate] error : " + e.Message);
                logMsg("[SQL] " + sql);
                result = false;
            }

            return result;
        }

        public bool ftpFailUpdate(StsInfo info)
        {
            bool result = false;
            logMsg("[ ftpFailUpdate::databaseSendUpdate ] database updated => " + info.s_code + "[" + info.srv_file_cnt + "]");

            // Database에 등록한다.
            MySqlCommand oCmd = null;
            string sql = "";
            try
            {
                using (MySqlConnection conn = ConnectionPool.Instance.getConnection())
                {
                    sql = String.Format("update T_RCV_STS set err_chk='Y', upt_dt=current_timestamp where no={1}", info.no);
                    oCmd = new MySqlCommand(sql, conn);
                    oCmd.ExecuteNonQuery();
                    result = true;
                }
            }
            catch (MySqlException e)
            {
                logMsg("[ftpFailUpdate::databaseSendUpdate] error : " + e.Message);
                logMsg("[SQL] " + sql);
                result = false;
            }

            return result;
        }


        /**
         * FTP Server에 데이터를 업로드 할 수 있는지 체크한다.
         * 라이다에서 데이터를 쓰고 있으면 FTP Server에 데이터를 전송하면 안된다.
         * path : FTP root directory (D:\ftp_user\site_code)
         * SndDataInfo 클래스에 담는다.
         */
        public bool HasWritePermissionOnDir(StsInfo info)
        {

            clear();

            // 구조체에서 파일 정보를 얻는다.
            // 10_08_55_00_-_10_09_00_58.sta
            // yyyy-mm-dd

            string year = info.st_time.Substring(0, 4);
            string mon = info.st_time.Substring(5, 2);
            string day = info.st_time.Substring(8, 2); 

            string dataPath = Path.Combine(m_sourceDir, info.s_code, year, mon, day);


            // 디렉토리 내에 파일이 존재하는지 체크한다.
            if (Directory.Exists(dataPath) == false)
            {
                Console.WriteLine("Directory not exist.... : {0}", dataPath);
                logMsg("[FileProcess::HasWritePermissionOnDir] Directory not exist.... : " + dataPath);
                return false;
            }

            string staName = Path.GetFileNameWithoutExtension(info.f_name);  // 10_08_55_00_-_10_09_00_58
            DateTime[] arrDt = fromDateTimeExtract(staName);

            DirectoryInfo dir = new DirectoryInfo(dataPath);
            int cnt = 0;
            sendInfo.path = dataPath;
            sendInfo.m_year = year;
            sendInfo.m_mon = mon;
            sendInfo.m_day = day;

            //Console.WriteLine("fname : " + info.f_name);
            foreach (FileInfo fi in dir.GetFiles().OrderBy(fi => fi.CreationTime))      // 날짜순 정렬
            {
                string file = fi.FullName;
                string ext = Path.GetExtension(file);

                //Console.WriteLine("fi : " + fi.Name + ", " + fi.FullName);
               
                if (ext == ".sta" && fi.Name == info.f_name)
                {
                    sendInfo.staFileName = fi.Name;
                    sendInfo.staFullFileName = file;
                    sendInfo.fileCount++;
                    cnt++;

                    if (sendInfo.fileCount == 2) break;
                }
                if (ext == ".ini")
                {
                    DateTime rtdDt = convertTimeExtract(fi.Name);

                    // to ~ from에 속하는 데이터인지 확인한다.
                    double s1 = (arrDt[0] - rtdDt).TotalSeconds;
                    double s2 = (arrDt[1] - rtdDt).TotalSeconds;

                    if (s1 <= 0 && s2 >= 0)         // 해당 시간내에 속한 파일이다.
                    {
                        sendInfo.iniFileName = fi.Name;
                        sendInfo.iniFullFileName = file;
                        sendInfo.fileCount++;

                       if (sendInfo.fileCount == 2) break;
                    }
                }
            }
            if (cnt == 0)
            {
                logMsg("[FileProcess::HasWritePermissionOnDir] Upload data does not exists[cnt="+cnt+"]");
                return false;
            }

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
          * 파일이 존재하면 이동할 때 삭제를 하고 이동할 것인지를 결정해야 한다.
          */
        public bool FileMoveProcess(StsInfo info)
        {
            var result = false;
            try
            {

                string year = info.st_time.Substring(0, 4);
                string mon = info.st_time.Substring(5, 2);
                string day = info.st_time.Substring(8, 2);

                string dataPath = Path.Combine(m_sourceDir, info.s_code, year, mon, day);
                string backupPath = Path.Combine(m_backupDir, info.s_code, year, mon, day);


                // 디렉토리 내에 파일이 존재하는지 체크한다.
                if (Directory.Exists(backupPath) == false)
                {
                    // directory 생성
                    // 디렉토리 생성
                    string path = m_backupDir + "\\" + info.s_code;
                    DirectoryInfo dir1 = new DirectoryInfo(path);
                    if (dir1.Exists == false) dir1.Create();

                    path = path + "\\" + year;
                    DirectoryInfo dir2 = new DirectoryInfo(path);
                    if (dir2.Exists == false) dir2.Create();

                    path = path + "\\" + mon;
                    DirectoryInfo dir3 = new DirectoryInfo(path);
                    if (dir3.Exists == false) dir3.Create();

                    path = path + "\\" + day;
                    DirectoryInfo dir4 = new DirectoryInfo(path);
                    if (dir4.Exists == false) dir4.Create();
                }

                // Ini 파일 이동            
                string destFileName = Path.Combine(backupPath, sendInfo.iniFileName);  
                FileInfo iniFile = new FileInfo(sendInfo.iniFullFileName);
                iniFile.MoveTo(destFileName);

                // rtd 파일 이동
                foreach (SndDataInfo.sFileInfo sInfo in sendInfo.lstInfo)
                {
                    destFileName = Path.Combine(backupPath, sInfo.fileName);
                    FileInfo rtdFile = new FileInfo(sInfo.fullFileName);
                    rtdFile.MoveTo(destFileName);
                }
                // sta 파일 이동
                destFileName = Path.Combine(backupPath, sendInfo.staFileName);  
                FileInfo staFile = new FileInfo(sendInfo.staFullFileName);
                staFile.MoveTo(destFileName);
                result = true;
            }
            catch (IOException ex)
            {
                logMsg("[FileProcess::FileMoveProcess] error : " +ex.ToString());
                Console.WriteLine(ex.ToString());
                result = false;
            }

            return result;
        }


        DateTime[] fromDateTimeExtract(string data)
        {
            // data 10_08_55_00-10_09_00_58
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
            arr[0] = DateTime.ParseExact(toDt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
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
            //Console.WriteLine(data);

            return DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
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
    }
}
