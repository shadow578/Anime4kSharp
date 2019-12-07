using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Anime4k.Algorithm;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;

namespace Anime4kCli
{
    public class App
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("READY");
            Console.ReadLine();

            Stopwatch sw = new Stopwatch();

            //quick test of algorithm
            Image<Rgba32> img = Image.Load<Rgba32>(@"./i/test.png");

            sw.Start();
            img = img.PushAnime4K().GetAwaiter().GetResult();
            sw.Stop();

            img.Save(@"./i/out.png");

            //end
            Console.WriteLine($"DONE in {sw.ElapsedMilliseconds} ms!");
            Console.ReadLine();
        }
    }
}
