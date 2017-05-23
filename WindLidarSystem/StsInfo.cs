using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindLidarSystem
{

    public class StsInfo
    {
        public int no;
        public string s_code;
        public string st_time;
        public string et_time;
        public int read_file_cnt;
        public int acc_file_cnt;
        public string err_chk;
        public string s_chk;   // real filename (minute)
        public int srv_file_cnt;
        public string f_name;
        public string ini_name;
        public string raw_name;
        public string rtd_name;
        public int mode;   // 0 : STA, 1 : RAW, RTD, INI

        public StsInfo()
        {
            clear();
        }

        public void clear()
        {

        }
    }
}
