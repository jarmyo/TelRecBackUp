using System;

namespace Repos.TelRecBackUp
{
    public class CallItem
    {
        public CallItem(DateTime __DateTime, short channel, double __lenght)
        {
            CallDate = __DateTime;            
            Channel = channel;            
            Lenght = TimeSpan.FromSeconds(__lenght);            
        }

        public System.DateTime CallDate { get; set; }
        public System.TimeSpan Lenght { get; set; }
        public int IdRecorder { get; set; }
        public int IndexRecorder { get; set; }
        public short Channel { get; set; }                        
        public string Url { get; set; }
        public string LocalPath { get; set; }
        
        public int RecordOffset { get; set; }
    }
}