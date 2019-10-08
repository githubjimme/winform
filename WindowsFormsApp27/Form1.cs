using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace WindowsFormsApp27
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            Window.WindowName = textBox1.Text;
            Bot.Start();
        }

        private void RecordButton_Click(object sender, EventArgs e)
        {
            if (string.Equals(RecordButton.Text, "Record"))
            {
                StartRecording();
                RecordButton.Text = "Stop";
            }
            else
            {
                StopRecording();
                RecordButton.Text = "Record";
            }
        }

        private void StartRecording()
        {
            RecordingForm.StartForm();
            InterceptKeys.StartHook();
            InterceptMouse.StartHook();
            InterceptDelays.ResetTimer();
        }

        private void StopRecording()
        {
            RecordingForm.StopForm();
            InterceptKeys.StopHook();
            InterceptMouse.StopHook();
            XMLFile.Serialize();
        }
    }

    /// <summary>
    /// reads lines from xmlfile and sends the keys, mouselcicks and delays to the window through a hook
    /// </summary>
    public class Bot
    {
        private static readonly CancellationToken Token = new CancellationToken();

        public static void Start()
        {
            ThreadPool.QueueUserWorkItem(SendKeysToWindow, Token);
        }

        public static void Stop()
        {

        }

        private static void SendKeysToWindow(object obj)
        {

            List<Recording> list = XMLFile.Deserialize();

            foreach (var recordObj in list)
            {
                if (Token.IsCancellationRequested)
                {
                    break;
                }

                SendDelayToWindow(recordObj.Delay);
                SendMouseToWindow(recordObj.MouseClick, recordObj.X, recordObj.Y);
                SendKeyToWindow(recordObj.KeyPress);
            }
        }

        private static void SendDelayToWindow(int delay)
        {
            try
            {
                Window.Delay(delay);
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not send delay to window ", e.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private static void SendKeyToWindow(Keys key)
        {
            if (!string.IsNullOrWhiteSpace(key.ToString()))
            {
                Window.KeyPress(key);
            }
        }

        private static void SendMouseToWindow(string mouseclick, int x, int y)
        {
            if (!string.IsNullOrWhiteSpace(mouseclick))
            {
                Window.MouseClick(mouseclick, (uint)x, (uint)y);
            }
        }
    }
       
    /// <summary>
    /// creates xmlfile, serializes and deserializes xml file
    ///  </summary>
    public class XMLFile
    {
        public static void Serialize()
        {
            FileCheck();

            XmlSerializer serializer = new XmlSerializer(typeof(List<Recording>));
            StreamWriter writer = new StreamWriter(Environment.CurrentDirectory + @"\Recordings.xml");

            serializer.Serialize(writer, RecordingList.Records);
            writer.Close();

            RecordingList.Records.Clear();
        }

        public static List<Recording> Deserialize()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<Recording>));
            StreamReader reader = new StreamReader(Environment.CurrentDirectory + @"\Recordings.xml");
            List<Recording> list = null;

            try
            {
                list = serializer.Deserialize(reader) as List<Recording>;
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not deserialize List ", e.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            reader.Close();

            return list;
        }

        private static void FileCheck()
        {
            if (File.Exists(Environment.CurrentDirectory + @"\Recordings.xml"))
            {
                File.Delete(Environment.CurrentDirectory + @"\Recordings.xml");
            }
        }
    }

    /// <summary>
    /// saves the recorded key delays and mouseclick with locations
    /// </summary>
    public class Recording
    {
        public int Delay { get; set; }
        public Keys KeyPress { get; set; }
        public string MouseClick { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>
    /// list of recoding objects
    /// </summary>
    public class RecordingList
    {
        public static readonly List<Recording> Records = new List<Recording>();

        public static void AddToList(Recording recording)
        {
            Records.Add(recording);
        }
    }


    /// <summary>
    /// intercepts the keys pressed after the hook is set
    /// </summary>
    public class InterceptKeys
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookName = Window.FindWindow(Window.WindowName);

        public static void StartHook()
        {
            _hookName = SetHook(_proc);
        }

        public static void StopHook()
        {
            UnhookWindowsHookEx(_hookName);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                var recording = new Recording();
                int vkCode = Marshal.ReadInt32(lParam);

                recording.Delay = InterceptDelays.getDelay();
                recording.KeyPress = ((Keys)vkCode);

                RecordingList.AddToList(recording);
            }

            return CallNextHookEx(_hookName, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    }


    /// <summary>
    /// intercepts the mouse clicks with locations after the hook is set
    /// </summary>
    public class InterceptMouse
    {
        private static readonly List<string> ListOfKeysAndDelays = new List<string>();
        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookName = Window.FindWindow(Window.WindowName);

        public static void StartHook()
        {
            _hookName = SetHook(_proc);
        }

        public static void StopHook()
        {
            UnhookWindowsHookEx(_hookName);
        }

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())

            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if ((MouseMessages)wParam == MouseMessages.WM_RBUTTONDOWN || (MouseMessages)wParam == MouseMessages.WM_LBUTTONDOWN)
            {
                var recording = new Recording();
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                recording.X = hookStruct.pt.x;
                recording.Y = hookStruct.pt.y;

                CheckMouseClick(wParam, lParam, recording);
            }

            return CallNextHookEx(_hookName, nCode, wParam, lParam);
        }

        public static void CheckMouseClick(IntPtr wParam, IntPtr lParam, Recording recording)
        {
            recording.Delay = InterceptDelays.getDelay();

            if ((MouseMessages)wParam == MouseMessages.WM_LBUTTONDOWN)
            {
                recording.MouseClick = "Left";
            }
            else if ((MouseMessages)wParam == MouseMessages.WM_RBUTTONDOWN)
            {
                recording.MouseClick = "Right";
            }

            RecordingList.AddToList(recording);
        }


        private const int WH_MOUSE_LL = 14;

        private enum MouseMessages : int
        {
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_MOUSEMOVE = 0x0200,
            WM_MOUSEWHEEL = 0x020A,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205
        }

        private struct POINT
        {
            public int x;
            public int y;
        }

        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }

    /// <summary>
    /// get the exact time after an input and calculates the delay between them
    /// </summary>
    public class InterceptDelays
    {
        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        internal struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private static int _lastInput;

        public static int GetSystemTime()
        {
            var lastInputInfo = new LASTINPUTINFO();

            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            GetLastInputInfo(ref lastInputInfo);

            return (int)lastInputInfo.dwTime;
        }

        public static void ResetTimer()
        {
            _lastInput = GetSystemTime();
        }

        public static int getDelay()
        {
            int delayBetweenKeys = GetSystemTime() - _lastInput;

            _lastInput = GetSystemTime();

            return delayBetweenKeys;
        }
    }
}

