using System.Collections.Generic;

namespace GoogleDynamicDns
{
    public class AppConfig
    {
        public string ipProviderUrl;
        public int timeInterval;

        public int heartBeat;

        public List<Host> hosts;

        public string mailDomain;

        public string mailKey;
    }
}
