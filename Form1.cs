using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

using BOOL = System.Boolean;
using BYTE = System.Byte;
using DWORD = System.Int32;
using static LauncherCS.Define;
using static LauncherCS.INIFile;


namespace LauncherCS
{
    public partial class Form1 : Form
    {
        int width;
        int height;
        PATCHER patcher;
        public static ChromiumWebBrowser chromeBrowser;

        public Form1()
        {
            InitializeComponent();
            InitializeChromium();
        }
        public void InitializeChromium()
        {
            CefSettings settings = new CefSettings();// Initialize cef with the provided settings
            Cef.Initialize(settings);
            chromeBrowser = new ChromiumWebBrowser("http://127.0.0.1");// Create a browser component with url
            //chromeBrowser = new ChromiumWebBrowser( $"{Application.StartupPath}\\Launcher\\index.html" );// Create a browser component with html file
            //chromeBrowser.BackColor = Color.Transparent;
            chromeBrowser.Dock = DockStyle.Fill;

            // Allow the use of local resources in the browser
            BrowserSettings browserSettings = new BrowserSettings();
            browserSettings.FileAccessFromFileUrls = CefState.Enabled;
            browserSettings.UniversalAccessFromFileUrls = CefState.Enabled;
            chromeBrowser.BrowserSettings = browserSettings;

            chromeBrowser.JavascriptMessageReceived += OnBrowserJavascriptMessageReceived;
            chromeBrowser.FrameLoadEnd += OnFrameLoadEnd;
            CefSharpSettings.LegacyJavascriptBindingEnabled = true;
            chromeBrowser.JavascriptObjectRepository.Register("jscallcs", new JSCallCS(), true);
            Controls.Add(chromeBrowser);

            //this.Dock = System.Windows.Forms.DockStyle.None;
            //this.TransparencyKey = Color.Transparent;
            //FormBorderStyle = FormBorderStyle.None;
            //Dock = DockStyle.None;
            //BackColor = Color.Magenta;
            //TransparencyKey = Color.Magenta;
            //FormBorderStyle = FormBorderStyle.None;

        }
        void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cef.Shutdown();
        }
        public void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (e.Frame.IsMain)
            {
                GetWidthHeight();

                //call cs function by js code
                //onclick
                chromeBrowser.ExecuteScriptAsync(@"
                    document.body.onmouseup = function(e)
                    {
                       var id = e.target.id;
                       if( id == 'StartButton' )
                            jscallcs.start('TwelveSky2.exe');
                       if( id == 'ExitButton' )
                           jscallcs.exit();
                       //alert(e.target.id);
                    }
                ");
            }
        }
        public void SetWidthHeight()
        {
            this.ClientSize = new Size(width, height);
        }
        async void GetWidthHeight()
        {
            //Check if the browser can execute JavaScript and the ScriptTextBox is filled
            if (chromeBrowser.CanExecuteJavascriptInMainFrame)
            {
                //Evaluate javascript and remember the evaluation result
                JavascriptResponse response = await chromeBrowser.EvaluateScriptAsync(" document.getElementById('back_ground').width; ");
                JavascriptResponse response2 = await chromeBrowser.EvaluateScriptAsync(" document.getElementById('back_ground').height; ");

                if (response.Result != null && response2.Result != null)
                {
                    width = (int)response.Result;
                    height = (int)response2.Result;
                    this.Invoke(new MethodInvoker(delegate {
                        SetWidthHeight();
                        patcher = new PATCHER();
                        patcher.Init();
                    }));
                    //MessageBox.Show( "width:"+response.Result.ToString() + ",height:" + response2.Result.ToString(), "JavaScript Result");
                }
            }
        }
        void OnBrowserJavascriptMessageReceived(object sender, JavascriptMessageReceivedEventArgs e)
        {
            var windowSelection = (string)e.Message;
        }
        public static void Exit()
        {
            Application.ExitThread();
            Application.Exit();
        }
    }


    public static class Define
    {
        public static string FOLDER_PATH = Application.StartupPath + "\\";
        public static object NULL = null;
        public const int GMEM_FIXED = 0x0000;
        public static BOOL FALSE = false;
        public static BOOL TRUE = true;
        public static string[] stringSeparators = new string[] { "\r\n" };

        public static int CopyMemory(BYTE[] dst, BYTE[] src, int len, int dstOffset, int srcOffset)
        {
            int len2 = len;
            int pos2 = srcOffset;
            while (len-- > 0)
                dst[dstOffset++] = (BYTE)src[srcOffset++];
            return pos2 + len2;
        }
        public static void sprintf(ref string obj1, string obj2, params object[] args)
        {
            int num = 0;
            string tmp = obj2;
            int matchEnd = 0;
            bool matchInt = false;
            for (int i = 0; i < tmp.Length; i++)
            {
                foreach (Match match in Regex.Matches(obj2, $@"\%s|\%d|\%f|\%x|\%X|\%0[1-9]d|\%10d"))
                {
                    matchEnd = match.Index + 4;
                    matchInt = false;
                    for (int j = 1; j < 11; j++)
                    {
                        if (match.Value.Equals($"%{j:D2}d"))
                        {
                            obj2 = obj2.Substring(0, match.Index) + "{" + (num++).ToString() + $":D{j}" + "}" + obj2.Substring(matchEnd, obj2.Length - matchEnd);
                            matchInt = true;
                            break;
                        }
                    }
                    if (matchInt)
                        break;
                    matchEnd = match.Index + 2;
                    if (!matchInt)
                    {
                        if (match.Value.Equals("%X"))
                            obj2 = obj2.Substring(0, match.Index) + $"{{{num++}:X2}}" + obj2.Substring(matchEnd, obj2.Length - matchEnd);
                        else if (match.Value.Equals("%x"))
                            obj2 = obj2.Substring(0, match.Index) + $"{{{num++}:x2}}" + obj2.Substring(matchEnd, obj2.Length - matchEnd);
                        else
                            obj2 = obj2.Substring(0, match.Index) + $"{{{num++}}}" + obj2.Substring(matchEnd, obj2.Length - matchEnd);
                    }
                    break;
                }
            }
            obj1 = string.Format(obj2, args);
        }
        public static string btoa(byte[] data)//byte to string
        {
            if (data == null || data[0] == 0x00)
            {
                return string.Empty;
            }
            int length = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0x00)
                {
                    break;
                }
                length++;
            }
            return Encoding.UTF8.GetString(data, 0, length);
        }
        public static int btoi(byte[] b, int p = 0)//byte to int
        {
            return BitConverter.ToInt32(b, p);
        }
        public static int btoi(byte[] b, ref int p)//byte to int
        {
            int i = BitConverter.ToInt32(b, p);
            p += 4;
            return i;
        }
        public static int atoi(string nptr)//string to int
        {
            int tValue;
            bool tConvert = int.TryParse(nptr, out tValue);
            if (!tConvert)
            {
                return 0;
            }
            return tValue;
        }
        public static long atol(string nptr)//string to long
        {
            long tValue;
            bool tConvert = long.TryParse(nptr, out tValue);
            if (!tConvert)
            {
                return 0;
            }
            return tValue;
        }
        public static BOOL ReadFile(FileStream fs, ref DWORD lpBuffer, DWORD nNumberOfBytesToRead)
        {
            if (fs == null || !fs.CanRead)
            {
                return FALSE;
            }
            if ((fs.Length - (fs.Position + nNumberOfBytesToRead) < 0))
            {
                return FALSE;
            }
            BYTE[] b = new BYTE[nNumberOfBytesToRead];
            try
            {
                fs.Read(b, 0, nNumberOfBytesToRead);
            }
            catch
            {
                return FALSE;
            }
            lpBuffer = btoi(b);
            return TRUE;
        }
        public static BOOL ReadFile(FileStream fs, ref BYTE[] lpBuffer, DWORD nNumberOfBytesToRead)
        {
            if (fs == null || !fs.CanRead)
            {
                return FALSE;
            }
            if ((fs.Length - (fs.Position + nNumberOfBytesToRead) < 0))
            {
                return FALSE;
            }
            try
            {
                fs.Read(lpBuffer, 0, nNumberOfBytesToRead);
            }
            catch
            {
                return FALSE;
            }
            return TRUE;
        }
        public static BOOL CloseHandle(FileStream fs)
        {
            if (fs == null)
            {
                return FALSE;
            }
            fs.Close();
            fs.Dispose();
            return TRUE;
        }
        public static BOOL uncompress(DWORD tCompressSize, BYTE[] tCompress, DWORD tOriginalSize, BYTE[] tOriginal)
        {
            return uncompress(ref tOriginal, tCompress);
        }
        static BOOL uncompress(ref BYTE[] output, BYTE[] input)
        {
            try
            {
                MemoryStream inputStream = new MemoryStream(input);
                //MemoryStream outputStream = new MemoryStream();
                inputStream.Position = 2;
                DeflateStream decompressionStream = new DeflateStream(inputStream, CompressionMode.Decompress);
                decompressionStream.Read(output, 0, output.Length);
                return TRUE;
            }
            catch (Exception)
            {
                return FALSE;
            }
        }
        public static T[] Create<T>(int length) where T : new()
        {
            var array = new T[length];
            for (int i = 0; i < length; i++)
                array[i] = new T();

            return array;
        }
        public static BYTE[] GlobalAlloc(int t, int tSize)
        {
            return Create<BYTE>(tSize);
        }
        public static void GlobalFree<T>(T t)
        {
            if (t != null)
                t = (T)(object)null;//Marshal.FreeHGlobal( (IntPtr)(object)t );
            GC.Collect();
        }
    }
    public class JSCallCS
    {
        public void start(string message)
        {
            //System.Diagnostics.ProcessStartInfo p = new System.Diagnostics.ProcessStartInfo();
            try
            {
                System.Diagnostics.Process.Start(FOLDER_PATH + message);
                Application.ExitThread();
                Application.Exit();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "[[ERROR:]]");
            }
        }
        public void exit()
        {
            Form1.Exit();
        }
    }
    public class PATCHER
    {
        public Uri uri;
        public WebClient client;
        public string url = "http://pvp3.12sky2.online/launcher/";
        public int mDownloadingPatch = 0;
        public int mRealTotalPatch = 0;
        public long[] mTotalPatchSize = null;
        public long mTotalAllDownloaded = 0;
        public long mTotalAllSize = 0;
        public long mCurrentLoadSize = 0;
        public int mCurrentLoadIndex = 0;
        public long mLastReceived = 0;
        public void Init()
        {
            client = new WebClient();
            setDownloadInfoJS(0, 0, 0, 0);
            CreatePatch();
        }
        void Close()
        {
            if (client != null)
            {
                client.CancelAsync();
                client.DownloadProgressChanged -= DownloadProgressChanged;
                client.DownloadFileCompleted -= DownloadFileCompleted;
                client = null;
            }
        }
        void CreatePatch()
        {
            Thread.Sleep(2000);

            string tempString01 = "";
            string tFileName = "PRESENTVERSION.DAT";
            int tRealTotalNum;
            int index01;
            int tCurrentPatch;

            sprintf(ref tempString01, "%s%s", FOLDER_PATH, tFileName);
            try
            {
                tempString01 = File.ReadAllText(tempString01);
            }
            catch
            {
                tempString01 = "";
            }
            if (tempString01.Length < 5)
            {
                tempString01 = "00001";
            }
            tempString01 = tempString01.Substring(0, 5);
            tCurrentPatch = atoi(tempString01);


            sprintf(ref tempString01, "%sSERVERVER.INI", url);
            uri = new Uri(tempString01);
            INIFile.CreateWithText("SERVERVER.INI", GetTotalPatch(uri));
            if (GetPrivateProfileString("UPTODATE", "SERVER", "00001", out tempString01, 5, "SERVERVER.INI") == 0)
            {
                MessageBox.Show("SERVERVER.INI file not found on server.");
                Form1.Exit();
                return;
            }
            mRealTotalPatch = atoi(tempString01) - 1;

            tRealTotalNum = mRealTotalPatch - tCurrentPatch;
            if (tRealTotalNum > -1)
            {
                mTotalPatchSize = new long[tRealTotalNum + 1];
            }
            if (mTotalPatchSize != null)
            {
                //MessageBox.Show( "number to download : " + mTotalPatchSize.Length.ToString() );
                mTotalAllSize = 0;
                for (index01 = 0; index01 < mTotalPatchSize.Length; index01++)
                {
                    sprintf(ref tempString01, "%s%05d.DAT", url, tCurrentPatch + index01);
                    uri = new Uri(tempString01);
                    mTotalPatchSize[index01] = GetFileSize(uri);
                    mTotalAllSize += mTotalPatchSize[index01];
                }
                mDownloadingPatch = tCurrentPatch;
                DownloadFile();
                return;
            }
            setCompletedJS();
        }
        public BOOL ExtractPatch(int tVersion)
        {
            int index01;
            BYTE[] tOriginal;
            string tempString01 = "";
            string tFileName = "";
            int tFileCount;
            int tPosition;
            int tFileNameLength;
            BYTE[] tFileNameSub;
            int tFileOriginalSize;
            int tFileCompressSize;
            BYTE[] tFileCompress;
            BYTE[] tFileOriginal;
            FileInfo hFileInfo;
            FileStream hFile;

            sprintf(ref tFileName, "%s%05d.DAT", FOLDER_PATH, tVersion);

            try
            {
                hFileInfo = new FileInfo(tFileName);
                hFile = hFileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "[[ERROR:]]");
                Close();
                return FALSE;
            }
            tOriginal = GlobalAlloc(GMEM_FIXED, (int)hFile.Length);
            if (tOriginal == NULL)
            {
                CloseHandle(hFile);
                return FALSE;
            }
            if (!ReadFile(hFile, ref tOriginal, tOriginal.Length))
            {
                CloseHandle(hFile);
                GlobalFree(tOriginal);
                return FALSE;
            }
            if (!CloseHandle(hFile))
            {
                GlobalFree(tOriginal);
                return FALSE;
            }
            hFile.Dispose();

            tPosition = 0;
            tFileCount = btoi(tOriginal, ref tPosition);
            for (index01 = 0; index01 < tFileCount; index01++)
            {
                tFileNameLength = btoi(tOriginal, ref tPosition);
                tFileNameSub = Create<BYTE>(tFileNameLength);
                if (tFileNameSub == NULL)
                {
                    return FALSE;
                }
                tPosition = CopyMemory(tFileNameSub, tOriginal, tFileNameLength, 0, tPosition);
                tFileOriginalSize = btoi(tOriginal, ref tPosition);
                tFileOriginal = Create<BYTE>(tFileOriginalSize);
                if (tFileOriginal == NULL)
                {
                    return FALSE;
                }
                tFileCompressSize = btoi(tOriginal, ref tPosition);
                tFileCompress = Create<BYTE>(tFileCompressSize);
                if (tFileCompress == NULL)
                {
                    return FALSE;
                }
                tPosition = CopyMemory(tFileCompress, tOriginal, tFileCompressSize, 0, tPosition);
                //tNextPosition = index01 * ( 4 + tFileNameLength + 4 + 4 + tFileCompressSize );

                if (!uncompress(tFileCompressSize, tFileCompress, tFileOriginalSize, tFileOriginal))
                {
                    GlobalFree(tFileCompress);
                    GlobalFree(tFileOriginal);
                    GlobalFree(tOriginal);
                    return FALSE;
                }

                string filePath = FOLDER_PATH + btoa(tFileNameSub);
                new FileInfo(filePath).Directory.Create(); // If the directory already exists, this method does nothing.

                //MessageBox.Show( filePath, "dd" );
                try
                {
                    File.WriteAllBytes(filePath, tFileOriginal);
                }
                catch
                {
                    return FALSE;
                }

                GlobalFree(tFileCompress);
                GlobalFree(tFileOriginal);
            }
            File.Delete(tFileName);//delete .DAT after extracted
            try
            {
                sprintf(ref tempString01, "%05d", tVersion + 1);
                File.WriteAllText(FOLDER_PATH + "PRESENTVERSION.DAT", tempString01);
            }
            catch
            {
                Close();
                return FALSE;
            }
            return TRUE;
        }
        static string GetTotalPatch(Uri uriPath)
        {
            var webRequest = HttpWebRequest.Create(uriPath);

            using (var webResponse = webRequest.GetResponse())
            {
                WebHeaderCollection header = webResponse.Headers;
                var encoding = ASCIIEncoding.ASCII;
                using (var reader = new StreamReader(webResponse.GetResponseStream(), encoding))
                {
                    string responseText = reader.ReadToEnd();
                    return responseText;
                }
            }
        }
        static long GetFileSize(Uri uriPath)
        {
            var webRequest = HttpWebRequest.Create(uriPath);
            webRequest.Method = "HEAD";

            using (var webResponse = webRequest.GetResponse())
            {
                var fileSize = webResponse.Headers.Get("Content-Length");
                var fileSizeInMegaByte = fileSize;// Math.Round(Convert.ToDouble(fileSize) / 1024.0 / 1024.0, 2);
                return atol(fileSizeInMegaByte);
            }
        }
        void DownloadFile()
        {
            //Task.WaitAll();
            string tempString01 = "";
            string tempString02 = "";

            mLastReceived = 0;
            sprintf(ref tempString01, "%s%05d.DAT", url, mDownloadingPatch);
            sprintf(ref tempString02, "%s%05d.DAT", FOLDER_PATH, mDownloadingPatch);
            uri = new Uri(tempString01);
            using (client = new WebClient())
            {
                client.DownloadProgressChanged += DownloadProgressChanged;
                client.DownloadFileCompleted += DownloadFileCompleted;
                client.DownloadFileAsync(uri, tempString02);
            }
        }
        void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            mCurrentLoadSize += e.BytesReceived - mLastReceived;
            mTotalAllDownloaded = mCurrentLoadSize;
            mLastReceived = e.BytesReceived;
            setDownloadInfoJS(e.BytesReceived, e.TotalBytesToReceive, mDownloadingPatch, mRealTotalPatch);
            Thread.Sleep(50);
        }
        void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                Close();
                MessageBox.Show("The download has been cancelled");
                Form1.Exit();
                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                Close();
                MessageBox.Show("An error ocurred while trying to download file");
                Form1.Exit();
                return;
            }
            Close();
            setExtracting();
        }
        async void setCompletedJS()
        {
            await Form1.chromeBrowser.EvaluateScriptAsync("setCompleted();");
        }
        async void setExtractingJS()
        {
            string tempString = "";
            sprintf(ref tempString, "setExtracting('%05d','%05d');", mDownloadingPatch, mRealTotalPatch);
            await Form1.chromeBrowser.EvaluateScriptAsync(tempString);
        }
        void setExtracting()
        {
            setExtractingJS();
            if (ExtractPatch(mDownloadingPatch))
            {
                if (mDownloadingPatch >= mRealTotalPatch)
                {
                    setCompletedJS();
                    return;
                }
                mCurrentLoadIndex++;
                mDownloadingPatch++;
                DownloadFile();
            }
        }
        async void setDownloadInfoJS(long tDownloadedBytes, long tTotalBytes, int tCurrentPatch, long tTotalPatch)
        {
            string tempString = "";
            sprintf(ref tempString, "setDownloadInfo(%d,%d,%d,%d,'%05d','%05d');", tDownloadedBytes, tTotalBytes, mTotalAllDownloaded, mTotalAllSize, tCurrentPatch, tTotalPatch);
            await Form1.chromeBrowser.EvaluateScriptAsync(tempString);
        }
    }
    public static class INIFile
    {
        public static string mFileName = string.Empty;
        public static BYTE[] mFileData = null;
        static List<KeyValuePair<string, Dictionary<string, string>>> mKeyValue;
        static BOOL CheckValid(in string lpFileName)
        {
            if (!lpFileName.Equals(mFileName))
            {
                if (!CreateWithFile(lpFileName))
                {
                    return FALSE;
                }
            }
            return TRUE;
        }
        public static void RemoveWhiteSpace(ref string nstr)
        {
            string[] str = nstr.Split(new string[] { "//" }, StringSplitOptions.None);
            nstr = str[0];
            for (int index04 = nstr.Length - 1; index04 > 0; index04--)//find tab string from last
            {
                if (nstr[index04] != 0x09)
                {
                    nstr = nstr.Substring(0, index04 + 1);
                    break;
                }
            }
            for (int index04 = nstr.Length - 1; index04 > 0; index04--)//find space string from last
            {
                if (!nstr[index04].Equals(' '))
                {
                    nstr = nstr.Substring(0, index04 + 1);
                    break;
                }
            }
            for (int index04 = 0; index04 < nstr.Length; index04++)//find tab string from start
            {
                if (nstr[index04] != 0x09)
                {
                    nstr = nstr.Substring(index04, nstr.Length - index04);
                    break;
                }
            }
            for (int index04 = 0; index04 < nstr.Length; index04++)//find space string from start
            {
                if (!nstr[index04].Equals(' '))
                {
                    nstr = nstr.Substring(index04, nstr.Length - index04);
                    break;
                }
            }
        }
        public static BOOL CreateWithText(in string lpFileName, in string lpString)
        {
            mFileName = lpFileName;
            mKeyValue = new List<KeyValuePair<string, Dictionary<string, string>>>();
            mFileData = new byte[lpString.Length];

            int index01 = 0;
            foreach (char c in lpString.ToCharArray())
            {
                mFileData[index01++] = (BYTE)c;
            }
            return CreateBody();
        }
        static BOOL CreateWithFile(in string lpFileName)
        {
            FileInfo tFileInfo;
            FileStream tFileStream;

            tFileInfo = new FileInfo(lpFileName);
            if (!tFileInfo.Exists)
            {
                return FALSE;
            }

            tFileStream = tFileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            mFileData = new byte[tFileStream.Length];
            tFileStream.Read(mFileData, 0, mFileData.Length);
            tFileStream.Dispose();

            mFileName = lpFileName;
            mKeyValue = new List<KeyValuePair<string, Dictionary<string, string>>>();

            return CreateBody();
        }
        static BOOL CreateBody()
        {
            string[] tData = Encoding.UTF8.GetString(mFileData).Split(stringSeparators, StringSplitOptions.None);
            string tSection = string.Empty;
            string[] tKeyValue;
            Dictionary<string, string> tValue2;
            int index01;
            int index02;
            int index03;
            int index04;
            for (index01 = 0; index01 < tData.Length; index01++)
            {
                if (tData[index01].StartsWith("[") && tData[index01].Contains('[') && tData[index01].Contains(']'))
                {
                    tData[index01] = tData[index01].Replace("[", "");
                    index03 = tData[index01].Length;
                    for (index02 = 0; index02 < tData[index01].Length; index02++)
                    {
                        if (tData[index01][index02] == ']')
                        {
                            index03 = index02;
                            break;
                        }
                    }
                    if (index03 != tData[index01].Length)
                    {
                        tData[index01] = tData[index01].Substring(0, index03);
                    }
                    tSection = tData[index01];
                    continue;
                }
                if (tData[index01] == string.Empty)
                {
                    continue;
                }
                tKeyValue = tData[index01].Split('=');
                if (tKeyValue.Length < 2)
                {
                    continue;
                }
                if (tKeyValue[0].Length >= 2 && tKeyValue[0].Contains("//"))
                {
                    continue;
                }
                for (index04 = 2; index04 < tKeyValue.Length; index04++)
                {
                    tKeyValue[1] += tKeyValue[index04];
                }
                RemoveWhiteSpace(ref tKeyValue[0]);
                tValue2 = new Dictionary<string, string>();
                tValue2.Add(tKeyValue[0], tKeyValue[1]);
                KeyValuePair<string, Dictionary<string, string>> tKeyPair = new KeyValuePair<string, Dictionary<string, string>>(tSection, tValue2);
                mKeyValue.Add(tKeyPair);
            }
            return TRUE;
        }
        static int GetValue(in string lpAppName, in string lpKeyName, ref string lpReturnedString)
        {
            foreach (KeyValuePair<string, Dictionary<string, string>> tKeyPair in mKeyValue)
            {
                if (tKeyPair.Key.Equals(lpAppName))
                {
                    foreach (KeyValuePair<string, string> tDic in tKeyPair.Value)
                    {
                        if (tDic.Key.Equals(lpKeyName))
                        {
                            lpReturnedString = tDic.Value;
                            RemoveWhiteSpace(ref lpReturnedString);
                            return 1;
                        }
                    }
                }
            }
            return 0;
        }
        public static int GetPrivateProfileString(in string lpAppName, in string lpKeyName, in string lpDefault, out string lpReturnedString, in int nSize, in string lpFileName)
        {
            lpReturnedString = lpDefault;
            if (!CheckValid(lpFileName))
            {
                return 0;
            }
            int tValue = GetValue(lpAppName, lpKeyName, ref lpReturnedString);
            if (lpReturnedString.Length > nSize)
            {
                lpReturnedString = lpReturnedString.Substring(0, nSize);
            }
            return tValue;
        }
        public static int GetPrivateProfileInt(in string lpAppName, in string lpKeyName, in int nDefault, in string lpFileName)
        {
            if (!CheckValid(lpFileName))
            {
                return nDefault;
            }
            string tValue0 = "";
            int tValue1 = GetValue(lpAppName, lpKeyName, ref tValue0);
            if (tValue1 != 1)
            {
                return nDefault;
            }
            return atoi(tValue0);
        }
    }
}