/// <summary>
/// there are only 2 ways to simulate key and mousepresses to a window that is not active, they are both methods from a dll import.
/// this class sends keybinds and mouseclicks to the direcx application, for that it needs to inject keys and mouseclicks to the directx inputstream
/// </summary>
public class Window
{

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
    static extern IntPtr PostMessage(IntPtr hWnd, uint msg, uint wParam, uint lParam);

    public static string WindowName { get; set; }

    private enum WMessages : int
    {
        WM_LBUTTONDOWN = 0x201, //Left mousebutton down
        WM_LBUTTONUP = 0x202, //Left mousebutton up
        WM_LBUTTONDBLCLK = 0x203, //Left mousebutton doubleclick
        WM_RBUTTONDOWN = 0x204, //Right mousebutton down
        WM_RBUTTONUP = 0x205, //Right mousebutton up
        WM_RBUTTONDBLCLK = 0x206, //Right mousebutton doubleclick
        WM_KEYDOWN = 0x100, //Key down
        WM_KEYUP = 0x101, //Key up
        WM_SETCURSOR = 0x0020, //Sets the cursor
        WM_MOUSEMOVE = 0x0200, //Moves mouse
        WM_CHAR = 0x0102, //Sets the key to a key value
        WM_MOUSEACTIVATE = 0x0021, //Activates mouse
    }

