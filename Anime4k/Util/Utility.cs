using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Anime4k.Util
{
    /// <summary>
    /// Contains useful extension methods
    /// </summary>
    public static class Utility
    {
        #region Math
        /// <summary>
        /// Clamp a float to be between the min and max value
        /// </summary>
        /// <param name="val">the value to clamp</param>
        /// <param name="min">the minimum value to clamp to</param>
        /// <param name="max">the maximum value to clamp to</param>
        /// <returns>the clamped value</returns>
        public static float Clamp(float val, float min, float max)
        {
            return (val < min) ? min : (val > max) ? max : val;
        }

        /// <summary>
        /// Get the smallest value of three
        /// </summary>
        /// <param name="a">the first value</param>
        /// <param name="b">the second value</param>
        /// <param name="c">the third value</param>
        /// <returns>the smallest value of three</returns>
        public static float Min3(float a, float b, float c)
        {
            return Math.Min(a, Math.Min(b, c));
        }

        /// <summary>
        /// Get the biggest value of three
        /// </summary>
        /// <param name="a">the first value</param>
        /// <param name="b">the second value</param>
        /// <param name="c">the third value</param>
        /// <returns>the biggest value of three</returns>
        public static float Max3(float a, float b, float c)
        {
            return Math.Max(a, Math.Max(b, c));
        }

        /// <summary>
        /// Get the average of three
        /// </summary>
        /// <param name="a">the first value</param>
        /// <param name="b">the second value</param>
        /// <param name="c">the third value</param>
        /// <returns>the average of three</returns>
        public static float Average3(float a, float b, float c)
        {
            return (a + b + c) / 3f;
        }
        #endregion

        #region Image Manipulation
        /// <summary>
        /// a function executed on a pixel
        /// </summary>
        /// <typeparam name="T">the pixel format to use</typeparam>
        /// <param name="x">the x coord of the pixel</param>
        /// <param name="y">the y coord of the pixel</param>
        /// <param name="pixel">the current color of the pixel</param>
        /// <returns>the color the pixel should be set to</returns>
        public delegate T PixelFunc<T>(int x, int y, T pixel) where T : struct, IPixel<T>;

        /// <summary>
        /// Execute a function for every pixel in the image and apply changes to the pixel to the image directly.
        /// </summary>
        /// <typeparam name="T">the pixel format of the image</typeparam>
        /// <param name="img">the image to use</param>
        /// <param name="pixelFunction">the function to execute for every pixel</param>
        /// <returns>the modified image (equal to img param)</returns>
        public static Image<T> DirectChangeEachPixel<T>(this Image<T> img, PixelFunc<T> pixelFunction) where T : struct, IPixel<T>
        {
            for (int px = 0; px < img.Width - 1; px++)
            {
                for (int py = 0; py < img.Height - 1; py++)
                {
                    img[px, py] = pixelFunction(px, py, img[px, py]);
                }
            }

            return img;
        }

        /// <summary>
        /// Execute a function for every pixel in the image and apply changes to the pixel to a copy of the image.
        /// </summary>
        /// <typeparam name="T">the pixel format of the image</typeparam>
        /// <param name="img">the image to use</param>
        /// <param name="pixelFunction">the function to execute for every pixel</param>
        /// <returns>the modified image</returns>
        public static Image<T> ChangeEachPixel<T>(this Image<T> img, PixelFunc<T> pixelFunction) where T : struct, IPixel<T>
        {
            //create output image
            Image<T> output = new Image<T>(img.Width, img.Height);

            //enumerate all pixels
            for (int px = 0; px < img.Width - 1; px++)
            {
                for (int py = 0; py < img.Height - 1; py++)
                {
                    output[px, py] = pixelFunction(px, py, img[px, py]);
                }
            }

            return output;
        }

        /// <summary>
        /// Execute a function for every pixel in the image and apply changes to the pixel to a copy of the image.
        /// </summary>
        /// <typeparam name="T">the pixel format of the image</typeparam>
        /// <param name="img">the image to use</param>
        /// <param name="pixelFunction">the function to execute for every pixel</param>
        /// <returns>the modified image</returns>
        public static async Task<Image<T>> ChangeEachPixelAsync<T>(this Image<T> img, PixelFunc<T> pixelFunction) where T : struct, IPixel<T>
        {
            //create output image
            Image<T> output = new Image<T>(img.Width, img.Height);

            //enumerate all pixels
            List<Task> pixelFunctionTasks = new List<Task>();
            for (int px = 0; px < img.Width - 1; px++)
            {
                for (int py = 0; py < img.Height - 1; py++)
                {
                    pixelFunctionTasks.Add(Task.Run(() =>
                    {
                        output[px, py] = pixelFunction(px, py, img[px, py]);
                    }));
                }
            }

            //wait for all tasks to finish.
            await Task.WhenAll(pixelFunctionTasks);
            return output;
        }
        #endregion
    }
}
