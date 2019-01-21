using System;
using System.IO;
using System.Runtime.InteropServices;
using StarburstGaming;


namespace MouseDeamon {

	using Ptr = IntPtr;

	[StructLayout(LayoutKind.Explicit, Size = 40)]
	public struct CInput {
		public const uint INPUT_MOUSE = 0;
		public const uint INPUT_KEYBOARD = 1;

		[FieldOffset(0)] public uint type;
		[FieldOffset(8)] public CMouseInput mi;
		[FieldOffset(8)] public CKbdInput ki;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct CMouseInput {
		public const uint FLAG_ABSOLUTE = 0x8000;
		public const uint FLAG_MOUSEMOVED = 0x1;
		public const uint FLAG_LEFTDOWN = 0x2;
		public const uint FLAG_LEFTUP = 0x4;
		public const uint FLAG_RIGHTDOWN = 0x8;
		public const uint FLAG_RIGHTUP = 0x10;

		public int dx;
		public int dy;
		public uint mouseData;
		public uint dwFlags;
		public uint time;
		public ulong dwExtraInfo;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct CKbdInput {
		public ushort wVk;
		public ushort wScan;
		public uint dwFlags;
		public uint time;
		public ulong dwExtraInfo;
	}
	/*new CKbdInput {
		wVk = 0x42,
		wScan = 0,
		dwFlags = 0,
		dwExtraInfo = 0
	};*/

	static class InputExtensions {
		public static CInput AsCInput(this CMouseInput cmi) => new CInput { type = CInput.INPUT_MOUSE, mi = cmi };
		public static CInput AsCInput(this CKbdInput cki) => new CInput { type = CInput.INPUT_KEYBOARD, ki = cki };
	}

	static class Program {

		[DllImport("User32.dll")]
		static extern int SetForegroundWindow(Ptr hWnd);

		[DllImport("User32.dll")]
		static extern uint SendInput(uint cInputs, Ptr pInputs, int cbSize);

		[DllImport("Kernel32.dll")]
		static extern uint GetLastError();

		static void ErrorCallback(string msg) {
			Console.WriteLine("SBGError: " + msg);
		}

		static int WinRange(float i) => (int)((i + 1.0f) * 32768.0f);

		static void Main(string[] args) {
			Toolbox.RegisterLogCallback(ErrorCallback, LogMask.ERROR);
			Toolbox.SetDefinitionLoadCallback(File.ReadAllText);
			Toolbox.SetResourceLoadCallback(File.ReadAllBytes);
			Comet.InitModule();
			Nebula.InitModule();
			Engine.InitModule("SBG/MouseDeamon");

			try {
				Satellite.GetSelf();
			} catch (StarburstException) {
				Satellite.Deploy();
			}
			Component mouseComp = new Component("SBG/MouseControl");

			Topic.CometReceiver mousePositionTopic = new Topic.CometReceiver("mousePosition", Topic.CometReceiver.StorageType.STREAM);
			Topic.CometReceiver mouseEventTopic = new Topic.CometReceiver("mouseEvent", Topic.CometReceiver.StorageType.QUEUE);

			Session se = null;
			while (!Session.TryFind(out se)) { System.Threading.Thread.Sleep(1000); }
			Engine.JoinSession(se);
			while (mouseComp.Action != ComponentAction.START) { System.Threading.Thread.Sleep(100); }
			mouseComp.ReportState(ComponentState.STARTED);

			CMouseInput mi = new CMouseInput { dwFlags = CMouseInput.FLAG_ABSOLUTE };

			var inputPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CInput>());
			bool inputChanged = false;

			while (mouseComp.Action != ComponentAction.STOP) {
				Comet c = mousePositionTopic.GetComet();    // Range: -1 to 1
				if (c != null) {
					if (mi.dx != WinRange(c["X"].AsFloat()) || mi.dy != WinRange(c["Y"].AsFloat())) {
						mi.dx = WinRange(c["X"].AsFloat());   // Range: 0 to 65536
						mi.dy = WinRange(c["Y"].AsFloat());
						mi.dwFlags |= CMouseInput.FLAG_MOUSEMOVED;
						inputChanged = true;
					}

					c.Dispose();
				}

				c = mouseEventTopic.GetComet();
				if (c != null) {
					mi.dwFlags |= c["LeftDown"].AsBool() ? CMouseInput.FLAG_LEFTDOWN : 0;
					mi.dwFlags |= c["LeftUp"].AsBool() ? CMouseInput.FLAG_LEFTUP : 0;
					mi.dwFlags |= c["RightDown"].AsBool() ? CMouseInput.FLAG_RIGHTDOWN : 0;
					mi.dwFlags |= c["RightUp"].AsBool() ? CMouseInput.FLAG_RIGHTUP : 0;
					inputChanged = true;

					c.Dispose();
				}

				if (inputChanged) {
					Marshal.StructureToPtr(mi.AsCInput(), inputPtr, true);
					if (SendInput(1, inputPtr, Marshal.SizeOf<CInput>()) < 1) throw new Exception("Couldn't send input : error " + GetLastError());
					mi.dwFlags = CMouseInput.FLAG_ABSOLUTE;  // reseting flags
					inputChanged = false;
				}

				System.Threading.Thread.Sleep(20);
			}
			mouseComp.ReportState(ComponentState.STOPPED);
		}
	}
}
