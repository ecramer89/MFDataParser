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

    enum ParticipantIDToNameAndGroupIndex
    {
        ID=0, Name=1, Group=2
    }

    enum EventDataType
    {

        StartGame = 0, GotToken = 1, Other = 2

    }
      
    
    

    enum EventDataIndex
    {
        EventTime, EventType
    }

    enum HeadsetDataIndex
    {
        EventTime=0, SignalQuality=2, Relaxation=3, Attention=4
    }

    class GameType
    {
        //constants are implicitly static
        private const string RockName = "Rock";
        private const string PinName = "Pinwheel";
        private const string ParaName = "Paraglider";

        public static readonly GameType Rock =new GameType(RockName);
        public static readonly GameType Pinwheel = new GameType(PinName);
        public static readonly GameType Paraglider = new GameType(ParaName);
        public string Name { get; }
        private GameType(string name)
        {
            this.Name = name;
        }

        public static GameType StringToGameType(string gameTypeName)
        {
            switch (gameTypeName)
            {
                case RockName:
                    return Rock;
                   
                case PinName:
                    return Pinwheel;

                case ParaName:
                    return Paraglider;

                default:
                    return null;
            }
        }
    }

    class ParticipantNameAndGroup
    {
        public string Name { get; set; }
        public int Group { get; set; }
    }



    class Program
    {
        static bool TESTING = true;
        private const int DURATION_OF_ONE_EVENT = 1;
        private const string UNKNOWN_PARTICIPANT_PREFIX = "UK";
        private const string OUTPUT_CSV_COLUMN_HEADINGS = "Name,ID,Group,Session,Game,SecondsPoorSignal,SecondsTotal";
        private const string OUTPUT_CSV_VALUE_STRING_FORMAT = "{0},{1},{2},{3},{4},{5},{6}";
        static string INPUT_DATA_FILE_DIRECTORY_PATH = "C:/Users/root960/Desktop/MFData/"+(TESTING? "testSet" : "files");
        static Regex ParticipantIdRegex = new Regex("\\d{10}");
        static Regex HeadsetFileRegex = new Regex("headset");
        static Regex SessionNumberRegex = new Regex("(_)(\\d{1,2})(_)");
        static Regex DateRegex = new Regex("\\d{2,4}-\\d{1,2}-\\d{1,2}");
        static Regex TimeRegex = new Regex("\\d{1,2}:\\d{1,2}(:\\d{0,2})?");
        static Regex DuplicateFileRegex = new Regex("(\\(\\d\\))$");
        static Regex UnknownParticipantRegex = new Regex(UNKNOWN_PARTICIPANT_PREFIX+"\\d+");

        static string OutputFilePath = "C:/Users/root960/Desktop/MFData/"+(TESTING? "TestOutput" : "Output")+".csv";
        static string PathToParticipantIdToNameAndGroupFile= "C:/Users/root960/Desktop/MFData/ParticipantIdToNameAndGroup.csv";
        //nb for dup ids, just add them as additional keys linking to that participant name and data files. then when we sort the sheet it will get sorted out
        static Dictionary<int, ParticipantNameAndGroup> ParticipantNameAndGroupLookup = new Dictionary<int, ParticipantNameAndGroup>();
        static Dictionary<String, Participant> Participants = new Dictionary<string, Participant>();
        static int SerializedUnknownParticipant = 0;

        static void Main(string[] args)
        {
            LoadParticipantIDToNameAndGroupData();
            LoadParticipants();
            LoadParticipantsData();
            WriteParticipantsDataToNewCSVFile();
        }

        static void WriteParticipantsDataToNewCSVFile()
        {
            StringBuilder CSVBuilder = new StringBuilder();
            CSVBuilder.AppendLine(OUTPUT_CSV_COLUMN_HEADINGS);
            foreach(Participant participant in Participants.Values)
            {   //skip data from participants we couldn't identify
                if (!UnknownParticipantRegex.IsMatch(participant.Name))
                {
                    WriteDataForOneParticipantToCSVFile(participant, ref CSVBuilder);
                }

            }

            File.WriteAllText(OutputFilePath, CSVBuilder.ToString());
        }

        static void WriteDataForOneParticipantToCSVFile(Participant participant, ref StringBuilder CSVBuilder)
        {
            foreach (Session session in participant.Sessions)
            {
               
                HashSet<GameType> recorded = new HashSet<GameType>();

                for (int i = session.Games.Count() - 1; i > -1; i--)
                {
                    SessionGame current = session.Games.ElementAt(i);
                    GameType currentType = GameType.StringToGameType(current.Name);
                    if (WasParticipantsLastAttemptAtGameForThisSession(recorded, currentType))
                    {   
                        string row = String.Format(OUTPUT_CSV_VALUE_STRING_FORMAT, participant.Name, participant.Id, participant.Group, session.Number, current.Name, current.SecondsPoorQuality, current.TotalSeconds);
                        CSVBuilder.AppendLine(row);
                      
                        recorded.Add(currentType);

                    }
                }
            }

        }

        static bool WasParticipantsLastAttemptAtGameForThisSession(HashSet<GameType> recorded, GameType currentType)
        {
            return !recorded.Contains(currentType);
        }

     


        static void LoadParticipantIDToNameAndGroupData()
        {
          
            Action<string[]> addIdAndNameGroupToDictionary = (string[] eventData) => {

                int participantId;
                if(!Int32.TryParse(eventData[(int)ParticipantIDToNameAndGroupIndex.ID], out participantId)){
                    throw new FormatException("Error attempting to parse Participant Id from ID to Name and Group sheet.");
                }

                string participantName = eventData[(int)ParticipantIDToNameAndGroupIndex.Name];

                int participantGroup;
                if (!Int32.TryParse(eventData[(int)ParticipantIDToNameAndGroupIndex.Group], out participantGroup)){
                    throw new FormatException("Error attempting to parse Participant Group from ID to Name and Group sheet.");
                }

                ParticipantNameAndGroup nameAndGroup= new ParticipantNameAndGroup
                {
                    Name=participantName,
                    Group=participantGroup
                };

                try
                {
                    ParticipantNameAndGroupLookup.Add(participantId, nameAndGroup);
                }catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine("Duplicate Id was: {0}, Name was: {1}", participantId, participantName, participantGroup);
                }

            };

            ParseCSVFileFor(PathToParticipantIdToNameAndGroupFile, addIdAndNameGroupToDictionary);
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
            string[] files;
          
            try
            {
                files = Directory.GetFiles(INPUT_DATA_FILE_DIRECTORY_PATH);
                Console.WriteLine("Number of files: "+files.Count());
            }
            catch (Exception e)
            {
                throw new Exception(e.Message+"\nCheck that the string identifying the root directory of the MF files is accurate.");
            }

            int countfiles = 0;
            StringBuilder rejectedFiles = new StringBuilder();
            foreach (string file in files)
            {
                if (!IsADuplicateFile(file) && IsAParticipantDataFile(file))
                {
                    InitializeNewParticipantForFileOrSaveFileToExistingParticipant(file);
                    countfiles++;
                }else
                {
                    rejectedFiles.AppendLine(file);
                }
            }

            File.WriteAllText("C:/Users/root960/Desktop/MFData/skipped.csv", rejectedFiles.ToString());
            Console.WriteLine("Num participant files " + countfiles);
          
        }

        static void InitializeNewParticipantForFileOrSaveFileToExistingParticipant(string file)
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

        static bool IsADuplicateFile(string fileName)
        {
            return DuplicateFileRegex.IsMatch(fileName);
        }

        static bool IsAParticipantDataFile(string fileName)
        {
            return ParticipantIdRegex.IsMatch(fileName); 
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


                ParticipantNameAndGroup nameAndGroup;
                if (ParticipantNameAndGroupLookup.TryGetValue(IdAsInt, out nameAndGroup))
                {
                    participant.Name = nameAndGroup.Name;
                    participant.Group = nameAndGroup.Group;

                }
                else
                {
                    participant.Name = UNKNOWN_PARTICIPANT_PREFIX + SerializedUnknownParticipant++;
                    Console.WriteLine("Participant ID {0} was not registered in the Id to Name and Group Dictionary.", IdAsInt);
                   
                }

            }
            else throw new FormatException("Unable to convert participant Id string: " + participantNumber + " into int.");

        }



        static void SaveFilePathToParticipant(ref Participant participant, string file) {
            int sessionNumber = ParseSessionNumber(file);
            SessionDataFiles participantDataFilesForSession;
            GetOrInitializeDataFilesObjectForParticipantAndSession(out participantDataFilesForSession, ref participant, sessionNumber);
            SetOrOverwriteGameOrHeadsetDataFileForParticipantAndSession(file, ref participantDataFilesForSession);
        }

        static void GetOrInitializeDataFilesObjectForParticipantAndSession(out SessionDataFiles participantDataFilesForSession, ref Participant participant, int sessionNumber)
        {
            if (!participant.DataFilesForSession.TryGetValue(sessionNumber, out participantDataFilesForSession))
            {
                participantDataFilesForSession = new SessionDataFiles();

                participant.DataFilesForSession.Add(sessionNumber, participantDataFilesForSession);
            }

        }

        static void SetOrOverwriteGameOrHeadsetDataFileForParticipantAndSession(string file, ref SessionDataFiles participantDataFilesForSession)
        {
            MFFileType fileType = ParseFileType(file);
            switch (fileType)
            {
                case MFFileType.GameData:
                    participantDataFilesForSession.GameDataFile = file;
                    break;
                case MFFileType.HeadSetData:
                    participantDataFilesForSession.HeadsetDataFile = file;
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
            else throw new FormatException("Error attempting to parse session number from string: " + sessionNumberAsString);
        }

    

        static void ParseDataForSession(out Session session, int sessionNumber, SessionDataFiles sessionDataFiles)
        {
            session = InitializeSession(sessionNumber);
            if (sessionDataFiles.HasBothFiles())
            {
                InitializeSessionGamesAndStartTimesFromFile(session, sessionDataFiles.GameDataFile);
                ParseHeadsetDataFor(session, sessionDataFiles.HeadsetDataFile);
            }

        }

        static Session InitializeSession(int sessionNumber)
        {
            var session = new Session
            {
                Number = sessionNumber, //we would know this, having constructed the path to the session data, for now just hard code

                Games = new List<SessionGame>()

            };

            return session;
        }

        static void ParseCSVFileFor(string pathToFile, Action<string[]> lineHandler)
        {
            StreamReader reader=CreateReaderForFile(pathToFile);
            

            while (!reader.EndOfStream)
            {
                string[] eventData = reader.ReadLine().Split(',');
                lineHandler(eventData);
            }
          
            reader.Close();
        }

        static void InitializeSessionGamesAndStartTimesFromFile(Session session, string pathToGameEventData = "")
        {
        
            SessionGame currentGame = null;
            TimeInterval currentGamePlayTimeInterval = null;


            Action<string[]> lineHandler = (string[] eventData) =>
            {
                EventDataType eventType = EventDataToEventType(eventData);
                switch (eventType)
                {
                    case EventDataType.StartGame:
                        string gameName = ParseNameOfGameFromGameStartedEvent(eventData);
                            if (IsNewGame(currentGame, gameName))
                            {
                                //create new game
                                SessionGame game = CreateSessionGameForSession(eventData, ref session, gameName);
                                currentGame = game;
                            }
                            //regardless, initialize a new gameplay interval.
                            currentGamePlayTimeInterval = InitializeNewGameplayInterval(eventData);
                        break;

                    case EventDataType.GotToken:
                       
                        ExtendCurrentGamePlayTimeInterval(ref currentGamePlayTimeInterval, eventData);
                        break;

                    case EventDataType.Other:
                        if(currentGamePlayTimeInterval!=null) currentGame.GamePlayTimeIntervals.Add(currentGamePlayTimeInterval);
                        currentGamePlayTimeInterval = null;
                        break;
                }
            
            }; //end delegate definition


            ParseCSVFileFor(pathToGameEventData, lineHandler);


            //print the sessions non game intervals for each game
            Console.WriteLine("Gameplay intervals for Session "+session.Number+": \n");
            foreach(SessionGame game in session.Games)
            {
                Console.WriteLine("\t" + game.Name);
                foreach(TimeInterval nonGame in game.GamePlayTimeIntervals)
                {
                    Console.WriteLine("\t" + nonGame.ToString());
                }
            }
        }

        static bool IsNewGame(SessionGame currentGame, string newGameName)
        {
            return currentGame == null || newGameName != currentGame.Name;
        }

        static EventDataType EventDataToEventType(string[] eventData)
        {
            string eventString = eventData[(int)EventDataIndex.EventType];
            if (IsStartOfNewGame(eventData, eventString)) return EventDataType.StartGame;
            if (IsGotTokenEvent(eventString)) return EventDataType.GotToken;
            return EventDataType.Other;

        }


        static bool IsGotTokenEvent(string eventString)
        {
            return eventString.Contains("token");
        }

        static bool IsStartOfNewGame(string[] eventData, string eventString)
        {
            return eventData.Length == 2 && eventString.Contains("started");
        }




        static SessionGame CreateSessionGameForSession(string[] eventData, ref Session session, string gameName)
        {
           
            DateTime gameStartTime = ParseDateTime(eventData[(int)EventDataIndex.EventTime]);
            SessionGame game = new SessionGame { Name = gameName, StartTime = gameStartTime }; 
            session.Games.Add(game);
            return game;

        }

   

        static void ExtendCurrentGamePlayTimeInterval(ref TimeInterval currentGameplayTimeInterval, string[] eventData, int pushBackSeconds = 0)
        {
            TimeSpan timeOfNewEvent = ParseDateTime(eventData[(int)EventDataIndex.EventTime]).TimeOfDay;
            currentGameplayTimeInterval.End = timeOfNewEvent.CloneToSeconds(pushBackSeconds: pushBackSeconds);
   
        }

        static TimeInterval InitializeNewGameplayInterval(string[] eventData)
        {
            TimeSpan eventTime = ParseDateTime(eventData[(int)EventDataIndex.EventTime]).TimeOfDay;
            return new TimeInterval { Start = eventTime, End = eventTime.CloneToSeconds() };

        }

    

        static string ParseNameOfGameFromGameStartedEvent(string[] eventData)
        {
            return eventData[(int)EventDataIndex.EventType].Split(' ')[0];
        }

    

        static void ParseHeadsetDataFor(Session session, string pathToHeadsetData)
        {
             int currentGameIdx = 0;
             SessionGame currentGame = session.Games[currentGameIdx];
          
            Action<string[]> lineHandler = (string[] headSetData) => {

                DateTime timeOfEvent = ParseDateTime(headSetData[(int)HeadsetDataIndex.EventTime]);

                if (AtNextGame(timeOfEvent, session, currentGame, currentGameIdx))
                {   
                    GetNextGame(ref currentGame, ref currentGameIdx, session);
                }

                if (RowRepresentsTrueGamePlayAndNotNavigationOrRestart(currentGame, timeOfEvent)) {
                    if (DataIsReliable(headSetData))
                    {
                        ParseAOrRGivenGame(currentGame, headSetData, currentGameIdx);
                    } else
                    {
                        currentGame.SecondsPoorQuality++;
                    }
                    //total time computed empirically; from from time ranges
                    currentGame.TotalSeconds++;
                } 
            };

        

            ParseCSVFileFor(pathToHeadsetData, lineHandler);

        }

        /*Need to check if in a time range because the game does not log individual rows (seconds) for each non game event
         in the spreadsheet that logs the meta data
         */
        static bool RowRepresentsTrueGamePlayAndNotNavigationOrRestart(SessionGame currentGame, DateTime timeOfEvent)
        {
            
            foreach(TimeInterval gameplayTimeInterval in currentGame.GamePlayTimeIntervals)
            {
                if (timeOfEvent.TimeOfDay >= gameplayTimeInterval.Start && timeOfEvent.TimeOfDay <= gameplayTimeInterval.End)
                {
                    return true;


                }
            }
            return false;
        }

        static StreamReader CreateReaderForFile(string filePath)
        {
            FileStream fs;
            try
            {
                fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
            } catch(Exception e)
            {
                throw new Exception(e.Message + "\n.Unable to locate: " + filePath + ". Verify that the ParticipantIDToNameAndGroupFile is in the same directory as the A and B directories.");
            }

            StreamReader reader = new StreamReader(fs);

            return reader;
        }


       

       static bool AtNextGame(DateTime time, Session session, SessionGame currentGame, int idx)
        {
          
            return ((idx + 1) < session.Games.Count() && time.CompareTo(session.Games[idx + 1].StartTime) >= 0) ;

        }

    
   

        static void GetNextGame(ref SessionGame currentGame, ref int idx, Session session)
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

            string date = DateRegex.Match(timeString).ToString();
            string time = TimeRegex.Match(timeString).ToString();

            DateTime dateTime;
            if(!DateTime.TryParseExact(date, "dd-MM-yy", null, System.Globalization.DateTimeStyles.None, out dateTime))
            {
                if(!DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateTime)){
                    throw new FormatException("Unable to parse date from: " + date);
                }
            }

            TimeSpan timeSpan;
            if(!TimeSpan.TryParseExact(time, "hh\\:mm\\:ss", null, out timeSpan))
            {
                if (!TimeSpan.TryParseExact(time, "hh\\:mm", null, out timeSpan))
                {
                    throw new FormatException("Unable to parse timespan from: " + time);
                }
            }
            dateTime = dateTime.Add(timeSpan);
            return dateTime;

        }


        static void ParseAOrRGivenGame(SessionGame currentGame, string[] headSetData, int idx)
        {
            int valueIdx = (int)(currentGame.Name == GameType.Rock.Name ? HeadsetDataIndex.Attention : HeadsetDataIndex.Relaxation);
            string dataString = headSetData[valueIdx];
            int value;
            if (Int32.TryParse(dataString.Substring(5, dataString.Length - 5), out value))
               {
                currentGame.MARSum += value;
               }
        }   
    }

 
