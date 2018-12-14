using System;
using System.IO;
using System.Runtime.InteropServices;
using StarburstGaming;


namespace MouseDeamon {

	using Ptr = IntPtr;

	[StructLayout(LayoutKind.Explicit, Size = 40)]
	public struct CInput {
		[FieldOffset(0)] public uint type;
		[FieldOffset(8)] public CMouseInput mi;
		[FieldOffset(8)] public CKbdInput ki;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct CMouseInput {
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

	class Program {

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
			} catch(StarburstException) {
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

			CInput input = new CInput {
				type = 0,
				mi = new CMouseInput {
					dx = 0,
					dy = 0,
					mouseData = 1,
					dwFlags = 0x8001,
					time = 0,
					dwExtraInfo = 0
				}/*,
				ki = new CKbdInput {
					wVk = 0x42,
					wScan = 0,
					dwFlags = 0,
					dwExtraInfo = 0
				}*/
			};
			var inputPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CInput>());
			bool inputChanged = false;

			while (mouseComp.Action != ComponentAction.STOP) {
				Comet c = mousePositionTopic.GetComet();	// Range: -1 to 1
				if(c != null) {
					if(input.mi.dx != WinRange(c["X"].AsFloat()) || input.mi.dy != WinRange(c["Y"].AsFloat())) {
						input.mi.dx = WinRange(c["X"].AsFloat());   // Range: 0 to 65536
						input.mi.dy = WinRange(c["Y"].AsFloat());
						input.mi.dwFlags |= 0x1u;   // setting mouse moved bit
						inputChanged = true;
					}

					c.Dispose();
				}

				c = mouseEventTopic.GetComet();
				if (c != null) {
					if (c["LeftDown"].AsBool()) input.mi.dwFlags |= 0x2u;
					if (c["LeftUp"].AsBool()) input.mi.dwFlags |= 0x4u;
					if (c["RightDown"].AsBool()) input.mi.dwFlags |= 0x8u;
					if (c["RightUp"].AsBool()) input.mi.dwFlags |= 0x10u;
					inputChanged = true;

					c.Dispose();
				}

				if(inputChanged) {
					Marshal.StructureToPtr(input, inputPtr, true);
					if (SendInput(1, inputPtr, Marshal.SizeOf<CInput>()) < 1) throw new Exception("Couldn't send input : code " + GetLastError());
					input.mi.dwFlags = 0x8000;  // unsetting mouse moved bit
					inputChanged = false;
				}

				System.Threading.Thread.Sleep(50);
			}
			mouseComp.ReportState(ComponentState.STOPPED);
		}
	}
}
