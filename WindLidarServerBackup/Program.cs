using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindLidarServerBackup
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine("Start.............");
            BackupCls cls = new BackupCls();

            Console.WriteLine("Backup called....");
            cls.process();
        }
    }
}
