using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

class MemoryEditor
{
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    public static extern short GetKeyState(int keyCode);

    const int PROCESS_ALL_ACCESS = 0x001F0FFF;
    const int NEW_AMMO = 999; // Valor da munição infinita
    const int AMMO_CHECK_INTERVAL = 100; // Intervalo de verificação em milissegundos
    const int VK_NUMLOCK = 0x90; // Código da tecla NumLock

    static bool infiniteAmmoEnabled = false; // Flag para habilitar/desabilitar munição infinita

    static void Main(string[] args)
    {
        Process[] processes = Process.GetProcessesByName("Postal2");

        if (processes.Length == 0)
        {
            Console.WriteLine("Postal2 process not found.");
            return;
        }

        Process process = processes[0];
        IntPtr processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, process.Id);

        IntPtr baseAddress = GetModuleBaseAddress(process, "Postal2.exe") + 0x0002C094;
        if (baseAddress == IntPtr.Zero)
        {
            Console.WriteLine("Base address not found.");
            return;
        }

        int[] offsets = { 0x14, 0x308, 0x8C, 0x98, 0xC, 0x38, 0x9CC };
        IntPtr ammoAddress = FindDMAAddy(processHandle, baseAddress, offsets);

        Console.WriteLine($"Base Address: {baseAddress.ToString("X")}");
        Console.WriteLine($"Calculated Ammo Address: {ammoAddress.ToString("X")}");

        // Thread para verificar e atualizar a munição
        Thread ammoThread = new Thread(() => MonitorAmmo(processHandle, ammoAddress));
        ammoThread.Start();

     
        bool lastNumLockState = GetNumLockState();
        while (true)
        {
            bool currentNumLockState = GetNumLockState();
            if (currentNumLockState != lastNumLockState)
            {
                infiniteAmmoEnabled = !infiniteAmmoEnabled;
                Console.WriteLine(infiniteAmmoEnabled ? "Munição infinita habilitada." : "Munição infinita desabilitada.");
                lastNumLockState = currentNumLockState;
            }
            Thread.Sleep(100); 
        }

    }

    static void MonitorAmmo(IntPtr processHandle, IntPtr ammoAddress)
    {
        while (true)
        {
            if (infiniteAmmoEnabled)
            {
                byte[] ammoBuffer = new byte[4];
                if (ReadProcessMemory(processHandle, ammoAddress, ammoBuffer, ammoBuffer.Length, out int bytesRead))
                {
                    int currentAmmo = BitConverter.ToInt32(ammoBuffer, 0);
                    if (currentAmmo < NEW_AMMO)
                    {
                        byte[] newAmmoBuffer = BitConverter.GetBytes(NEW_AMMO);
                        if (WriteProcessMemory(processHandle, ammoAddress, newAmmoBuffer, newAmmoBuffer.Length, out int bytesWritten))
                        {
                            Console.WriteLine($"Ammo changed to: {NEW_AMMO}");
                        }
                        else
                        {
                            Console.WriteLine("Failed to write new ammo value.");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Failed to read current ammo value.");
                    Console.WriteLine($"Ammo Address: {ammoAddress.ToString("X")}, Bytes Read: {bytesRead}");
                }
            }
            Thread.Sleep(AMMO_CHECK_INTERVAL); // Aguarda antes de verificar novamente
        }
    }

    public static bool GetNumLockState()
    {
        return (GetKeyState(VK_NUMLOCK) & 0x0001) != 0;
    }

    public static IntPtr FindDMAAddy(IntPtr hProc, IntPtr ptr, int[] offsets)
    {
        byte[] buffer = new byte[4];
        for (int i = 0; i < offsets.Length; i++)
        {
            Console.WriteLine($"Reading memory at {ptr.ToString("X")} with offset {offsets[i]:X}");
            if (!ReadProcessMemory(hProc, ptr, buffer, buffer.Length, out int bytesRead))
            {
                Console.WriteLine($"Failed to read memory at offset index {i}");
                return IntPtr.Zero;
            }
            ptr = (IntPtr)(BitConverter.ToInt32(buffer, 0) + offsets[i]);
            Console.WriteLine($"New pointer address after offset {i}: {ptr.ToString("X")}");
        }
        return ptr;
    }

    public static IntPtr GetModuleBaseAddress(Process process, string moduleName)
    {
        foreach (ProcessModule module in process.Modules)
        {
            if (module.ModuleName == moduleName)
            {
                return module.BaseAddress;
            }
        }
        return IntPtr.Zero;
    }

    public static void AddInfiniteAmmoForWeapon(IntPtr processHandle, IntPtr baseAddress, int[] offsets)
    {
        IntPtr weaponAmmoAddress = FindDMAAddy(processHandle, baseAddress, offsets);
        Console.WriteLine($"Calculated Weapon Ammo Address: {weaponAmmoAddress.ToString("X")}");

        Thread weaponAmmoThread = new Thread(() => MonitorAmmo(processHandle, weaponAmmoAddress));
        weaponAmmoThread.Start();
    }

    public static void ScanForWeaponAddresses(IntPtr processHandle, IntPtr baseAddress)
    {
   
        int scanRange = 0x1000; // Range de memória para escanear
        byte[] buffer = new byte[4];

        for (int i = 0; i < scanRange; i += 4)
        {
            IntPtr currentAddress = IntPtr.Add(baseAddress, i);
            if (ReadProcessMemory(processHandle, currentAddress, buffer, buffer.Length, out int bytesRead))
            {
                int value = BitConverter.ToInt32(buffer, 0);
            
                if (value > 0 && value < 1000)
                {
                    Console.WriteLine($"Possible weapon ammo address found at: {currentAddress.ToString("X")} with value: {value}");
                  
                    AddInfiniteAmmoForWeapon(processHandle, currentAddress, new int[] { });
                }
            }
        }
    }
}
