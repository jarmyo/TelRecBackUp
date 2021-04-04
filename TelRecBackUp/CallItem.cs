using System;

namespace Repos.TelRecBackUp
{
    public class CallItem
    {                
        public System.DateTime CallDate { get; set; }
        public System.TimeSpan Lenght { get; set; }
        public int IdRecorder { get; set; }
        public short Channel { get; set; }                        
        public string Url { get; set; }
    }
}