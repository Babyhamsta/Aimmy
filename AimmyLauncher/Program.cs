using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

// Credits to https://github.com/ewwink/MD5-Hash-Changer

byte[] GenerateByte()
{
    Random random = new Random();
    int num = random.Next(2, 7);
    byte[] extraByte = new byte[num];

    for (int j = 0; j < num; j++)
    {
        extraByte[j] = (byte)0;
    }

    return extraByte;
}

static string GenerateRandomString(int length)
{
    Random random = new Random();
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
    StringBuilder result = new StringBuilder(length);

    for (int i = 0; i < length; i++)
    {
        result.Append(chars[random.Next(chars.Length)]);
    }

    return result.ToString();
}

static bool MatchesSequence(byte[] fileBytes, int position, byte[] sequence)
{
    for (int i = 0; i < sequence.Length; i++)
    {
        if (fileBytes[position + i] != sequence[i])
        {
            return false;
        }
    }
    return true;
}

void ShuffleMD5(string file)
{
    long fileSize = new FileInfo(file).Length;
    int bufferSize = fileSize > 1048576L ? 1048576 : 4096;

    byte[] extraByte = GenerateByte();

    // Write NULL bytes to file
    using (FileStream fileStream = new FileStream(file, FileMode.Append))
    {
        fileStream.Write(extraByte, 0, extraByte.Length);
    }

    // Check MD5 hash
    using (MD5 md = MD5.Create())
    {
        using (FileStream fileStream2 = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
        {
            Console.WriteLine($"New MD5 hash: {BitConverter.ToString(md.ComputeHash(fileStream2)).Replace("-", "")}");
        }
    }
}

string ShuffleName(string currentDirectory, string oldFilePath)
{
    string exeNameWithoutExtension = Path.GetFileNameWithoutExtension(oldFilePath);
    string newFileName = GenerateRandomString(8);
    string newFilePath = Path.Combine(currentDirectory, $"{newFileName}.exe");

    try
    {
        File.Move(oldFilePath, newFilePath);
        Console.WriteLine($"Renamed '{oldFilePath}' to '{newFilePath}'.");
    }
    catch (IOException ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
    }

    return newFilePath;
}

void main()
{
    try
    {
        // Check for Aimmy EXE (it shuffles in name so we need another way to find it)
        string currentDirectory = AppContext.BaseDirectory;

        var exeFiles = Directory.GetFiles(currentDirectory, "*.exe")
                    .Where(file => Path.GetFileName(file) != "AimmyLauncher.exe")
                    .ToList();

        if (exeFiles.Count == 1)
        {
            Console.WriteLine("Found Aimmy, obscuring.");
            Console.WriteLine(exeFiles[0]);

            Console.WriteLine("Shuffling Hash, one moment..");
            ShuffleMD5(exeFiles[0]);

            Console.WriteLine("Shuffling exe name, one moment..");
            string NewEXE = ShuffleName(currentDirectory, exeFiles[0]);

            Console.WriteLine("Shuffling complete, launching Aimmy.");

            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.FileName = NewEXE;
            processInfo.UseShellExecute = true;
            //processInfo.Verb = "runas";
            Process.Start(processInfo);
        }
        else
        {
            Console.WriteLine("Couldn't find Aimmy or too many exe files in folder.");
            Console.ReadLine();
        }

        Thread.Sleep(5000);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"There was an error: {ex}");
        Console.ReadLine();
    }
}

main();