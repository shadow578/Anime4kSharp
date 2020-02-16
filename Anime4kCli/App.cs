using Anime4k.Algorithm;
using Anime4kCli.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System;
using System.Diagnostics;
using System.IO;

namespace Anime4kCli
{
    /// <summary>
    /// Command line Flags (new):
    /// -help           -?                  show help page
    /// 
    /// ~~ File Processing ~~
    /// -input (file)       -i (file)       input path for SINGLE file processing (only that file is processed; cannot be used with -bd or -bm)
    /// -batchdir (dir)     -bd (dir)       input path for BATCH file processing (all files matching the batchmask (-bm), -bm is REQUIRED)
    /// -batchmsk (string)  -bm (string)    input file name mask for BATCH file processing (eg. "*.png" to process all png files in dir, or "img_*.png" to process all pngs starting with "img_")
    /// -output (file/dir)  -o (file/dir)   output path for SINGLE or BATCH file processing
    ///                                     in SINGLE file mode, this is the path to the output file (WITH filename + extension); if not given, the input filename is suffixed with "_a4k"
    ///                                     in BATCH file mode, this is the path to the output directory (filenames are NOT changed); if not given, the output dir is "/a4k/" inside the input dir (-bd)
    /// -overwrite (bool)   -ow (bool)      if set, any output file that exist will be overwritten
    /// 
    /// ~~ Processing Basic Settings ~~
    /// -scale (float)      -s (float)          by what factor is the resolution of the input image(s) scaled? Default is 2
    /// -resolution (size)  -r (size)           what resolution the output image(s) should be scaled to? (if not given, -scale is used)
    /// -version (ver)      -v (ver)            sets the anime4k version used, default is v09
    /// -debug (bool)       -d (bool)           if enabled, the different phases are saved to disk 
    /// 
    /// ~~ Processing Advanced Settings ~~
    /// -passes (int)       -p (int)            how many passes of anime4k are ran
    /// -strenghtC (float)  -sc (float)         sets the color push strenght
    /// -strenghtG (float)  -sg (float)         sets the gradient/line push strenght
    /// </summary>
    public static class App
    {
        /// <summary>
        /// Error code for when processing file(s) goes wrong
        /// </summary>
        enum ErrorCode
        {
            /// <summary>
            /// No Processing error occured
            /// </summary>
            NoError,

            /// <summary>
            /// The command line is invalid (bad combination of parameters OR missing parameters)
            /// </summary>
            InvalidCommandline,

            /// <summary>
            /// The input file could not be found
            /// </summary>
            InputNotFound,

            /// <summary>
            /// The output file that was tried to write to already exists (and overwrite is not allowed)
            /// </summary>
            OutputFileExists
        }

        /// <summary>
        /// Main entry point of the application
        /// </summary>
        /// <param name="args">command line args</param>
        public static void Main(string[] args)
        {
            //call MainWrapped which does all the processing, stop the total time it takes
            Stopwatch processingWatch = new Stopwatch();
            processingWatch.Start();
            ErrorCode mainError = MainWrapped(args);
            processingWatch.Stop();

            //output processing time
            Console.WriteLine($"Anime4K finished in {processingWatch.ElapsedMilliseconds} ms!");

            //show help page with error code (if we have one)
            if (mainError != ErrorCode.NoError)
            {
                ShowHelp(mainError.ToString());
            }

#if DEBUG
            //exit after enter (in DEBUG builds
            Console.WriteLine("Press <ENTER> to exit");
            Console.ReadLine();
#endif
        }

        /// <summary>
        /// Main entry point wrapped for errorcode
        /// </summary>
        /// <param name="args">command line args</param>
        /// <returns>errorcode of the function</returns>
        static ErrorCode MainWrapped(string[] args)
        {
            //parse command line input
            if (!CommandlineDictionary.TryParse(args, out CommandlineDictionary commandline, '-', '/'))
            {
                //command line parse failed
                return ErrorCode.InvalidCommandline;
            }

            //check if called for help page
            if (commandline.TryGetAny(out bool helpPageRequested, "help", "?") && helpPageRequested)
            {
                //show help and exit
                ShowHelp();
                return ErrorCode.NoError;
            }

            //process single file or batch, depending if -bd and -bm are set AND -i is NOT set
            bool isSingle = !commandline.HasAnyKey("batchdir", "bd") && !commandline.HasAnyKey("batchmask", "bm") && commandline.HasAnyKey("input", "i");
            bool isBatch = commandline.HasAnyKey("batchdir", "bd") && commandline.HasAnyKey("batchmask", "bm") && !commandline.HasAnyKey("input", "i");

            //check if command line is valid
            if (isSingle == isBatch)
            {
                //invalid command line
                return ErrorCode.InvalidCommandline;
            }

            //process batch or single
            if (isSingle)
            {
                return ProcessSingle(commandline);
            }

            if (isBatch)
            {
                return ProcessBatch(commandline);
            }

            //how did we end up here?? (this should NEVER happen, assume command line is invalid...)
            return ErrorCode.InvalidCommandline;
        }

