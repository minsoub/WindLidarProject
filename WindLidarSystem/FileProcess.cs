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
        /**
         * 파일전송 UDP 메시지를 수신한다.
         */
        public bool fileStsUpdate(string msg, string client_ip)
        {
            bool result = false;
            //FT:관측소ID:IP ADDR:시작시각:종료시각:파일개수:INI파일명:RAW파일명:RTD파일명:P1:P2:P3:P4:P5:P6:S  => START
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

                        if (arrMsg[6] != "" && arrMsg[6].IndexOf("DBS") == -1)  
                        {
                            sql = String.Format("UPDATE T_RCV_NOT_DBS_FILE set acc_file_cnt = {0} WHERE s_code = '{1}' and st_time='{2}' and et_time='{3}'",
                            arrMsg[5], arrMsg[1], st_time, et_time
                            );
                        }
                        else
                        {
                            sql = String.Format("UPDATE T_RCV_FILE set acc_file_cnt = {0} WHERE s_code = '{1}' and st_time='{2}' and et_time='{3}'",
                            arrMsg[5], arrMsg[1], st_time, et_time
                            );
                        }

                        logMsg(sql);
                        oCmd = new MySqlCommand(sql, conn);
                        oCmd.ExecuteNonQuery();

                        udpOkSend(arrMsg[1], arrMsg[2], client_ip);

                    }
                    else
                    {
                        if (arrMsg.Length == 16)        // start message
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

                            if ((arrMsg[6] != "" && arrMsg[6].IndexOf("DBS") == -1) || (arrMsg[7] != "" && arrMsg[8].IndexOf("DBS") == -1)
                                || (arrMsg[8] != "" && arrMsg[8].IndexOf("DBS") == -1))     
                            {
                                // sts insert
                                sql = String.Format("insert into T_RCV_NOT_DBS_FILE (s_code, st_time, et_time, real_file_cnt, acc_file_cnt, err_chk, s_chk, srv_file_cnt, ini_name, raw_name, rtd_name, reg_dt) values"
                                + " ('{0}', '{1}', '{2}', '{3}', '{4}', 'N', 'N', 0,   '{5}', '{6}', '{7}',  current_timestamp ) ",
                                arrMsg[1], st_time, et_time, arrMsg[5], 0, arrMsg[6], arrMsg[7], arrMsg[8]
                                );
                                oCmd = new MySqlCommand(sql, conn);
                                oCmd.ExecuteNonQuery();
                            }
                            else   // DBS
                            {

                                // FILE insert
                                sql = String.Format("SELECT COUNT(S_CODE) as cnt FROM T_RCV_FILE WHERE S_CODE='{0}' AND ST_TIME='{1}' AND ET_TIME='{2}'",
                                        arrMsg[1], st_time, et_time
                                );
                                oCmd = new MySqlCommand(sql, conn);
                                MySqlDataReader rs = oCmd.ExecuteReader();
                                int cnt = 0;
                                if (rs.Read())
                                {
                                    cnt = rs.GetInt32("cnt");
                                }
                                rs.Close();
                                rs = null;
                                oCmd = null;

                                if (cnt == 0)       // Insert
                                {
                                    sql = String.Format("insert into T_RCV_FILE (s_code, st_time, et_time, real_file_cnt, acc_file_cnt, err_chk, s_chk, srv_file_cnt, ini_name, raw_name, rtd_name, reg_dt) values"
                                    + " ('{0}', '{1}', '{2}', '{3}', '{4}', 'N', 'N', 0,   '{5}', '{6}', '{7}',  current_timestamp ) ",
                                    arrMsg[1], st_time, et_time, arrMsg[5], 0, arrMsg[6], arrMsg[7], arrMsg[8]
                                    );
                                }
                                else
                                {
                                    sql = String.Format("UPDATE T_RCV_FILE set real_file_cnt='{0}', acc_file_cnt='{1}', err_chk='{2}', s_chk='{3}', srv_file_cnt='{4}', ini_name='{5}', raw_name='{6}', rtd_name='{7}'"
                                        + " WHERE s_code='{8}' and st_time='{9}' and et_time='{10}'",
                                    arrMsg[5], 0, 'N', 'N', 0, arrMsg[6], arrMsg[7], arrMsg[8], arrMsg[1], st_time, et_time
                                    );
                                }
                                oCmd = new MySqlCommand(sql, conn);
                                oCmd.ExecuteNonQuery();

                                // ini insert
                                sql = String.Format("insert into T_RCV_PARAM_INFO (s_code, st_time, et_time, p_type, p_pam1, p_pam2, p_pam3, p_pam4, avt_tm, reg_dt) values"
                                + " ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', current_timestamp ) "
                                + " ON DUPLICATE KEY "
                                + " UPDATE p_pam1='{9}', p_pam2='{10}', p_pam3='{11}', p_pam4='{12}', avt_tm='{13}'",
                                arrMsg[1], st_time, et_time, arrMsg[9], arrMsg[10], arrMsg[11], arrMsg[12], arrMsg[13], arrMsg[14],
                                arrMsg[10], arrMsg[11], arrMsg[12], arrMsg[13], arrMsg[14]
                                );
                                oCmd = new MySqlCommand(sql, conn);
                                oCmd.ExecuteNonQuery();
                            }

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
         * STA 파일에 대해서 전송 메시지를 저장한다.
         */
        public bool fileStaUpdate(string msg, string client_ip)
        {
            bool result = false;
            // AT : 관측소 ID : IP ADDR : 시작시각 : 종료시각 : 파일개수 : sta파일명 : S  => START
            // AT : 관측소 ID : IP ADDR : 시작시각 : 종료시각 : 총개수 : 파일명 : E => END
            logMsg("[ FileProcess::fileStaUpdate ] received msg : " + msg);

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


                        sql = String.Format("UPDATE T_RCV_STA set acc_file_cnt = {0} WHERE s_code = '{1}' and st_time='{2}' and et_time='{3}'",
                        arrMsg[5], arrMsg[1], st_time, et_time
                        );

                        logMsg(sql);
                        oCmd = new MySqlCommand(sql, conn);
                        oCmd.ExecuteNonQuery();

                        udpOkStaSend(arrMsg[1], arrMsg[2], client_ip);

                    }
                    else
                    {
                        if (arrMsg[7] == "S")       //  start message
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

                            // STA insert
                            string fileName = arrMsg[6];
                            fileName = fileName.Replace("snd", "sta");


                            // FILE insert
                            sql = String.Format("SELECT COUNT(S_CODE) as cnt FROM T_RCV_STA WHERE S_CODE='{0}' AND ST_TIME='{1}' AND ET_TIME='{2}'",
                                    arrMsg[1], st_time, et_time
                            );
                            oCmd = new MySqlCommand(sql, conn);
                            MySqlDataReader rs = oCmd.ExecuteReader();
                            int cnt = 0;
                            if (rs.Read())
                            {
                                cnt = rs.GetInt32("cnt");
                            }
                            rs.Close();
                            rs = null;
                            oCmd = null;

                            if (cnt == 0)       // Insert
                            {
                                sql = String.Format("insert into T_RCV_STA (s_code, st_time, et_time, real_file_cnt, acc_file_cnt, err_chk, s_chk, srv_file_cnt, f_name,  reg_dt) values"
                                + " ('{0}', '{1}', '{2}', '{3}', '{4}', 'N', 'N', 0,  '{5}',  current_timestamp ) ",
                                arrMsg[1], st_time, et_time, arrMsg[5], 0, fileName
                                );
                            }
                            else
                            {
                                sql = String.Format("UPDATE T_RCV_STA set real_file_cnt='{0}', acc_file_cnt='{1}', err_chk='{2}', s_chk='{3}', srv_file_cnt='{4}', f_name='{5}' "
                                + " WHERE s_code='{6}' and st_time='{6}' and et_time='{6}'",
                                arrMsg[5], 0, 'N', 'N', 0, fileName, arrMsg[1], st_time, et_time
                                );
                            }
                            oCmd = new MySqlCommand(sql, conn);
                            oCmd.ExecuteNonQuery();

                            udpOkStaSend(arrMsg[1], arrMsg[2], client_ip);
                        }
                        else
                        {
                            log.Log("[ FileProcess::fileStaUpdate ] received error msg : " + msg);
                            logMsg("[ FileProcess::fileStaUpdate ] received error msg : " + msg);
                            result = false;
                        }
                    }

                    result = true;
                }
            }
            catch (MySqlException e)
            {
                log.Log("[FileProcess::fileStaUpdate] error : " + e.Message);
                logMsg("[FileProcess::fileStaUpdate] error : " + e.Message);
                Console.WriteLine(e.Message);

                result = false;
            }

            return result;
        }

        /**
         * 데이터베이스를 조회해서 Other 데이터 구조체에 데이터를 담아서 리턴한다. 
         * NOT DBS
         */
        public StsInfo getRcvOtherDataInfo()
        {
            MySqlCommand oCmd = null;
            StsInfo stsInfo = null;
            string sql = "";
            try
            {
                using (MySqlConnection conn = ConnectionPool.Instance.getConnection())
                {
                    // 작업처리하지 않은건(s_chk='N')과 받은 파일 개수가 0개 이상인 건을 조회해서 작업을 수행한다.
                    sql = "select no, s_code, st_time, et_time, real_file_cnt, acc_file_cnt, err_chk, s_chk, srv_file_cnt, ini_name, raw_name, rtd_name, reg_dt from T_RCV_NOT_DBS_FILE ";
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
                        stsInfo.ini_name = rs.GetString("ini_name");
                        stsInfo.raw_name = rs.GetString("raw_name");
                        stsInfo.rtd_name = rs.GetString("rtd_name");
                        stsInfo.mode = 2;
                    }
                    rs.Close();
                    rs = null;
                    oCmd = null;
                }
            }
            catch (MySqlException e)
            {
                log.Log("[FileProcess::getRcvOtherDataInfo] error : " + e.Message);
                logMsg("[FileProcess::getRcvOtherDataInfo] error : " + e.Message);
            }

            return stsInfo;
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
                    sql = "select no, s_code, st_time, et_time, real_file_cnt, acc_file_cnt, err_chk, s_chk, srv_file_cnt, ini_name, raw_name, rtd_name, reg_dt from T_RCV_FILE ";
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
                        stsInfo.ini_name = rs.GetString("ini_name");
                        stsInfo.raw_name = rs.GetString("raw_name");
                        stsInfo.rtd_name = rs.GetString("rtd_name");
                        stsInfo.mode = 1;
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

        /**
         * STA 파일에 대해서 전송할 데이터가 있는지 데이터베이스에 조회한다.
         * T_RCV_STA
         */
        public StsInfo getStaRcvDataInfo()
        {
            MySqlCommand oCmd = null;
            StsInfo stsInfo = null;
            string sql = "";
            try
            {
                using (MySqlConnection conn = ConnectionPool.Instance.getConnection())
                {
                    // 작업처리하지 않은건(s_chk='N')과 받은 파일 개수가 0개 이상인 건을 조회해서 작업을 수행한다.
                    sql = "select no, s_code, st_time, et_time, real_file_cnt, acc_file_cnt, err_chk, s_chk, srv_file_cnt, f_name, reg_dt from T_RCV_STA ";
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
                        stsInfo.mode = 0;
                    }
                    rs.Close();
                    rs = null;
                    oCmd = null;
                }
            }
            catch (MySqlException e)
            {
                log.Log("[FileProcess::getStaRcvDataInfo] error : " + e.Message);
                logMsg("[FileProcess::getStaRcvDataInfo] error : " + e.Message);
            }

            return stsInfo;
        }

        public void udpOkSend(string s_code, string host, string client_ip)
        {
            try
            {
                // Client에 전송한다.
                int localPort = System.Convert.ToInt32(ParamInitInfo.Instance.m_localPort);        // 10005
                int sndPort = System.Convert.ToInt32(ParamInitInfo.Instance.m_dataclientport);     // 10003

                string sndMsg = "FT:" + s_code + ":" + host + ":ok";
                byte[] buf = Encoding.ASCII.GetBytes(sndMsg);

                using (UdpClient c = new UdpClient(localPort + 1))  // source port (로컬 포트에서 상태 포트를 하나 사용하므로 중복이 발생하므로 사용포트 - 1)
                {
                    c.Send(buf, buf.Length, client_ip, sndPort);
                    logMsg("[FileProcess::udpOkSend] Send Msg [host : " + client_ip + " : " + sndPort + " : " + sndMsg);
                }
            }catch(Exception ex)
            {
                logMsg("[FileProcess::udpOkSend] " + ex.ToString());
            }
        }

        public void udpOkStaSend(string s_code, string host, string client_ip)
        {
            try
            {
                // Client에 전송한다.
                int localPort = System.Convert.ToInt32(ParamInitInfo.Instance.m_localPort);        // 10005
                int sndPort = System.Convert.ToInt32(ParamInitInfo.Instance.m_staclientport);      // 10004

                string sndMsg = "AT:" + s_code + ":" + host + ":ok";
                byte[] buf = Encoding.ASCII.GetBytes(sndMsg);

                using (UdpClient c = new UdpClient(localPort + 1))  // source port (로컬 포트에서 상태 포트를 하나 사용하므로 중복이 발생하므로 사용포트 - 1)
                {
                    c.Send(buf, buf.Length, client_ip, sndPort);
                    logMsg("[FileProcess::udpOkSend] Send Msg [host : " + client_ip + " : " + sndPort + " : " + sndMsg);
                }
            }
            catch (Exception ex)
            {
                logMsg("[FileProcess::udpOkStaSend] " + ex.ToString());
            }
        }

        /**
         * STA파일을 FTP 서버에 전송한다.
         */
        public bool ftpStaSendData(StsInfo info)
        {
            bool result = false;

            sendInfo = new SndDataInfo();

            try
            {
                // FTP 전송할 파일을 읽어 들인다.
                bool ok = HasWritePermissionOnDir(info);

                if (ok == true)
                {
                    setSendData(sendInfo);

                    int sendCount = sendStaDataToFtpServer();      // FTP에 전송하고 전송된 개수를 리턴 받는다.

                    info.srv_file_cnt = sendCount;

                    if (databaseSendUpdate(info) == true)
                    {
                        logMsg("[ftpStaSendData] The data is successfully updated.[" + info.f_name + "]");

                        if (FileMoveProcess(info) == true)
                        {
                            logMsg("[ftpStaSendData] The data is successfully moved in the backup directory.[" + info.f_name + "]");
                            result = true;
                        }
                        else
                        {
                            logMsg("[ftpStaSendData] The job moving to the backup directory is not successed.[" + info.f_name + "]");
                            result = false;
                        }
                    }
                    else
                    {
                        logMsg("[ftpStaSendData] The update is not successed.[" + info.s_code + "]");

                        result = false;
                    }
                }
                else
                {
                    logMsg("[ftpStaSendData] File is not exists...[" + info.f_name + "]");
                    result = false;
                }
            }
            catch (Exception ex)
            {
                logMsg("[ftpStaSendData] " + ex.ToString());
                result = false;
            }
            return result;

        }
        /**
         * FTP Server에 데이터를 전송한다.
         */
        public bool ftpSendData(StsInfo info)
        {
            bool result = false;

            sendInfo = new SndDataInfo();
            sendInfo.mode = info.mode;
            sendInfo.iniFileName = "";
            sendInfo.rawFileName = "";
            sendInfo.rtdFileName = "";
            Console.WriteLine("ftpSendData mode : " + sendInfo.mode);

            try
            {
                // FTP 전송할 파일을 읽어 들인다.
                bool ok = HasWritePermissionOnDir(info);

                if (ok == true)
                {
                    setSendData(sendInfo);

                    int sendCount = sendDataToFtpServer();      // FTP에 전송하고 전송된 개수를 리턴 받는다.

                    info.srv_file_cnt = sendCount;

                    if (databaseSendUpdate(info) == true)
                    {
                        logMsg("[ftpSendData] The data is successfully updated.[" + info.s_code + "]");

                        if (FileMoveProcess(info) == true)
                        {
                            logMsg("[ftpSendData] The data is successfully moved in the backup directory.[" + info.s_code + "]");
                            result = true;
                        }
                        else
                        {
                            logMsg("[ftpSendData] The job moving to the backup directory is not successed.[" + info.s_code + "]");
                            result = false;
                        }
                    }
                    else
                    {
                        logMsg("[ftpSendData] The update is not successed.[" + info.s_code + "]");

                        result = false;
                    }
                }
                else
                {
                    logMsg("[ftpSendData] File is not exists...[" + info.s_code + "]");
                    result = false;
                }
            }
            catch (Exception ex)
            {
                logMsg("[ftpSendData] " + ex.ToString());
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
                    if (info.mode == 0)
                    {
                        sql = String.Format("update T_RCV_STA set s_chk='Y', srv_file_cnt={0}, upt_dt=current_timestamp where no={1}", info.srv_file_cnt, info.no);
                    }else if(info.mode == 1)
                    {
                        sql = String.Format("update T_RCV_FILE set s_chk='Y', srv_file_cnt={0}, upt_dt=current_timestamp where no={1}", info.srv_file_cnt, info.no);
                    }else if(info.mode == 2)
                    {
                        sql = String.Format("update T_RCV_NOT_DBS_FILE set s_chk='Y', srv_file_cnt={0}, upt_dt=current_timestamp where no={1}", info.srv_file_cnt, info.no);
                    }
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
                    if (info.mode == 0)
                    {
                        sql = String.Format("update T_RCV_STA set err_chk='Y', upt_dt=current_timestamp where no={0}", info.no);
                    }
                    else if(info.mode == 1)  // DBS
                    {
                        sql = String.Format("update T_RCV_FILE set err_chk='Y', upt_dt=current_timestamp where no={0}", info.no);
                    }
                    else if(info.mode == 2) // NOT DBS
                    {
                        sql = String.Format("update T_RCV_NOT_DBS_FILE set err_chk='Y', upt_dt=current_timestamp where no={0}", info.no);
                    }
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
            // 10_08_55_00.sta
            // yyyy-mm-dd
            //public string f_name;
            //public string ini_name;
            //public string raw_name;
            //public string rtd_name;

            string year = info.et_time.Substring(0, 4);
            string mon = info.et_time.Substring(5, 2);
            string day = info.et_time.Substring(8, 2); 

            string dataPath = Path.Combine(m_sourceDir, info.s_code, year, mon, day);
            sendInfo.path = dataPath;
            sendInfo.m_year = year;
            sendInfo.m_mon = mon;
            sendInfo.m_day = day;

            try
            {
                // 디렉토리 내에 파일이 존재하는지 체크한다.
                if (Directory.Exists(dataPath) == false)
                {
                    Console.WriteLine("Directory not exist.... : {0}", dataPath);
                    logMsg("[FileProcess::HasWritePermissionOnDir] Directory not exist.... : " + dataPath);
                    log.Log("[FileProcess::HasWritePermissionOnDir] Directory not exist.... : " + dataPath);
                    return false;
                }

                Console.WriteLine("HasWritePermissionOnDir mode => " + info.mode);
                // sta check
                if (info.mode == 0)     // STA
                {
                    string stsFull = Path.Combine(dataPath, info.f_name);

                    if (File.Exists(stsFull))
                    {
                        sendInfo.staFileName = info.f_name;
                        sendInfo.staFullFileName = stsFull;
                        sendInfo.fileCount++;
                    }
                    else
                    {
                        Console.WriteLine("HasWritePermissionOnDir file not exist [error] : " + stsFull);
                        log.Log("HasWritePermissionOnDir file not exist [error] : " + stsFull);
                        return false;
                    }
                }
                else
                {
                    // ini check
                    string iniFull = Path.Combine(dataPath, info.ini_name);
                    if (File.Exists(iniFull))
                    {
                        sendInfo.iniFileName = info.ini_name;
                        sendInfo.iniFullFileName = iniFull;
                        sendInfo.fileCount++;
                    }
                    else
                    {
                        Console.WriteLine("HasWritePermissionOnDir file not exist [error] : " + iniFull);
                        log.Log("HasWritePermissionOnDir file not exist [error] : " + iniFull);
                        //return false;
                    }
                    // raw check
                    string rawFull = Path.Combine(dataPath, info.raw_name);
                    if (File.Exists(rawFull))
                    {
                        sendInfo.rawFileName = info.raw_name;
                        sendInfo.rawFullFileName = rawFull;
                        sendInfo.fileCount++;
                    }
                    else
                    {
                        Console.WriteLine("HasWritePermissionOnDir file not exist [error] : " + rawFull);
                        log.Log("HasWritePermissionOnDir file not exist [error] : " + rawFull);
                       // return false;
                    }

                    if (info.rtd_name != "")
                    {
                        // rtd check
                        string rtdFull = Path.Combine(dataPath, info.rtd_name);
                        if (File.Exists(rtdFull))
                        {
                            sendInfo.rtdFileName = info.rtd_name;
                            sendInfo.rtdFullFileName = rtdFull;
                            sendInfo.fileCount++;
                        }
                        else
                        {
                            Console.WriteLine("HasWritePermissionOnDir file not exist [error] : " + rtdFull);
                            log.Log("HasWritePermissionOnDir file not exist [error] : " + rtdFull);
                            //return false;
                        }
                    }
                }
            }catch(Exception ex)
            {
                log.Log("HasWritePermissionOnDir error : " + ex.ToString());
                return false;
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
                string destFileName = "";
                if (info.mode == 1)
                {
                    // Ini 파일 이동  
                    if (sendInfo.iniFileName != "")
                    {
                        destFileName = Path.Combine(backupPath, sendInfo.iniFileName);
                        if (File.Exists(destFileName))
                        {
                            File.Delete(destFileName);
                        }
                        FileInfo iniFile = new FileInfo(sendInfo.iniFullFileName);
                        iniFile.MoveTo(destFileName);
                    }
                    // rtd 파일 이동
                    if (sendInfo.rtdFileName != "")
                    {
                        destFileName = Path.Combine(backupPath, sendInfo.rtdFileName);
                        if (File.Exists(destFileName))
                        {
                            File.Delete(destFileName);
                        }
                        FileInfo rtdFile = new FileInfo(sendInfo.rtdFullFileName);
                        rtdFile.MoveTo(destFileName);
                    }
                    // raw 파일 이동
                    if (sendInfo.rawFileName != "")
                    {
                        destFileName = Path.Combine(backupPath, sendInfo.rawFileName);
                        if (File.Exists(destFileName))
                        {
                            File.Delete(destFileName);
                        }
                        FileInfo rawFile = new FileInfo(sendInfo.rawFullFileName);
                        rawFile.MoveTo(destFileName);
                    }
                }
                else
                {
                    // sta 파일 이동
                    destFileName = Path.Combine(backupPath, sendInfo.staFileName);
                    if (File.Exists(destFileName))
                    {
                        File.Delete(destFileName);
                    }
                    FileInfo staFile = new FileInfo(sendInfo.staFullFileName);
                    staFile.MoveTo(destFileName);
                }
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
