﻿using Anime4k.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;

namespace Anime4k.Algorithm
{
    /// <summary>
    /// Contains the Anime4K algorithm, Version 0.9
    /// </summary>
    public static class Anime4K09
    {
        #region ScaleAnime4K Overloads

        /// <summary>
        /// Scale a image up and apply Anime4K to the upscaled image. 
        /// Automatically calculates strength factors based on scale.
        /// </summary>
        /// <remarks>the source image is NOT changed</remarks>
        /// <param name="img">the source image to scale</param>
        /// <param name="scaleFactor">how much the image should be scaled up (0.5 = half size, 2 = double size)</param>
        /// <param name="passes">how many times the algorithm should be executed on the image (more = sharper)</param>
        /// <param name="debugSavePhases">if true, each phase of the pushing algorithm is saved to ./debug/</param>
        /// <returns>the upscaled image</returns>
        public static Image<Rgba32> ScaleAnime4K(Image<Rgba32> img, float scaleFactor, int passes = 2, bool debugSavePhases = false)
        {
            //calculate push strenght (range 0-1)
            float strengthColor = Utility.Clamp(scaleFactor / 6f, 0f, 1f);
            float strengthGradient = Utility.Clamp(scaleFactor / 2f, 0f, 1f);

            //apply anime4k
            return ScaleAnime4K(img, scaleFactor, passes, strengthColor, strengthGradient, debugSavePhases);
        }

        /// <summary>
        /// Scale a image up and apply Anime4K to the upscaled image. 
        /// Automatically calculates strength factors based on scale.
        /// </summary>
        /// <remarks>the source image is NOT changed</remarks>
        /// <param name="img">the source image to scale</param>
        /// <param name="newWidth">the width of the scaled image</param>
        /// <param name="newHeight">the height of the scaled image</param>
        /// <param name="passes">how many times the algorithm should be executed on the image (more = sharper)</param>
        /// <param name="debugSavePhases">if true, each phase of the pushing algorithm is saved to ./debug/</param>
        /// <returns>the upscaled image</returns>
        public static Image<Rgba32> ScaleAnime4K(Image<Rgba32> img, int newWidth, int newHeight, int passes = 2, bool debugSavePhases = false)
        {
            //calculate scale
            float scaleW = newWidth / img.Width;
            float scaleH = newHeight / img.Height;
            float scale = Math.Min(scaleW, scaleH);

            //calculate push strenght (range 0-1)
            float strengthColor = Utility.Clamp(scale / 6f, 0f, 1f);
            float strengthGradient = Utility.Clamp(scale / 2f, 0f, 1f);

            //apply anime4k
            return ScaleAnime4K(img, newWidth, newHeight, passes, strengthColor, strengthGradient, debugSavePhases);
        }

        /// <summary>
        /// Scale a image up and apply Anime4K to the upscaled image. 
        /// </summary>
        /// <remarks>the source image is NOT changed</remarks>
        /// <param name="img">the source image to scale</param>
        /// <param name="scaleFactor">how much the image should be scaled up (0.5 = half size, 2 = double size)</param>
        /// <param name="passes">how many times the algorithm should be executed on the image (more = sharper)</param>
        /// <param name="strengthColor">how strong color push operations should be (scale / 6, range from 0-1)</param>
        /// <param name="strengthGradient">how strong gradient push operations should be (scale / 2, range from 0-1)</param>
        /// <param name="debugSavePhases">if true, each phase of the pushing algorithm is saved to ./debug/</param>
        /// <returns>the upscaled image</returns>
        public static Image<Rgba32> ScaleAnime4K(Image<Rgba32> img, float scaleFactor, int passes, float strengthColor, float strengthGradient, bool debugSavePhases = false)
        {
            int w = (int)Math.Floor(img.Width * scaleFactor);
            int h = (int)Math.Floor(img.Height * scaleFactor);
            return ScaleAnime4K(img, w, h, passes, strengthColor, strengthGradient, debugSavePhases);
        }

