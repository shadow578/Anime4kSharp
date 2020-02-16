using Anime4k.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Anime4kCli.Utils
{
    /// <summary>
    /// a dictionary for command line args
    /// </summary>
    public class CommandlineDictionary
    {
        /// <summary>
        /// Parse a list of command line arguments (as given in main) into a key/value argument dictionary
        /// 
        /// a commandline like this: ".exe -foo_bool true -foo_int 10 -foo_bar "foobar" -bar_flag"
        /// would result in a dictionary that looks like this:
        /// | Key       | Value |
        /// |-----------|-------|
        /// | foo_bool  | true  |
        /// | foo_int   | 10    |
        /// | foo_bar   | foobar|
        /// | bar_flag  | true  |
        /// </summary>
        /// <param name="commandLineArgs">the command line arguments (from main)</param>
        /// <param name="commandlineDict">the arguments dictionary in key/value format</param>
        /// <param name="keyNamePrefix">a char each key has to be prefixed with</param>
        /// <returns>was the command line parsed ok? </returns>
        public static bool TryParse(string[] commandLineArgs, out CommandlineDictionary commandlineDict, params char[] keyNamePrefix)
        {
            //create output dictionary
            commandlineDict = new CommandlineDictionary();

            //check we have args AND we have keyName prefixes
            if (commandLineArgs == null || commandLineArgs.Length <= 0
                || keyNamePrefix == null || keyNamePrefix.Length <= 0)
            {
                return false;
            }

            //enumerate all command line args in a queue
            Queue<string> argQueue = new Queue<string>(commandLineArgs);
            string currentArg, nextArg;
            while (argQueue.Count > 0)
            {
                //get current and next arg
                currentArg = argQueue.Dequeue();
                nextArg = (argQueue.Count > 0) ? argQueue.Peek() : null;

                //skip if current arg is not a key name OR empty
                if (string.IsNullOrWhiteSpace(currentArg) || !currentArg.StartsWithAny(keyNamePrefix))
                    continue;

                //check if next arg is key name (if next is a key and this is also a key, or this is a key and there is no next arg, this can ONLY be a flag)
                if (string.IsNullOrEmpty(nextArg) || nextArg.StartsWithAny(keyNamePrefix))
                {
                    //this is a flag, add it with value=TRUE
                    commandlineDict.AddArg(currentArg, "true");
                }
                else
                {
                    //this is a normal key, next is the value. Add it like that and pop the value too
                    //also trim off spaces and quotes (single and double) from the value
                    commandlineDict.AddArg(currentArg.TrimStart(currentArg[0]).ToLower(), argQueue.Dequeue().Trim(' ', '"', '\''));
                }
            }

            //all ok
            return true;
        }


        /// <summary>
        /// Dictionary of arg keys + values
        /// </summary>
        StringDictionary args = new StringDictionary();

        /// <summary>
        /// add a key value pair to the dictionary
        /// </summary>
        /// <param name="key">the key to add</param>
        /// <param name="value">the value to add</param>
        void AddArg(string key, string value)
        {
            args.Add(key, value);
        }

        /// <summary>
        /// Try to get a value by key
        /// </summary>
        /// <typeparam name="T">the type of the value to get</typeparam>
        /// <param name="key">the key to get</param>
        /// <param name="value">the value of the key</param>
        /// <returns>was the key found (and the value could be converted to the correct type)?</returns>
        public bool TryGet<T>(out T value, string key)
        {
            //dummy for value
            value = default;

            //check key exists
            if (args == null || !args.ContainsKey(key))
                return false;

            //get value string
            string val = args[key];

            if (typeof(T).IsEnum)
            {
                //try to use Enum.Parse for enums
                try
                {
                    value = (T)Enum.Parse(typeof(T), val, true);
                }
                catch (Exception)
                {
                    //failed, fail silent
                    return false;
                }
            }
            else
            {
                //try to use Convert.ChangeType for anything else
                try
                {
                    value = (T)Convert.ChangeType(val, typeof(T));
                }
                catch (Exception)
                {
                    //failed, fail silent
                    return false;
                }
            }

            //all went ok
            return true;
        }

        /// <summary>
        /// Try to get a value by key
        /// </summary>
        /// <typeparam name="T">the type of the value to get</typeparam>
        /// <param name="validKeys">all the valid keys of the value to get (first is checked first)</param>
        /// <param name="value">the value of the key</param>
        /// <returns>was the key found (and the value could be converted to the correct type)?</returns>
        public bool TryGetAny<T>(out T value, params string[] validKeys)
        {
            //dummy value
            value = default;

            //check each key
            foreach (string key in validKeys)
            {
                if (TryGet<T>(out value, key))
                {
                    //got key
                    return true;
                }
            }

            //not found
            return false;
        }

        /// <summary>
        /// Does the commandline dictionary contain the given key?
        /// </summary>
        /// <param name="key">the key to check</param>
        /// <returns>does it contain the key?</returns>
        public bool HasKey(string key)
        {
            //try to get, dont care about type
            return TryGet(out object _, key);
        }

        /// <summary>
        /// Does the commandline dictionary contain any of the given keys?
        /// </summary>
        /// <param name="validKeys">the keys to check</param>
        /// <returns>does it contain any of the keys?</returns>
        public bool HasAnyKey(params string[] validKeys)
        {
            //try to get, dont care about the type
            return TryGetAny(out object _, validKeys);
        }
    }
}
