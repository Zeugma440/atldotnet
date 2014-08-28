/*
Title:    CueSharp
Version:  0.5
Released: March 24, 2007

Author:   Wyatt O'Day
Website:  wyday.com/cuesharp
*/
using System;
using System.Text;
using System.IO;

namespace CueSharp
{
    /// <summary>
    /// A CueSheet class used to create, open, edit, and save cuesheets.
    /// </summary>
    public class CueSheet
    {
        #region Private Variables
        string[] cueLines;

        private string m_Catalog = "";
        private string m_CDTextFile = "";
        private string[] m_Comments = new string[0];
        // strings that don't belong or were mistyped in the global part of the cue
        private string[] m_Garbage = new string[0];
        private string m_Performer = "";
        private string m_Songwriter= "";
        private string m_Title="";
        private Track[] m_Tracks = new Track[0];

        #endregion

        #region Properties


        /// <summary>
        /// Returns/Sets track in this cuefile.
        /// </summary>
        /// <param name="tracknumber">The track in this cuefile.</param>
        /// <returns>Track at the tracknumber.</returns>
        public Track this[int tracknumber]
        {
            get
            {
                return m_Tracks[tracknumber];
            }
            set
            {
                m_Tracks[tracknumber] = value;
            }
        }


        /// <summary>
        /// The catalog number must be 13 digits long and is encoded according to UPC/EAN rules.
        /// Example: CATALOG 1234567890123
        /// </summary>
        public string Catalog
        {
            get { return m_Catalog; }
            set { m_Catalog = value; }
        }

        /// <summary>
        /// This command is used to specify the name of the file that contains the encoded CD-TEXT information for the disc. This command is only used with files that were either created with the graphical CD-TEXT editor or generated automatically by the software when copying a CD-TEXT enhanced disc.
        /// </summary>
        public string CDTextFile
        {
            get { return m_CDTextFile; }
            set { m_CDTextFile = value; }
        }

        /// <summary>
        /// This command is used to put comments in your CUE SHEET file.
        /// </summary>
        public string[] Comments
        {
            get { return m_Comments; }
            set { m_Comments = value; }
        }

        /// <summary>
        /// Lines in the cue file that don't belong or have other general syntax errors.
        /// </summary>
        public string[] Garbage
        {
            get { return m_Garbage; }
        }

        /// <summary>
        /// This command is used to specify the name of a perfomer for a CD-TEXT enhanced disc.
        /// </summary>
        public string Performer
        {
            get { return m_Performer; }
            set { m_Performer = value; }
        }

        /// <summary>
        /// This command is used to specify the name of a songwriter for a CD-TEXT enhanced disc.
        /// </summary>
        public string Songwriter
        {
            get { return m_Songwriter; }
            set { m_Songwriter = value; }
        }

        /// <summary>
        /// The title of the entire disc as a whole.
        /// </summary>
        public string Title
        {
            get { return m_Title; }
            set { m_Title = value; }
        }

