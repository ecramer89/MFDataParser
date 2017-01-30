using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Data.Entity;
namespace MFDataParser
{
   enum MFFileType
    {
        GameData=0, HeadSetData=1
    }

    enum EventDataIndex
    {
        EventTime, EventType
    }

    enum HeadsetDataIndex
    {
        EventTime=0, SignalQuality=2, Relaxation=3, Attention=4
    }

    class Program
    {
        
        static Regex ParticipantIdRegex = new Regex("\\d{10}");
        static Regex HeadsetFileRegex = new Regex("headset");
        static Regex SessionNumberRegex = new Regex("(_)(\\d{1,2})(_)");

        static string pathToGameEventData = null;
        static string pathToHeadsetData = null;
        static Dictionary<String, Participant> Participants = new Dictionary<string, Participant>();

        static void Main(string[] args)
        {

            LoadParticipants();
            LoadParticipantsData();
            PrintParticipants();



        }

        static void LoadParticipantsData()
        {
            foreach(Participant p in Participants.Values)
            {
                LoadParticipantData(p);
            }
        }

     

        static void LoadParticipantData(Participant p)
        {
            foreach(var sessionAndFiles in p.DataFilesForSession)
            {
                Session session;
                ParseDataForSession(out session, sessionAndFiles.Key, sessionAndFiles.Value);
                p.Sessions.Add(session);
            }
        }


        static void PrintParticipants()
        {
          
            foreach (var kvp in Participants)
            {
                Console.WriteLine("Participant: {0} {1}", kvp.Key, kvp.Value.ToString());
            }
        }


        static void LoadParticipants()
        {
            //load directories
            string[] files = Directory.GetFiles("C:/Users/root960/Desktop/MFData/test");

      
            foreach(string file in files)
            {
                //parse the participant number.
                string participantNumber = ParseParticipantNumber(file);
                //if we have a participant object associated with this number, then get it.
                Participant participant;
                if (!Participants.TryGetValue(participantNumber, out participant))
                {
                    InitializeParticipant(participantNumber, out participant);
                    Participants.Add(participantNumber, participant);
                }

                SaveFilePathToParticipant(ref participant, file);
               
            }
          
        }


        static void InitializeParticipant(string participantNumber, out Participant participant)
        {
            int IdAsInt;
         
            if (Int32.TryParse(participantNumber, out IdAsInt))
            {
                participant = new Participant
                {
                    Id = IdAsInt,
                    Sessions = new List<Session>(),
                    DataFilesForSession = new Dictionary<int, SessionDataFiles>(),
                  
                };
            }
            else throw new FormatException("Unable to convert participant Id string: " + participantNumber + " into int.");

        }

        static void SaveFilePathToParticipant(ref Participant participant, string file) {
            int sessionNumber = ParseSessionNumber(file);
            SessionDataFiles dataFiles;
            if (!participant.DataFilesForSession.TryGetValue(sessionNumber, out dataFiles))
            {
                dataFiles = new SessionDataFiles();
                participant.DataFilesForSession.Add(sessionNumber, dataFiles);
            }

            MFFileType fileType = ParseFileType(file);
            switch (fileType)
            {
                case MFFileType.GameData:
                    dataFiles.GameDataFile = file;
                    //participant.GameDataFiles.Add(file);
                    break;
                case MFFileType.HeadSetData:
                    dataFiles.HeadsetDataFile = file;
                   // participant.HeadetDataFiles.Add(file);
                    break;
            }
        }

        static string ParseParticipantNumber(string file)
        {

            return ParticipantIdRegex.Match(file).ToString();
        }

        static MFFileType ParseFileType(string file)
        {
            return (HeadsetFileRegex.IsMatch(file) ? MFFileType.HeadSetData : MFFileType.GameData);
        }

