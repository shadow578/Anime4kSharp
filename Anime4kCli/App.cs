using Anime4k.Algorithm;
using Anime4k.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Anime4kCli
{
    public static class App
    {
        /// <summary>
        /// Error codes that are returned by the application
        /// </summary>
        enum ErrorCode : int
        {
            NO_ERROR = 0,
            NO_ARGUMENTS,
            CANNOT_FIND_INPUT_FILE
        }

        public static int Main(string[] args)
        {
            //run a4k
            ErrorCode err = MainA4K(args);

            //show help page on errors
            if (err != ErrorCode.NO_ERROR)
            {
                PrintHelp(Enum.GetName(typeof(ErrorCode), err));
            }

            return (int)err;
        }

        /// <summary>
        /// Anime4K Cli main function
        /// </summary>
        /// <param name="args">console arguments</param>
        /// <returns>the return code</returns>
        static ErrorCode MainA4K(string[] args)
        {
            //error when no args are given
            if (args.Length <= 0)
            {
                return ErrorCode.NO_ARGUMENTS;
            }

            //prepare variables (+ default values) that should be parsed from command line
            string inputFile;
            string outputFile;
            float scaleFactor = 2f;
            Size targetSize = Size.Empty;
            float strengthColor = -1f;
            float strengthGradient = -1f;
            int a4kLaps = 2;
            bool debug = false;

            //parse the command line arguments
            Dictionary<string, string> cArgs = ParseArgs(args, new char[] { '-', '/' });

            #region Parse Argument List

            //get input path
            if (!cArgs.TryGetValue("input", "i", out inputFile)
                || string.IsNullOrWhiteSpace(inputFile) || !File.Exists(inputFile))
            {
                return ErrorCode.CANNOT_FIND_INPUT_FILE;
            }


            //get output path
            if (!cArgs.TryGetValue("output", "o", out outputFile))
            {
                //default to inputFile + "_anime4k.png"
                outputFile = Path.Combine(Path.GetDirectoryName(inputFile), Path.GetFileNameWithoutExtension(inputFile) + "_anime4k.png");
            }

            //get scale
            if (cArgs.TryGetValue("scale", "s", out string scaleStr)
                && !float.TryParse(scaleStr, out scaleFactor))
            {
                Console.WriteLine($"Failed to parse scale factor value from input string \"{scaleStr}\"! Defaulting to {scaleFactor}");
            }

            //get resolution
            if (cArgs.TryGetValue("resolution", "r", out string resStr)
                && !TryParseSize(resStr, out targetSize))
            {
                Console.WriteLine($"Failed to parse target resolution value from input string \"{resStr}\"!");
            }

            //get color strength
            if (cArgs.TryGetValue("strenghtC", "sc", out string strenghtColStr)
                && !float.TryParse(strenghtColStr, out strengthColor))
            {
                Console.WriteLine($"Failed to parse color push strength value from input string \"{strenghtColStr}\"!");
            }

            //get gradient strength
            if (cArgs.TryGetValue("strenghtG", "sg", out string strenghtGradStr)
                && !float.TryParse(strenghtGradStr, out strengthGradient))
            {
                Console.WriteLine($"Failed to parse gradient push strength value from input string \"{strenghtGradStr}\"!");
            }

            //get a4k laps count
            if (cArgs.TryGetValue("laps", "l", out string lapsStr)
                && !int.TryParse(lapsStr, out a4kLaps))
            {
                Console.WriteLine($"Failed to parse anime4k laps count value from input string \"{lapsStr}\"!");
            }

            //get debug flag
            if (cArgs.TryGetValue("debug", "d", out string dbStr))
            {
                //default to true if no value given
                if (string.IsNullOrWhiteSpace(dbStr)) dbStr = "true";

                if (!bool.TryParse(dbStr, out debug))
                {
                    Console.WriteLine($"Failed to parse debug flag from input string \"{dbStr}\"!");
                }
            }

            #endregion

            //get mode flags
            bool hasTargetSize = targetSize != Size.Empty;
            bool hasStrengthValues = strengthColor >= 0 && strengthGradient >= 0;

            //dump out input parameters
            Console.WriteLine($@"
Input Parameters DUMP:
--Files-----------------------------------------
Input File Path:    ""{inputFile}""
Output File Path:   ""{outputFile}""
Save Sub- Phases:   {(debug ? "YES" : "NO")}
--Resolution--------------------------------------
Scale Factor:       {scaleFactor}
Target Resolution:  {targetSize.Width} x {targetSize.Height}
Use Resolution:     {(hasTargetSize ? "YES" : "NO")}
--Anime4K-Config----------------------------------
Anime4K Laps:           {a4kLaps}
Color Push Strength:    {strengthColor}
Gradient Push Strength: {strengthGradient}
Use User Strengths:     {(hasStrengthValues ? "YES" : "NO")}
");

            //load input file image
            Console.WriteLine("Load Source Image...");
            Image<Rgba32> inputImg = Image.Load<Rgba32>(inputFile);

            #region Create Scaler
            //create scaler using version 0.9
            Anime4KScaler anime4KScaler = new Anime4KScaler();

            #endregion

            #region Run Anime4K
            //apply scaling according to mode flags
            Console.WriteLine("Run Anime4K based on mode flags...");
            Image<Rgba32> outputImg;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            if (hasTargetSize)
            {
                if (hasStrengthValues)
                {
                    //target size + strength
                    outputImg = anime4KScaler.Scale(inputImg, targetSize.Width, targetSize.Height, a4kLaps, strengthColor, strengthGradient, debug);
                }
                else
                {
                    //target size no strength
                    outputImg = anime4KScaler.Scale(inputImg, targetSize.Width, targetSize.Height, a4kLaps, debug);
                }
            }
            else
            {
                if (hasStrengthValues)
                {
                    //scale factor + strength
                    outputImg = anime4KScaler.Scale(inputImg, scaleFactor, a4kLaps, strengthColor, strengthGradient, debug);
                }
                else
                {
                    //scale factor no strength
                    outputImg = anime4KScaler.Scale(inputImg, scaleFactor, a4kLaps, debug);
                }
            }
            sw.Stop();
            #endregion

            //save output image
            Console.WriteLine($"Anime4K Finished in {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalSeconds.ToString("0.##")} s)");
            Console.WriteLine("Saving output image...");
            outputImg.Save(outputFile);

            //exit without error
            Console.WriteLine("Finished!");
            return ErrorCode.NO_ERROR;
        }

        /// <summary>
        /// Print the help page to console
        /// </summary>
        /// <param name="cause">why the help page is printed</param>
        static void PrintHelp(string cause = "")
        {
            Console.WriteLine($@"
Anime4K CLI Help Page
Shown to you because of error code: {cause}.

Console Arguments:
-input / -i       <file>    input file
-output / -o      <file>    output file                                         
                            (optional, default: input + _anime4k.png)
-scale / -s       <float>   by how much the image should be upscaled            
                            (optional, default: 2)
-resolution / -r  <size>    target resolution                                   
                            (optional, overrides -f, example: 1920x1080)
-strenghtC / -sc  <float>   color push strength                                 
                            (optional)
-strenghtG / -sg  <float>   gradient algorithm strength                         
                            (optional)
-laps / -l        <int>     how often the algorithm is repeated                 
                            (optional, default value: 2)
-debug / -d       <bool>    when set the different phases are saved to disk     
                            (optional, default value: false)

Any Argument Value may be encapsulated in (double) quotes
Flags (arguments of type bool) default to TRUE if no value is given
    so ""-d"" is the same as ""-d true""

Press <ENTER> to continue.");
            Console.ReadLine();
        }

        /// <summary>
        /// parse a list of command line arguments into a key/value dictionary
        /// </summary>
        /// <param name="cmdArgs">the command line args</param>
        /// <param name="paramNamePrefix">the prefixes that are valid for command line parameter names (e.g. "-" or "/")</param>
        /// <returns>a dictionary of parameter names and values</returns>
        static Dictionary<string, string> ParseArgs(string[] cmdArgs, char[] paramNamePrefix)
        {
            //create dictionary that later contains key/value pairs
            Dictionary<string, string> args = new Dictionary<string, string>();

            //enumerate each command line arg, also get the arguments that follows the current arg.
            string arg, nextArg, paramValue;
            for (int ap = 0; ap < cmdArgs.Length; ap++)
            {
                //get args in lowercase
                arg = cmdArgs[ap].ToLower();
                nextArg = ((ap + 1) < cmdArgs.Length) ? cmdArgs[ap + 1] : string.Empty;

                //skip if arg is empty or not a parameter name
                if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWithAny(paramNamePrefix)) continue;

                //check if next arg is a parameter name
                if (!string.IsNullOrWhiteSpace(nextArg) && !nextArg.StartsWithAny(paramNamePrefix))
                {
                    //next arg is NOT a parameter name, so a value
                    paramValue = nextArg;
                }
                else
                {
                    //next arg is empty or a parameter name, set value to string.Empty
                    paramValue = string.Empty;
                }

                //set key/value
                arg = arg.TrimStart(paramNamePrefix);
                if (!args.ContainsKey(arg))
                {
                    args.Add(arg, paramValue.Trim('"'));
                }
            }

            return args;
        }

        /// <summary>
        /// Get a value from the dictiionary
        /// </summary>
        /// <param name="dic">the dictionary</param>
        /// <param name="key">the primary key</param>
        /// <param name="altKey">the alternative key if primary key is not found</param>
        /// <param name="value">the value that was found</param>
        /// <returns>was the key/altKey found?</returns>
        static bool TryGetValue(this Dictionary<string, string> dic, string key, string altKey, out string value)
        {
            //try primary key
            if (dic.TryGetValue(key, out value))
            {
                return true;
            }

            //try alt key
            return dic.TryGetValue(altKey, out value);
        }

        /// <summary>
        /// Parse a Size from a string in format WxH
        /// </summary>
        /// <param name="str">the string to parse</param>
        /// <param name="size">the parsed size, or Size.Empty if parse failed</param>
        /// <returns>was the size parsed ok?</returns>
        public static bool TryParseSize(string str, out Size size)
        {
            //dummy size
            size = Size.Empty;

            //check input is ok
            if (string.IsNullOrWhiteSpace(str)) return false;

            //split on x
            string[] split = str.ToLower().Split('x');

            //check there are 2 parts after split
            if (split.Length != 2) return false;

            //try to parse size components
            if (!int.TryParse(split[0], out int width)
                || !int.TryParse(split[1], out int heigth))
            {
                return false;
            }

            //return parsed size
            size = new Size(width, heigth);
            return true;
        }
    }
}