        /// <summary>
        /// The array of tracks on the cuesheet.
        /// </summary>
        public Track[] Tracks
        {
            get { return m_Tracks; }
            set { m_Tracks = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Create a cue sheet from scratch.
        /// </summary>
        public CueSheet()
        { }

        /// <summary>
        /// Parse a cue sheet string.
        /// </summary>
        /// <param name="cueString">A string containing the cue sheet data.</param>
        /// <param name="lineDelims">Line delimeters; set to "(char[])null" for default delimeters.</param>
        public CueSheet(string cueString, char[] lineDelims)
        {
            if (lineDelims == null)
            {
                lineDelims = new char[] { '\n' };
            }

            cueLines = cueString.Split(lineDelims);
            RemoveEmptyLines(ref cueLines);
            ParseCue(cueLines);
        }

        /// <summary>
        /// Parses a cue sheet file.
        /// </summary>
        /// <param name="cuefilename">The filename for the cue sheet to open.</param>
        public CueSheet(string cuefilename)
        {
            ReadCueSheet(cuefilename, Encoding.Default);
        }

        /// <summary>
        /// Parses a cue sheet file.
        /// </summary>
        /// <param name="cuefilename">The filename for the cue sheet to open.</param>
        /// <param name="encoding">The encoding used to open the file.</param>
        public CueSheet(string cuefilename, Encoding encoding)
        {
            ReadCueSheet(cuefilename, encoding);
        }

        private void ReadCueSheet(string filename, Encoding encoding)
        {
            // array of delimiters to split the sentence with
            char[] delimiters = new char[] { '\n' };
            
            // read in the full cue file
            TextReader tr = new StreamReader(filename, encoding);
            //read in file
            cueLines = tr.ReadToEnd().Split(delimiters);

            // close the stream
            tr.Close();

            RemoveEmptyLines(ref cueLines);

            ParseCue(cueLines);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Removes any empty lines, elimating possible trouble.
        /// </summary>
        /// <param name="file"></param>
        private void RemoveEmptyLines(ref string[] file)
        {
            int itemsRemoved = 0;

            for (int i = 0; i < file.Length; i++)
            {
                if (file[i].Trim() != "")
                {
                    file[i - itemsRemoved] = file[i];
                }
                else if (file[i].Trim() == "")
                {
                    itemsRemoved++;
                }
            }

            if (itemsRemoved > 0)
            {
                file = (string[])ResizeArray(file, file.Length - itemsRemoved);
            }
        }

        private void ParseCue(string[] file)
        {
            //-1 means still global, 
            //all others are track specific
            int trackOn = -1;
            AudioFile currentFile = new AudioFile();

            for (int i = 0; i < file.Length; i++)
            {
                file[i] = file[i].Trim();

                switch (file[i].Substring(0, file[i].IndexOf(' ')).ToUpper())
                {
                    case "CATALOG":
                        ParseString(file[i], trackOn);
                        break;
                    case "CDTEXTFILE":
                        ParseString(file[i], trackOn);
                        break;
                    case "FILE":
                        currentFile = ParseFile(file[i], trackOn);
                        break;
                    case "FLAGS":
                        ParseFlags(file[i], trackOn);
                        break;
                    case "INDEX":
                        ParseIndex(file[i], trackOn);
                        break;
                    case "ISRC":
                        ParseString(file[i], trackOn);
                        break;
                    case "PERFORMER":
                        ParseString(file[i], trackOn);
                        break;
                    case "POSTGAP":
                        ParseIndex(file[i], trackOn);
                        break;
                    case "PREGAP":
                        ParseIndex(file[i], trackOn);
                        break;
                    case "REM":
                        ParseComment(file[i], trackOn);
                        break;
                    case "SONGWRITER":
                        ParseString(file[i], trackOn);
                        break;
                    case "TITLE":
                        ParseString(file[i], trackOn);
                        break;
                    case "TRACK":
                        trackOn++;
                        ParseTrack(file[i], trackOn);
                        if (currentFile.Filename != "") //if there's a file
                        {
                            m_Tracks[trackOn].DataFile = currentFile;
                            // Commented by Z440 (resets current file attributes for no reason ?)
                            //currentFile = new AudioFile();
                        }
                        break;
                    default:
                        ParseGarbage(file[i], trackOn);
                        //save discarded junk and place string[] with track it was found in
                        break;
                }
            }

        }

        private void ParseComment(string line, int trackOn)
        {
            //remove "REM" (we know the line has already been .Trim()'ed)
            line = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            if (trackOn == -1)
            {
                if (line.Trim() != "")
                {
                    m_Comments = (string[])ResizeArray(m_Comments, m_Comments.Length + 1);
                    m_Comments[m_Comments.Length - 1] = line;
                }
            }
            else
            {
                m_Tracks[trackOn].AddComment(line);
            }
        }

        private AudioFile ParseFile(string line, int trackOn)
        {
            string fileType;

            line = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            fileType = line.Substring(line.LastIndexOf(' '), line.Length - line.LastIndexOf(' ')).Trim();

            line = line.Substring(0, line.LastIndexOf(' ')).Trim();

            //if quotes around it, remove them.
            if (line[0] == '"')
            {
                line = line.Substring(1, line.LastIndexOf('"') - 1);
            }

            return new AudioFile(line, fileType);
        }

        private void ParseFlags(string line, int trackOn)
        {
            string temp;

            if (trackOn != -1)
            {
                line = line.Trim();
                if (line != "")
                {
                    try
                    {
                        temp = line.Substring(0, line.IndexOf(' ')).ToUpper();
                    }
                    catch (Exception)
                    {
                        temp = line.ToUpper();
                        
                    }

                    switch (temp)
                    {
                        case "FLAGS":
                            m_Tracks[trackOn].AddFlag(temp);
                            break;
                        case "DATA":
                            m_Tracks[trackOn].AddFlag(temp);
                            break;
                        case "DCP":
                            m_Tracks[trackOn].AddFlag(temp);
                            break;
                        case "4CH":
                            m_Tracks[trackOn].AddFlag(temp);
                            break;
                        case "PRE":
                            m_Tracks[trackOn].AddFlag(temp);
                            break;
                        case "SCMS":
                            m_Tracks[trackOn].AddFlag(temp);
                            break;
                        default:
                            break;
                    }

                    //processing for a case when there isn't any more spaces
                    //i.e. avoiding the "index cannot be less than zero" error
                    //when calling line.IndexOf(' ')
                    try
                    {
                        temp = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' '));
                    }
                    catch (Exception)
                    {
                        temp = line.Substring(0, line.Length);
                    }

                    //if the flag hasn't already been processed
                    if (temp.ToUpper().Trim() != line.ToUpper().Trim())
                    {
                        ParseFlags(temp, trackOn);
                    }
                }
            }
        }

        private void ParseGarbage(string line, int trackOn)
        {
            if (trackOn == -1)
            {
                if (line.Trim() != "")
                {
                    m_Garbage = (string[])ResizeArray(m_Garbage, m_Garbage.Length + 1);
                    m_Garbage[m_Garbage.Length - 1] = line;
                }
            }
            else
            {
                m_Tracks[trackOn].AddGarbage(line);
            }
        }

        private void ParseIndex(string line, int trackOn)
        {
            string indexType;
            string tempString;

            int number = 0;
            int minutes;
            int seconds;
            int frames;

            indexType = line.Substring(0, line.IndexOf(' ')).ToUpper();

            tempString = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            if (indexType == "INDEX")
            {
                //read the index number
                number = Convert.ToInt32(tempString.Substring(0, tempString.IndexOf(' ')));
                tempString = tempString.Substring(tempString.IndexOf(' '), tempString.Length - tempString.IndexOf(' ')).Trim();
            }

            //extract the minutes, seconds, and frames
            minutes = Convert.ToInt32(tempString.Substring(0, tempString.IndexOf(':')));
            seconds = Convert.ToInt32(tempString.Substring(tempString.IndexOf(':') + 1, tempString.LastIndexOf(':') - tempString.IndexOf(':') - 1));
            frames = Convert.ToInt32(tempString.Substring(tempString.LastIndexOf(':') + 1, tempString.Length - tempString.LastIndexOf(':') - 1));

            if (indexType == "INDEX")
            {
                m_Tracks[trackOn].AddIndex(number, minutes, seconds, frames);
            }
            else if (indexType == "PREGAP")
            {
                m_Tracks[trackOn].PreGap = new Index(0, minutes, seconds, frames);
            }
            else if (indexType == "POSTGAP")
            {
                m_Tracks[trackOn].PostGap = new Index(0, minutes, seconds, frames);
            }
        }

        private void ParseString(string line, int trackOn)
        {
            string category = line.Substring(0, line.IndexOf(' ')).ToUpper();

            line = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            //get rid of the quotes
            if (line[0] == '"')
            {
                line = line.Substring(1, line.LastIndexOf('"') - 1);
            }

            switch (category)
            {
                case "CATALOG":
                    if (trackOn == -1)
                    {
                        this.m_Catalog = line;
                    }
                    break;
                case "CDTEXTFILE":
                    if (trackOn == -1)
                    {
                        this.m_CDTextFile = line;
                    }
                    break;
                case "ISRC":
                    if (trackOn != -1)
                    {
                        m_Tracks[trackOn].ISRC = line;
                    }
                    break;
                case "PERFORMER":
                    if (trackOn == -1)
                    {
                        this.m_Performer = line;
                    }
                    else
                    {
                        m_Tracks[trackOn].Performer = line;
                    }
                    break;
               case "SONGWRITER":
                   if (trackOn == -1)
                   {
                       this.m_Songwriter = line;
                   }
                   else
                   {
                       m_Tracks[trackOn].Songwriter = line;
                   }
                    break;
                case "TITLE":
                    if (trackOn == -1)
                    {
                        this.m_Title = line;
                    }
                    else
                    {
                        m_Tracks[trackOn].Title = line;
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Parses the TRACK command. 
        /// </summary>
        /// <param name="line">The line in the cue file that contains the TRACK command.</param>
        /// <param name="trackOn">The track currently processing.</param>
        private void ParseTrack(string line, int trackOn)
        {
            string tempString;
            int trackNumber;

            tempString = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            try
            {
                trackNumber = Convert.ToInt32(tempString.Substring(0, tempString.IndexOf(' ')));
            }
            catch (Exception)
            { throw; }

            //find the data type.
            tempString = tempString.Substring(tempString.IndexOf(' '), tempString.Length - tempString.IndexOf(' ')).Trim();

            AddTrack(trackNumber, tempString);
        }
        
        /// <summary>
        /// Reallocates an array with a new size, and copies the contents
        /// of the old array to the new array.
        /// </summary>
        /// <param name="oldArray">The old array, to be reallocated.</param>
        /// <param name="newSize">The new array size.</param>
        /// <returns>A new array with the same contents.</returns>
        /// <remarks >Useage: int[] a = {1,2,3}; a = (int[])ResizeArray(a,5);</remarks>
        public static System.Array ResizeArray(System.Array oldArray, int newSize)
        {
            int oldSize = oldArray.Length;
            System.Type elementType = oldArray.GetType().GetElementType();
            System.Array newArray = System.Array.CreateInstance(elementType, newSize);
            int preserveLength = System.Math.Min(oldSize, newSize);
            if (preserveLength > 0)
                System.Array.Copy(oldArray, newArray, preserveLength);
            return newArray;
        }

        /// <summary>
        /// Add a track to the current cuesheet.
        /// </summary>
        /// <param name="tracknumber">The number of the said track.</param>
        /// <param name="datatype">The datatype of the track.</param>
        private void AddTrack(int tracknumber, string datatype)
        {
            m_Tracks = (Track[])ResizeArray(m_Tracks, m_Tracks.Length + 1);
            m_Tracks[m_Tracks.Length - 1] = new Track(tracknumber, datatype);
        }

        /// <summary>
        /// Add a track to the current cuesheet
        /// </summary>
        /// <param name="title">The title of the track.</param>
        /// <param name="performer">The performer of this track.</param>
        public void AddTrack(string title, string performer)
        {
            m_Tracks = (Track[])ResizeArray(m_Tracks, m_Tracks.Length + 1);
            m_Tracks[m_Tracks.Length - 1] = new Track(m_Tracks.Length, "");

            m_Tracks[m_Tracks.Length - 1].Performer = performer;
            m_Tracks[m_Tracks.Length - 1].Title = title;
        }


        public void AddTrack(string title, string performer, string filename, FileType fType)
        {
            m_Tracks = (Track[])ResizeArray(m_Tracks, m_Tracks.Length + 1);
            m_Tracks[m_Tracks.Length - 1] = new Track(m_Tracks.Length, "");

            m_Tracks[m_Tracks.Length - 1].Performer = performer;
            m_Tracks[m_Tracks.Length - 1].Title = title;
            m_Tracks[m_Tracks.Length - 1].DataFile = new AudioFile (filename, fType);
        }

        /// <summary>
        /// Add a track to the current cuesheet
        /// </summary>
        /// <param name="title">The title of the track.</param>
        /// <param name="performer">The performer of this track.</param>
        /// <param name="datatype">The datatype for the track (typically DataType.Audio)</param>
        public void AddTrack(string title, string performer, DataType datatype)
        {
            m_Tracks = (Track[])ResizeArray(m_Tracks, m_Tracks.Length + 1);
            m_Tracks[m_Tracks.Length - 1] = new Track(m_Tracks.Length, datatype);

            m_Tracks[m_Tracks.Length - 1].Performer = performer;
            m_Tracks[m_Tracks.Length - 1].Title = title;
        }

        /// <summary>
        /// Add a track to the current cuesheet
        /// </summary>
        /// <param name="track">Track object to add to the cuesheet.</param>
        public void AddTrack(Track track)
        {
            m_Tracks = (Track[])ResizeArray(m_Tracks, m_Tracks.Length + 1);
            m_Tracks[m_Tracks.Length - 1] = track;
        }

        /// <summary>
        /// Remove a track from the cuesheet.
        /// </summary>
        /// <param name="trackIndex">The index of the track you wish to remove.</param>
        public void RemoveTrack(int trackIndex)
        {
            for (int i = trackIndex; i < m_Tracks.Length - 1; i++)
            {
                m_Tracks[i] = m_Tracks[i + 1];
            }
            m_Tracks = (Track[])ResizeArray(m_Tracks, m_Tracks.Length - 1);
        }

        /// <summary>
        /// Add index information to an existing track.
        /// </summary>
        /// <param name="trackIndex">The array index number of track to be modified</param>
        /// <param name="indexNum">The index number of the new index</param>
        /// <param name="minutes">The minute value of the new index</param>
        /// <param name="seconds">The seconds value of the new index</param>
        /// <param name="frames">The frames value of the new index</param>
        public void AddIndex(int trackIndex, int indexNum, int minutes, int seconds, int frames)
        {
            m_Tracks[trackIndex].AddIndex(indexNum, minutes, seconds, frames);
        }

        /// <summary>
        /// Remove an index from a track.
        /// </summary>
        /// <param name="trackIndex">The array-index of the track.</param>
        /// <param name="indexIndex">The index of the Index you wish to remove.</param>
        public void RemoveIndex(int trackIndex, int indexIndex)
        {
            //Note it is the index of the Index you want to delete, 
            //which may or may not correspond to the number of the index.
            m_Tracks[trackIndex].RemoveIndex(indexIndex);
        }

        /// <summary>
        /// Save the cue sheet file to specified location.
        /// </summary>
        /// <param name="filename">Filename of destination cue sheet file.</param>
        public void SaveCue(string filename)
        {
            SaveCue(filename, Encoding.Default);
        }

        /// <summary>
        /// Save the cue sheet file to specified location.
        /// </summary>
        /// <param name="filename">Filename of destination cue sheet file.</param>
        /// <param name="encoding">The encoding used to save the file.</param>
        public void SaveCue(string filename, Encoding encoding)
        {
            TextWriter tw = new StreamWriter(filename, false, encoding);

            tw.WriteLine(this.ToString());

            //close the writer stream
            tw.Close();
        }

        /// <summary>
        /// Method to output the cuesheet into a single formatted string.
        /// </summary>
        /// <returns>The entire cuesheet formatted to specification.</returns>
        public override string ToString()
        {
            StringBuilder output = new StringBuilder();

            foreach (string comment in m_Comments)
            {
                output.Append("REM " + comment + Environment.NewLine);
            }

            if (m_Catalog.Trim() != "")
            {
                output.Append("CATALOG " + m_Catalog + Environment.NewLine);
            }

            if (m_Performer.Trim() != "")
            {
                output.Append("PERFORMER \"" + m_Performer + "\"" + Environment.NewLine);
            }

            if (m_Songwriter.Trim() != "")
            {
                output.Append("SONGWRITER \"" + m_Songwriter + "\"" + Environment.NewLine);
            }

            if (m_Title.Trim() != "")
            {
                output.Append("TITLE \"" + m_Title + "\"" + Environment.NewLine);
            }

            if (m_CDTextFile.Trim() != "")
            {
                output.Append("CDTEXTFILE \"" + m_CDTextFile.Trim() + "\"" + Environment.NewLine);
            }

            for (int i = 0; i < m_Tracks.Length; i++)
            {
                output.Append(m_Tracks[i].ToString());

                if (i != m_Tracks.Length - 1)
                {
                    //add line break for each track except last
                    output.Append(Environment.NewLine);
                }
            }

            return output.ToString();
        }

        #endregion

        //TODO: Fix calculation bugs; currently generates erroneous IDs.
        #region CalculateDiscIDs
        //For complete CDDB/freedb discID calculation, see:
        //http://www.freedb.org/modules.php?name=Sections&sop=viewarticle&artid=6

        public string CalculateCDDBdiscID()
        {
            int i, t = 0, n = 0;

            /* For backward compatibility this algorithm must not change */

            i = 0;

            while (i < m_Tracks.Length)
            {
                n = n + cddb_sum((lastTrackIndex(m_Tracks[i]).Minutes * 60) + lastTrackIndex(m_Tracks[i]).Seconds);
                i++;
            }

            Console.WriteLine(n.ToString());

            t = ((lastTrackIndex(m_Tracks[m_Tracks.Length - 1]).Minutes * 60) + lastTrackIndex(m_Tracks[m_Tracks.Length - 1]).Seconds) -
                ((lastTrackIndex(m_Tracks[0]).Minutes * 60) + lastTrackIndex(m_Tracks[0]).Seconds);

            ulong lDiscId = (((uint)n % 0xff) << 24 | (uint)t << 8 | (uint)m_Tracks.Length);
            return String.Format("{0:x8}", lDiscId);
        }

        private Index lastTrackIndex(Track track)
        {
            return track.Indices[track.Indices.Length - 1];
        }

        private int cddb_sum(int n)
        {
            int ret;

            /* For backward compatibility this algorithm must not change */

            ret = 0;

            while (n > 0)
            {
                ret = ret + (n % 10);
                n = n / 10;
            }

            return (ret);
        }

        #endregion CalculateDiscIDS


    }   

    /// <summary>
    ///DCP - Digital copy permitted
    ///4CH - Four channel audio
    ///PRE - Pre-emphasis enabled (audio tracks only)
    ///SCMS - Serial copy management system (not supported by all recorders)
    ///There is a fourth subcode flag called "DATA" which is set for all non-audio tracks. This flag is set automatically based on the datatype of the track.
    /// </summary>
    public enum Flags
    {
        DCP, CH4, PRE, SCMS, DATA, NONE
    }

    /// <summary>
    /// BINARY - Intel binary file (least significant byte first)
    /// MOTOROLA - Motorola binary file (most significant byte first)
    /// AIFF - Audio AIFF file
    /// WAVE - Audio WAVE file
    /// MP3 - Audio MP3 file
    /// </summary>
    public enum FileType
    {
        BINARY, MOTOROLA, AIFF, WAVE, MP3
    }

    /// <summary>
    /// <list>
    /// <item>AUDIO - Audio/Music (2352)</item>
    /// <item>CDG - Karaoke CD+G (2448)</item>
    /// <item>MODE1/2048 - CDROM Mode1 Data (cooked)</item>
    /// <item>MODE1/2352 - CDROM Mode1 Data (raw)</item>
    /// <item>MODE2/2336 - CDROM-XA Mode2 Data</item>
    /// <item>MODE2/2352 - CDROM-XA Mode2 Data</item>
    /// <item>CDI/2336 - CDI Mode2 Data</item>
    /// <item>CDI/2352 - CDI Mode2 Data</item>
    /// </list>
    /// </summary>
    public enum DataType
    {
        AUDIO , CDG , MODE1_2048 , MODE1_2352 , MODE2_2336 , MODE2_2352 , CDI_2336 , CDI_2352 
    }

    /// <summary>
    /// This command is used to specify indexes (or subindexes) within a track.
    /// Syntax:
    ///  INDEX [number] [mm:ss:ff]
    /// </summary>
    public struct Index
    {
        //0-99
        int m_number;

        int m_minutes;
        int m_seconds;
        int m_frames;

        /// <summary>
        /// Index number (0-99)
        /// </summary>
        public int Number
        {
            get { return m_number; }
            set
            {
                if (value > 99)
                {
                    m_number = 99;
                }
                else if (value < 0)
                {
                    m_number = 0;
                }
                else
                {
                    m_number = value;
                }
            }
        }

        /// <summary>
        /// Possible values: 0-99
        /// </summary>
        public int Minutes
        {
            get { return m_minutes; }
            set
            {
                if (value > 99)
                {
                    m_minutes = 99;
                }
                else if (value < 0)
                {
                    m_minutes = 0;
                }
                else
                {
                    m_minutes = value;
                }
            }
        }

        /// <summary>
        /// Possible values: 0-59
        /// There are 60 seconds/minute
        /// </summary>
        public int Seconds
        {
            get { return m_seconds; }
            set
            {
                if (value >= 60)
                {
                    m_seconds = 59;
                }
                else if (value < 0)
                {
                    m_seconds = 0;
                }
                else
                {
                    m_seconds = value;
                }
            }
        }

        /// <summary>
        /// Possible values: 0-74
        /// There are 75 frames/second
        /// </summary>
        public int Frames
        {
            get { return m_frames; }
            set
            {
                if (value >= 75)
                {
                    m_frames = 74;
                }
                else if (value < 0)
                {
                    m_frames = 0;
                }
                else
                {
                    m_frames = value;
                }
            }
        }

        /// <summary>
        /// The Index of a track.
        /// </summary>
        /// <param name="number">Index number 0-99</param>
        /// <param name="minutes">Minutes (0-99)</param>
        /// <param name="seconds">Seconds (0-59)</param>
        /// <param name="frames">Frames (0-74)</param>
        public Index(int number, int minutes, int seconds, int frames)
        {
            m_number = number;

            m_minutes = minutes;
            m_seconds = seconds;
            m_frames = frames;
        }
    }

    /// <summary>
    /// This command is used to specify a data/audio file that will be written to the recorder.
    /// </summary>
    public struct AudioFile
    {
        private string m_Filename;
        private FileType m_Filetype;

        public string Filename
        {
            get { return m_Filename; }
            set { m_Filename = value; }
        }

        /// <summary>
        /// BINARY - Intel binary file (least significant byte first)
        /// MOTOROLA - Motorola binary file (most significant byte first)
        /// AIFF - Audio AIFF file
        /// WAVE - Audio WAVE file
        /// MP3 - Audio MP3 file
        /// </summary>
        public FileType Filetype
        {
            get { return m_Filetype; }
            set { m_Filetype = value; }
        }

        public AudioFile(string filename, string filetype)
        {
            m_Filename = filename;

            switch (filetype.Trim().ToUpper())
            {
                case "BINARY":
                    m_Filetype = FileType.BINARY;
                    break;
                case "MOTOROLA":
                    m_Filetype = FileType.MOTOROLA;
                    break;
                case "AIFF":
                    m_Filetype = FileType.AIFF;
                    break;
                case "WAVE":
                    m_Filetype = FileType.WAVE;
                    break;
                case "MP3":
                    m_Filetype = FileType.MP3;
                    break;
                default:
                    m_Filetype = FileType.BINARY;
                    break;
            }
        }

        public AudioFile(string filename, FileType filetype)
        {
            m_Filename = filename;
            m_Filetype = filetype;
        }
    }

    /// <summary>
    /// Track that contains either data or audio. It can contain Indices and comment information.
    /// </summary>
    public struct Track
    {
        #region Private Variables
        private string[] m_Comments;
        // strings that don't belong or were mistyped in the global part of the cue
        private AudioFile m_DataFile;
        private string[] m_Garbage;
        private Index[] m_Indices;
        private string m_ISRC;

        private string m_Performer;
        private Index m_PostGap;
        private Index m_PreGap;
        private string m_Songwriter;
        private string m_Title;
        private Flags[] m_TrackFlags;
        private DataType m_TrackDataType;
        private int m_TrackNumber;
        #endregion

        #region Properties

        /// <summary>
        /// Returns/Sets Index in this track.
        /// </summary>
        /// <param name="indexnumber">Index in the track.</param>
        /// <returns>Index at indexnumber.</returns>
        public Index this[int indexnumber]
        {
            get
            {
                return m_Indices[indexnumber];
            }
            set
            {
                m_Indices[indexnumber] = value;
            }
        }


        public string[] Comments
        {
            get { return m_Comments; }
            set { m_Comments = value; }
        }


        public AudioFile DataFile
        {
            get { return m_DataFile; }
            set { m_DataFile = value; }
        }

        /// <summary>
        /// Lines in the cue file that don't belong or have other general syntax errors.
        /// </summary>
        public string[] Garbage
        {
            get { return m_Garbage; }
            set { m_Garbage = value; }
        }

        public Index[] Indices
        {
            get { return m_Indices; }
            set { m_Indices = value; }
        }

        public string ISRC
        {
            get { return m_ISRC; }
            set { m_ISRC = value; }
        }

        public string Performer
        {
            get { return m_Performer; }
            set { m_Performer = value; }
        }

        public Index PostGap
        {
            get { return m_PostGap; }
            set { m_PostGap = value; }
        }

        public Index PreGap
        {
            get { return m_PreGap; }
            set { m_PreGap = value; }
        }

        public string Songwriter
        {
            get { return m_Songwriter; }
            set { m_Songwriter = value; }
        }

        /// <summary>
        /// If the TITLE command appears before any TRACK commands, then the string will be encoded as the title of the entire disc.
        /// </summary>
        public string Title
        {
            get { return m_Title; }
            set { m_Title = value; }
        }

        public DataType TrackDataType
        {
            get { return m_TrackDataType; }
            set { m_TrackDataType = value; }
        }

        public Flags[] TrackFlags
        {
            get { return m_TrackFlags; }
            set { m_TrackFlags = value; }
        }

        public int TrackNumber
        {
            get { return m_TrackNumber; }
            set { m_TrackNumber = value; }
        }
        #endregion

        #region Contructors

        public Track(int tracknumber, string datatype)
        {
            m_TrackNumber = tracknumber;

            switch (datatype.Trim ().ToUpper ())
            {
                case "AUDIO":
                    m_TrackDataType = DataType.AUDIO;
                    break;
                case "CDG":
                    m_TrackDataType = DataType.CDG;
                    break;
                case "MODE1/2048":
                    m_TrackDataType = DataType.MODE1_2048;
                    break;
                case "MODE1/2352":
                    m_TrackDataType = DataType.MODE1_2352;
                    break;
                case "MODE2/2336":
                    m_TrackDataType = DataType.MODE2_2336;
                    break;
                case "MODE2/2352":
                    m_TrackDataType = DataType.MODE2_2352;
                    break;
                case "CDI/2336":
                    m_TrackDataType = DataType.CDI_2336;
                    break;
                case "CDI/2352":
                    m_TrackDataType = DataType.CDI_2352;
                    break;
                default:
                    m_TrackDataType = DataType.AUDIO;
                    break;
            }

            m_TrackFlags = new Flags[0];
            m_Songwriter = "";
            m_Title = "";
            m_ISRC = "";
            m_Performer = "";
            m_Indices = new Index[0];
            m_Garbage = new string[0];
            m_Comments = new string[0];
            m_PreGap = new Index(-1, 0, 0, 0);
            m_PostGap = new Index(-1, 0, 0, 0);
            m_DataFile = new AudioFile();
        }

        public Track(int tracknumber, DataType datatype)
        {
            m_TrackNumber = tracknumber;
            m_TrackDataType = datatype;

            m_TrackFlags = new Flags[0];
            m_Songwriter = "";
            m_Title = "";
            m_ISRC = "";
            m_Performer = "";
            m_Indices = new Index[0];
            m_Garbage = new string[0];
            m_Comments = new string[0];
            m_PreGap = new Index(-1, 0, 0, 0);
            m_PostGap = new Index(-1, 0, 0, 0);
            m_DataFile = new AudioFile();
        }
        
        #endregion

        #region Methods
        public void AddFlag(Flags flag)
        {
            //if it's not a none tag
            //and if the tags hasn't already been added
            if (flag != Flags.NONE && NewFlag(flag) == true)
            {
                m_TrackFlags = (Flags[])CueSheet.ResizeArray(m_TrackFlags, m_TrackFlags.Length + 1);
                m_TrackFlags[m_TrackFlags.Length - 1] = flag;
            }
        }

        public void AddFlag(string flag)
        {
            switch (flag.Trim().ToUpper())
            {
                case "DATA":
                    AddFlag(Flags.DATA);
                    break;
                case "DCP":
                    AddFlag(Flags.DCP);
                    break;
                case "4CH":
                    AddFlag(Flags.CH4);
                    break;
                case "PRE":
                    AddFlag(Flags.PRE);
                    break;
                case "SCMS":
                    AddFlag(Flags.SCMS);
                    break;
                default:
                    return;
            }
        }

        public void AddGarbage(string garbage)
        {
            if (garbage.Trim() != "")
            {
                m_Garbage = (string[])CueSheet.ResizeArray(m_Garbage, m_Garbage.Length + 1);
                m_Garbage[m_Garbage.Length - 1] = garbage;
            }
        }

        public void AddComment(string comment)
        {
            if (comment.Trim() != "")
            {
                m_Comments = (string[])CueSheet.ResizeArray(m_Comments, m_Comments.Length + 1);
                m_Comments[m_Comments.Length - 1] = comment;
            }
        }

        public void AddIndex(int number, int minutes, int seconds, int frames)
        {
            m_Indices = (Index[])CueSheet.ResizeArray(m_Indices, m_Indices.Length + 1);

            m_Indices[m_Indices.Length - 1] = new Index(number, minutes, seconds, frames);
        }

        public void RemoveIndex(int indexIndex)
        {
            for (int i = indexIndex; i < m_Indices.Length - 1; i++)
            {
                m_Indices[i] = m_Indices[i + 1];
            }
            m_Indices = (Index[])CueSheet.ResizeArray(m_Indices, m_Indices.Length - 1);
        }

        /// <summary>
        /// Checks if the flag is indeed new in this track.
        /// </summary>
        /// <param name="flag">The new flag to be added to the track.</param>
        /// <returns>True if this flag doesn't already exist.</returns>
        private bool NewFlag(Flags new_flag)
        {
            foreach (Flags flag in m_TrackFlags)
            {
                if (flag == new_flag)
                {
                    return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            StringBuilder output = new StringBuilder();

            //write file
            if (m_DataFile.Filename != null && m_DataFile.Filename.Trim() != "")
            {
                output.Append("FILE \"" + m_DataFile.Filename.Trim() + "\" " + m_DataFile.Filetype.ToString() + Environment.NewLine);
            }

            output.Append("  TRACK " + m_TrackNumber.ToString().PadLeft(2, '0') + " " + m_TrackDataType.ToString().Replace('_', '/'));

            //write comments
            foreach (string comment in m_Comments)
            {
                output.Append(Environment.NewLine + "    REM " + comment);
            }

            if (m_Performer.Trim() != "")
            {
                output.Append(Environment.NewLine + "    PERFORMER \"" + m_Performer + "\"");
            }

            if (m_Songwriter.Trim() != "")
            {
                output.Append(Environment.NewLine + "    SONGWRITER \"" + m_Songwriter + "\"");
            }

            if (m_Title.Trim() != "")
            {
                output.Append(Environment.NewLine + "    TITLE \"" + m_Title + "\"");
            }

            //write flags
            if (m_TrackFlags.Length > 0)
            {
                output.Append(Environment.NewLine + "    FLAGS");
            }

            foreach (Flags flag in m_TrackFlags)
            {
                output.Append(" " + flag.ToString().Replace("CH4", "4CH"));
            }

            //write isrc
            if (m_ISRC.Trim() != "")
            {
                output.Append(Environment.NewLine + "    ISRC " + m_ISRC.Trim());
            }

            //write pregap
            if (m_PreGap.Number != -1)
            {
                output.Append(Environment.NewLine + "    PREGAP " + m_PreGap.Minutes.ToString().PadLeft(2, '0') + ":" + m_PreGap.Seconds.ToString().PadLeft(2, '0') + ":" + m_PreGap.Frames.ToString().PadLeft(2, '0'));
            }

            //write Indices
            for (int j = 0; j < m_Indices.Length; j++)
            {
                output.Append(Environment.NewLine + "    INDEX " + this[j].Number.ToString().PadLeft(2, '0') + " " + this[j].Minutes.ToString().PadLeft(2, '0') + ":" + this[j].Seconds.ToString().PadLeft(2, '0') + ":" + this[j].Frames.ToString().PadLeft(2, '0'));
            }

            //write postgap
            if (m_PostGap.Number != -1)
            {
                output.Append(Environment.NewLine + "    POSTGAP " + m_PostGap.Minutes.ToString().PadLeft(2, '0') + ":" + m_PostGap.Seconds.ToString().PadLeft(2, '0') + ":" + m_PostGap.Frames.ToString().PadLeft(2, '0'));
            }

            return output.ToString();
        }

        #endregion Methods
    }
}