        static int ParseSessionNumber(string filePath)
        {
            string sessionNumberAsString = SessionNumberRegex.Match(filePath).ToString().Replace("_", "");
            int sessionNumber;
            if (Int32.TryParse(sessionNumberAsString, out sessionNumber))
            {
                return sessionNumber;
            }
            else throw new FormatException("Error attempting to pare session number from string: " + sessionNumberAsString);
        }

    

        static void ParseDataForSession(out Session session, int sessionNumber, SessionDataFiles sessionDataFiles)
        {
            session = InitializeSession(sessionNumber);
            ParseGameStartTimesFor(session, sessionDataFiles.GameDataFile);
            ParseHeadsetDataFor(session, sessionDataFiles.HeadsetDataFile);

        }

      

        static Session InitializeSession(int sessionNumber)
        {
            var session = new Session
            {
                Number = sessionNumber, //we would know this, having constructed the path to the session data, for now just hard code

                Games = new Game[]{
                     new Game
                    {
                        Name="Pinwheel",
                     
                    },
                    new Game
                    {
                        Name="Paraglider",
                        
                    },
                    new Game
                    {
                        Name="Pinwheel"
                      
                    }

                }

            };

            return session;
        }

        static void ParseFileFor(string pathToFile, Session session, Action<string[]> lineHandler)
        {
            StreamReader reader = CreateReaderForFile(pathToFile);
            while (!reader.EndOfStream)
            {
                string[] eventData = reader.ReadLine().Split(',');
                lineHandler(eventData);
            }
          
            reader.Close();
        }

        static void ParseGameStartTimesFor(Session session, string pathToGameEventData="")
        {
            Game current;
            int currentGameIdx = 0;
            Action<string[]> lineHandler = (string[] eventData) =>
            {
                if (IsStartOfNewGame(eventData))
                {   //idx starts at 0, not ++idx, because we need to account for the first row, which indicates the start of the first game.
                    current = session.Games[currentGameIdx];
                    current.Name = ParseNameOfGameFromGameStartedEvent(eventData);
                    current.StartTime = ParseDateTime(eventData[(int)EventDataIndex.EventTime]);
                    currentGameIdx++;

                }
            };

            ParseFileFor(pathToGameEventData, session, lineHandler);

        }

        static string ParseNameOfGameFromGameStartedEvent(string[] eventData)
        {
            return eventData[(int)EventDataIndex.EventType].Split(' ')[0];
        }

        static bool IsStartOfNewGame(string[] eventData)
        {
            return eventData.Length == 2 && eventData[(int)EventDataIndex.EventType].Contains("started");
        }

        static void ParseHeadsetDataFor(Session session, string pathToHeadsetData)
        {
             int currentGameIdx = 0;
             Game currentGame = session.Games[currentGameIdx];
          
            Action<string[]> lineHandler = (string[] headSetData) => {
                
                if (AtNextGame(headSetData, session, currentGame, currentGameIdx))
                {
                    GetNextGame(ref currentGame, ref currentGameIdx, session);
                }

                if (DataIsReliable(headSetData))
                {
                    ParseAOrRGivenGame(currentGame, headSetData, currentGameIdx);
                } else
                {
                    currentGame.SecondsPoorQuality++;
                }

                currentGame.TotalSeconds++;
            };

        

            ParseFileFor(pathToHeadsetData, session, lineHandler);

        }

        static StreamReader CreateReaderForFile(string filePath)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);

            StreamReader reader = new StreamReader(fs);

