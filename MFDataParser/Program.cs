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
            LoadParticipantData();
            PrintParticipants();



        }

        static void LoadParticipantData()
        {
            foreach(Participant p in Participants.Values)
            {
                LoadParticipantGameData(p);
                LoadParticipantHeadSetData(p);
            }
        }

        static void LoadParticipantGameData(Participant participant)
        { 
            foreach(string gameDataFile in participant.GameDataFiles)
            {
                int session = ParseSessionNumber(gameDataFile);
            }

        }

        static void LoadParticipantHeadSetData(Participant participant)
        {
            foreach(string headsetDataFile in participant.HeadetDataFiles)
            {

            }

        }




        static void PrintParticipants()
        {
            foreach (var kvp in Participants)
            {
                Console.WriteLine("{0}: {1}", kvp.Key, kvp.Value.ToString());
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
                    HeadetDataFiles = new List<String>(),
                    GameDataFiles = new List<String>()
                };
            }
            else throw new FormatException("Unable to convert participant Id string: " + participantNumber + " into int.");

        }

        static void SaveFilePathToParticipant(ref Participant participant, string file) {
            MFFileType fileType = ParseFileType(file);
            switch (fileType)
            {
                case MFFileType.GameData:
                    participant.GameDataFiles.Add(file);
                    break;
                case MFFileType.HeadSetData:
                    participant.HeadetDataFiles.Add(file);
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

        }

        static void runTest()
        {//may need to load in the contents of the directory
            pathToGameEventData = "C:/Users/root960/Desktop/MFData/1420008612_17_09Feb15_1122_game.csv";
            pathToHeadsetData = pathToGameEventData.Replace("game", "headset");


            //for each child, for each session, parse the child's session


            ParseSession(0);//doesn't really matter right now

        }


        static Session ParseSession(int sessionNumber)
        {
            var session = InitializeSession(sessionNumber);

            ParseGameStartTimesFor(session);

          
            ParseHeadsetDataFor(session);

            //check results
            //BTW: remember that you cannot define any methods or calculated fields on the anonymous type, nor could you successfully pass them to a method if you wanted to create a 
            //convenient "toString" method. I'm also not convinced that you would be able to save an anonymous type to a database in ORM.
            var games = from game in session.Games select new { MAR = game.MAR, Name = game.Name, SecondsDuration = game.TotalSeconds, NumPoorQual = game.NumPoorQuality };
            foreach (var item in games)
            {
                Console.WriteLine("Name: {0}. MAR: {1}. TotalTime: {2}. NumPoorQuality: {3}", item.Name, item.MAR, item.SecondsDuration, item.NumPoorQual);
            }

            return session;
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

        static void ParseFileFor(string pathToFile, Session session, Action<string[]> lineHandler, Action endOfFileHandler=null)
        {
            StreamReader reader = CreateReaderForFile(pathToFile);
            while (!reader.EndOfStream)
            {
                string[] eventData = reader.ReadLine().Split(',');
                lineHandler(eventData);
            }
            if(endOfFileHandler!=null) endOfFileHandler();
            reader.Close();
        }

        static void ParseGameStartTimesFor(Session session)
        {
            Game current;
            int idx = 0;
            Action<string[]> lineHandler = (string[] eventData) =>
            {
                if (eventData.Length == 2 && eventData[(int)EventDataIndex.EventType].Contains("started"))
                {
                    current = session.Games[idx];
                    current.Name = eventData[(int)EventDataIndex.EventType].Split(' ')[0];
                    current.StartTime = ParseDateTime(eventData[(int)EventDataIndex.EventTime]);
                    idx++;

                }
            };

            ParseFileFor(pathToGameEventData, session, lineHandler);

        }

        static void ParseHeadsetDataFor(Session session)
        {
             int idx = 0;
             Game currentGame = session.Games[idx];
          
            Action<string[]> lineHandler = (string[] headSetData) => {
                
                if (AtNextGame(headSetData, session, currentGame, idx))
                {
                    GetNextGame(ref currentGame, ref idx, session);
                }

                if (DataIsReliable(headSetData))
                {
                    ParseAOrRGivenGame(currentGame, headSetData, idx);
                } else
                {
                    currentGame.NumPoorQuality++;
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
                    builder.Append(prop.Name);
                    builder.Append(": ");
               
                    builder.Append(prop.GetValue(this));
                    builder.Append("\n");
                }
                AsString = builder.ToString();
            }

            return AsString;

        }
    }



    //Domain Models
    class Participant : MFEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Group { get; set; }
        public List<Session> Sessions { get; set; }

        public List<String> GameDataFiles { get; set; }
        public List<String> HeadetDataFiles { get; set; }

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
            builder.Append("Files:");
            foreach (string hsfile in HeadetDataFiles)
            {
                builder.Append("\t");
                builder.Append(hsfile);
            }
            foreach (string hsfile in GameDataFiles)
            {
                builder.Append("\t");
                builder.Append(hsfile);

            }

            return builder.ToString();

        }

    }



    class Session : MFEntity
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public Game[] Games { get; set; }

    }

    class Game : MFEntity
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public string Name { get; set;}
        public int MARSum { get; set; }
        public int MAR { get { return MARSum / NumGoodQuality; } }
        public int PARSum { get; set; }
        public double PAR { get; set; }
        public int TT { get; set; }
        public int NumPoorQuality { get; set; }
        public int TotalSeconds { get; set; }
        public int NumGoodQuality { get { return TotalSeconds - NumPoorQuality; } }
        




    }


  
}
