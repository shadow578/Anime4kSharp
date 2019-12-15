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
        //console params:
        //-input/-i <file>      input file
        //-output/-o <file>     output file (optional, default value: -i + _anime4k.png)
        //-magnification/-m     by how much the image should be upscaled (optional, default value: 2)
        //-resolution/-r        target resolution (optional, overrides -m)
        //-strenght/-s <float>  algorithm strenght (optional, default value: 33 (% => 0.33))
        //-laps/-l <int>        how often the algorithm is repeated (optional, default value: 2)
        //-debug/-d             debug flag, when set the different phases are saved to disk
        public static void Main(string[] args)
        {
            Console.WriteLine("READY");
            Console.ReadLine();

            Stopwatch sw = new Stopwatch();

            //quick test of algorithm
            Image<Rgba32> img = Image.Load<Rgba32>(@"./i/test.png");

            sw.Start();
            img = Anime4K09.PushAnime4K(img, 0.6f, 3);
            sw.Stop();

            img.Save(@"./i/out.png");

            //end
            Console.WriteLine($"DONE in {sw.ElapsedMilliseconds} ms!");
            Console.ReadLine();
        }
    }
}