            return reader;
        }


       

       static bool AtNextGame(string[] headSetData, Session session, Game currentGame, int idx)
        {
            DateTime time = ParseDateTime(headSetData[(int)HeadsetDataIndex.EventTime]);
            return ((idx + 1) < session.Games.Count() && time.CompareTo(session.Games[idx + 1].StartTime) >= 0) ;

        }

    
   

        static void GetNextGame(ref Game currentGame, ref int idx, Session session)
        {
            currentGame = session.Games[++idx];
        }

        static bool DataIsReliable(string[] headSetData)
        {
            string signalQualityString = headSetData[(int)HeadsetDataIndex.SignalQuality];
            signalQualityString = signalQualityString.Substring(9, signalQualityString.Length - 9);
            int quality;
            if(Int32.TryParse(signalQualityString, out quality))
            {
                return quality == 0;
            } throw new FormatException("Unable to read signal quality value.");

        }


        static DateTime ParseDateTime(string timeString)
        {
            
            string date = timeString.Substring(0, 8);
            string time = timeString.Substring(9, 8);

            DateTime dateTime = DateTime.ParseExact(date, "dd-MM-yy", null, System.Globalization.DateTimeStyles.None);

            TimeSpan timeSpan = TimeSpan.ParseExact(time, "hh\\:mm\\:ss", null, System.Globalization.TimeSpanStyles.None);
            dateTime = dateTime.Add(timeSpan);
            return dateTime;

        }


        static void ParseAOrRGivenGame(Game currentGame, string[] headSetData, int idx)
        {
            int valueIdx = (int)(currentGame.Name == "Rock" ? HeadsetDataIndex.Attention : HeadsetDataIndex.Relaxation);
            string dataString = headSetData[valueIdx];
            int value;
            if (Int32.TryParse(dataString.Substring(5, dataString.Length - 5), out value))
               {
                currentGame.MARSum += value;
                
            
               }
   
        }

        
    }

 


class MFEntity
    {
        protected string AsString = null;

        public override string ToString()
        {
            if (AsString == null)
            {
                StringBuilder builder = new StringBuilder();
                var props = System.Reflection.Assembly.GetExecutingAssembly().GetType(this.GetType().ToString()).GetProperties();
                foreach (var prop in props)
                {
                    builder.Append("\n\t");
                    builder.Append(prop.Name);
                    builder.Append(": ");
               
                    builder.Append(prop.GetValue(this));
                  
                }
                AsString = builder.ToString();
            }

            return AsString;

        }
    }

    class SessionDataFiles
    {
        public string GameDataFile { get; set; }
        public string HeadsetDataFile { get; set; }

        public override string ToString()
        {
            return "Game Data File: "+GameDataFile+"\n\t\tHeadset Data File: " + HeadsetDataFile;
        }
    }


    //Domain Models
    class Participant : MFEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Group { get; set; }
        public List<Session> Sessions { get; set; }

        public Dictionary<int, SessionDataFiles> DataFilesForSession;


        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType()) return false;
            Participant other = (Participant)obj;
            return other.Id == other.Id;
        }


        public override int GetHashCode()
        {

            return Id;
        }


        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.ToString());
            builder.Append("\n");
            builder.Append("Files: ");
            foreach (var sessionData in DataFilesForSession)
            {
                builder.Append("\n\tSession Number: ");
                builder.Append(sessionData.Key);
                builder.Append("\n\t\t");
                builder.Append(sessionData.Value.ToString());
            }
            builder.Append("\nSessions: ");
            foreach(Session session in Sessions)
            {
                builder.Append("\nSession:");
                builder.Append(session.ToString());
            }
       
            return builder.ToString();

        }

    }



    class Session : MFEntity
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public Game[] Games { get; set; }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.ToString());
            builder.Append("\nGame Data: ");
            foreach(Game game in Games)
            {
                builder.Append("\nGame:");
                builder.Append(game.ToString());
            }


            return builder.ToString();
        }

    }

    class Game : MFEntity
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public string Name { get; set;}
        public int MARSum { get; set; }
        public int MAR { get { return (SecondsGoodQuality > 0 ? MARSum / SecondsGoodQuality : 0); } }
        public int PARSum { get; set; }
        public double PAR { get; set; }
        public int TT { get; set; }
        public int SecondsPoorQuality { get; set; }
        public int TotalSeconds { get; set; }
        public int SecondsGoodQuality { get { return TotalSeconds - SecondsPoorQuality; } }


    }


  
}