        /// <summary>
        /// Scale a image up and apply Anime4K to the upscaled image. 
        /// </summary>
        /// <remarks>the source image is NOT changed</remarks>
        /// <param name="img">the source image to scale</param>
        /// <param name="newWidth">the width of the scaled image</param>
        /// <param name="newHeight">the height of the scaled image</param>
        /// <param name="passes">how many times the algorithm should be executed on the image (more = sharper)</param>
        /// <param name="strengthColor">how strong color push operations should be (scale / 6, range from 0-1)</param>
        /// <param name="strengthGradient">how strong gradient push operations should be (scale / 2, range from 0-1)</param>
        /// <param name="debugSavePhases">if true, each phase of the pushing algorithm is saved to ./debug/</param>
        /// <returns>the upscaled image</returns>
        public static Image<Rgba32> ScaleAnime4K(Image<Rgba32> img, int newWidth, int newHeight, int passes, float strengthColor, float strengthGradient, bool debugSavePhases = false)
        {
            //check new dimensions are valid
            if (newWidth <= 0 || newHeight <= 0)
            {
                throw new InvalidOperationException("Scaled Dimensions cannot be smaller than 0!");
            }

            //check passes count is ok (miniumum is 1)
            if (passes < 1)
            {
                throw new InvalidOperationException("Anime4K needs at least 1 pass to be executed!");
            }

            //check strenghts are valid
            if (strengthColor < 0 || strengthGradient < 0)
            {
                throw new InvalidOperationException("Anime4K Push Strenght has to be larger than or equal 0!");
            }

            //create ./debug/ if needed
            if (debugSavePhases && !Directory.Exists("./debug/"))
            {
                Directory.CreateDirectory("./debug/");
            }

            //Upscale image
            Image<Rgba32> imgScaled = img.Clone((i) => i.Resize(newWidth, newHeight, KnownResamplers.Bicubic));

            //save upscaled image to ./debug/
            if (debugSavePhases) imgScaled.Save($@"./debug/0-0_scale-up.png");

            //apply anime4k
            return PushAnime4K(imgScaled, passes, strengthColor, strengthGradient, debugSavePhases);
        }

        #endregion

        #region Anime4K Implementation

        /// <summary>
        /// Apply the anime4K algorithm to the image
        /// </summary>
        /// <remarks>this does NOT scale the image up</remarks>
        /// <param name="img">the image to apply the algorithm to</param>
        /// <param name="passes">how many times the algorithm should be executed on the image (more = sharper)</param>
        /// <param name="strengthColor">how strong color push operations should be (scale / 6, range from 0-1)</param>
        /// <param name="strengthGradient">how strong gradient push operations should be (scale / 2, range from 0-1)</param>
        /// <param name="debugSavePhases">if true, each phase of the pushing algorithm is saved to ./debug/</param>
        /// <returns>the processed image (same as img param)</returns>
        public static Image<Rgba32> PushAnime4K(Image<Rgba32> img, int passes = 2, float strengthColor = 0.33f, float strengthGradient = 0.99f, bool debugSavePhases = false)
        {
            //clamp & calc strenght for algorithm
            strengthColor *= 255f;
            strengthColor = Utility.Clamp(strengthColor, 0, 65535);

            strengthGradient *= 255f;
            strengthGradient = Utility.Clamp(strengthGradient, 0, 65535);

            //create ./debug/ if needed
            if (debugSavePhases && !Directory.Exists("./debug/"))
            {
                Directory.CreateDirectory("./debug/");
            }

            //execute laps
            Image<Rgba32> res = null;
            for (int p = 0; p < passes; p++)
            {
                //get luminance into alpha channel
                img = GetLuminance(img);
                if (debugSavePhases) img.Save($@"./debug/{p}-1_get-lum.png");

                //push color (INCLUDING alpha channel)
                res = PushColor(img, strengthColor);
                img.Dispose();
                img = res;
                if (debugSavePhases) img.Save($@"./debug/{p}-2_push-col.png");

                //get gradient into alpha channel
                res = GetGradient(img);
                img.Dispose();
                img = res;
                if (debugSavePhases) img.Save($@"./debug/{p}-3_get-grad.png");

                //push gradient
                res = PushGradient(img, strengthGradient);
                img.Dispose();
                img = res;
                if (debugSavePhases) img.Save($@"./debug/{p}-4_push-grad.png");
            }

            return img;
        }

