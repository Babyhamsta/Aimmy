using System.Diagnostics;
using System.IO.Ports;
using System.Text;


namespace MouseMovementLibraries.MakcuSupport
{
    public enum MakcuMouseButton
    {
        Left = 0, Right = 1, Middle = 2, Mouse4 = 3, Mouse5 = 4
    }

    public class MakcuMouse : IDisposable
    {
        private static readonly byte[] BaudChangeCommand = { 0xDE, 0xAD, 0x05, 0x00, 0xA5, 0x00, 0x09, 0x3D, 0x00 };

        private SerialPort _serialPort;
        private readonly bool _debugLogging;
        private readonly bool _sendInitCommands;

        private readonly object _serialLock = new object();
        private bool _isInitializedAndConnected = false;
        private Thread _listenerThread;
        private ManualResetEventSlim _stopListenerEvent = new ManualResetEventSlim(false);
        private volatile bool _pauseListener = false;

        private readonly Dictionary<MakcuMouseButton, bool> _buttonStates = new Dictionary<MakcuMouseButton, bool>();

        public event Action<MakcuMouseButton, bool> ButtonStateChanged;

        public string PortName { get; private set; }
        public int BaudRate => _serialPort?.BaudRate ?? (_isInitializedAndConnected ? 4000000 : 115200);
        public bool IsInitializedAndConnected => _isInitializedAndConnected && _serialPort != null && _serialPort.IsOpen;


        public MakcuMouse(bool debugLogging = false, bool sendInitCommands = true)
        {
            _debugLogging = debugLogging;
            _sendInitCommands = sendInitCommands;
            foreach (MakcuMouseButton btn in Enum.GetValues(typeof(MakcuMouseButton)))
            {
                _buttonStates[btn] = false;
            }
        }

        private void Log(string message)
        {
            if (_debugLogging)
            {
                Console.WriteLine($"[MakcuMouse {DateTime.Now:HH:mm:ss}] {message}");
            }
        }