    private enum VKeys : int
    {
        LButton = 0x01, //Left mouse button 
        RButton = 0x02, //Right mouse button 
        Cancel = 0x03, //Control-break processing 
        MButton = 0x04, //Middle mouse button (three-button mouse) 
        Back = 0x08, //BACKSPACE key 
        Tab = 0x09, //TAB key 
        Clear = 0x0C, //CLEAR key 
        Return = 0x0D, //ENTER key 
        ShiftKey = 0x10, //SHIFT key 
        ControlKey = 0x11, //CTRL key 
        Menu = 0x12, //ALT key 
        Pause = 0x13, //PAUSE key 
        Capital = 0x14, //CAPS LOCK key 
        Escape = 0x1B, //ESC key 
        Space = 0x20, //SPACEBAR 
        PageUp = 0x21, //PAGE UP key 
        Next = 0x22, //PAGE DOWN key 
        End = 0x23, //END key 
        Home = 0x24, //HOME key 
        Left = 0x25, //LEFT ARROW key 
        Up = 0x26, //UP ARROW key 
        Right = 0x27, //RIGHT ARROW key 
        Down = 0x28, //DOWN ARROW key 
        Select = 0x29, //SELECT key 
        Print = 0x2A, //PRINT key
        Execute = 0x2B, //EXECUTE key 
        PrintScreen = 0x2C, //PRINT SCREEN key 
        Insert = 0x2D, //INS key 
        Delete = 0x2E, //DEL key 
        Help = 0x2F, //HELP key
        D0 = 0x30, //0 key 
        D1 = 0x31, //1 key 
        D2 = 0x32, //2 key 
        D3 = 0x33, //3 key 
        D4 = 0x34, //4 key 
        D5 = 0x35, //5 key 
        D6 = 0x36, //6 key 
        D7 = 0x37, //7 key 
        D8 = 0x38, //8 key 
        D9 = 0x39, //9 key 
        A = 0x41, //A key 
        B = 0x42, //B key 
        C = 0x43, //C key 
        D = 0x44, //D key 
        E = 0x45, //E key 
        F = 0x46, //F key 
        G = 0x47, //G key 
        H = 0x48, //H key 
        I = 0x49, //I key 
        J = 0x4A, //J key 
        K = 0x4B, //K key 
        L = 0x4C, //L key 
        M = 0x4D, //M key 
        N = 0x4E, //N key 
        O = 0x4F, //O key 
        P = 0x50, //P key 
        Q = 0x51, //Q key 
        R = 0x52, //R key 
        S = 0x53, //S key 
        T = 0x54, //T key 
        U = 0x55, //U key 
        V = 0x56, //V key 
        W = 0x57, //W key 
        X = 0x58, //X key 
        Y = 0x59, //Y key 
        Z = 0x5A, //Z key
        NumPad0 = 0x60, //Numeric keypad 0 key 
        NumPad1 = 0x61, //Numeric keypad 1 key 
        NumPad2 = 0x62, //Numeric keypad 2 key 
        NumPad3 = 0x63, //Numeric keypad 3 key 
        NumPad4 = 0x64, //Numeric keypad 4 key 
        NumPad5 = 0x65, //Numeric keypad 5 key 
        NumPad6 = 0x66, //Numeric keypad 6 key 
        NumPad7 = 0x67, //Numeric keypad 7 key 
        NumPad8 = 0x68, //Numeric keypad 8 key 
        NumPad9 = 0x69, //Numeric keypad 9 key 
        Separator = 0x6C, //Separator key 
        Subtract = 0x6D, //Subtract key 
        Decimal = 0x6E, //Decimal key 
        Divide = 0x6F, //Divide key
        F1 = 0x70, //F1 key 
        F2 = 0x71, //F2 key 
        F3 = 0x72, //F3 key 
        F4 = 0x73, //F4 key 
        F5 = 0x74, //F5 key 
        F6 = 0x75, //F6 key 
        F7 = 0x76, //F7 key 
        F8 = 0x77, //F8 key 
        F9 = 0x78, //F9 key 
        F10 = 0x79, //F10 key 
        F11 = 0x7A, //F11 key 
        F12 = 0x7B, //F12 key
        Scroll = 0x91, //SCROLL LOCK key 
        LShiftKey = 0xA0, //Left SHIFT key
        RShiftKey = 0xA1, //Right SHIFT key
        LControlKey = 0xA2, //Left CONTROL key
        RControlKey = 0xA3, //Right CONTROL key
        LMenu = 0xA4, //Left MENU key
        RMenu = 0xA5, //Right MENU key
        Play = 0xFA, //Play key
        Zoom = 0xFB, //Zoom key 
    }