        /// <summary>
        /// Process a single file with the given command line
        /// </summary>
        /// <param name="commandline">the command line to use</param>
        /// <returns>errorcode of the processing step</returns>
        static ErrorCode ProcessSingle(CommandlineDictionary commandline)
        {
            Console.WriteLine("Process SINGLE...");

            //get input file path
            if (!commandline.TryGetAny(out string inputFilePath, "input", "i")
                || !File.Exists(inputFilePath))
            {
                //input file does NOT exist
                return ErrorCode.InputNotFound;
            }

            //get the output path
            if (!commandline.TryGetAny(out string outputFilePath, "output", "o"))
            {
                //default to input file path + _a4k
                outputFilePath = Path.Combine(Path.GetDirectoryName(inputFilePath), $"{Path.GetFileNameWithoutExtension(inputFilePath)}_a4k{Path.GetExtension(inputFilePath)}");
            }

            //process the file
            return ProcessFile(inputFilePath, outputFilePath, commandline);
        }

        /// <summary>
        /// Process multiple files with the given command line
        /// </summary>
        /// <param name="commandline">the command line to use</param>
        /// <returns>errorcode of the processing step</returns>
        static ErrorCode ProcessBatch(CommandlineDictionary commandline)
        {
            Console.WriteLine("Process BATCH...");

            //get batch input directory and filename mask
            if (!commandline.TryGetAny(out string batchDir, "batchdir", "bd")
                || !Directory.Exists(batchDir))
            {
                //batch directory not found!
                return ErrorCode.InputNotFound;
            }

            if (!commandline.TryGetAny(out string batchMask, "batchmask", "bm"))
            {
                //batch mask not found
                return ErrorCode.InvalidCommandline;
            }

            //get batch output directory
            if (!commandline.TryGetAny(out string batchOutputDir, "output", "o"))
            {
                //no output directory given, default to batchdir/a4k/
                batchOutputDir = Path.Combine(batchDir, "a4k");
            }

            //process each file in the batch directory that matches the mask
            int fileCounter = 0;
            ErrorCode errors = ErrorCode.NoError;
            foreach (string sourceFile in Directory.EnumerateFiles(batchDir, batchMask, SearchOption.TopDirectoryOnly))
            {
                //get output filename
                string targetFile = Path.Combine(batchOutputDir, Path.GetFileName(sourceFile));

                //process the file
                errors |= ProcessFile(sourceFile, targetFile, commandline);
                fileCounter++;
            }

            Console.WriteLine($"Processed {fileCounter} files in {batchDir}!");
            return errors;
        }