        private string FindComPortInternal()
        {
            Log("Searching for COM port for Makcu device using SerialPort.GetPortNames() and connection test...");
            string[] availablePorts;
            try
            {
                availablePorts = SerialPort.GetPortNames();
            }
            catch (Exception ex)
            {
                Log($"Error getting COM port list: {ex.Message}. Cannot continue search.");
                return null;
            }

            if (availablePorts == null || !availablePorts.Any())
            {
                Log("No COM ports available according to SerialPort.GetPortNames().");
                return null;
            }

            Log($"Available COM ports: {string.Join(", ", availablePorts)}. Testing each one...");

            foreach (string portName in availablePorts)
            {
                Log($"Testing port: {portName}");
                SerialPort testPort = null;
                try
                {

                    testPort = new SerialPort(portName, 115200)
                    {
                        ReadTimeout = 250,
                        WriteTimeout = 500,
                        DtrEnable = true,
                        RtsEnable = true
                    };
                    testPort.Open();
                    Log($"Port {portName} opened at 115200 baud.");

                    Log($"Sending command to change baudrate to 4M on {portName}.");
                    testPort.Write(BaudChangeCommand, 0, BaudChangeCommand.Length);
                    testPort.BaseStream.Flush();
                    Thread.Sleep(150);

                    testPort.Close();
                    testPort.BaudRate = 4000000;
                    testPort.Open();
                    Log($"Port {portName} reopened at 4000000 baud.");

                    testPort.DiscardInBuffer();
                    testPort.Write("km.version()\r\n");
                    Log($"Command 'km.version()' sent to {portName}. Waiting for response...");

                    string response = "";
                    try
                    {
                        response = testPort.ReadLine().Trim();
                    }
                    catch (TimeoutException)
                    {
                        Log($"Timeout waiting for 'km.version()' response on {portName}. Probably not the Makcu device.");
                        testPort.Close();
                        continue;
                    }

                    Log($"Response from 'km.version()' on {portName}: '{response}'");

                    if (!string.IsNullOrEmpty(response) && (response.Contains("KMBOX") || response.Contains("Makcu") || response.Contains("MAKCU") || response.StartsWith("v") || char.IsDigit(response.FirstOrDefault())))
                    {
                        Log($"Makcu device found on port: {portName}! Version response: '{response}'");
                        testPort.Close();
                        return portName;
                    }
                    else
                    {
                        Log($"Response from 'km.version()' on {portName} does not seem to be from a Makcu device. Response: '{response}'");
                    }

                    testPort.Close();
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log($"Unauthorized access error on {portName}: {ex.Message} (Port might be in use).");
                }
                catch (TimeoutException ex)
                {
                    Log($"Timeout on {portName} (possibly during baud rate change or write): {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log($"Failed to test port {portName}: {ex.GetType().Name} - {ex.Message}");
                }
                finally
                {
                    if (testPort != null && testPort.IsOpen)
                    {
                        testPort.Close();
                    }
                    testPort?.Dispose();
                }
            }

            Log("Makcu device not found on any available COM ports after connection test.");
            return null;
        }


        private bool OpenSerialPort(string portNameToOpen, int baudRate)
        {
            try
            {
                _serialPort?.Close();
                _serialPort?.Dispose();

                Log($"Attempting to open {portNameToOpen} at {baudRate} baud.");
                _serialPort = new SerialPort(portNameToOpen, baudRate)
                {
                    ReadTimeout = 100,
                    WriteTimeout = 500,
                    DtrEnable = true,
                    RtsEnable = true
                };
                _serialPort.Open();
                PortName = portNameToOpen;
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to open {portNameToOpen} at {baudRate} baud. Error: {ex.Message}");
                _serialPort?.Dispose(); _serialPort = null; return false;
            }
        }

        private bool ChangeBaudRateTo4M()
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                Log("ChangeBaudRateTo4M: _serialPort is null or not open.");
                return false;
            }
            Log("Sending command to change baudrate to 4M.");
            try
            {
                _serialPort.Write(BaudChangeCommand, 0, BaudChangeCommand.Length);
                _serialPort.BaseStream.Flush();
                Thread.Sleep(150);

                string currentPortName = _serialPort.PortName;
                _serialPort.Close();

                _serialPort.BaudRate = 4000000;
                _serialPort.Open();

                Log("Successfully changed to 4M baud."); return true;
            }
            catch (Exception ex)
            {
                Log($"Error changing baudrate to 4M: {ex.Message}");

                _serialPort?.Close();
                return false;
            }
        }

        public bool Init()
        {
            lock (_serialLock)
            {
                if (_isInitializedAndConnected) { Log("MakcuMouse is already initialized."); return true; }

                string portToUse = FindComPortInternal();
                if (string.IsNullOrEmpty(portToUse))
                { Log("Init failed: Could not find a valid COM port for Makcu."); return false; }


                if (!OpenSerialPort(portToUse, 115200))
                { Log($"Init failed: Could not open port {portToUse} at 115200 baud."); return false; }

                if (!ChangeBaudRateTo4M())
                { Log("Init failed: Could not change to 4M baud."); Close(); return false; }

                _isInitializedAndConnected = true;
                PortName = portToUse;

                if (_sendInitCommands)
                {
                    if (!SendCommandInternal("km.buttons(1)", false, out _))
                    { Log("Init failed: Could not send 'km.buttons(1)'."); Close(); return false; }

                    _stopListenerEvent.Reset();
                    _listenerThread = new Thread(() => ListenForButtonEvents(_debugLogging))
                    { IsBackground = true, Name = "MakcuButtonListener" };
                    _listenerThread.Start();
                }
                Log($"MakcuMouse initialized successfully on port {PortName} at {BaudRate} baud."); return true;
            }
        }

