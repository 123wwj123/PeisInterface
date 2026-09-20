using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SpdService
{
    static class Program
    {
        public static string gstrToken = "";          //羚云LIS业务Token
        public static DateTime gdtToken = System.DateTime.Now;         //羚云LISToken有效时间

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
    }
}