        /// <summary>
        /// Process a single file based on the command line given
        /// </summary>
        /// <param name="sourcePath">the file to process</param>
        /// <param name="targetPath">the path to save the processed image to (if the file exists AND -overwrite is NOT set, errorcode "OutputFileExists" will be returned)</param>
        /// <param name="commandline">the commandline to use</param>
        /// <returns>errorcode of the file processing action</returns>
        static ErrorCode ProcessFile(string sourcePath, string targetPath, CommandlineDictionary commandline)
        {
            Console.WriteLine($"Processing \"{sourcePath}\", saving to \"{targetPath}\"...");

            #region Prepare Input and Output file paths
            //check input file exists
            if (!File.Exists(sourcePath))
            {
                return ErrorCode.InputNotFound;
            }

            //prepare any directories needed for the output path
            string targetDir = Path.GetDirectoryName(targetPath);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            //check output file can be written to (does not exist or am allowed to overwrite)
            if (File.Exists(targetPath))
            {
                //delete the file if we are allowed to overwrite it
                if (commandline.TryGetAny(out bool allowOverwrite, "overwrite", "o") && allowOverwrite)
                {
                    //have overwrite flag and is set to true, delete the file and be A-OK
                    try
                    {
                        File.Delete(targetPath);
                    }
                    catch (Exception)
                    {
                        //delete failed, return error code
                        return ErrorCode.OutputFileExists;
                    }
                }
                else
                {
                    //are not allowed to overwrite, return error code
                    return ErrorCode.OutputFileExists;
                }
            }
            #endregion

            //get scaler version to use
            if (!commandline.TryGetAny(out Anime4KAlgorithmVersion scalerVersion, "version", "v"))
            {
                scalerVersion = Anime4KAlgorithmVersion.v09;
            }

            //create the scaler
            Anime4KScaler scaler = new Anime4KScaler(scalerVersion);

            #region Prepare scaler parameters
            //check if we have a fixed output size given
            Size targetSize = Size.Empty;
            bool hasTargetSize = commandline.TryGetAny(out string targetSizeStr, "resolution", "r") //can get value
                                    && Util.TryParseSize(targetSizeStr, out targetSize); //can parse value

            //check if we have strenght values given
            float strengthC = -1f, strengthG = -1f;
            bool hasStrengthValues = commandline.TryGetAny(out strengthC, "strengthC", "sc")
                                        && commandline.TryGetAny(out strengthG, "strengthG", "sg");

            //get number of passes to run
            if (!commandline.TryGetAny(out int passes, "passes", "p"))
            {
                //default to 1 pass
                passes = 1;
            }

            //get scale factor
            if (!commandline.TryGetAny(out float scaleFactor, "scale", "s"))
            {
                //default to 2
                scaleFactor = 2f;
            }

            //get debug flag
            if (!commandline.TryGetAny(out bool debug, "debug", "d"))
            {
                //default debug to false
                debug = false;
            }
            #endregion

            //dump scaler parameters
            Console.WriteLine($@"-----------------------------------------------
Input File:  ""{sourcePath}""
Output File: ""{targetPath}""
Save Phases:            {(debug ? "YES" : "NO")}
Scale Factor:           {scaleFactor}
Target Resolution:      {targetSize.Width} x {targetSize.Height}
Use Resolution:         {(hasTargetSize ? "YES" : "NO")}
Anime4K Version:        {scalerVersion}
Anime4K Passes:         {passes}
Color Push Strength:    {(strengthC == -1 ? "AUTO" : strengthC.ToString())}
Gradient Push Strength: {(strengthG == -1 ? "AUTO" : strengthG.ToString())}
Use User Strengths:     {(hasStrengthValues ? "YES" : "NO")}
-----------------------------------------------");

            #region load the image and scale it
            Stopwatch fileProcessingWatch = new Stopwatch();
            fileProcessingWatch.Start();
            using (Image<Rgba32> inputImg = Image.Load<Rgba32>(sourcePath))
            {
                //prepeare output image
                Image<Rgba32> outputImg = null;

                //do scaling
                if (hasTargetSize)
                {
                    if (hasStrengthValues)
                    {
                        //target size + strength
                        outputImg = scaler.Scale(inputImg, targetSize.Width, targetSize.Height, passes, strengthC, strengthG, debug);
                    }
                    else
                    {
                        //target size no strength
                        outputImg = scaler.Scale(inputImg, targetSize.Width, targetSize.Height, passes, debug);
                    }
                }
                else
                {
                    if (hasStrengthValues)
                    {
                        //scale factor + strength
                        outputImg = scaler.Scale(inputImg, scaleFactor, passes, strengthC, strengthG, debug);
                    }
                    else
                    {
                        //scale factor no strength
                        outputImg = scaler.Scale(inputImg, scaleFactor, passes, debug);
                    }
                }

                //save the output image and dispose it
                outputImg?.Save(targetPath);
                outputImg?.Dispose();
            }
            fileProcessingWatch.Stop();
            #endregion

            Console.WriteLine($"Finished processing \"{sourcePath}\" in {fileProcessingWatch.ElapsedMilliseconds} ms!");
            return ErrorCode.NoError;
        }

        /// <summary>
        /// show the help page to the user
        /// </summary>
        /// <param name="errorText">the error why the help page is shown to the user</param>
        static void ShowHelp(string errorText = null)
        {
            //create a list of available versions
            string verStr = "";
            foreach (string ver in Enum.GetNames(typeof(Anime4KAlgorithmVersion)))
            {
                verStr += ver;
                verStr += ", ";
            }

            //print help page
            Console.WriteLine($@"
Anime4K CLI Help Page
Error Code: {(string.IsNullOrWhiteSpace(errorText) ? "NO ERROR" : errorText)}

Console Arguments:
-help / -? (bool)               Show the help page

~~ File Input / Output ~~
-input / -i (file)              input path when processing a SINGLE file
                                CANNOT be used in combination with -bd or -bm
-batchdir / -bd (directory)     input directory when BATCH processing
                                Any file in the directory matching the mask (-bm) is processed
                                CANNOT be used in combination with -i
                                MUST be used in combination with -bm
-batchmask / -bm (mask)         input file name mask when BATCH processing
                                eg. ""*.png"" to process all pngs
                                CANNOT be used in combination with -i
                                MUST be used in combination with -bd
-output / -o (file/dir)         output path for SINGLE or BATCH processing mode
                                in SINGLE mode the path to the output file, defaults to
                                input filename + _a4k
                                in BATCH mode the path of the output directory, defaults to
                                ""/a4k/"" inside batch dir (-bd)
-overwrite / -ow (bool)         Enables overwriting of existing output files

~~ Processing Basic Settings ~~
-scale / -s (float)             the factor by which the input image is scaled, defaults to 2
-resolution / -r (size)         the resolution of output images (in format WxH (eg. 100x200))
                                by default, the value of scale (-s) is used
-version / -v (version)         sets the version of the Anime4K algorithm that is used.
                                by default, v09 is used
                                Available Versions: {verStr}
-debug / -d (bool)              debug mode, if enabled sub- phases are saved to disk

~~ Processing Advanced Settings ~~
-strengthC / -sc  (float)   color push strength                                 
-strengthG / -sg  (float)   gradient algorithm strength                         
-passes / -p      (int)     how often the algorithm is repeated                 
                            default value is 1
                                
Any Parameter Value may be encapsulated in single or double quotes
Flags (Parameters of type bool) default to TRUE if no value is given
    so ""-d"" is the same as ""-d true""

Available Anime4K versions are:
    {verStr}

Press <ENTER> to continue.");
            Console.ReadLine();
        }
    }
}
