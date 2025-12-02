using System;
using DokanNet;

namespace Shaman.Dokan
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Shaman.Dokan.Archive <archive-file> <mount-point>");
                return 1;
            }

            string archivePath = args[0];
            string mountPoint = args[1];

            Console.WriteLine($"Mounting archive: {archivePath}");
            Console.WriteLine($"Mounting at: {mountPoint}");

            var fs = new SevenZipFs(archivePath);

            DokanOptions options =
                DokanOptions.DebugMode |
                DokanOptions.StderrOutput |
                DokanOptions.WriteProtection;

            try
            {
                // IMPORTANT: namespace complet
                DokanNet.Dokan.Mount(fs, mountPoint, options);

                Console.WriteLine("Mounted successfully. Press Enter to exit.");
                Console.ReadLine();

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Mount failed:");
                Console.WriteLine(ex);
                return -1;
            }
        }
    }
}
