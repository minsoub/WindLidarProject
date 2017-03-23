using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;


namespace WindLidarClient
{
    public class AlarmProcess
    {

        public struct AlmInfo
        {
            public string f_name;
            public string f_full_name;    
            public bool   snd_chk;
        }

        private List<AlmInfo> lstAlaram;
        private string m_stCode, m_stPort, m_stHost;
        private int m_cstLocalPort;
        private LogCls log;
        private ObserverProcess main;
        private char[] delimiterChar = { ':' };

        public AlarmProcess(ObserverProcess m)
        {
            lstAlaram = new List<AlmInfo>();
            log = new LogCls();
            main = m;
        }

        public void clear()
        {
            lstAlaram.Clear();
        }

        public void setData(string stCode, string stPort, int cstLocalPort, string stHost)
        {
            m_stCode = stCode;
            m_stPort = stPort;
            m_cstLocalPort = cstLocalPort;
            m_stHost = stHost;
        }
        /**
         * Alarm 디렉토리에서 알람 데이터를 읽어서 리스트에 담아 놓는다.
         */
        public bool almDataRead(string path)
        {
            bool sts = false;
            string year = DateTime.Today.ToString("yyyy");
            string mon = DateTime.Today.ToString("MM");
            string almPath = Path.Combine(path, year, mon);
            almPath = Path.Combine(almPath, "EOLID", "ALARM");

            // 디렉토리 내에 파일이 존재하는지 체크한다.
            if (Directory.Exists(almPath) == false)
            {
                Console.WriteLine("Directory not exist.... : {0}", almPath);
                log.Log("Directory not exist : " + almPath);
                log.Log("return false");
                //log("Directory not exist.... : " + path);
                return false;
            }

            // 파일이 존재하면 접근가능한지 체크한다.
            DirectoryInfo dir = new DirectoryInfo(almPath);

            foreach (FileInfo fi in dir.GetFiles().OrderBy(fi => fi.CreationTime))
            {
                AlmInfo info = new AlmInfo();

                info.f_name = fi.Name;
                info.f_full_name = fi.FullName;
                info.snd_chk = false;
                lstAlaram.Add(info);

                sts = true;
            }

            if (lstAlaram.Count == 0)
            {
               // log("Upload data does not exists");
                return false;
            }

            return sts;
        }
        /**
         * Alarm 데이터를 읽어서 수집서버에 전송한다.
         * 전송이 완료되면 읽은 상태를 변경한다.
         * 최종 마지막건에 대해서만 전송하고 나머지 건들은 전송 완료로 세팅한다.
         */
        public bool almDataSend()
        {
            bool sts = false;
            string bufStr = "";

            FileInfo fi = new FileInfo(lstAlaram[lstAlaram.Count - 1].f_full_name);     // 마지막 파일
            using (StreamReader sr = fi.OpenText())
            {
                string s = "";
                while((s = sr.ReadLine()) != null)
                {
                    bufStr += s;
                }
                string year = DateTime.Today.ToString("yyyy");
                string mon = DateTime.Today.ToString("MM");
                string rDate = year+"_"+mon+"_"+Path.GetFileNameWithoutExtension(fi.FullName);


                string msg = "AM:" + m_stCode + ":" + m_stHost+":"+rDate + ":" + bufStr;
                byte[] buf = Encoding.ASCII.GetBytes(msg);
                int stPort = System.Convert.ToInt32(m_stPort);
                try
                {
                    using (UdpClient c = new UdpClient(stPort+1))   /// m_cstLocalPort-1))  // source port (로컬 포트에서 상태 포트를 하나 사용하므로 중복이 발생하므로 사용포트 - 1)
                    {                        
                        c.Send(buf, buf.Length, m_stHost, stPort);
                        log.Log("Alarm data send (almDataSend :"+m_stHost+"["+stPort+"]) " + msg);
                        Console.WriteLine("Alaram send msg : " + msg);
                        main.setMsg("Alarm data send (almDataSend :"+m_stHost+"["+stPort+"])" + msg);

                        c.Client.ReceiveTimeout = 2000;     // 2 second
                        IPEndPoint ipepLocal = new IPEndPoint(IPAddress.Any, stPort+1);     // 10001 + 1
                        EndPoint remote = (EndPoint)ipepLocal;

                        byte[] rcvBuf = c.Receive(ref ipepLocal);
                        if (rcvBuf == null)
                        {
                            sts = false;
                        }
                        else
                        {
                            string data = Encoding.UTF8.GetString(rcvBuf);
                            string[] msgArr = data.Split(delimiterChar);
                            log.Log("Alarm receive msg(almDataSend) : " + data);
                            Console.WriteLine("Alaram get msg : " + data);
                            main.setMsg("Alarm receive msg(almDataSend) : " + data);

                            if (msgArr[3] == "ok")
                            {
                                sts = true;
                            }
                            else
                            {
                                sts = false;
                            }
                        }
                    }
                }catch(System.Net.Sockets.SocketException ex)
                {
                    sts = false;
                    Console.WriteLine(ex.ToString());
                    log.Log("Alarm data send error(almDataSend) : " + ex.ToString());
                    main.setMsg("Alarm data send error(almDataSend) : " + ex.ToString());
                }
            }

            if (sts == true)
            {
                for (int i=0; i<lstAlaram.Count; i++)
                {
                    AlmInfo info = lstAlaram[i];
                    info.snd_chk = true;
                    lstAlaram[i] = info;

                }
            }
            return sts;
        }

        public bool almDataBackup()
        {
            bool sts = false;

            try
            {
                for (int i = 0; i < lstAlaram.Count; i++)
                {
                    AlmInfo info = lstAlaram[i];

                    if (info.snd_chk == true)
                    {
                        string destFileName = info.f_full_name.Replace("ALARM", "BACKUP");
                        // backup 디렉토리 생성
                        string destDirName = Path.GetDirectoryName(destFileName);
                        // 디렉토리 내에 파일이 존재하는지 체크한다.
                        DirectoryInfo dir = new DirectoryInfo(destDirName); 
                        if (dir.Exists == false)
                        {
                            Console.WriteLine("Directory not exist.... : {0}", destDirName);
                            log.Log("Directory no exist : " + destDirName);
                            // 생성한다.
                            dir.Create();  // create folder
                        }

                        FileInfo file = new FileInfo(info.f_full_name);
                        file.MoveTo(destFileName);

                        sts = true;
                    }
                }                    
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.ToString());
                log.Log(ex.ToString());

                sts = false;
                main.setMsg("Alarm data error(almDataBackup) : " + ex.ToString());
            }
            
            return sts;
        }
    }
}