        /// <summary>
        /// Compute the Luminance of every Pixel in the image and store it in the image's alpha channel
        /// </summary>
        /// <param name="img">the image to modify, in RGBA32 format</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        static Image<Rgba32> GetLuminance(Image<Rgba32> img)
        {
            //process pixels directly
            return img.ChangeEachPixelParallel((x, y, p) =>
            {
                //calc luminance for pixel in range 0 - 255
                float pxLuminance = p.GetLuminance() * 255f;

                //clamp luminance to 0 - 255
                pxLuminance = Utility.Clamp(pxLuminance, 0f, 255f);

                //create new pixel
                return new Rgba32(p.R, p.G, p.B, (byte)Math.Floor(pxLuminance));
            });
        }

        /// <summary>
        /// Push the pixels (including alpha channel) based on the luminance stored in the alpha channel
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <param name="strength">how strong the gradient is pushed (0.0-255.0)</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        static Image<Rgba32> PushColor(Image<Rgba32> img, float strength)
        {
            Rgba32 GetLargest(Rgba32 cc, Rgba32 lightest, Rgba32 a, Rgba32 b, Rgba32 c)
            {
                float aR = (cc.R * (255f - strength) + (Utility.Average3(a.R, b.R, c.R) * strength)) / 255f;
                float aG = (cc.G * (255f - strength) + (Utility.Average3(a.G, b.G, c.G) * strength)) / 255f;
                float aB = (cc.B * (255f - strength) + (Utility.Average3(a.B, b.B, c.B) * strength)) / 255f;
                float aA = (cc.A * (255f - strength) + (Utility.Average3(a.A, b.A, c.A) * strength)) / 255f;
                return (aA > lightest.A) ? new Rgba32(aR / 255f, aG / 255f, aB / 255f, aA / 255f) : lightest;
            }

            return img.ChangeEachPixelParallel((x, y, mc) =>
            {
                // Kernel defination:
                // [tl][tc][tr]
                // [ml][mc][mr]
                // [bl][bc][br]

                #region Kernel setup
                //set translation constants
                int xNeg = (x <= 0) ? 0 : -1;
                int xPro = (x >= img.Width - 1) ? 0 : 1;
                int yNeg = (y <= 0) ? 0 : -1;
                int yPro = (y >= img.Height - 1) ? 0 : 1;

                //get pixels:
                //top
                Rgba32 tl = img[x + xNeg, y + yNeg];
                Rgba32 tc = img[x, y + yNeg];
                Rgba32 tr = img[x + xPro, y + yNeg];

                //middle
                Rgba32 ml = img[x + xNeg, y];
                //mc set by pixel function
                Rgba32 mr = img[x + xPro, y];

                //bottom
                Rgba32 bl = img[x + xNeg, y + yPro];
                Rgba32 bc = img[x, y + yPro];
                Rgba32 br = img[x + xPro, y + yPro];

                //default lightest color to current pixel
                Rgba32 lightest = mc;
                float maxD;
                float minL;
                #endregion

                #region Kernel 0+4
                maxD = Utility.Max3(br.A, bc.A, bl.A);
                minL = Utility.Min3(tl.A, tc.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    lightest = GetLargest(mc, lightest, tl, tc, tr);
                }
                else
                {
                    maxD = Utility.Max3(tl.A, tc.A, tr.A);
                    minL = Utility.Min3(br.A, bc.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        lightest = GetLargest(mc, lightest, br, bc, bl);
                    }
                }
                #endregion

                #region Kernel 1+5
                maxD = Utility.Max3(mc.A, ml.A, bc.A);
                minL = Utility.Min3(mr.A, tc.A, tr.A);

                if (minL > maxD)
                {
                    lightest = GetLargest(mc, lightest, mr, tc, tr);
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, tc.A);
                    minL = Utility.Min3(bl.A, ml.A, bc.A);

                    if (minL > maxD)
                    {
                        lightest = GetLargest(mc, lightest, bl, ml, bc);
                    }
                }
                #endregion

                #region Kernel 2+6
                maxD = Utility.Max3(ml.A, tl.A, bl.A);
                minL = Utility.Min3(mr.A, br.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    lightest = GetLargest(mc, lightest, mr, br, tr);
                }
                else
                {
                    maxD = Utility.Max3(mr.A, br.A, tr.A);
                    minL = Utility.Min3(ml.A, tl.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        lightest = GetLargest(mc, lightest, ml, tl, bl);
                    }
                }
                #endregion

                #region Kernel 3+7
                maxD = Utility.Max3(mc.A, ml.A, tc.A);
                minL = Utility.Min3(mr.A, br.A, bc.A);

                if (minL > maxD)
                {
                    lightest = GetLargest(mc, lightest, mr, br, bc);
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, bc.A);
                    minL = Utility.Min3(tc.A, ml.A, tl.A);

                    if (minL > maxD)
                    {
                        lightest = GetLargest(mc, lightest, tc, ml, tl);
                    }
                }
                #endregion

                //set pixel
                return lightest;
            });
        }

        /// <summary>
        /// Sobel Matrix for sobel operation in GetGradient
        /// </summary>
        static readonly float[,] gradientSobelX = { {-1, 0, 1 },
                                                    {-2, 0, 2 },
                                                    {-1, 0, 1 }};

        /// <summary>
        /// Sobel Matrix for sobel operation in GetGradient
        /// </summary>
        static readonly float[,] gradientSobelY = { {-1, -2, -1 },
                                                    { 0,  0,  0 },
                                                    { 1,  2,  1 }};

        /// <summary>
        /// Compute the Gradient from the alpha channel of the image and store the result in the alpha channel
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        static Image<Rgba32> GetGradient(Image<Rgba32> img)
        {
            float SobelAlpha(float[/*3*/,/*3*/] mat, float[/*3*/,/*3*/] col)
            {
                return col[0, 0] * mat[0, 0] + col[0, 1] * mat[0, 1] + col[0, 2] * mat[0, 2]
                     + col[1, 0] * mat[1, 0] + col[1, 1] * mat[1, 1] + col[1, 2] * mat[1, 2]
                     + col[2, 0] * mat[2, 0] + col[2, 1] * mat[2, 1] + col[2, 2] * mat[2, 2];
            }

            //change each pixel
            return img.ChangeEachPixelParallel((x, y, p) =>
            {
                //skip first & last row & collumn
                if (x == 0 || y == 0 || x == img.Width - 1 || y == img.Height - 1)
                {
                    return p;
                }

                //get pixels for sobel calculation
                float[,] sobAlpha = {{img[x-1, y-1].A, img[x, y-1].A, img[x+1, y-1].A },
                                     {img[x-1, y].A,   p.A,           img[x+1, y].A },
                                     {img[x-1, y+1].A, img[x, y+1].A, img[x+1, y+1].A }};

                //do sobel operations on alpha channels
                float dX = SobelAlpha(gradientSobelX, sobAlpha);
                float dY = SobelAlpha(gradientSobelY, sobAlpha);

                //calculate derivata. Dont take square root to save processing time
                double derivataSq = (dX * dX) + (dY * dY);

                //set pixel based on derivata
                if (derivataSq > (255 * 255))
                {
                    return new Rgba32(p.R, p.G, p.B, 0);
                }
                else
                {
                    return new Rgba32(p.R, p.G, p.B, (byte)Math.Floor(255 - Math.Sqrt(derivataSq)));
                }
            });
        }

        /// <summary>
        /// Push the pixels based on the gradient in the alpha channel
        /// </summary>
        /// <param name="img">the image to modify</param>
        /// <param name="strength">how strong the gradient is pushed (0.0-255.0)</param>
        /// <returns>the modified image, with luminance = alpha channel (same as img param)</returns>
        static Image<Rgba32> PushGradient(Image<Rgba32> img, float strength)
        {
            Rgba32 GetAverage(Rgba32 cc, Rgba32 a, Rgba32 b, Rgba32 c)
            {
                float aR = (cc.R * (255f - strength) + (Utility.Average3(a.R, b.R, c.R) * strength)) / 255f;
                float aG = (cc.G * (255f - strength) + (Utility.Average3(a.G, b.G, c.G) * strength)) / 255f;
                float aB = (cc.B * (255f - strength) + (Utility.Average3(a.B, b.B, c.B) * strength)) / 255f;
                float aA = (cc.A * (255f - strength) + (Utility.Average3(a.A, b.A, c.A) * strength)) / 255f;
                return new Rgba32(aR / 255f, aG / 255f, aB / 255f, aA / 255f);
            }

            return img.ChangeEachPixelParallel((x, y, mc) =>
            {
                // Kernel defination:
                // [tl][tc][tr]
                // [ml][mc][mr]
                // [bl][bc][br]

                #region Kernel setup
                //set translation constants
                int xNeg = (x <= 0) ? 0 : -1;
                int xPro = (x >= img.Width - 1) ? 0 : 1;
                int yNeg = (y <= 0) ? 0 : -1;
                int yPro = (y >= img.Height - 1) ? 0 : 1;

                //get pixels:
                //top
                Rgba32 tl = img[x + xNeg, y + yNeg];
                Rgba32 tc = img[x, y + yNeg];
                Rgba32 tr = img[x + xPro, y + yNeg];

                //middle
                Rgba32 ml = img[x + xNeg, y];
                //mc set by pixel function
                Rgba32 mr = img[x + xPro, y];

                //bottom
                Rgba32 bl = img[x + xNeg, y + yPro];
                Rgba32 bc = img[x, y + yPro];
                Rgba32 br = img[x + xPro, y + yPro];

                //default lightest color to current pixel
                Rgba32 lightest = mc;
                float maxD;
                float minL;
                #endregion

                #region Kernel 0+4
                maxD = Utility.Max3(br.A, bc.A, bl.A);
                minL = Utility.Min3(tl.A, tc.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    lightest = GetAverage(mc, tl, tc, tr);
                }
                else
                {
                    maxD = Utility.Max3(tl.A, tc.A, br.A);
                    minL = Utility.Min3(br.A, bc.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        lightest = GetAverage(mc, br, bc, bl);
                    }
                }
                #endregion

                #region Kernel 1+5
                maxD = Utility.Max3(mc.A, ml.A, bc.A);
                minL = Utility.Min3(mr.A, tc.A, tr.A);

                if (minL > maxD)
                {
                    lightest = GetAverage(mc, mr, tc, tr);
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, tc.A);
                    minL = Utility.Min3(bl.A, ml.A, bc.A);

                    if (minL > maxD)
                    {
                        lightest = GetAverage(mc, bl, ml, bc);
                    }
                }
                #endregion

                #region Kernel 2+6
                maxD = Utility.Max3(ml.A, tl.A, bl.A);
                minL = Utility.Min3(mr.A, br.A, tr.A);

                if (minL > mc.A && minL > maxD)
                {
                    lightest = GetAverage(mc, mr, br, tr);
                }
                else
                {
                    maxD = Utility.Max3(mr.A, br.A, tr.A);
                    minL = Utility.Min3(ml.A, tl.A, bl.A);

                    if (minL > mc.A && minL > maxD)
                    {
                        lightest = GetAverage(mc, ml, tl, bl);
                    }
                }
                #endregion

                #region Kernel 3+7
                maxD = Utility.Max3(mc.A, ml.A, tc.A);
                minL = Utility.Min3(mr.A, br.A, bc.A);

                if (minL > maxD)
                {
                    lightest = GetAverage(mc, mr, br, bc);
                }
                else
                {
                    maxD = Utility.Max3(mc.A, mr.A, bc.A);
                    minL = Utility.Min3(tc.A, ml.A, tl.A);

                    if (minL > maxD)
                    {
                        lightest = GetAverage(mc, tc, ml, tl);
                    }
                }
                #endregion

                //reset alpha channel since it is no longer needed
                return new Rgba32(lightest.R, lightest.G, lightest.B, 255);
            });
        }

        #endregion
    }
}