//Classes

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

        public Boolean HasBothFiles()
        {
            return GameDataFile != null && HeadsetDataFile != null;
        }
    }


    //Domain Models
    class Participant : MFEntity
    {
        public int Id { get; set; }

        public string Name { get; set; }
        public int Group { get; set; } = -1;
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
        public List<SessionGame> Games { get; set; }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.ToString());
            builder.Append("\nGame Data: ");
            foreach(SessionGame game in Games)
            {
                builder.Append("\nGame:");
                builder.Append(game.ToString());
            }


            return builder.ToString();
        }

    }

    //represents a session of play for a particular game and for a particular child.
    class SessionGame : MFEntity
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
        public HashSet<TimeInterval> GamePlayTimeIntervals = new HashSet<TimeInterval>();

    

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType()) return false;
            SessionGame other = (SessionGame)obj;
            return other.Name.Equals(Name);

        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

    }

    class TimeInterval
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        public override string ToString()
        {
            return "Start: "+Start.ToString() + " End:" + End.ToString();
        }
    }

    public static class Extension
    {
        public static TimeSpan CloneToSeconds(this TimeSpan t, int pushBackHours=0, int pushBackMinutes=0, int pushBackSeconds=0)
        {
            return new TimeSpan(t.Hours- pushBackHours, t.Minutes- pushBackMinutes, t.Seconds- pushBackSeconds);
        }

     
    }

  
}
