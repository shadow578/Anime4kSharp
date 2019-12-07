using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Anime4k.Util
{
    /// <summary>
    /// Contains useful extension methods
    /// </summary>
    public static class Utility
    {
        #region Pixel Change
        /// <summary>
        /// a function executed on a pixel
        /// </summary>
        /// <typeparam name="T">the pixel format to use</typeparam>
        /// <param name="x">the x coord of the pixel</param>
        /// <param name="y">the y coord of the pixel</param>
        /// <param name="pixel">the current color of the pixel</param>
        /// <returns>the color the pixel should be set to</returns>
        public delegate T PixelFunc<T>(int x, int y, T pixel);

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
            for (int px = 0; px < img.Width; px++)
            {
                for (int py = 0; py < img.Height; py++)
                {
                    output[px, py] = pixelFunction(px, py, img[px, py]);
                }
            }

            return output;
        }

        /// <summary>
        /// Execute a function for every pixel in the image and apply changes to the pixel to the image directly.
        /// </summary>
        /// <typeparam name="T">the pixel format of the image</typeparam>
        /// <param name="img">the image to use</param>
        /// <param name="pixelFunction">the function to execute for every pixel</param>
        /// <returns>the modified image (equal to img param)</returns>
        public static Image<T> DirectChangeEachPixel<T>(this Image<T> img, PixelFunc<T> pixelFunction) where T : struct, IPixel<T>
        {
            for (int px = 0; px < img.Width; px++)
            {
                for (int py = 0; py < img.Height; py++)
                {
                    img[px, py] = pixelFunction(px, py, img[px, py]);
                }
            }

            return img;
        }
        #endregion

        #region Pixel Change Async
        /// <summary>
        /// a function executed on a pixel
        /// </summary>
        /// <typeparam name="T">the pixel format to use</typeparam>
        /// <param name="x">the x coord of the pixel</param>
        /// <param name="y">the y coord of the pixel</param>
        /// <param name="pixel">the current color of the pixel</param>
        /// <returns>the color the pixel should be set to</returns>
        public delegate Task<T> AsyncPixelFunc<T>(int x, int y, T pixel);

        /// <summary>
        /// Execute a function for every pixel in the image and apply changes to the pixel to a copy of the image.
        /// </summary>
        /// <typeparam name="T">the pixel format of the image</typeparam>
        /// <param name="img">the image to use</param>
        /// <param name="pixelFunction">the function to execute for every pixel</param>
        /// <returns>the modified image</returns>
        public static async Task<Image<T>> ChangeEachPixelAsync<T>(this Image<T> img, AsyncPixelFunc<T> pixelFunction) where T : struct, IPixel<T>
        {
            //create output image
            Image<T> output = new Image<T>(img.Width, img.Height);

            //enumerate all pixels
            List<Task> pixelFunctionTasks = new List<Task>();
            for (int px = 0; px < img.Width; px++)
            {
                for (int py = 0; py < img.Height; py++)
                {
                    pixelFunctionTasks.Add(pixelFunction(px, py, img[px, py]).ContinueWith((r) =>
                    {
                        if (!r.IsFaulted)
                        {
                            output[px, py] = r.Result;
                        }
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
