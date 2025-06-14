﻿using log4net;
using ManagedNativeWifi.Simple;
using MissionPlanner.Arduino;
using MissionPlanner.ArduPilot;
using MissionPlanner.Comms;
using px4uploader;
using SharpAdbClient;
using solo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace MissionPlanner.Utilities
{
    public class Firmware
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event ProgressEventHandler Progress;

        string firmwareurl = "https://github.com/ArduPilot/binary/raw/master/Firmware/firmware2.xml;https://firmware.ardupilot.org/Tools/MissionPlanner/Firmware/firmware2.xml";

        static readonly string gholdurl = ("https://github.com/diydrones/binary/raw/!Hash!/Firmware/firmware2.xml");
        static readonly string gholdfirmwareurl = ("https://github.com/diydrones/binary/raw/!Hash!/Firmware/!Firmware!");

        static string[] gholdurls = new string[] { };

        public static List<KeyValuePair<string, string>> niceNames = new List<KeyValuePair<string, string>>();

        private optionsObject options = new optionsObject();

        [Serializable]
        [XmlType(TypeName = "options")]
        public class optionsObject
        {
            [XmlElement(ElementName = "Firmware")]
            public List<software> softwares = new List<software>();
        }

        [Serializable]
        [XmlType(TypeName = "Firmware")]
        public class software
        {
            public string url = "";
            public string url2560 = "";
            [XmlElement(ElementName = "url2560-2")]
            public string url2560_2 = "";
            public string urlpx4v1 = "";
            public string urlpx4rl = "";
            public string urlpx4v2 = "";
            public string urlpx4v3 = "";
            public string urlpx4v4 = "";
            public string urlpx4v4pro = "";
            public string urlvrbrainv40 = "";
            public string urlvrbrainv45 = "";
            public string urlvrbrainv50 = "";
            public string urlvrbrainv51 = "";
            public string urlvrbrainv52 = "";
            public string urlvrbrainv54 = "";
            public string urlvrcorev10 = "";
            public string urlvrubrainv51 = "";
            public string urlvrubrainv52 = "";
            public string urlbebop2 = "";
            public string urldisco = "";
            public string urlnxpfmuk66 = "";

            // chibios - libraries\AP_HAL_ChibiOS\hwdef
            public string urlfmuv2 = "";
            public string urlfmuv3 = "";
            public string urlfmuv4 = "";
            public string urlfmuv5 = "";
            public string urlrevomini = "";
            public string urlmindpxv2 = "";

            public string name = "";
            public string desc = "";
            public int k_format_version;

            public override string ToString()
            {
                return this.ToJSON();
            }
        }

        public static string getUrl(string hash, string filename)
        {
            if (hash.ToLower().StartsWith("http"))
            {
                if (filename == "")
                    return hash;

                var url = new Uri(hash);
                return new Uri(url, filename).AbsoluteUri;
            }

            foreach (string x in gholdurls)
            {
                if (x == hash)
                {
                    if (filename == "")
                        return gholdurl.Replace("!Hash!", hash);
                    string fn = Path.GetFileName(filename);
                    filename = gholdfirmwareurl.Replace("!Hash!", hash);
                    filename = filename.Replace("!Firmware!", fn);
                    return filename;
                }
            }
            return "";
        }

        static Firmware()
        {
            string file = Path.GetDirectoryName(Path.GetFullPath(Assembly.GetExecutingAssembly().Location)) + Path.DirectorySeparatorChar +
                          "FirmwareHistory.txt";

            if (!File.Exists(file))
            {
                //CustomMessageBox.Show("Missing FirmwareHistory.txt file");
                return;
            }

            gholdurls = File.ReadAllLines(file);
            int a = 0;
            foreach (string gh in gholdurls)
            {
                if (gh.Length > 40)
                {
                    int index = gh.IndexOf(' ');

                    if (index >= 40)
                    {
                        gholdurls[a] = gh.Trim().Substring(0, index);
                    }
                    else
                    {
                        continue;
                    }

                    try
                    {
                        niceNames.Add(new KeyValuePair<string, string>(gholdurls[a], gh.Substring(index + 1).Trim()));
                    }
                    catch
                    {
                        niceNames.Add(new KeyValuePair<string, string>(gholdurls[a], gholdurls[a]));
                    }

                    a++;
                }
            }
        }

        /// <summary>
        /// Load firmware history from file
        /// </summary>
        public Firmware()
        {
        }

        /// <summary>
        /// Load xml from internet based on firmwareurl, and return softwarelist
        /// </summary>
        /// <returns></returns>
        public List<software> getFWList(string firmwareurl = "")
        {
            if (firmwareurl == "")
                firmwareurl = this.firmwareurl;

            // mirror support
            log.Info("getFWList");

            options.softwares.Clear();

            software temp = new software();

            updateProgress(-1, Strings.GettingFWList);

            var urls = firmwareurl.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var valid = false;
            Exception invalidex = null;

            foreach (var url in urls)
            {
                try
                {
                    var dnsinfo = Dns.GetHostAddresses(new Uri(url).DnsSafeHost);

                    if (dnsinfo.Length == 0)
                        throw new Exception("Failed to resolve dns");

                    XmlSerializer xms = new XmlSerializer(typeof(optionsObject), new Type[] { typeof(software) });

                    log.Info("url: " + url);
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", Settings.Instance.UserAgent);
                    client.Timeout = TimeSpan.FromSeconds(30);

                    using (var response = client.GetAsync(url))
                    using (XmlReader xmlreader = XmlReader.Create(response.Result.Content.ReadAsStreamAsync().Result))
                    {
                        options = (optionsObject)xms.Deserialize(xmlreader);
                    }

                    valid = true;

                    Parallel.ForEach(options.softwares, software =>
                    {
                        try
                        {
                            getAPMVersion(software);
                        }
                        catch
                        {
                        }
                    });

                    break;
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    //CustomMessageBox.Show("Failed to get Firmware List : " + ex.Message);
                    invalidex = ex;
                }
            }

            if (!valid)
                throw invalidex;

            log.Info("load done");

            updateProgress(-1, Strings.ReceivedList);

            return options.softwares;
        }
      
        void updateProgress(int percent, string status)
        {
            if (Progress != null)
            {
                Progress(percent, status);
            }
        }

        object urlcachelock = new object();
        Dictionary<string, SemaphoreSlim> urlcacheSem = new Dictionary<string, SemaphoreSlim>();
        static Dictionary<string, string> urlcache = new Dictionary<string, string>();

        /// <summary>
        /// Get fw version from firmeware.diydrones.com
        /// </summary>
        /// <param name="fwurl"></param>
        /// <returns></returns>
        void getAPMVersion(object tempin)
        {
            try
            {
                software temp = (software)tempin;

                string baseurl = temp.urlfmuv3;
                string baseurl2 = temp.urlpx4v2;

                if (!Download.CheckHTTPFileExists(baseurl))
                {
                    baseurl = baseurl2;
                }

                if (baseurl == "" || !baseurl.ToLower().StartsWith("http")) return;

                Uri url = new Uri(new Uri(baseurl), "git-version.txt");

                log.Info("Get url " + url.ToString() + " for " + temp.name);

                updateProgress(-1, Strings.GettingFWVersion);

                var line = GetAPMVERSIONFile(url);

                // get index
                var index = options.softwares.IndexOf(temp);
                // get item to modify
                var item = options.softwares[index];
                // move existing name
                item.desc = item.name;
                // change name
                item.name = line.Substring(line.IndexOf(':') + 2);
                // save back to list
                options.softwares[index] = item;
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        public string GetAPMVERSIONFile(Uri url)
        {
            lock (urlcachelock)
                if (!urlcacheSem.ContainsKey(url.AbsoluteUri))
                    urlcacheSem[url.AbsoluteUri] = new SemaphoreSlim(1, 1);

            urlcacheSem[url.AbsoluteUri].Wait();

            try
            {
                lock (urlcachelock)
                    if (urlcache.ContainsKey(url.AbsoluteUri))
                    {
                        log.Info("GetAPMVERSIONFile: using cache " + url.AbsoluteUri);
                        return urlcache[url.AbsoluteUri];
                    }

                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", Settings.Instance.UserAgent);
                client.Timeout = TimeSpan.FromSeconds(30);

                using (var response = client.GetAsync(url))
                using (StreamReader sr = new StreamReader(response.GetAwaiter().GetResult().Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();

                        if (line.Contains("APMVERSION:"))
                        {
                            log.Info(line);

                            lock (urlcachelock)
                                urlcache[url.AbsoluteUri] = line;

                            return line;
                        }
                    }
                }
            }
            finally
            {
                urlcacheSem[url.AbsoluteUri].Release();
            }

            throw new TimeoutException();
        }

        /// <summary>
        /// Do full update - get firmware from internet
        /// </summary>
        /// <param name="comport"></param>
        /// <param name="temp"></param>
        /// <param name="historyhash"></param>
        /// <param name="relType"></param>
        public bool updateLegacy(string comport, software temp, string historyhash, List<DeviceInfo> ports)
        {
            BoardDetect.boards board = BoardDetect.boards.none;
            string baseurl = "";

            try
            {
                updateProgress(-1, Strings.DetectingBoardVersion);

                if (board != BoardDetect.boards.pass)
                    board = BoardDetect.DetectBoard(comport, ports);

                if (board == BoardDetect.boards.none)
                {
                    CustomMessageBox.Show(Strings.CantDetectBoardVersion);
                    return false;
                }

                log.Info("Detected a " + board);

                updateProgress(-1, Strings.DetectedA + board);

                // modify board to update to new bootloader
                if ((board == BoardDetect.boards.px4v2 || board == BoardDetect.boards.px4v3) && historyhash == "")
                {
                    //
                    if ((int)DialogResult.Yes ==
                        CustomMessageBox.Show("Is this a CubeBlack?", "CubeBlack", MessageBoxButtons.YesNo))
                    {
                        BoardDetect.chbootloader = "CubeBlack";
                        board = BoardDetect.boards.chbootloader;
                    }
                }

                if (board == BoardDetect.boards.b2560)
                {
                    baseurl = temp.url2560.ToString();
                }
                else if (board == BoardDetect.boards.b1280)
                {
                    baseurl = temp.url.ToString();
                }
                else if (board == BoardDetect.boards.b2560v2)
                {
                    baseurl = temp.url2560_2.ToString();
                }
                else if (board == BoardDetect.boards.px4)
                {
                    baseurl = temp.urlpx4v1.ToString();
                }
                else if (board == BoardDetect.boards.px4rl)
                {
                    baseurl = temp.urlpx4rl.ToString();
                }
                else if (board == BoardDetect.boards.px4v2)
                {
                    baseurl = temp.urlpx4v2.ToString();
                    baseurl = CheckChibiOS(baseurl, temp.urlfmuv2);
                }
                else if (board == BoardDetect.boards.px4v3)
                {
                    baseurl = temp.urlpx4v3.ToString();

                    if (String.IsNullOrEmpty(baseurl) || !Download.CheckHTTPFileExists(baseurl))
                    {
                        baseurl = temp.urlpx4v2.ToString();
                    }

                    baseurl = CheckChibiOS(baseurl, temp.urlfmuv3);
                }
                else if (board == BoardDetect.boards.px4v4)
                {
                    baseurl = temp.urlpx4v4.ToString();
                    baseurl = CheckChibiOS(baseurl, temp.urlfmuv4);
                }
                else if (board == BoardDetect.boards.fmuv5)
                {
                    baseurl = temp.urlfmuv5;
                }
                else if (board == BoardDetect.boards.px4v4pro)
                {
                    baseurl = temp.urlpx4v4pro.ToString();
                }
                else if (board == BoardDetect.boards.vrbrainv40)
                {
                    baseurl = temp.urlvrbrainv40.ToString();
                }
                else if (board == BoardDetect.boards.vrbrainv45)
                {
                    baseurl = temp.urlvrbrainv45.ToString();
                }
                else if (board == BoardDetect.boards.vrbrainv50)
                {
                    baseurl = temp.urlvrbrainv50.ToString();
                }
                else if (board == BoardDetect.boards.vrbrainv51)
                {
                    baseurl = temp.urlvrbrainv51.ToString();
                }
                else if (board == BoardDetect.boards.vrbrainv52)
                {
                    baseurl = temp.urlvrbrainv52.ToString();
                }
                else if (board == BoardDetect.boards.vrbrainv54)
                {
                    baseurl = temp.urlvrbrainv54.ToString();
                }
                else if (board == BoardDetect.boards.vrcorev10)
                {
                    baseurl = temp.urlvrcorev10.ToString();
                }
                else if (board == BoardDetect.boards.vrubrainv51)
                {
                    baseurl = temp.urlvrubrainv51.ToString();
                }
                else if (board == BoardDetect.boards.vrubrainv52)
                {
                    baseurl = temp.urlvrubrainv52.ToString();
                }
                else if (board == BoardDetect.boards.bebop2)
                {
                    baseurl = temp.urlbebop2.ToString();
                }
                else if (board == BoardDetect.boards.disco)
                {
                    baseurl = temp.urldisco.ToString();
                }
                else if (board == BoardDetect.boards.revomini)
                {
                    baseurl = temp.urlrevomini.ToString();
                }
                else if (board == BoardDetect.boards.mindpxv2)
                {
                    baseurl = temp.urlmindpxv2.ToString();
                }
                else if (board == BoardDetect.boards.nxpfmuk66)
                {
                    baseurl = temp.urlnxpfmuk66.ToString();
                }
                else if (board == BoardDetect.boards.chbootloader)
                {
                    baseurl = temp.urlfmuv2.Replace("fmuv2", BoardDetect.chbootloader);

                    if (String.IsNullOrEmpty(baseurl) || !Download.CheckHTTPFileExists(baseurl))
                    {
                        CustomMessageBox.Show(Strings.No_firmware_available_for_this_board);
                        return false;
                    }
                }
                else if (board == BoardDetect.boards.pass)
                {

                }
                else
                {
                    CustomMessageBox.Show(Strings.InvalidBoardType);
                    return false;
                }

                if (board < BoardDetect.boards.px4)
                {
                    CustomMessageBox.Show(Strings.ThisBoardHasBeenRetired, Strings.Note);
                }

                if (historyhash != "")
                    baseurl = getUrl(historyhash, baseurl);

                // update to use mirror url
                log.Info("Using " + baseurl);

                var starttime = DateTime.Now;

                Download.getFilefromNet(baseurl, Settings.GetUserDataDirectory() +
                                                 @"firmware.hex",
                    (i, s) => updateProgress(i, s));

                var timetook = (DateTime.Now - starttime).TotalMilliseconds;

                Tracking.AddTiming("Firmware Download", board.ToString(), timetook, temp.name);

                updateProgress(100, Strings.DownloadedFromInternet);
                log.Info("Downloaded");
            }
            catch (Exception ex)
            {
                updateProgress(50, Strings.FailedDownload);
                CustomMessageBox.Show("Failed to download new firmware : " + ex.ToString());
                return false;
            }

            MissionPlanner.Utilities.Tracking.AddFW(temp.name, board.ToString());

            var uploadstarttime = DateTime.Now;

            var ans = UploadFlash(comport,
                Settings.GetUserDataDirectory() + @"firmware.hex", board);

            var uploadtime = (DateTime.Now - uploadstarttime).TotalMilliseconds;

            Tracking.AddTiming("Firmware Upload", board.ToString(), uploadtime, temp.name);

            return ans;
        }

        private string CheckChibiOS(string existingfw, string chibiosurl)
        {
            try
            {
                if (String.IsNullOrEmpty(chibiosurl) || !Download.CheckHTTPFileExists(chibiosurl))
                {
                    return existingfw;
                }
            }
            catch (UriFormatException)
            {

            }

            if (CustomMessageBox.Show("Upload ChibiOS", "ChibiOS", MessageBoxButtons.YesNo) == (int)DialogResult.Yes)
            {
                return chibiosurl;
            }

            return existingfw;
        }

        /// <summary>
        /// upload to px4 standalone
        /// </summary>
        /// <param name="filename"></param>
        public bool UploadPX4(string filename, BoardDetect.boards board)
        {
            updateProgress(-1, "Reading Hex File");
            px4uploader.Firmware fw;
            try
            {
                fw = px4uploader.Firmware.ProcessFirmware(filename);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(Strings.ErrorFirmwareFile + "\n\n" + ex.ToString(), Strings.ERROR);
                return false;
            }

            AttemptRebootToBootloader();

            DateTime DEADLINE = DateTime.Now.AddSeconds(30);

            updateProgress(-1, "Scanning comports");

            while (DateTime.Now < DEADLINE)
            {
                log.Info(DateTime.Now.Millisecond + " get Ports ");
                string[] allports = SerialPort.GetPortNames();

                var foundboard = false;
                var result = false;
                Uploader uploader = null;

                Parallel.ForEach(allports, (port,state) =>
                {
                    log.Info(DateTime.Now.Millisecond + " Trying Port " + port);

                    Uploader up;

                    try
                    {
                        up = new Uploader(port, 115200);
                    }
                    catch (Exception ex)
                    {
                        //System.Threading.Thread.Sleep(50);
                        log.Debug(port + " " +ex.Message);
                        return;
                    }

                    try
                    {
                        if (state.ShouldExitCurrentIteration)
                        {
                            up.close();
                            return;
                        }

                        log.Info(DateTime.Now.Millisecond + " Trying identify " + port);
                        up.identify();

                        if (state.ShouldExitCurrentIteration)
                        {
                            up.close();
                            return;
                        }

                        updateProgress(-1, port + " Identify");
                        log.InfoFormat(
                            "Found board type {0} brdrev {1} blrev {2} fwmax {3} chip {5:X} chipdes {6} on {4} extmax {7}",
                            up.board_type,
                            up.board_rev, up.bl_rev, up.fw_maxsize, port, up.chip, up.chip_desc, up.extf_maxsize);

                        // if the apj is not for the detected board type - keep looking
                        if (up.board_type != fw.board_id)
                        {
                            log.InfoFormat("Board type mismatch - keep looking, detected {0}, fw file {1}", up.board_type, fw.board_id);
                            up.close();
                            return;
                        }

                        up.ProgressEvent += new Uploader.ProgressEventHandler(up_ProgressEvent);
                        up.LogEvent += new Uploader.LogEventHandler(up_LogEvent);
                        up.identify();
                        state.Break();
                        foundboard = true;
                        uploader = up;
                    }
                    catch (Exception ex)
                    {
                        log.Debug(port + " Not There.. " + ex.Message);
                        up.close();
                        return;
                    }
                });

                log.Info(DateTime.Now.Millisecond + " Portscan done found:" + foundboard);
                Application.DoEvents();

                if (foundboard)
                {
                    updateProgress(-1, "Connecting");

                    // test if pausing here stops - System.TimeoutException: The write timed out.
                    System.Threading.Thread.Sleep(500);

                    try
                    {
                        uploader.currentChecksum(fw);
                    }
                    catch (IOException ex)
                    {
                        log.Error(ex);
                        CustomMessageBox.Show("потеряна связь с платой", "потеря связи");
                        uploader.close();
                        result = false;
                        return false;
                    }
                    catch (TimeoutException ex)
                    {
                        log.Error(ex);
                        CustomMessageBox.Show("потеряна связь с платой", "тайм-аут");
                        uploader.close();
                        result = false;
                        return false;
                    }
                    catch
                    {
                        uploader.__reboot();
                        uploader.close();
                        CustomMessageBox.Show(Strings.NoNeedToUpload);
                        result = true;
                        return true;
                    }

                    try
                    {
                        updateProgress(0, "Upload");
                        uploader.upload(fw);
                        updateProgress(100, "Upload Done");
                    }
                    catch (Exception ex)
                    {
                        updateProgress(0, "ERROR: " + ex.Message);
                        log.Info(ex);
                        Console.WriteLine(ex.ToString());
                        result = false;
                        return false;
                    }
                    finally
                    {
                        uploader.close();
                    }

                    result = true;
                    return result;
                }
            }

            updateProgress(0, "ERROR: No Response from board");
            return false;
        }

        private void AttemptRebootToBootloader()
        {
            string[] allports = SerialPort.GetPortNames();

            List<Task<bool>> tasklist = new List<Task<bool>>();
            // check if its in BL mode already
            foreach (string port in allports)
            {
                log.Info(DateTime.Now.Millisecond + " Trying Port " + port);
                try
                {
                    var task = Task.Run(() =>
                    {
                        using (var up = new Uploader(port, 115200))
                        {
                            up.identify();
                            return true;
                        }

                        return false;
                    });

                    tasklist.Add(task);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

            foreach (var task in tasklist)
            {
                try
                {
                    if (task.Wait(TimeSpan.FromSeconds(3)) && task.GetAwaiter().GetResult() == true)
                        return;
                    else
                    {
                        //not there
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

            if (MainV2.comPort.BaseStream is SerialPort)
            {
                try
                {
                    updateProgress(-1, "Look for HeartBeat");
                    var task = Task.Run(() =>
                    {
                        // check if we are seeing heartbeats
                        MainV2.comPort.BaseStream.Open();
                        MainV2.comPort.giveComport = true;

                        if (MainV2.comPort.getHeartBeat().Length > 0)
                        {
                            MainV2.comPort.doReboot(true, false);
                            MainV2.comPort.Close();
                        }
                        else
                        {
                            MainV2.comPort.BaseStream.Close();
                            throw new Exception("No HeartBeat found");
                        }
                    });
                    if (task.Wait(TimeSpan.FromSeconds(3)))
                    {
                        updateProgress(-1, "Reboot to Bootloader");
                    }
                    else
                    {
                        CustomMessageBox.Show(Strings.PleaseUnplugTheBoardAnd);
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    CustomMessageBox.Show(Strings.PleaseUnplugTheBoardAnd);
                }
            }
        }

        /// <summary>
        /// upload to vrbrain standalone
        /// </summary>
        /// <param name="filename"></param>
        public bool UploadVRBRAIN(string filename, BoardDetect.boards board)
        {
            px4uploader.Uploader up;
            updateProgress(0, "Reading Hex File");
            px4uploader.Firmware fw;
            try
            {
                fw = px4uploader.Firmware.ProcessFirmware(filename);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(Strings.ErrorFirmwareFile + "\n\n" + ex.ToString(), Strings.ERROR);
                return false;
            }

            try
            {
                // check if we are seeing heartbeats
                MainV2.comPort.BaseStream.Open();
                MainV2.comPort.giveComport = true;

                if (MainV2.comPort.getHeartBeat().Length > 0)
                {
                    MainV2.comPort.doReboot(true, false);
                    MainV2.comPort.Close();

                    //specific action for VRBRAIN4 board that needs to be manually disconnected before uploading
                    if (board == BoardDetect.boards.vrbrainv40)
                    {
                        CustomMessageBox.Show(
                            "VRBRAIN 4 detected. Please unplug the board, and then press OK and plug back in.\n");
                    }
                }
                else
                {
                    MainV2.comPort.BaseStream.Close();
                    CustomMessageBox.Show(Strings.PleaseUnplugTheBoardAnd);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                CustomMessageBox.Show(Strings.PleaseUnplugTheBoardAnd);
            }

            DateTime DEADLINE = DateTime.Now.AddSeconds(30);

            updateProgress(0, "Scanning comports");

            while (DateTime.Now < DEADLINE)
            {
                string[] allports = SerialPort.GetPortNames();

                foreach (string port in allports)
                {
                    log.Info(DateTime.Now.Millisecond + " Trying Port " + port);

                    updateProgress(-1, "Connecting");

                    try
                    {
                        up = new px4uploader.Uploader(port, 115200);
                    }
                    catch (Exception ex)
                    {
                        //System.Threading.Thread.Sleep(50);
                        Console.WriteLine(ex.Message);
                        continue;
                    }

                    try
                    {
                        up.identify();
                        updateProgress(-1, "Identify");
                        log.InfoFormat("Found board type {0} brdrev {1} blrev {2} fwmax {3} chip {5:X} chipdes {6} on {4}", up.board_type,
                        up.board_rev, up.bl_rev, up.fw_maxsize, port, up.chip, up.chip_desc);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Not There..");
                        //Console.WriteLine(ex.Message);
                        up.close();
                        continue;
                    }

                    up.skipotp = true;

                    try
                    {
                        up.currentChecksum(fw);
                    }
                    catch
                    {
                        up.__reboot();
                        up.close();
                        CustomMessageBox.Show(Strings.NoNeedToUpload);
                        return true;
                    }

                    try
                    {
                        up.ProgressEvent += new px4uploader.Uploader.ProgressEventHandler(up_ProgressEvent);
                        up.LogEvent += new px4uploader.Uploader.LogEventHandler(up_LogEvent);

                        updateProgress(0, "Upload");
                        up.upload(fw);
                        updateProgress(100, "Upload Done");
                    }
                    catch (Exception ex)
                    {
                        updateProgress(0, "ERROR: " + ex.Message);
                        log.Info(ex);
                        Console.WriteLine(ex.ToString());
                        return false;
                    }
                    finally
                    {
                        up.close();
                    }

                    if (up.board_type == 1140 || up.board_type == 1145 || up.board_type == 1150 || up.board_type == 1151 ||
                        up.board_type == 1152 || up.board_type == 1210 || up.board_type == 1351 || up.board_type == 1352 ||
                        up.board_type == 1411 || up.board_type == 1520)
                    {
                        //VR boards have no tone alarm
                        if (up.board_type == 1140)
                            CustomMessageBox.Show("Загрузка завершена! Пожалуйста, отключите и заново подключите плату.");
                        else
                            CustomMessageBox.Show("Загрузка завершена!");
                    }
                    else
                    {
                        // wait for IO firmware upgrade and boot to a mavlink state
                        CustomMessageBox.Show(Strings.PleaseWaitForTheMusicalTones);
                    }
                    return true;
                }
            }

            updateProgress(0, "ERROR: No Response from board");
            return false;
        }

        /// <summary>
        /// upload to Parrot boards
        /// </summary>
        /// <param name="filename"></param>
        public bool UploadParrot(string filename, BoardDetect.boards board)
        {
            string vehicleName = board.ToString().Substring(0, 1).ToUpper() + board.ToString().Substring(1).ToLower();
            Ping ping = new Ping();
            PingReply pingReply = pingParrotVehicle(ping);

            updateProgress(0, "Trying to connect to " + vehicleName);

            if (pingReply == null || pingReply.Status != IPStatus.Success)
            {
                bool ssidFound = isParrotWifiConnected(vehicleName);

                if (!ssidFound)
                {
                    CustomMessageBox.Show("Подключитесь к Wi-Fi " + vehicleName + " и нажмите OK", vehicleName, MessageBoxButtons.OK);
                    ssidFound = isParrotWifiConnected(vehicleName);
                    pingReply = pingParrotVehicle(ping);
                }

                while (pingReply == null || pingReply.Status != IPStatus.Success)
                {
                    if (!ssidFound)
                    {
                        if (CustomMessageBox.Show("Похоже, вы не подключены к Wi-Fi " + vehicleName + ". Подключитесь и нажмите OK, чтобы попробовать снова", vehicleName, MessageBoxButtons.OKCancel) == (int)DialogResult.Cancel)
                        {
                            return false;
                        }
                    }
                    else if (CustomMessageBox.Show("Вы подключены к Wi-Fi " + vehicleName + ", но ответа нет. Повторить попытку?", vehicleName, MessageBoxButtons.OKCancel) == (int)DialogResult.Cancel)
                    {
                        return false;
                    }

                    ssidFound = isParrotWifiConnected(vehicleName);
                    pingReply = pingParrotVehicle(ping);
                }
            }

            try
            {
                AdbServer.Instance.StartServer("adb.exe", true);
                IAdbClient adbClient = AdbClient.Instance;

                string response = adbClient.Connect(new DnsEndPoint("192.168.42.1", 9050));

                if (!response.Contains("connected to 192.168.42.1:9050"))
                {
                    string ntimes = "four";

                    if (board == BoardDetect.boards.disco)
                    {
                        ntimes = "two";
                    }

                    CustomMessageBox.Show("Нажмите кнопку питания " + vehicleName + " " + ntimes + " раз", vehicleName, MessageBoxButtons.OK);
                    response = adbClient.Connect(new DnsEndPoint("192.168.42.1", 9050));

                    while (!response.Contains("connected to 192.168.42.1:9050"))
                    {
                        if (CustomMessageBox.Show("Не удалось связаться с " + vehicleName + ". Нажмите кнопку питания " + ntimes + " раз. Попробовать снова?", vehicleName, MessageBoxButtons.OKCancel) == (int)DialogResult.Cancel)
                        {
                            return false;
                        }

                        response = adbClient.Connect(new DnsEndPoint("192.168.42.1", 9050));
                    }
                }

                DeviceData device = adbClient.GetDevices().First();
                ConsoleOutputReceiver consoleOut = new ConsoleOutputReceiver();

                try
                {
                    using (SyncService service = new SyncService(device))
                    {
                        using (FileStream stream = File.OpenRead(filename))
                        {
                            updateProgress(10, "Uploading software...");
                            service.Push(stream, "/data/ftp/internal_000/APM/" + (board == BoardDetect.boards.disco ? "apm-plane-disco" : "arducopter"), 777, DateTime.Now, CancellationToken.None);
                            updateProgress(50, "Software uploaded");
                        }

                        if (board != BoardDetect.boards.disco)
                        {
                            using (MemoryStream stream = new MemoryStream())
                            using (StreamReader sr = new StreamReader(stream))
                            {
                                updateProgress(60, "Looking for need to update init scripts...");
                                service.Pull("/etc/init.d/rcS_mode_default", stream, CancellationToken.None);

                                bool initChanged = false;
                                List<string> initLines = new List<string>();
                                string[] initAPLines = { "if test -x /data/ftp/internal_000/APM/start_ardupilot.sh && test -x /data/ftp/internal_000/APM/arducopter; then",
                                                         "  /data/ftp/internal_000/APM/start_ardupilot.sh &",
                                                         "else" };
                                int[] initAPLinesIndex = Enumerable.Repeat(-1, initAPLines.Length).ToArray();
                                int dragonLineIndex = -1;

                                stream.Seek(0, SeekOrigin.Begin);

                                while (!sr.EndOfStream)
                                {
                                    string line = sr.ReadLine();

                                    int dragonIndex = line.IndexOf("DragonStarter.sh");
                                    bool acLine = line.ToLower().Trim().StartsWith("arducopter");

                                    if (dragonIndex != -1)
                                    {
                                        int dragonCommentIndex = line.IndexOf('#');

                                        if (dragonCommentIndex != -1 && dragonCommentIndex < dragonIndex)
                                        {
                                            line = line.Remove(dragonCommentIndex, 1);
                                            initChanged = true;
                                        }

                                        if (line.Substring(0, 2) != "  ")
                                        {
                                            line = line.Insert(0, "  ");
                                            initChanged = true;
                                        }

                                        dragonLineIndex = initLines.Count;
                                    }
                                    else if (acLine)
                                    {
                                        initChanged = true;
                                        continue;
                                    }
                                    else
                                    {
                                        foreach (int i in Enumerable.Range(0, initAPLines.Length))
                                        {
                                            if (line == initAPLines[i])
                                            {
                                                initAPLinesIndex[i] = initLines.Count;
                                                break;
                                            }
                                        }
                                    }

                                    initLines.Add(line);
                                }

                                if (initAPLinesIndex[0] == -1 ||
                                    initAPLinesIndex[1] != (initAPLinesIndex[0] + 1) ||
                                    initAPLinesIndex[2] != (initAPLinesIndex[1] + 1) ||
                                    dragonLineIndex != (initAPLinesIndex[2] + 1))
                                {
                                    foreach (int i in initAPLinesIndex)
                                    {
                                        if (i != -1)
                                        {
                                            if (i < dragonLineIndex)
                                                dragonLineIndex--;

                                            initLines.RemoveAt(i);
                                        }
                                    }

                                    initLines.InsertRange(dragonLineIndex, initAPLines);

                                    if (initLines[dragonLineIndex + initAPLines.Length + 1] != "fi")
                                    {
                                        initLines.Insert(dragonLineIndex + initAPLines.Length + 1, "fi");
                                    }

                                    initChanged = true;
                                }

                                string startAPText = "#!/bin/sh\n\n" +
                                                     "# startup fan\n" +
                                                     "echo 1 > /sys/devices/platform/user_gpio/FAN/value\n\n" +
                                                     "while :; do\n" +
                                                     "  ulogger -t \"rcS_mode_default\" -p I \"Launching ArduPilot\"\n" +
                                                     "  /data/ftp/internal_000/APM/arducopter -A udp:192.168.42.255:14550:bcast -B /dev/ttyPA1 -C udp:192.168.42.255:14551:bcast -l /data/ftp/internal_000/APM/logs -t /data/ftp/internal_000/APM/terrain\n" +
                                                     "done\n";

                                // if the above script is changed, change this date to a future date
                                DateTime startAPDate = new DateTime(2016, 10, 21, 05, 10, 19);
                                FileStatistics startAPStat = service.Stat("/data/ftp/internal_000/APM/start_ardupilot.sh");

                                if (startAPStat.FileMode == 0 || startAPStat.Time.CompareTo(startAPDate) < 0)
                                {
                                    updateProgress(70, "Uploading ArduPilot startup script...");
                                    using (MemoryStream startScriptStream = new MemoryStream(sr.CurrentEncoding.GetBytes(startAPText)))
                                    {
                                        service.Push(startScriptStream, "/data/ftp/internal_000/APM/start_ardupilot.sh", 777, startAPDate, CancellationToken.None);
                                    }
                                }

                                FileStatistics binaryStat = service.Stat("/usr/bin/arducopter");

                                if (initChanged || (binaryStat.FileMode.HasFlag(UnixFileMode.Regular)))
                                {
                                    adbClient.ExecuteRemoteCommand("mount -o remount,rw /", device, consoleOut);

                                    if (binaryStat.FileMode.HasFlag(UnixFileMode.Regular))
                                    {
                                        // remove old binary location
                                        adbClient.ExecuteRemoteCommand("rm -f /usr/bin/arducopter", device, consoleOut);
                                    }

                                    if (initChanged)
                                    {
                                        // only backup init file if a backup doesn't exist
                                        adbClient.ExecuteRemoteCommand("mv -n /etc/init.d/rcS_mode_default /etc/init.d/rcS_mode_default.backup", device, consoleOut);

                                        updateProgress(80, "Writing modified init script");
                                        stream.SetLength(0);

                                        using (StreamWriter sw = new StreamWriter(stream, sr.CurrentEncoding))
                                        {
                                            sw.NewLine = "\n";
                                            initLines.ForEach(line => sw.WriteLine(line));
                                            sw.Flush();
                                            stream.Seek(0, SeekOrigin.Begin);
                                            service.Push(stream, "/etc/init.d/rcS_mode_default", 755, DateTime.Now, CancellationToken.None);

                                            // a bug in 'adb push' sets 'group' and 'other' permissions equal to 'owner' so we run chmod to have the correct original permissions
                                            adbClient.ExecuteRemoteCommand("chmod 755 /etc/init.d/rcS_mode_default", device, consoleOut);
                                        }
                                    }
                                }
                                updateProgress(90, "Scripts updated");
                            }
                        }
                    }
                }
                finally
                {
                    updateProgress(100, "Rebooting...");
                    adbClient.ExecuteRemoteCommand("reboot.sh", device, consoleOut);
                }

                CustomMessageBox.Show("Прошивка установлена!");
                updateProgress(-1, "Прошивка установлена");
            }
            catch (Exception e)
            {
                log.Error(e);
                Console.WriteLine("An error occurred: " + e.ToString());
                updateProgress(-1, "ERROR: " + e.Message);
                return false;
            }
            finally
            {
                AdbClient.Instance.KillAdb();
            }

            return true;
        }

        bool isParrotWifiConnected(string ssid)
        {
            IEnumerable<string> connectedSSIDs = NativeWifi.GetConnectedNetworkSsids();
            bool ssidFound = false;

            foreach (string ssidName in connectedSSIDs)
            {
                if (ssidName.StartsWith(ssid))
                {
                    ssidFound = true;
                    break;
                }
            }

            return ssidFound;
        }

        PingReply pingParrotVehicle(Ping ping)
        {
            try
            {
                return ping.Send("192.168.42.1");
            }
            catch (PingException)
            {
                return null;
            }
        }

        string _message = "";

        void up_LogEvent(string message, int level = 0)
        {
            log.Debug(message);

            _message = message;
            updateProgress(-1, message);
        }

        void up_ProgressEvent(double completed)
        {
            updateProgress((int)completed, _message);
        }

        /// <summary>
        /// Upload firmware
        /// </summary>
        /// <param name="comport"></param>
        /// <param name="filename"></param>
        /// <param name="board"></param>
        /// <returns>pass/fail</returns>
        public bool UploadFlash(string comport, string filename, BoardDetect.boards board)
        {
            if (board == BoardDetect.boards.px4 || board == BoardDetect.boards.px4v2 ||
                board == BoardDetect.boards.px4v3 || board == BoardDetect.boards.px4v4 ||
                board == BoardDetect.boards.px4v4pro || board == BoardDetect.boards.fmuv5 ||
                board == BoardDetect.boards.revomini || board == BoardDetect.boards.mindpxv2 ||
                board == BoardDetect.boards.minipix || board == BoardDetect.boards.chbootloader ||
                board == BoardDetect.boards.pass || board == BoardDetect.boards.nxpfmuk66)
            {
                try
                {
                    return UploadPX4(filename, board);
                }
                catch (MissingFieldException)
                {
                    CustomMessageBox.Show("Обновите программу, ваша установка повреждена", Strings.ERROR);
                    return false;
                }
            }

            if (board == BoardDetect.boards.vrbrainv40 || board == BoardDetect.boards.vrbrainv45 ||
                board == BoardDetect.boards.vrbrainv50 || board == BoardDetect.boards.vrbrainv51 ||
                board == BoardDetect.boards.vrbrainv52 || board == BoardDetect.boards.vrbrainv54 ||
                board == BoardDetect.boards.vrcorev10 ||
                board == BoardDetect.boards.vrubrainv51 || board == BoardDetect.boards.vrubrainv52)
            {
                return UploadVRBRAIN(filename, board);
            }

            if (board == BoardDetect.boards.bebop2)
            {
                return UploadParrot(filename, board);
            }

            if (board == BoardDetect.boards.solo)
            {
                return UploadSolo(filename, board);
            }

            return UploadArduino(comport, filename, board);
        }

        private bool UploadSolo(string filename, BoardDetect.boards board)
        {
            try
            {
                Solo.flash_px4(filename);
            }
            catch (SocketException)
            {
                CustomMessageBox.Show(Strings.ErrorUploadingFirmware + " for SOLO", Strings.ERROR);
                return false;
            }

            return true;
        }

        public bool UploadArduino(string comport, string filename, BoardDetect.boards board)
        {
            byte[] FLASH = new byte[1];
            try
            {
                updateProgress(0, Strings.ReadingHexFile);
                using (StreamReader sr = new StreamReader(filename))
                {
                    FLASH = readIntelHEXv2(sr);
                }
                log.InfoFormat("\n\nSize: {0}\n\n", FLASH.Length);
            }
            catch (Exception ex)
            {
                updateProgress(0, Strings.FailedReadHEX);
                CustomMessageBox.Show(Strings.FailedToReadHex + ex.Message);
                return false;
            }
            IArduinoComms port = new ArduinoSTK();

            if (board == BoardDetect.boards.b1280)
            {
                if (FLASH.Length > 126976)
                {
                    CustomMessageBox.Show("Прошивка слишком большая для 1280, обновите оборудование!");
                    return false;
                }
                //port = new ArduinoSTK();
                port.BaudRate = 57600;
            }
            else if (board == BoardDetect.boards.b2560 || board == BoardDetect.boards.b2560v2)
            {
                port = new ArduinoSTKv2
                {
                    BaudRate = 115200
                };
            }
            port.DtrEnable = true;

            try
            {
                port.PortName = comport;

                port.Open();

                if (port.connectAP())
                {
                    log.Info("starting");
                    updateProgress(0, String.Format(Strings.UploadingBytesToBoard, FLASH.Length) + board);

                    // this is enough to make ap_var reset
                    //port.upload(new byte[256], 0, 2, 0);

                    port.Progress += updateProgress;

                    if (!port.uploadflash(FLASH, 0, FLASH.Length, 0))
                    {
                        if (port.IsOpen)
                            port.Close();
                        throw new Exception("Upload failed. Lost sync. Try Arduino!!");
                    }

                    port.Progress -= updateProgress;

                    updateProgress(100, Strings.UploadComplete);

                    log.Info("Uploaded");

                    int start = 0;
                    short length = 0x100;

                    byte[] flashverify = new byte[FLASH.Length + 256];

                    updateProgress(0, Strings.VerifyFirmware);

                    while (start < FLASH.Length)
                    {
                        updateProgress((int)((start / (float)FLASH.Length) * 100), Strings.VerifyFirmware);
                        port.setaddress(start);
                        //log.Info("Downloading " + length + " at " + start);
                        port.downloadflash(length).CopyTo(flashverify, start);
                        start += length;
                    }

                    for (int s = 0; s < FLASH.Length; s++)
                    {
                        if (FLASH[s] != flashverify[s])
                        {
                            CustomMessageBox.Show(
                                String.Format(Strings.UploadSucceededButVerifyFailed, FLASH[s].ToString("X"),
                                    flashverify[s].ToString("X")) + s);
                            port.Close();
                            return false;
                        }
                    }

                    updateProgress(100, Strings.VerifyComplete);
                }
                else
                {
                    updateProgress(0, Strings.FailedUpload);
                    CustomMessageBox.Show(Strings.CommunicationErrorNoConnection);
                }
                port.Close();

                try
                {
                    ((SerialPort)port).Open();
                }
                catch
                {
                }

                //CustomMessageBox.Show("1. If you are updating your firmware from a previous version, please verify your parameters are appropriate for the new version.\n2. Please ensure your accelerometer is calibrated after installing or re-calibrated after updating the firmware.");

                try
                {
                    ((SerialPort)port).Close();
                }
                catch
                {
                }

                updateProgress(100, Strings.Done);
            }
            catch (Exception ex)
            {
                updateProgress(0, Strings.FailedUpload);
                CustomMessageBox.Show(Strings.CheckPortSettingsOr + ex);
                try
                {
                    port.Close();
                }
                catch
                {
                }
                return false;
            }
            MainV2.comPort.giveComport = false;
            return true;
        }

        /// <summary>
        /// Read intel hex file
        /// </summary>
        /// <param name="sr"></param>
        /// <returns></returns>
        byte[] readIntelHEXv2(StreamReader sr)
        {
            byte[] FLASH = new byte[1024 * 1024];

            int optionoffset = 0;
            int total = 0;
            bool hitend = false;

            while (!sr.EndOfStream)
            {
                updateProgress((int)(((float)sr.BaseStream.Position / (float)sr.BaseStream.Length) * 100),
                    Strings.ReadingHex);

                string line = sr.ReadLine();

                if (line.StartsWith(":"))
                {
                    int length = Convert.ToInt32(line.Substring(1, 2), 16);
                    int address = Convert.ToInt32(line.Substring(3, 4), 16);
                    int option = Convert.ToInt32(line.Substring(7, 2), 16);
                    // log.InfoFormat("len {0} add {1} opt {2}", length, address, option);

                    if (option == 0)
                    {
                        string data = line.Substring(9, length * 2);
                        for (int i = 0; i < length; i++)
                        {
                            byte byte1 = Convert.ToByte(data.Substring(i * 2, 2), 16);
                            FLASH[optionoffset + address] = byte1;
                            address++;
                            if ((optionoffset + address) > total)
                                total = optionoffset + address;
                        }
                    }
                    else if (option == 2)
                    {
                        optionoffset = (int)Convert.ToUInt16(line.Substring(9, 4), 16) << 4;
                    }
                    else if (option == 1)
                    {
                        hitend = true;
                    }
                    int checksum = Convert.ToInt32(line.Substring(line.Length - 2, 2), 16);

                    byte checksumact = 0;
                    for (int z = 0; z < ((line.Length - 1 - 2) / 2); z++) // minus 1 for : then mins 2 for checksum itself
                    {
                        checksumact += Convert.ToByte(line.Substring(z * 2 + 1, 2), 16);
                    }
                    checksumact = (byte)(0x100 - checksumact);

                    if (checksumact != checksum)
                    {
                        CustomMessageBox.Show("The hex file loaded is invalid, please try again.");
                        throw new Exception("Checksum Failed - Invalid Hex");
                    }
                }
                //Regex regex = new Regex(@"^:(..)(....)(..)(.*)(..)$"); // length - address - option - data - checksum
            }

            if (!hitend)
            {
                CustomMessageBox.Show("The hex file did no contain an end flag. aborting");
                throw new Exception("No end flag in file");
            }

            Array.Resize<byte>(ref FLASH, total);

            return FLASH;
        }
    }
}