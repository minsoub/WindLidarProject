using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Net;
using System.Net.Sockets;

namespace WindLidarSystem
{
    public class AlarmProcess
    {
        private ProcessReceiver main;
        private LogCls log;

        private delegate void LogMessageCallback(String msg);
        LogMessageCallback logMsg;

        public AlarmProcess(ProcessReceiver m)
        {
            main = m;
            log = new LogCls();

            logMsg = new LogMessageCallback(main.setLogMessage);
        }

        /**
         * 수신 된 알람 정보를 데이터베이스에 등록한다.
         * 최신 1건 자료만 입력한다.
         */
        public bool almMessage(string msg, string clientIP)
        {
            bool result = false;

            logMsg("[ almMessage ] received msg : " + msg);

            // Database에 등록한다.
            MySqlCommand oCmd = null;
            string sql = "";
            try
            {
                using (MySqlConnection conn = ConnectionPool.Instance.getConnection())
                {
                    char[] splitData = { ':' };
                    char[] splitSt = { '_' };
                    string[] arrMsg = msg.Split(splitData);
                    string host = arrMsg[2];

                    string st_time = "";
                    string[] arrTime = arrMsg[3].Split(splitSt);
                    st_time = String.Format("{0}/{1}/{2} {3}:{4}:{5}",
                            arrTime[0], arrTime[1], arrTime[2], arrTime[3], arrTime[4], arrTime[5]
                        );

                    sql = String.Format("INSERT INTO T_RCV_ALM_INFO (s_code, st_time, content, reg_dt) VALUES ('{0}', '{1}', '{2}', current_timestamp) "
                        + " ON DUPLICATE KEY UPDATE st_time = '{3}', content = '{4}', reg_dt = current_timestamp ",
                        arrMsg[1], st_time, arrMsg[4], st_time, arrMsg[4]
                    );

                    oCmd = new MySqlCommand(sql, conn);
                    oCmd.ExecuteNonQuery();

                    // Client에 전송한다.
                    int localPort = System.Convert.ToInt32(ParamInitInfo.Instance.m_localPort);
                    int sndPort = System.Convert.ToInt32(ParamInitInfo.Instance.m_clientRcvPort);

                    string sndMsg = "AM:" + arrMsg[1] + ":" + clientIP + ":ok";
                    byte[] buf = Encoding.ASCII.GetBytes(sndMsg);

                    using (UdpClient c = new UdpClient(localPort))  // source port (로컬 포트에서 상태 포트를 하나 사용하므로 중복이 발생하므로 사용포트 - 1)
                    {
                        c.Send(buf, buf.Length, clientIP, sndPort);
                        logMsg("Send Msg [host : " + host + " : " + sndPort + " : " + sndMsg);
                    }
                }
            }
            catch (MySqlException e)
            {
                logMsg("almMessage error : " + e.Message);
                log.Log("almMessage error : " + e.Message);
            }

            return result;
        }

    }
}
