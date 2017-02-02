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

    class EventDataType
    {
        public static readonly EventDataType StartGame = new EventDataType();
        public static readonly EventDataType InGame = new EventDataType();
        public static readonly EventDataType NonGame = new EventDataType();
        

        public static EventDataType EventDataToEventType(string[] eventData)
        {
            string eventString = eventData[(int)EventDataIndex.EventType];
            if (IsStartOfNewGame(eventData, eventString)) return StartGame;
            if (IsGameEvent(eventString)) return InGame;
            return NonGame;

        }


        static bool IsGameEvent(string eventString)
        {
            return eventString.Contains("token");
        }

        static bool IsStartOfNewGame(string[] eventData, string eventString)
        {
            return eventData.Length == 2 && eventString.Contains("started");
        }
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
        public static readonly string Rock="Rock";
        public static readonly string Pinwheel = "Pinwheel";
        public static readonly string Paraglider = "Paraglider";
    }

    class ParticipantNameAndGroup
    {
        public string Name { get; set; }
        public int Group { get; set; }
    }



    class Program
    {
        private const int DURATION_OF_ONE_EVENT = 1;
        static Regex ParticipantIdRegex = new Regex("\\d{10}");
        static Regex HeadsetFileRegex = new Regex("headset");
        static Regex SessionNumberRegex = new Regex("(_)(\\d{1,2})(_)");

        static string PathToParticipantIdToNameAndGroupFile= "C:/Users/root960/Desktop/MFData/ParticipantIdToNameAndGroup.csv";
        static Dictionary<int, ParticipantNameAndGroup> ParticipantNameAndGroupLookup = new Dictionary<int, ParticipantNameAndGroup>();
        static Dictionary<String, Participant> Participants = new Dictionary<string, Participant>();

        static void Main(string[] args)
        {
            LoadParticipantIDToNameAndGroupData();
            LoadParticipants();
            LoadParticipantsData();
            PrintParticipants();



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
                files = Directory.GetFiles("C:/Users/root960/Desktop/MFData/test");
            }catch (Exception e)
            {
                throw new Exception(e.Message+"\nCheck that the string identifying the root directory of the MF files is accurate.");
            }

            
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


                ParticipantNameAndGroup nameAndGroup;
                if (ParticipantNameAndGroupLookup.TryGetValue(IdAsInt, out nameAndGroup))
                {
                    participant.Name = nameAndGroup.Name;
                    participant.Group = nameAndGroup.Group;

                }
                else
                {
                    Console.WriteLine("Participant ID {0} was not registered in the Id to Name and Group Dictionary.", IdAsInt);
                   
                }




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
                    break;
                case MFFileType.HeadSetData:
                    dataFiles.HeadsetDataFile = file;
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
            InitializeSessionGamesAndStartTimesFromFile(session, sessionDataFiles.GameDataFile);
            ParseHeadsetDataFor(session, sessionDataFiles.HeadsetDataFile);

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
            StreamReader reader = CreateReaderForFile(pathToFile);
            while (!reader.EndOfStream)
            {
                string[] eventData = reader.ReadLine().Split(',');
                lineHandler(eventData);
            }
          
            reader.Close();
        }

        static void InitializeSessionGamesAndStartTimesFromFile(Session session, string pathToGameEventData = "")
        {
            const int inGameEvent = 0;
            const int inNonGameEvent = 1;
            int state = inGameEvent;
            SessionGame currentGame = null;
            TimeRange currentNonEventTimeRange = null;


            Action<string[]> lineHandler = (string[] eventData) =>
            {
                EventDataType eventType = EventDataType.EventDataToEventType(eventData);
                if (eventType == EventDataType.StartGame)
                {
                    CreateNewSessionGame(eventData, out currentGame, ref session);

                    if (state == inNonGameEvent)
                    {
                        ExtendCurrentNonGameEventTimeRangeToCurrentEventTime(ref currentNonEventTimeRange, eventData, pushBackSeconds: DURATION_OF_ONE_EVENT);
                        ConcludeNonGameTimeIntervalAndAddToCurrentGame(eventData, ref currentGame, ref currentNonEventTimeRange);
                        ToggleState(ref state);
                    }
                }
                else if (eventType == EventDataType.InGame)
                {
                    if (state == inNonGameEvent)
                    {
          
                        ExtendCurrentNonGameEventTimeRangeToCurrentEventTime(ref currentNonEventTimeRange, eventData, pushBackSeconds: DURATION_OF_ONE_EVENT);
                        ConcludeNonGameTimeIntervalAndAddToCurrentGame(eventData, ref currentGame, ref currentNonEventTimeRange);
                        ToggleState(ref state);

                    }

                }
                else if (eventType == EventDataType.NonGame)
                {
                    if (state == inGameEvent)
                    {
                        BeginAndSaveNewNonGameEventTimeRangeForCurrentGame(eventData, out currentNonEventTimeRange);
                        ToggleState(ref state);
                    }
                    else if (state == inNonGameEvent)
                    {
                        ExtendCurrentNonGameEventTimeRangeToCurrentEventTime(ref currentNonEventTimeRange, eventData);
                    }
                }
            }; //end delegate definition


            ParseCSVFileFor(pathToGameEventData, lineHandler);
        }
           

        static void ToggleState(ref int state)
        {
            state = 1 - state;
        }

        static void CreateNewSessionGame(string[] eventData, out SessionGame currentGame, ref Session session)
        {
            string gameName = ParseNameOfGameFromGameStartedEvent(eventData);
            DateTime gameStartTime = ParseDateTime(eventData[(int)EventDataIndex.EventTime]);
            SessionGame game = new SessionGame { Name = gameName, StartTime = gameStartTime };
            session.Games.Add(game);
            currentGame = game;

        }

        static void ExtendCurrentNonGameEventTimeRangeToCurrentEventTime(ref TimeRange currentNonEventTimeRange, string[] eventData, int pushBackSeconds = 0)
        {
            TimeSpan timeOfNewEvent = ParseDateTime(eventData[(int)EventDataIndex.EventTime]).TimeOfDay;
            currentNonEventTimeRange.End = timeOfNewEvent.CloneToSeconds(pushBackSeconds: pushBackSeconds);
   
        }

        static void BeginAndSaveNewNonGameEventTimeRangeForCurrentGame(string[] eventData, out TimeRange currentNonEventTimeRange)
        {
            TimeSpan eventTime = ParseDateTime(eventData[(int)EventDataIndex.EventTime]).TimeOfDay;
            currentNonEventTimeRange = new TimeRange { Start = eventTime, End = eventTime.CloneToSeconds() };

        }

        static void ConcludeNonGameTimeIntervalAndAddToCurrentGame(string[] eventData, ref SessionGame currentGame, ref TimeRange currentNonEventTimeRange)
        {
            
            currentGame.NonGameEventTimeRanges.Add(currentNonEventTimeRange);
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
            
            foreach(TimeRange rangeOfNonGameEvent in currentGame.NonGameEventTimeRanges)
            {
                if (timeOfEvent.TimeOfDay > rangeOfNonGameEvent.Start && timeOfEvent.TimeOfDay < rangeOfNonGameEvent.End)
                {
                    return false;


                }
            }
            return true;
        }

        static StreamReader CreateReaderForFile(string filePath)
        {
            FileStream fs;
            try
            {
                fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
            } catch(Exception e)
            {
                throw new Exception(e.Message + " .Unable to locate: " + filePath + ". Verify that the ParticipantIDToNameAndGroupFile is in the same directory as the A and B directories.");
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
            
            string date = timeString.Substring(0, 8);
            string time = timeString.Substring(9, 8);

            DateTime dateTime = DateTime.ParseExact(date, "dd-MM-yy", null, System.Globalization.DateTimeStyles.None);

            TimeSpan timeSpan = TimeSpan.ParseExact(time, "hh\\:mm\\:ss", null, System.Globalization.TimeSpanStyles.None);
            dateTime = dateTime.Add(timeSpan);
            return dateTime;

        }


        static void ParseAOrRGivenGame(SessionGame currentGame, string[] headSetData, int idx)
        {
            int valueIdx = (int)(currentGame.Name == GameType.Rock ? HeadsetDataIndex.Attention : HeadsetDataIndex.Relaxation);
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
    }


    //Domain Models
    class Participant : MFEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Unknown";
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
        public HashSet<TimeRange> NonGameEventTimeRanges = new HashSet<TimeRange>();

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.ToString());
            builder.Append("\nNon Game Event Time Ranges:");
            foreach(TimeRange tr in NonGameEventTimeRanges)
            {
                builder.Append("\n");
                builder.Append(tr.ToString());

            }
            return builder.ToString();
        }

    }

    class TimeRange
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