    /// <summary>
    /// Sends key or mousepress to window
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="Msg"></param>
    /// <param name="wParam"></param>
    /// <param name="lParam"></param>
    private static void Postmessage(IntPtr handle, uint Msg, uint wParam, uint lParam)
    {
        PostMessage(handle, Msg, wParam, lParam);
    }

    /// <summary>
    /// Converts x and y to Lparam to set location
    /// </summary>
    /// <param name="loWord"></param>
    /// <param name="hiWord"></param>
    /// <returns></returns>
    private static uint MakeLParam(uint loWord, uint hiWord)
    {
        return ((hiWord << 16) | (loWord & 0xffff));
    }

    /// <summary>
    /// </summary>
    /// <param name="k"></param>
    public static void KeyPress(Keys k)
    {
        IntPtr hWnd = FindWindow(null, WindowName);
        uint key = (uint)k;

        PostMessage(hWnd, (uint)WMessages.WM_KEYDOWN, key, 0);
        PostMessage(hWnd, (uint)WMessages.WM_CHAR, key, 0);
        PostMessage(hWnd, (uint)WMessages.WM_KEYUP, key, 0);
    }

    /// <summary>
    /// note: lParam need to be uint because int can give incorrect values
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    public static void MouseClick(string mouseclick, uint hiWord, uint loWord)
    {
        if (string.Equals(mouseclick, "Left"))
        {
            LeftMouseClick(hiWord, loWord);
        }
        else if (string.Equals(mouseclick, "Right"))
        {
            RightMouseClick(hiWord, loWord);
        }
    }

    /// <summary>
    /// Stops sending for x amount of time
    /// </summary>
    /// <param name="delay"></param>
    public static void Delay(int delay)
    {
        Thread.Sleep(delay);
    }

    /// <summary>
    /// left mouseclick
    /// </summary>
    /// <param name="hiWord"></param>
    /// <param name="loWord"></param>
    public static void LeftMouseClick(uint hiWord, uint loWord)
    {
        IntPtr hWnd = FindWindow(null, WindowName);
        uint lParam = MakeLParam(hiWord, loWord);

        PostMessage(hWnd, (uint)WMessages.WM_MOUSEMOVE, 0, lParam);
        PostMessage(hWnd, (uint)WMessages.WM_SETCURSOR, 0, lParam);
        PostMessage(hWnd, (uint)WMessages.WM_LBUTTONDOWN, 0, lParam);
        PostMessage(hWnd, (uint)WMessages.WM_LBUTTONUP, 0, lParam);
    }

    /// <summary>
    /// right mouseclick
    /// </summary>
    /// <param name="hiWord"></param>
    /// <param name="loWord"></param>
    public static void RightMouseClick(uint hiWord, uint loWord)
    {
        IntPtr hWnd = FindWindow(null, WindowName);
        uint lParam = MakeLParam(hiWord, loWord);

        PostMessage(hWnd, (uint)WMessages.WM_MOUSEMOVE, 0, lParam);
        PostMessage(hWnd, (uint)WMessages.WM_SETCURSOR, 0, lParam);
        PostMessage(hWnd, (uint)WMessages.WM_RBUTTONDOWN, 0, lParam);
        PostMessage(hWnd, (uint)WMessages.WM_RBUTTONUP, 0, lParam);
    }

    /// <summary>
    /// returns the window
    /// </summary>
    /// <param name="wndName"></param>
    /// <returns></returns>
    public static IntPtr FindWindow(string wndName)
    {
        return FindWindow(null, wndName);
    }
}