        private bool SendCommandInternal(string command, bool expectResponse, out string responseText, int responseTimeoutMs = 200)
        {
            responseText = null;
            if (!_isInitializedAndConnected || _serialPort == null || !_serialPort.IsOpen)
            {
                Log($"Error in SendCommand: Connection not open or initialized. Command: {command}");
                return false;
            }

            lock (_serialLock)
            {
                if (!_serialPort.IsOpen)
                {
                    Log($"Error in SendCommand (lock): Port not open. Command: {command}");
                    return false;
                }

                _pauseListener = true;
                int originalReadTimeout = _serialPort.ReadTimeout;

                try
                {
                    if (expectResponse)
                    {
                        _serialPort.ReadTimeout = responseTimeoutMs;
                        _serialPort.DiscardInBuffer();
                    }

                    Log($"Sending: {command}");
                    _serialPort.Write(command + "\r\n");

                    if (expectResponse)
                    {
                        var stopwatch = Stopwatch.StartNew();
                        StringBuilder sb = new StringBuilder();
                        string commandTrimmed = command.Trim();
                        bool firstLineIsEcho = true;

                        while (stopwatch.ElapsedMilliseconds < responseTimeoutMs)
                        {
                            try
                            {
                                string line = _serialPort.ReadLine().Trim();
                                if (!string.IsNullOrEmpty(line))
                                {
                                    Log($"Received (raw): {line}");
                                    if (line.Equals(commandTrimmed, StringComparison.OrdinalIgnoreCase) && firstLineIsEcho)
                                    {
                                        firstLineIsEcho = false;
                                        continue;
                                    }
                                    if (line.Equals("OK", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (sb.Length > 0) continue;
                                    }
                                    if (sb.Length > 0) sb.Append("\n");
                                    sb.Append(line);
                                }
                                if (_serialPort.BytesToRead == 0 && sb.Length > 0)
                                {
                                    Thread.Sleep(15);
                                    if (_serialPort.BytesToRead == 0) break;
                                }
                            }
                            catch (TimeoutException)
                            {
                                Log($"ReadLine() timeout in SendCommandInternal for '{command}'.");
                                break;
                            }
                            catch (InvalidOperationException ioe) { Log($"InvalidOperationException in ReadLine (SendCommandInternal): {ioe.Message}"); _isInitializedAndConnected = false; Close(); return false; }
                            catch (Exception ex) { Log($"Exception in ReadLine (SendCommandInternal): {ex.Message}"); break; }
                        }
                        stopwatch.Stop();
                        responseText = sb.ToString().Trim();
                        Log($"Final response for '{command}': '{responseText}' (Time: {stopwatch.ElapsedMilliseconds}ms)");
                        return true;
                    }
                    return true;
                }
                catch (TimeoutException tex)
                {
                    Log($"General timeout in SendCommandInternal for '{command}': {tex.Message}");
                    return false;
                }
                catch (InvalidOperationException ioe)
                {
                    Log($"General InvalidOperationException in SendCommandInternal (possibly port closed): {ioe.Message}");
                    _isInitializedAndConnected = false; Close();
                    return false;
                }
                catch (Exception ex)
                {
                    Log($"General exception in SendCommandInternal for '{command}': {ex.Message}");
                    return false;
                }
                finally
                {
                    _pauseListener = false;
                    if (expectResponse)
                    {
                        _serialPort.ReadTimeout = originalReadTimeout;
                    }
                }
            }
        }

        private void ListenForButtonEvents(bool debug)
        {
            Log("Listener thread started. (PACKET PARSING MODE)");
            byte lastMask = 0x00; // <<-- CAMBIO CLAVE: Inicializar como todos los botones liberados
            var buttonMap = new Dictionary<int, MakcuMouseButton> {
        {0, MakcuMouseButton.Left}, {1, MakcuMouseButton.Right}, {2, MakcuMouseButton.Middle},
        {3, MakcuMouseButton.Mouse4}, {4, MakcuMouseButton.Mouse5}
    };

            byte[] packetHeader = { 0x6B, 0x6D, 0x2E };
            int currentHeaderIndex = 0;

            while (!_stopListenerEvent.IsSet)
            {
                if (!IsInitializedAndConnected || _pauseListener || _serialPort == null || !_serialPort.IsOpen)
                {
                    Thread.Sleep(20);
                    currentHeaderIndex = 0;
                    continue;
                }

                try
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        byte byteRead = (byte)_serialPort.ReadByte();

                        if (byteRead == packetHeader[currentHeaderIndex])
                        {
                            currentHeaderIndex++;
                            if (currentHeaderIndex == packetHeader.Length)
                            {
                                if (debug) Log($"Listener: Packet header {string.Join(",", packetHeader.Select(b => b.ToString("X2")))} detected!");

                                if (_serialPort.BytesToRead > 0)
                                {
                                    byte currentMask = (byte)_serialPort.ReadByte();
                                    if (debug) Log($"Listener: Potential button mask byte: 0x{currentMask:X2}");

                                    if (currentMask <= 0b00011111)
                                    {
                                        if (currentMask != lastMask)
                                        {
                                            if (debug) Log($"Listener: Processing button mask. New: 0x{currentMask:X2}, Prev: 0x{lastMask:X2}");
                                            byte changedBits = (byte)(currentMask ^ lastMask);

                                            lock (_buttonStates)
                                            {
                                                foreach (var pair in buttonMap)
                                                {
                                                    if ((changedBits & (1 << pair.Key)) != 0)
                                                    {
                                                        bool isPressed = (currentMask & (1 << pair.Key)) != 0;
                                                        _buttonStates[pair.Value] = isPressed;
                                                        if (debug) Log($"Listener: ---> EVENT: Button: {pair.Value}, IsPressed: {isPressed}");
                                                        try
                                                        {
                                                            ButtonStateChanged?.Invoke(pair.Value, isPressed);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            Log($"Exception in ButtonStateChanged handler: {ex.Message}");
                                                        }
                                                    }
                                                }
                                            }
                                            lastMask = currentMask;
                                            if (debug)
                                            {
                                                var pressedButtons = _buttonStates.Where(kvp => kvp.Value).Select(kvp => kvp.Key.ToString()).ToArray();
                                                Log($"Listener: Button states updated. Mask: 0x{currentMask:X2} -> {(pressedButtons.Any() ? string.Join(", ", pressedButtons) : "None")}");
                                            }
                                        }
                                    }

                                    int expectedTailBytes = 2;
                                    for (int i = 0; i < expectedTailBytes; i++)
                                    {
                                        if (_serialPort.BytesToRead > 0)
                                        {
                                            byte consumedByte = (byte)_serialPort.ReadByte();
                                            if (debug) Log($"Listener: Consumed tail byte {i + 1}: 0x{consumedByte:X2}");
                                        }
                                        else
                                        {
                                            if (debug) Log($"Listener: Expected tail byte {i + 1} but no data. Packet might be short.");
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    if (debug) Log("Listener: Header found, but no data for button mask byte. Packet might be short.");
                                }
                                currentHeaderIndex = 0;
                            }
                        }
                        else
                        {
                            if (currentHeaderIndex > 0 && debug)
                            {
                                Log($"Listener: Byte 0x{byteRead:X2} broke header sequence at index {currentHeaderIndex}. Resetting search.");
                            }
                            currentHeaderIndex = 0;
                            if (byteRead == packetHeader[0])
                            {
                                currentHeaderIndex = 1;
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (TimeoutException)
                {
                    if (debug) Log("Listener: TimeoutException during serial read.");
                    currentHeaderIndex = 0;
                }
                catch (InvalidOperationException ex)
                {
                    Log($"Listener: InvalidOperationException (port probably closed): {ex.Message}. Stopping listener.");
                    _isInitializedAndConnected = false;
                    currentHeaderIndex = 0;
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Error in listener: {ex.GetType().Name} - {ex.Message}");
                    currentHeaderIndex = 0;
                    Thread.Sleep(100);
                }
            }
            Log("Listener thread terminated.");
        }

        public bool Press(MakcuMouseButton button) => SendCommandInternal($"km.{GetButtonString(button)}(1)", false, out _);
        public bool Release(MakcuMouseButton button) => SendCommandInternal($"km.{GetButtonString(button)}(0)", false, out _);
        public bool Move(int x, int y) => SendCommandInternal($"km.move({x},{y})", false, out _);
        public bool MoveSmooth(int x, int y, int segments) => SendCommandInternal($"km.move({x},{y},{segments})", false, out _);
        public bool MoveBezier(int x, int y, int segments, int ctrlX, int ctrlY) => SendCommandInternal($"km.move({x},{y},{segments},{ctrlX},{ctrlY})", false, out _);
        public bool Scroll(int delta) => SendCommandInternal($"km.wheel({delta})", false, out _);

        public string GetKmVersion()
        {
            return SendCommandInternal("km.version()", true, out string response, 500) ? response : null;
        }
        private string GetButtonString(MakcuMouseButton button)
        {
            switch (button)
            {
                case MakcuMouseButton.Left: return "left";
                case MakcuMouseButton.Right: return "right";
                case MakcuMouseButton.Middle: return "middle";
                default: throw new ArgumentException($"Button {button} not supported for direct press/release actions (left/right/middle).");
            }
        }
        private string GetButtonLockString(MakcuMouseButton button)
        {
            switch (button)
            {
                case MakcuMouseButton.Left: return "ml";
                case MakcuMouseButton.Right: return "mr";
                case MakcuMouseButton.Middle: return "mm";
                case MakcuMouseButton.Mouse4: return "ms1";
                case MakcuMouseButton.Mouse5: return "ms2";
                default: throw new ArgumentException($"Button not supported for lock/catch: {button}");
            }
        }
        public Dictionary<MakcuMouseButton, bool> GetCurrentButtonStates()
        { lock (_buttonStates) { return new Dictionary<MakcuMouseButton, bool>(_buttonStates); } }

        public int GetCurrentButtonMask()
        {
            int mask = 0;
            lock (_buttonStates)
            {
                if (_buttonStates.TryGetValue(MakcuMouseButton.Left, out bool l) && l) mask |= (1 << (int)MakcuMouseButton.Left);
                if (_buttonStates.TryGetValue(MakcuMouseButton.Right, out bool r) && r) mask |= (1 << (int)MakcuMouseButton.Right);
                if (_buttonStates.TryGetValue(MakcuMouseButton.Middle, out bool m) && m) mask |= (1 << (int)MakcuMouseButton.Middle);
                if (_buttonStates.TryGetValue(MakcuMouseButton.Mouse4, out bool m4) && m4) mask |= (1 << (int)MakcuMouseButton.Mouse4);
                if (_buttonStates.TryGetValue(MakcuMouseButton.Mouse5, out bool m5) && m5) mask |= (1 << (int)MakcuMouseButton.Mouse5);
            }
            return mask;
        }

        private bool _disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Close();
                    _stopListenerEvent?.Dispose();
                }
                _disposedValue = true;
            }
        }
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        ~MakcuMouse() { Dispose(false); }

        public void Close()
        {
            Log("Closing MakcuMouse connection...");
            _isInitializedAndConnected = false;

            if (_listenerThread != null)
            {
                _stopListenerEvent.Set();
                if (_listenerThread.IsAlive)
                {
                    if (!_listenerThread.Join(TimeSpan.FromSeconds(1)))
                    {
                        Log("Listener thread did not terminate in time.");
                    }
                }
                _listenerThread = null;
            }
            lock (_serialLock)
            {
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        try { _serialPort.Close(); }
                        catch (Exception ex) { Log($"Error closing port: {ex.Message}"); }
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            PortName = null;
            Log("MakcuMouse connection closed.");
        }
    }
}