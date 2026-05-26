namespace EsnafPos.Network
{
    public enum AppMode { Standalone, Server, Client }

    public class NetworkSettings
    {
        public AppMode Mode       { get; set; } = AppMode.Standalone;
        public string  ServerIp   { get; set; } = "";
        public int     ServerPort { get; set; } = 5150;
        public string  ApiUsername{ get; set; } = "esnafpos";
        public string  ApiPassword{ get; set; } = "esnafpos123";
    }
}
