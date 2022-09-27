using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UpgradeComponentFixer
{
    internal class Program
    {
        static string GetMostRecentSave(bool gamepass)
        {
            if (gamepass)
            {
                string SaveFilePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Packages\MonomiPark.SlimeRancher2_9ahw7gx0g86p2\SystemAppData\wgs\";
                SaveFilePath = Directory.GetDirectories(SaveFilePath)[0];
                string[] dirs = Directory.GetDirectories(SaveFilePath);
                List<FileInfo> files = new List<FileInfo>();
                foreach(string dir in dirs)
                {
                    List<FileInfo> fs = new DirectoryInfo(dir).GetFiles().ToList();
                    if (fs.Count > files.Count) files = fs;
                }
                FileInfo file = files.OrderByDescending(f => f.LastWriteTime).Where(f => f.Name.Split('.').Length == 1).First();
                return file.FullName;
            }
            else
            {
                string SaveFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"..\LocalLow\MonomiPark\SlimeRancher2\Steam\";
                SaveFilePath = Directory.GetDirectories(SaveFilePath)[0];
                DirectoryInfo dir = new DirectoryInfo(SaveFilePath);
                FileInfo file = dir.GetFiles().OrderByDescending(f => f.LastWriteTime).Where(f => f.Name.Split('.')[1] == "sav").First();
                return file.FullName;
            }
        }

        static List<byte>[] SliceSaveFile(byte[] data)
        {
            int pos = 0;
            List<byte> front = new List<byte>();
            while (pos < data.Length)
            {
                if (Encoding.UTF8.GetString(data, pos, 25) == "SRUPGRADECOMPONENTS\x01\x00\x00\x00\x00\x30")
                {
                    pos += 25;
                    break;
                }
                front.Add(data[pos]);
                pos++;
            }

            if (pos == data.Length)
            {
                Console.WriteLine("failed to read save file");
                Environment.Exit(0);
            }

            List<byte> back = new List<byte>();
            for (int i = pos; i < data.Length; i++)
            {
                back.Add(data[i]);
            }

            return new List<byte>[] { front, back };
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Are you using the gamepass version?");
            Console.WriteLine("0 - no   1 - yes");
            bool resp = Console.ReadLine() == "0" ? false : true;

            string save = GetMostRecentSave(resp);
            string name = save.Split('\\')[save.Split('\\').Length - 1];
            if (File.Exists($"./{name}-backup")) File.Delete($"./{name}-backup");
            File.Copy(save, $"./{name}-backup");
            byte[] bytes;
            using (Stream s = File.OpenRead(save))
            {
                using (BinaryReader bw = new BinaryReader(s))
                {
                    bytes = new byte[bw.BaseStream.Length];
                    bw.Read(bytes, 0, bytes.Length);
                }
            }
            List<byte>[] data = SliceSaveFile(bytes);
            List<int> ownedUpgrades = new List<int>();
            using (Stream s = new MemoryStream(data[1].ToArray()))
            {
                using (BinaryReader br = new BinaryReader(s))
                {
                    int l = br.ReadInt32();
                    for (int i = 0; i < l; i++)
                    {
                        ownedUpgrades.Add(br.ReadInt32());
                    }
                }
            }
            data[1].RemoveRange(0, 4 * (ownedUpgrades.Count + 1));
            Console.WriteLine("Which upgrades are you missing (separate with spaces)?");
            Console.WriteLine("1 - Extra Tank");
            Console.WriteLine("2 - Heart Module");
            Console.WriteLine("3 - Energy Module");
            Console.WriteLine("4 - Tank Booster");
            Console.WriteLine("5 - Tank Guard");
            string[] missing = Console.ReadLine().Split(' ');
            Dictionary<string, int> inputToUpgrade = new Dictionary<string, int>() { { "1", 1 }, { "2", 2 }, { "3", 4 }, {"4", 7 }, { "5", 8 } };
            foreach(string key in missing)
            {
                if (!inputToUpgrade.ContainsKey(key))
                {
                    Console.WriteLine($"Unknown upgrade index {key} -- ignoring");
                }
                else
                {
                    if (!ownedUpgrades.Contains(inputToUpgrade[key])) ownedUpgrades.Add(inputToUpgrade[key]);
                }
            }

            using (Stream s = File.OpenWrite(save))
            {
                using (BinaryWriter bw = new BinaryWriter(s))
                {
                    bw.Write(data[0].ToArray());
                    bw.Write(Encoding.UTF8.GetBytes("SRUPGRADECOMPONENTS\x01\x00\x00\x00\x00\x30"));
                    bw.Write(ownedUpgrades.Count);
                    foreach(int i in ownedUpgrades) bw.Write(i);
                    bw.Write(data[1].ToArray());
                }
            }

            Console.WriteLine("Done!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}