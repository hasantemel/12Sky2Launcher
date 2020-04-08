using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
            //TransparencyKey = Color.Transparent;//delete libEGL.dll and libGLESv2.dll if using this

            CefSettings settings = new CefSettings();// Initialize cef with the provided settings

            Cef.Initialize(settings);
            chromeBrowser = new ChromiumWebBrowser("http://127.0.0.1");// Create a browser component with url
            //chromeBrowser = new ChromiumWebBrowser( $"{Application.StartupPath}\\Launcher\\index.html" );// Create a browser component with html file
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

        }
        void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cef.Shutdown();
        }
        public void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if ( e.Frame.IsMain  )
            {
                OnLoadEnd();
            }
        }
        public void SetBrowserSize()
        {
            this.ClientSize = new Size(width, height);
        }
        async void OnLoadEnd()
        {
            //Check if the browser can execute JavaScript and the ScriptTextBox is filled
            if ( chromeBrowser.CanExecuteJavascriptInMainFrame )
            {
                //Evaluate javascript and remember the evaluation result
                JavascriptResponse response = await chromeBrowser.EvaluateScriptAsync(" document.getElementById('back_ground').width; ");
                JavascriptResponse response2 = await chromeBrowser.EvaluateScriptAsync(" document.getElementById('back_ground').height; ");

                if ( response.Result != null && response2.Result != null )
                {
                    width = (int)response.Result;
                    height = (int)response2.Result;
                    this.Invoke(new MethodInvoker(delegate {
                        SetBrowserSize();
                        LauncherScript.LoadScript();
                        if (LauncherScript.mScript != "")
                        {
                            chromeBrowser.ExecuteScriptAsync(LauncherScript.mScript);
                        }
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
        public static void Error(string message)
        {
            MessageBox.Show( $"[[ERROR::{message}", "" );
        }
    }

    public static class LauncherScript
    {
        public static string mScript = "";
        public static string mURL = "";
        public static string mEXE = "";
        public static string mParameter = "";
        public static int mFullScreenMode = 2;
        public static int mWidth = 0;
        public static int mHeight = 0;
        public static string mResolution_Mode = "";
        public static string mResolution_Value = "";
        public static string mResoulution_List = "";
        public static BOOL LoadScript()
        {
            try
            {
                mScript = File.ReadAllText( FOLDER_PATH + "Launcher\\LauncherScript.js" );
                string[] tParse = mScript.Split( stringSeparators, StringSplitOptions.None );
                for( int i = 0; i < tParse.Length; i++ )
                {
                    if( tParse[i].Contains("var patch_url = ") )
                    {
                        mURL = tParse[i];
                        mURL = mURL.Substring( 0, mURL.Length - 2 ); // remove ';
                        mURL = mURL.Substring( 17, mURL.Length - 17 ); // remove var patch_url = '
                        continue;
                    }
                    if( tParse[i].Contains("var client_exe = ") )
                    {
                        mEXE = tParse[i];
                        mEXE = mEXE.Substring( 0, mEXE.Length - 2 ); // remove ';
                        mEXE = mEXE.Substring( 18, mEXE.Length - 18 ); // remove var client_exe = '
                        continue;
                    }
                    if( tParse[i].Contains("var client_parameter = ") )
                    {
                        mParameter = tParse[i];
                        mParameter = mParameter.Substring( 0, mParameter.Length - 2 ); // remove ';
                        mParameter = mParameter.Substring( 24, mParameter.Length - 24 ); // remove var client_parameter = '
                        continue;
                    }
                    if( tParse[i].Contains("var resolution_list = ") )
                    {
                        mResoulution_List = tParse[i];
                        mResoulution_List = mResoulution_List.Substring ( 0, mResoulution_List.Length - 2 ); // remove ';
                        mResoulution_List = mResoulution_List.Substring( 23, mResoulution_List.Length - 23 ); // remove var resolution_list = '
                        continue;
                    }
                }
                try//to load Option.INI
                {
                    string tOption = File.ReadAllText( FOLDER_PATH + "Option.INI" );
                }
                catch//not found Option.INI
                {
                    CreateOption();
                }
                mWidth = GetPrivateProfileInt( "RESOLUTION", "x", 0, FOLDER_PATH + "Option.INI" );
                mHeight = GetPrivateProfileInt( "RESOLUTION", "y", 0, FOLDER_PATH + "Option.INI" );
                string tFullScreen = "";
                GetPrivateProfileString( "RESOLUTION", "fullscreen", "FALSE", out tFullScreen, 5, FOLDER_PATH + "Option.INI" );
                mFullScreenMode = tFullScreen.ToUpper().Equals( "TRUE" ) ? 0 : 2;
                return TRUE;
            }
            catch( Exception e )
            {
                Form1.Error( "sdsd"+e.Message );
                return FALSE;
            }
        }
        static void CreateOption( string tFullScreen = "FALSE", int tWidth = 1024, int tHeight = 768, string tLanguage = "EN" )
        {
            string tempString01 = $@"//all
                                  [RESOLUTION]
                                  x = {tWidth}
                                  y = {tHeight}
                                  fullscreen = {tFullScreen}

                                  //mayngames
                                  [EXTRA]
                                  isptype = 0

                                  //playwith
                                  [LANGUAGE]
                                  code = {tLanguage}

                                  //tw
                                  [DEFAULT]
                                  minwidth = {tWidth}
                                  minheight = {tHeight}
                                  defaultfirst = {tFullScreen}";
            while( tempString01.IndexOf("  ") >= 0 )//find tab string
            {
                tempString01 = tempString01.Replace( "  ", "" );//replace tab string
            }
            File.WriteAllText( FOLDER_PATH + "Option.INI", tempString01 );
        }
        public static void SetDisplay( int tFullScreenMode, string tResoultion_Value )
        {
            string tempString01 = "";
            tResoultion_Value = tResoultion_Value.Replace( " ", "" );
            string[] tempString2 = tResoultion_Value.Split( 'X' );
            
            sprintf( ref tempString01, "%d,%d,%d", tFullScreenMode, tempString2[0], tempString2[1] );
            mFullScreenMode = tFullScreenMode;
            mWidth = atoi( tempString2[0] );
            mHeight = atoi( tempString2[1] );
            CreateOption( mFullScreenMode == 2 ? "FALSE" : "TRUE", mWidth, mHeight );
        }
        public async static void setDisplayOptionJS()
        {
            string tempString01 = "";
            sprintf( ref tempString01, "setDisplayOption(%d,%d,%d,'%s')", mFullScreenMode, mWidth, mHeight, mResoulution_List );
            await Form1.chromeBrowser.EvaluateScriptAsync( tempString01 );
        }
    }

    public class JSCallCS
    {
        public void start()
        {
            //System.Diagnostics.ProcessStartInfo p = new System.Diagnostics.ProcessStartInfo();
            try
            {
                string tempString = "";
                sprintf( ref tempString, "%s/%d/%d/%d", $"{LauncherScript.mParameter}", LauncherScript.mFullScreenMode, LauncherScript.mWidth, LauncherScript.mHeight );
                Process.Start( FOLDER_PATH + LauncherScript.mEXE, tempString  );
                exit();
            }
            catch ( Exception e )
            {
                Form1.Error( e.Message );
            }
        }
        public void exit()
        {
            Form1.Exit();
        }
        async public void display(string message)
        {
            LauncherScript.setDisplayOptionJS();
            JavascriptResponse response2 = await Form1.chromeBrowser.EvaluateScriptAsync(" showDisplaySetting('"+ message + "'); ");
        }
        public void displayok( int tFullScreenMode, string tResoultion_Value)
        {
            LauncherScript.SetDisplay( tFullScreenMode, tResoultion_Value );
        }
    }
    public class PATCHER
    {
        public Uri uri;
        public WebClient client;
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
            setDownloadInfoJS(-1, 0, 0, 0);
            CreatePatch();
        }
        void Close()
        {
            if ( client != null )
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


            sprintf(ref tempString01, "%sSERVERVER.INI", LauncherScript.mURL );
            uri = new Uri(tempString01);
            INIFile.CreateWithText( "SERVERVER.INI", GetTotalPatch(uri) );
            if ( GetPrivateProfileString("UPTODATE", "SERVER", "00001", out tempString01, 5, "SERVERVER.INI") == 0 )
            {
                Form1.Error( "SERVERVER.INI file not found on server.");
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
                for ( index01 = 0; index01 < mTotalPatchSize.Length; index01++ )
                {
                    sprintf( ref tempString01, "%s%05d.DAT", LauncherScript.mURL, tCurrentPatch + index01 );
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
            catch ( Exception e )
            {
                Form1.Error( e.Message );
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
        static string GetTotalPatch( Uri uriPath )
        {
            try
            {
                var webRequest = HttpWebRequest.Create(uriPath);
                using (var webResponse = webRequest.GetResponse())
                {
                    WebHeaderCollection header = webResponse.Headers;
                    var encoding = ASCIIEncoding.ASCII;
                    try
                    {
                        using (var reader = new StreamReader(webResponse.GetResponseStream(), encoding))
                        {
                            string responseText = reader.ReadToEnd();
                            return responseText;
                        }
                    }
                    catch
                    {
                        Form1.Error("Unable to get patch list.");
                        Form1.Exit();
                        return "";
                    }
                }
            }
            catch
            {
                Form1.Error( "Unable to connect the server or maybe wrong url." );
                Form1.Exit();
                return "";
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
            sprintf( ref tempString01, "%s%05d.DAT", LauncherScript.mURL, mDownloadingPatch );
            sprintf( ref tempString02, "%s%05d.DAT", FOLDER_PATH, mDownloadingPatch );
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
            if ( e.Cancelled )
            {
                Close();
                Form1.Error( "The download has been cancelled" );
                Form1.Exit();
                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                Close();
                Form1.Error( "An error ocurred while trying to download file" );
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
            if ( ExtractPatch( mDownloadingPatch ) )
            {
                if ( mDownloadingPatch >= mRealTotalPatch )
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
            for (int index04 = nstr.Length - 1; index04 > 0; index04--)//find tab string from last
            {
                if (nstr[index04] != 0x20)
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
            for (int index04 = 0; index04 < nstr.Length; index04++)//find tab string from start
            {
                if (nstr[index04] != 0x20)
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
            while( nstr.IndexOf("  ") >= 0 )//find tab string
            {
                nstr = nstr.Replace( "  ", "" );//replace tab string
            }
            while( nstr.IndexOf(" ") >= 0 )//find tab string
            {
                nstr = nstr.Replace( " ", "" );//replace tab string
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
        static BOOL CreateWithFile( in string lpFileName )
        {
            FileInfo tFileInfo;
            FileStream tFileStream;

            try
            {
                tFileInfo = new FileInfo(lpFileName);
                if ( !tFileInfo.Exists )
                {
                    return FALSE;
                }

                tFileStream = tFileInfo.Open( FileMode.Open, FileAccess.Read, FileShare.None );
                mFileData = new byte[ tFileStream.Length ];
                tFileStream.Read( mFileData, 0, mFileData.Length );
                tFileStream.Dispose();

                mFileName = lpFileName;
                mKeyValue = new List<KeyValuePair<string, Dictionary<string, string>>>();

                return CreateBody();
            }
            catch
            {
                return FALSE;
            }
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
            for ( index01 = 0; index01 < tData.Length; index01++ )
            {
                if ( tData[index01].Length >= 2 && tData[index01].Contains("//") )
                {
                    continue;
                }
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
