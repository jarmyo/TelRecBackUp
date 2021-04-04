using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Timers;

namespace Repos.TelRecBackUp
{
    //Series of functions to auto-backup records from severals TelRec Devices.
    public class TelRecManager
    {
        private static bool AllRecordersHasTheSamePassword;
        private static bool IsChecking = false;
        private static string RecordersUser = "Userfoo";
        private static string RecordersPassword = "PasswordBar";
        private static List<RecorderItem> Recorders = new List<RecorderItem>();
        private const int MinutesToCheck = 5; //change this line according to your needs
        private const int StartCheckInYear = 18; //change this line according to your needs
        private const int MaxChannels = 4; //this is for NAR6100
        private const string StoreFolder = "F:\\TelRecBackups\\";
        private BackgroundWorker _hiloFinDeDia = new BackgroundWorker();

        public TelRecManager()
        {
            TelRecInterface.Init();
            AllRecordersHasTheSamePassword = false;
        }

        public TelRecManager(string user, string password)
        {
            TelRecInterface.Init();
            RecordersUser = user;
            RecordersPassword = password;
            AllRecordersHasTheSamePassword = true;
        }

        public void StartBackUpCheck()
        {
            if (_hiloFinDeDia != null)
            {
                _hiloFinDeDia.CancelAsync();
            }

            _hiloFinDeDia = new BackgroundWorker();
            this._hiloFinDeDia.WorkerSupportsCancellation = true;
            this._hiloFinDeDia.WorkerReportsProgress = true;
            this._hiloFinDeDia.DoWork += delegate {
                
                SearchForNewCalls();
            
            };

            var _timerSync = new Timer
            {
                Interval = MinutesToCheck * 60000, /*por minuto*/
                Enabled = true,
            };
            _timerSync.Elapsed += delegate
            {

                if (!IsChecking)
                {
                    _hiloFinDeDia.RunWorkerAsync();                    
                }

            };
        }

        public void StopBackUpCheck()
        {
            if (IsChecking)
            {
                IsChecking = false;
            }
        }

        /// <summary>
        /// Add a new recorder to Recorder list
        /// </summary>
        /// <param name="serial">TelRec Device ID</param>
        /// <param name="ipAddress">TelRec IPAddress</param>
        /// <param name="user">Username</param>
        /// <param name="password">Password</param>
        /// <returns>A RecorderItem object with ConnectStatus property set</returns>
        public RecorderItem AddRecorderAndConnect(string serial, string ipAddress, string user = "admin", string password = "admin")
        {
            //TODO: verify if IP adress is correct.
            var recorder = new RecorderItem() { Serial = serial, IPAddress = ipAddress };
            recorder.TelRecId = TelRecInterface.CreateDevice(serial);

            //Try to connect
            if (recorder.TelRecId != 0)
            {
                user = AllRecordersHasTheSamePassword ? RecordersUser : user;
                password = AllRecordersHasTheSamePassword ? RecordersPassword : password;

                if (TelRecInterface.Login(recorder.TelRecId, ipAddress, 6066,
                                         user, password, false) == TelRecInterface.TelRecErrno.Succeed)
                {
                    Debug.WriteLine($"Recorder Connected! [{ipAddress}]");
                    SaveLastRecord(recorder.TelRecId);
                    recorder.ConnectStatus = TelRecInterface.ConnectStatusType.Connected;
                    TelRecInterface.CreateHeartbeatThread(recorder.TelRecId, HeartThread);
                }
                else
                {
                    recorder.ConnectStatus = TelRecInterface.ConnectStatusType.NotConnected;
                    Debug.WriteLine($"Recorder NOT Connected: [{ipAddress}]");
                }
            }

            Recorders.Add(recorder);
            return recorder;
        }

