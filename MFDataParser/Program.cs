using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Entity;
namespace MFDataParser
{

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

        static string pathToGameEventData = null;
        static string pathToHeadsetData = null;

        static void Main(string[] args)
        {
            pathToGameEventData = "C:/Users/root960/Desktop/MFData/1420008612_17_09Feb15_1122_game.csv";
            pathToHeadsetData = pathToGameEventData.Replace("game", "headset");

            ParseSession(0);//doesn't really matter right now



            
        }


        static void ParseSession(int sessionNumber)
        {
            var session = InitializeSession(sessionNumber);

            ParseGameStartTimesFor(session);

          
            ParseHeadsetDataFor(session);
        
            //check results
            foreach (Game g in session.Games)
            {
                Console.WriteLine(g.ToString());
            }
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
            int[] ARTalliesPerGame = new int[session.Games.Count()];
            Action<string[]> lineHandler = (string[] headSetData) => {
                
                if (AtNextGame(headSetData, session, currentGame, idx))
                {
                    GetNextGame(ref currentGame, ref idx, session);
                }

                if (DataIsReliable(headSetData))
                {
                    ParseAOrRGivenGame(currentGame, headSetData, ref ARTalliesPerGame, idx);
                }
            };

            Action endLineHandler = () =>
            {
                CalculateAndSaveAggregateGameData(session, ARTalliesPerGame);
            };

            ParseFileFor(pathToHeadsetData, session, lineHandler, endLineHandler);

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


        static void ParseAOrRGivenGame(Game currentGame, string[] headSetData, ref int[] ARTalliesPerGame, int idx)
        {
            int valueIdx = (int)(currentGame.Name == "Rock" ? HeadsetDataIndex.Attention : HeadsetDataIndex.Relaxation);
            string dataString = headSetData[valueIdx];
            int value;
            if (Int32.TryParse(dataString.Substring(5, dataString.Length - 5), out value))
               {
                currentGame.MAR += value;
                ARTalliesPerGame[idx]++;
            
               }
   
        }

        static void CalculateAndSaveAggregateGameData(Session session, int[] ARTalliesPerGame)
        {
            for(int i = 0; i < session.Games.Count(); i++) 
            {  
                session.Games[i].CalculateMAR(ARTalliesPerGame[i]);
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
        public List<Session> Sessions { get; set; }
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
        public int MAR { get; set; }
        public double PAR { get; set; }
        public int TT { get; set; }
        

        public void CalculateMAR(int timesMeasured)
        {
            this.MAR = this.MAR / timesMeasured;
        }

        



    }


  
}
