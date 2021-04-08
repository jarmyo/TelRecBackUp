namespace Repos.TelRecBackUp
{
    public class RecorderItem
    {
        public int Id;
        public string Serial;
        public string IPAddress;
        public int TelRecId;
        public TelRecInterface.ConnectStatusType ConnectStatus;
        public System.DateTime LastCheck= new System.DateTime(2018,1,1);
    }
}