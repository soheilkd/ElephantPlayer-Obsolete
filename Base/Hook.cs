﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Player.Hook
{
	public class KeyboardListener : IDisposable
	{
		public static KeyboardListener Create() => new KeyboardListener();

		private static IntPtr hookId = IntPtr.Zero;
		
		public event EventHandler<RawKeyEventArgs> KeyDown;
		public event EventHandler<RawKeyEventArgs> KeyUp;

		[MethodImpl(MethodImplOptions.NoInlining)]
		private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
		{
			try { return HookCallbackInner(nCode, wParam, lParam); }
			catch { return InterceptKeys.CallNextHookEx(hookId, nCode, wParam, lParam); }
		}

		private IntPtr HookCallbackInner(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (nCode >= 0)
			{
				int vkCode = Marshal.ReadInt32(lParam);
				if (wParam == (IntPtr)InterceptKeys.WM_KEYDOWN) KeyDown?.Invoke(this, new RawKeyEventArgs(vkCode, false));
				else if (wParam == (IntPtr)InterceptKeys.WM_KEYUP) KeyUp?.Invoke(this, new RawKeyEventArgs(vkCode, false));
			}
			return InterceptKeys.CallNextHookEx(hookId, nCode, wParam, lParam);
		}

		KeyboardListener() => hookId = InterceptKeys.SetHook(HookCallback);
		~KeyboardListener() => Dispose();

		public void Dispose() => InterceptKeys.UnhookWindowsHookEx(hookId);
	}

	internal static class InterceptKeys
	{
		public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

		private static LowLevelKeyboardProc lowLevelDelegate; //This will prevent proc in SetHook not to get garbage collected

		public const int WH_KEYBOARD_LL = 13;
		public const int WM_KEYDOWN = 0x0100;
		public const int WM_KEYUP = 0x0101;

		public static IntPtr SetHook(LowLevelKeyboardProc proc)
		{
			lowLevelDelegate = proc;
			using (Process curProcess = Process.GetCurrentProcess())
			using (ProcessModule curModule = curProcess.MainModule)
				return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UnhookWindowsHookEx(IntPtr hhk);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern IntPtr GetModuleHandle(string lpModuleName);
	}

	public class RawKeyEventArgs : EventArgs
	{
		public int VKCode;
		public Key Key;
		public bool IsSysKey;

		public RawKeyEventArgs(int vKCode, bool isSysKey)
		{
			VKCode = vKCode;
			IsSysKey = isSysKey;
			Key = KeyInterop.KeyFromVirtualKey(VKCode);
		}
	}
}