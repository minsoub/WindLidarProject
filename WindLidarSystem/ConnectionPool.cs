using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace WindLidarSystem
{
    public sealed class ConnectionPool
    {
        private static readonly ConnectionPool instance = new ConnectionPool();

        private string strCon;
        private ConnectionPool() {}

        public MySqlConnection getConnection()
        {
            MySqlConnection oCon;
            strCon = string.Format("data source={0}; port={1}; database={2}; user id={3}; password={4}",
                ParamInitInfo.Instance.m_dbHost, ParamInitInfo.Instance.m_dbPort, ParamInitInfo.Instance.m_dbName,
                ParamInitInfo.Instance.m_dbUser, ParamInitInfo.Instance.m_dbPass);

            oCon = new MySqlConnection(strCon);
            oCon.Open();

            return oCon;
        }

        public static ConnectionPool Instance
        {
            get
            {
                return instance;
            }
        }
    }
}
