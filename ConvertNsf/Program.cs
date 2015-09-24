using BizHawk.Client.Common;
using BizHawk.Common;
using BizHawk.Common.IOExtensions;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Nintendo.NES;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertNsf
{
    class Program
    {
        static void Main(string[] args)
        {

            //var pathToRom = @"\\Mac\Home\Downloads\contra-nes-[NSF-ID1909].nsf";
            var pathToRom = @"\\Mac\Home\Downloads\Contra (USA)\Contra (USA).nes";

            Global.Config = new Config();

            Global.Config.ResolveDefaults();
            HawkFile.ArchiveHandlerFactory = new SevenZipSharpArchiveHandler();

            Global.Config.NES_InQuickNES = false;
            Global.FirmwareManager = new FirmwareManager();

            Global.ActiveController = new Controller(NullEmulator.NullController);


            //NES.BootGodDB.GetDatabaseBytes = () => { return new byte[0]; };

            NES.BootGodDB.GetDatabaseBytes = () =>
            {
                using (var stream = new StreamReader(@"C:\Users\jeff\GitHub\BizHawk\output\gamedb\NesCarts (2012-10-22).xml"))
                {
                    return stream.BaseStream.ReadAllBytes();
                }

                //using (var NesCartFile =
                //        new HawkFile(Path.Combine(@"C:\Users\jeff\GitHub\BizHawk\output", "gamedb", "NesCarts.7z")).BindFirst())
                //{
                //    return NesCartFile
                //        .GetStream()
                //        .ReadAllBytes();
                //}
            };

            var loader = new RomLoader
            {
                //ChooseArchive = LoadArhiveChooser,
                ChoosePlatform = ChoosePlatformForRom,
                //Deterministic = deterministic,
                //MessageCallback = GlobalWin.OSD.AddMessage
            };

            var coreComm = new CoreComm(null, null);
            CoreFileProvider.SyncCoreCommInputSignals(coreComm);

            

            var result = loader.LoadRom(pathToRom, coreComm);

            if(!result)
            {
                Console.WriteLine("Could not load rom");
                Environment.Exit(1);
            }

            var emulator = loader.LoadedEmulator;
            var game = loader.Game;

            emulator.Controller = new Controller(emulator.ControllerDefinition);

            NES nes = (NES)emulator;
            APU apu = nes.apu;

            nes.apu.DebugCallbackDivider = 1;
            int lastP0Note = 0, lastP1Note = 0, lastTriNote = 0;

            int callbackCount = 0;

            nes.apu.DebugCallback = () => {
                callbackCount++;

                int pulse0_period = apu.pulse[0].timer_reload_value;
                float pulse0_freq = 1789773.0f / (16.0f * (pulse0_period + 1));
                int pulse0_note = FindNearestNote(pulse0_freq);

                int pulse1_period = apu.pulse[1].timer_reload_value;
                float pulse1_freq = 1789773.0f / (16.0f * (pulse1_period + 1));
                int pulse1_note = FindNearestNote(pulse1_freq);

                int tri_period = apu.triangle.Debug_PeriodValue;
                float tri_freq = 1789773.0f / (32.0f * (tri_period + 1));
                int tri_note = FindNearestNote(tri_freq);

                if (pulse0_note != lastP0Note || pulse1_note != lastP1Note || tri_note != lastTriNote)
                {
                    //Console.WriteLine("{0},{1},{2},{3}", callbackCount, NameForNote(pulse0_note), NameForNote(pulse1_note), NameForNote(tri_note));
                    Console.WriteLine("{0},{1},{2},{3}", callbackCount, pulse0_freq, pulse1_freq, tri_freq);

                    lastP0Note = pulse0_note;
                    lastP1Note = pulse1_note;
                    lastTriNote = tri_note;
                }
            };

            for (int i = 0; i < 60 * 10; i++)
            {
                emulator.FrameAdvance(false, true);
            }

            
        }

        static readonly float[] freqtbl = new[] {0,
            16.35f,17.32f,18.35f,19.45f,20.6f,21.83f,23.12f,24.5f,25.96f,27.5f,29.14f,30.87f,32.7f,34.65f,36.71f,38.89f,41.2f,43.65f,46.25f,49f,51.91f,55f,58.27f,61.74f,65.41f,69.3f,73.42f,77.78f,82.41f,87.31f,92.5f,98f,103.83f,110f,116.54f,123.47f,130.81f,138.59f,146.83f,155.56f,164.81f,174.61f,185f,196f,207.65f,220f,233.08f,246.94f,261.63f,277.18f,293.66f,311.13f,329.63f,349.23f,369.99f,392f,415.3f,440f,466.16f,493.88f,523.25f,554.37f,587.33f,622.25f,659.25f,698.46f,739.99f,783.99f,830.61f,880f,932.33f,987.77f,1046.5f,1108.73f,1174.66f,1244.51f,1318.51f,1396.91f,1479.98f,1567.98f,1661.22f,1760f,1864.66f,1975.53f,2093f,2217.46f,2349.32f,2489.02f,2637.02f,2793.83f,2959.96f,3135.96f,3322.44f,3520f,3729.31f,3951.07f,4186.01f,4434.92f,4698.63f,4978.03f,5274.04f,5587.65f,5919.91f,6271.93f,6644.88f,7040f,7458.62f,7902.13f,
            1000000
        };

        static int FindNearestNote(float freq)
        {
            for (int i = 1; i < freqtbl.Length; i++)
            {
                float a = freqtbl[i - 1];
                float b = freqtbl[i];
                float c = freqtbl[i + 1];
                float min = (a + b) / 2;
                float max = (b + c) / 2;
                if (freq >= min && freq <= max)
                    return i - 1;
            }
            return 95; //I guess?
        }

        static readonly string[] noteNames = new[] { "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-" };

        static string NameForNote(int note)
        {
            int tone = note % 12;
            int octave = note / 12;
            return string.Format("{0}{1}", noteNames[tone], octave);
        }

        private static string ChoosePlatformForRom(RomGame arg)
        {
            return "NES";
        }
    }
}
