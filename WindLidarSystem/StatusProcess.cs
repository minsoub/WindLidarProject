using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace WindLidarSystem
{
    public class StatusProcess
    {
        private ProcessReceiver main;
        private LogCls log;
        private delegate void LogMessageCallback(String msg);
        LogMessageCallback stsLog;

        public StatusProcess(ProcessReceiver m)
        {
            main = m;
            log = new LogCls();

            stsLog = new LogMessageCallback(main.setLogMessage);
        }


        /**
         * 데이터베이스 등록된 클라이언트 상태 정보가 주기적으로 데이터를 수신해서
         * 업데이트 되고 있는지 체크한다.
         * 없으면 상태를 0으로 업데이트 및 신규 등록한다.
         */
        public string[] getClientStatusInfo()
        {
            string[] arrMsg = new string[3];
            int[] arrSts = new int[3];
            string[] arrCode = new string[3];
            MySqlCommand oCmd = null;
            string sql = "";

            try
            {

                using (MySqlConnection conn = ConnectionPool.Instance.getConnection())
                {
                    sql = "SELECT a.s_code, a.s_name, IFNULL(b.s_sts, 0) s_sts, DATE_FORMAT(IFNULL(b.reg_dt, now()), '%Y-%m-%d %H:%i:%s') reg_dt, ";
                    sql += "       IFNULL(DATE_FORMAT(b.reg_dt, '%Y-%m-%d %H:%i:%s'), '') as reg_dt2  ";
                    sql += "  FROM T_ST_CODE a LEFT JOIN T_CLI_STS_INFO b ";
                    sql += "    ON a.S_CODE = b.S_CODE";    //  AND b.REG_DT <= date_add(now(), interval - 2 minute) ";

                    oCmd = new MySqlCommand(sql, conn);
                    MySqlDataReader rs = oCmd.ExecuteReader();
                    int i = 0;
                    while (rs.Read())
                    {
                        arrCode[i] = rs.GetString("s_code");
                        arrSts[i] = rs.GetInt32("s_sts");
                        string msg = "ST=" + arrCode[i] + "=" + arrSts[i] + "=" + rs.GetString("reg_dt2");
                        arrMsg[i] = msg;

                        // sts가 1일 경우 날짜 체크
                        DateTime currentDt = DateTime.Now.AddMinutes(-2);   // 2 Minute
                        DateTime dbDt = DateTime.ParseExact(rs.GetString("reg_dt"), "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                        int result = DateTime.Compare(dbDt, currentDt);
                        if (result <= 0)        // currentDt가 크다 : 시간내에 데이터가 수신되지 않는다.
                        {
                            arrSts[i] = 0;
                        }
                        else { }                // currentDt가 작다.: 시간내에 데이터가 수신된다.

                        i++;
                    }
                    rs.Close();
                    rs = null;
                    oCmd = null;

                    for (i = 0; i < 3; i++)
                    {
                        if (arrSts[i] == 0)
                        {
                            sql = String.Format("INSERT INTO T_CLI_STS_INFO (s_code, s_sts, upt_dt) VALUES ('{0}', '{1}', current_timestamp) "
                                + " ON DUPLICATE KEY UPDATE s_sts = '{2}', upt_dt = current_timestamp ",
                                    arrCode[i], arrSts[i], arrSts[i]
                                );
                            oCmd = new MySqlCommand(sql, conn);
                            oCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (MySqlException e)
            {
                stsLog("StatusProcess::getClientStatusInfo error : " + e.Message);
                stsLog("[SQL] " + sql);
                log.Log("StatusProcess::getClientStatusInfo error : " + e.Message);
                log.Log("[SQL] " + sql);
            }
            return arrMsg;
        }
        /**
         * 클라이언트 프로그램의 상태 정보를 등록한다.
         */
        public bool stsUpdate(string[] msg)
        {
            bool result = false;

            MySqlCommand oCmd = null;
            string sql = "";

            try
            {
                using (MySqlConnection conn = ConnectionPool.Instance.getConnection())
                {
                    sql = String.Format("INSERT INTO T_CLI_STS_INFO (s_code, s_sts, reg_dt) VALUES ('{0}', '{1}', current_timestamp) "
                        + " ON DUPLICATE KEY UPDATE s_sts = '{2}', reg_dt = current_timestamp ",
                        msg[1], msg[2], msg[2]
                    );
                    oCmd = new MySqlCommand(sql, conn);
                    oCmd.ExecuteNonQuery();

                    result = true;
                }
            }
            catch (MySqlException e)
            {
                log.Log("StatusProcess::stsUpdate error : " + e.Message);
                log.Log("[SQL] " + sql);

                stsLog("StatusProcess::stsUpdate error : " + e.Message);
                stsLog("[SQL] " + sql);
                result = false;
            }

            return result;
        }
    }
}