        /// <summary>
        /// Check for new calls, download and return a list of new calls.
        /// </summary>
        /// <returns></returns>
        public static List<CallItem> SearchForNewCalls()
        {
            IsChecking = true;
            var newCallslist = new List<CallItem>();
            Debug.WriteLine(DateTime.Now.TimeOfDay + " Backup Proccess Start");
            foreach (var _recorder in Recorders)
            {
                if (_recorder.ConnectStatus == TelRecInterface.ConnectStatusType.Connected)
                {
                    Debug.WriteLine("Checking:" + _recorder.IPAddress);
                  
                    int CurrentYear = DateTime.Now.Year - 2000;

                    for (int _year = StartCheckInYear; _year <= CurrentYear; _year++)
                    {
                        for (int _month = 1; _month <= 12; _month++)
                        {
                            byte[] DayArray = new byte[32];
                            TelRecInterface.GetDayListFromMonthDir(_recorder.TelRecId, _year, _month, DayArray);
                            for (int _day = 0; _day < 32; _day++)
                            {
                                if (DayArray[_day] > 0)
                                {
                                    for (short channel = 1; channel <= MaxChannels; channel++)
                                    {
                                        var _folderName = $"{_year:00}{_month:00}/{_day:00}/CH{channel:00}";

                                        TelRecInterface.TelRecErrno Errno;
                                        int IndexDownloadCount = 0, IndexFileSize = 0;
                                        byte[] IndexFileBuffer = null;
                                        Errno = TelRecInterface.DownloadFile(_recorder.TelRecId, $"/RecordFiles/{_folderName}.Index",
                                        (int Device, byte[] IndexData, int IndexLength) =>
                                        {
                                            if (IndexData == null)
                                            {
                                                IndexFileSize = IndexLength;
                                                IndexFileBuffer = new byte[IndexFileSize];
                                            }
                                            else
                                            {
                                                Array.Copy(IndexData, 0, IndexFileBuffer, IndexDownloadCount, IndexLength);
                                                IndexDownloadCount += IndexLength;
                                            }
                                            return false;
                                        });

                                        int ItemCount;
                                        ItemCount = (IndexFileSize - 512) / 128;
                                        if (ItemCount > 0)
                                        {
                                            for (int currentRecord_ = 0; currentRecord_ < ItemCount; currentRecord_++)
                                            {

                                                if ((IndexFileBuffer[currentRecord_ / 8] & (1 << (currentRecord_ % 8))) > 0)
                                                {

                                                    var offset = 512 + (currentRecord_ * 128);

                                                    var __year = IndexFileBuffer[offset];
                                                    var __month = IndexFileBuffer[offset + 1];
                                                    var __day = IndexFileBuffer[offset + 2];
                                                    var __hour = IndexFileBuffer[offset + 3];
                                                    var __minutes = IndexFileBuffer[offset + 4];
                                                    var __seconds = IndexFileBuffer[offset + 5];
                                                    var __DateTime = new DateTime(2000 + _year, __month, __day, __hour, __minutes, __seconds);
                                                    var __type = IndexFileBuffer[offset + 6];
                                                    var __lenght = (256 * IndexFileBuffer[offset + 9]) + IndexFileBuffer[offset + 8];

                                                    var dataname = $"{__year}{__month:00}{__day:00)}{__hour:00}{__minutes:00}{__seconds:00}-CH{channel:00}";
                                                    var _localAudioFileName = $"{StoreFolder}{_recorder.Serial}\\{_folderName}\\{dataname}.wav";
                                                    var _fileAlteadyExist = File.Exists(_localAudioFileName);


                                                    if (__lenght > 60) //just files of one minute or more
                                                    {

                                                        byte[] AudioFileBuffer = null;
                                                        int AudioDownloadCount = 0;
                                                        int AudioFileSize = 0;


                                                        if (_fileAlteadyExist)
                                                        {
                                                            Errno = TelRecInterface.EditRecordNotes(_recorder.TelRecId, currentRecord_, __year, __month, __day, channel - 1, "saved");

                                                            var callitem = new CallItem();
                                                            callitem.CallDate = __DateTime;
                                                            callitem.IdRecorder = _recorder.TelRecId;
                                                            callitem.Channel = channel;
                                                            callitem.Url = _recorder.Serial + "/" + _folderName + "/" + dataname;
                                                            callitem.Lenght = TimeSpan.FromSeconds(__lenght);
                                                            newCallslist.Add(callitem);
                                                            OnNewRecordFound(callitem);
                                                        }
                                                        else
                                                        {
                                                            Debug.WriteLine("Download file: " + _localAudioFileName);
                                                            Errno = TelRecInterface.DownloadFile(_recorder.TelRecId, $"/RecordFiles/{_folderName}/{dataname}.wav",
                                                            (int Device, byte[] AudioData, int AudioLength) =>
                                                            {
                                                                if (AudioData == null)
                                                                {
                                                                    AudioFileSize = AudioLength;
                                                                    AudioFileBuffer = new byte[AudioFileSize];
                                                                }
                                                                else
                                                                {
                                                                    Array.Copy(AudioData, 0, AudioFileBuffer, AudioDownloadCount, AudioLength);
                                                                    AudioDownloadCount += AudioLength;

                                                                    if (AudioDownloadCount == AudioFileSize)
                                                                    {
                                                                        if (!Directory.Exists(StoreFolder + _recorder.Serial + @"\" + _folderName))
                                                                        {
                                                                            Directory.CreateDirectory(StoreFolder + _recorder.Serial + @"\" + _folderName);
                                                                        }

                                                                        File.WriteAllBytes(_localAudioFileName, AudioFileBuffer);

                                                                        Errno = TelRecInterface.EditRecordNotes(_recorder.TelRecId, currentRecord_, __year, __month, __day, channel - 1, "saved");
                                                                        var callitem = new CallItem();
                                                                        callitem.CallDate = __DateTime;
                                                                        callitem.IdRecorder = _recorder.TelRecId;
                                                                        callitem.Channel = channel;
                                                                        callitem.Url = _recorder.Serial + "/" + _folderName + "/" + dataname;
                                                                        callitem.Lenght = TimeSpan.FromSeconds(__lenght);
                                                                        newCallslist.Add(callitem);
                                                                        OnNewRecordFound(callitem);

                                                                    }

                                                                }
                                                                return false;//return true will stop download
                                                                });
                                                        }

                                                    }
                                                    else
                                                    {
                                                        // 
                                                        //sino, borrar el archivo y su registro.

                                                        ////si hay un archivo, borrar
                                                        //if (_fileExist)
                                                        //{
                                                        //    File.Delete(path_1 + _recorder.Serie + @"\" + _folderName + @"\" + dataname + ".WAV");
                                                        //}
                                                        //Debug.WriteLine("BORRAR " + audioFilename + " => " + __lenght);
                                                        //TelRecInterface.TelRecRecordDeleteItem Item = new TelRecInterface.TelRecRecordDeleteItem()
                                                        //{
                                                        //    HasWavFile = false,
                                                        //    Channel = channel - 1,
                                                        //    Day = __day,
                                                        //    Hour = __hour,
                                                        //    Minutes = __minutes,
                                                        //    Month = __month,
                                                        //    Year = __year,
                                                        //    Seconds = __seconds,
                                                        //    Offset = currentRecord_
                                                        //};

                                                        //Errno = TelRecInterface.DeleteRecord(_recorder.TelRecId, Item);
                                                        //Errno = TelRecInterface.RemoveFile(_recorder.TelRecId, audioFilename);

                                                    }
                                                    // obtener datos del archivo
                                                }
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }

                }
                else
                {
                    //TODO: Tratar de reconectar.

                    // Primero Ping.
                    // Si hay ping, tratar de logear.

                    // Si no, Contador de reintentos, si son varios, mandar correo.

                }
                Debug.WriteLine(DateTime.Now.ToString() + " Checked: " + _recorder.IPAddress);
            }
            Debug.WriteLine(DateTime.Now.TimeOfDay + " Backup Proccess end");
            OnBackupEnd();
            IsChecking = false;
            Debug.WriteLine("Found: " + newCallslist.Count + " new records");
            return newCallslist;
        }


        public delegate void RecordHandler(CallItem item);
        public delegate void RecorderHandler(int idDevice);
        public delegate void VoidHandler();

        public static event RecordHandler OnNewRecordFound;
        public static event RecorderHandler OnSaveLastRecord;
        public static event VoidHandler OnBackupEnd;

        public static void SaveLastRecord(int Device)
        {
            // Change
            OnSaveLastRecord(Device);
        }


        private void HeartThread(TelRecInterface.TelRecEventType Event, int Device, int Channel, int Arg)
        {
            switch (Event)
            {
                case TelRecInterface.TelRecEventType.ConnectStatusChanged:
                    {
                        var newStatus = TelRecInterface.ConnectStatus(Device);
                        Recorders.Find(r => r.TelRecId == Device).ConnectStatus = newStatus;
                        break;
                    }
                case TelRecInterface.TelRecEventType.StorageStatusChanged:
                    {
                        if (TelRecInterface.StorageStatus(Device).Status == TelRecInterface.StorageStatusType.Fill)
                        {
                            //TODO: here, implement action if storage is full
                        }
                        break;
                    }
            }
        }



    }
}