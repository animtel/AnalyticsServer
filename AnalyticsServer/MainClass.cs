using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnalyticsServer
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            new AnalyticsServer<JSONSerializer<Event>>(8080);
        }
    }
}
